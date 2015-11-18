using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using DADInterfaces;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels;
using System.Configuration;
using System.Runtime.Remoting;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace PuppetMastersCoordinatorGUI
{
    public partial class Form1 : Form
    {
        //Structures
        Dictionary<String, PuppetMaster> pms;
        Dictionary<String, Site> site_site;
        Dictionary<String, String> site_parents;
        Dictionary<String, List<String>> site_childs;
        Dictionary<String, Broker> site_brokers;
        Dictionary<String, List<Publisher>> site_publishers;
        Dictionary<String, List<Subscriber>> site_subscribers;
        Dictionary<String, Broker> all_brokers;
        Dictionary<String, Publisher> all_publishers;
        Dictionary<String, Subscriber> all_subscribers;
        Dictionary<String, String> uri_processname;
        //Vars
        String site_root;
        RoutingPolicy rout;
        OrderingPolicy ord;
        LoggingLevel log;
        string myaddr;
        bool runningInstructions = false;

        public Form1()
        {
            InitializeComponent();
        }


        private List<String> parseURI(string str)
        {
            List<String> ret = new List<String>();
            string[] temp = str.Split('/');
            string type = temp[0];
            string remoteName = temp[3];
            string aux = temp[2];
            string ip = aux.Split(':')[0];
            string port = aux.Split(':')[1];
            ret.Add(type);
            ret.Add(ip);
            ret.Add(port);
            ret.Add(remoteName);
            return ret;

        }

        private void getPMs()
        {
            // get puppet masters proxies
            // TcpChannel channel = new TcpChannel();
            //ChannelServices.RegisterChannel(channel, true);
            pms = new Dictionary<String, PuppetMaster>();
            string puppetMasterConfigFile = ConfigurationManager.AppSettings["puppetmasters"];
            string[] lines = null;
            // WARNING no file detection done
            lines = System.IO.File.ReadAllLines(puppetMasterConfigFile);
            
            foreach (string line in lines)
            {
                PuppetMaster obj = (PuppetMaster)Activator.GetObject(typeof(PuppetMaster), line);
                string ip = parseURI(line)[1];
                pms.Add(ip, obj);
            }


        }

        public void reportEvent(string type, string uri1, string uri2, string topic, int seqnum)
        {
            if (log == LoggingLevel.light && type == EventType.BroEvent)
                return;
            string str = type + " " + uri_processname[uri1] + ", " + uri_processname[uri2] + ", " + topic + ", " + seqnum;
            logCommand(str);
        }

        delegate void stringIn(string s);
        public void logCommand(string str)
        {
            textBox13.AppendText(str + "\r\n");
            StreamWriter writetext = new StreamWriter(ConfigurationManager.AppSettings["logs"], true);
            writetext.WriteLine(str);
            writetext.Close();
        }

        public void logCommandExternalThread(string str)
        {          
            BeginInvoke(new stringIn(logCommand), new object[] { str });
        }


        private void button1_Click(object sender, EventArgs e)
        {
            StreamWriter writetext = new StreamWriter(ConfigurationManager.AppSettings["logs"], false);
            writetext.Write("");
            writetext.Close();

            myaddr = ConfigurationManager.AppSettings["myaddr"];
            int myport = Int32.Parse(parseURI(myaddr)[2]);
            TcpChannel channel = new TcpChannel(myport);
            ChannelServices.RegisterChannel(channel, true);
            CoordinatorRem c = new CoordinatorRem(this);
            RemotingServices.Marshal(c, "CoordinatorRem", typeof(CoordinatorRem));

            site_site = new Dictionary<string, Site>();
            site_parents = new Dictionary<string, string>();
            site_childs = new Dictionary<string, List<string>>();
            site_brokers = new Dictionary<String, Broker>();
            site_publishers = new Dictionary<String, List<Publisher>>();
            site_subscribers = new Dictionary<String, List<Subscriber>>();
            all_brokers = new Dictionary<String, Broker>();
            all_publishers = new Dictionary<String, Publisher>();
            all_subscribers = new Dictionary<String, Subscriber>();
            uri_processname = new Dictionary<String, String>();
            rout = RoutingPolicy.flooding;
            ord = OrderingPolicy.fifo;
            log = LoggingLevel.light;


            //load proxyies of pms
            getPMs();
            string configFile = ConfigurationManager.AppSettings["config"];


            if (!File.Exists(configFile))
            {
                MessageBox.Show("config.file not found " + Path.GetFullPath(configFile));
                return;
            }

            string[] lines = System.IO.File.ReadAllLines(configFile);


            foreach (string line in lines)
            {
                string[] keywords = line.Split(' ');
                var type = keywords[0];
                if (type == "RoutingPolicy" && keywords.Length >= 2)
                {
                    if (keywords[1] == "filter")
                        rout = RoutingPolicy.filter;
                    else if (keywords[1] == "flooding") { }
                    else
                        MessageBox.Show("Wrong Routing format!");
                }
                else if (type == "LoggingLevel" && keywords.Length >= 2)
                {
                    if (keywords[1] == "full")
                        log = LoggingLevel.full;
                    else if (keywords[1] == "light") { }
                    else
                        MessageBox.Show("Wrong Logging format!");
                }
                else if (type == "Ordering" && keywords.Length >= 2)
                {
                    if (keywords[1] == "NO")
                        ord = OrderingPolicy.no;
                    else if (keywords[1] == "TOTAL")
                        ord = OrderingPolicy.total;
                    else if (keywords[1] == "FIFO") { }
                    else
                        MessageBox.Show("Wrong Ordering format!");


                }
                else if (type == "Site" && keywords.Length >= 4)
                {

                    //Example "Site site0 Parent none"
                    //"Site site1 Parent site0"
                    var parent_site = keywords[3];
                    var site_name = keywords[1];

                    if (parent_site == "none")
                    {
                        site_root = site_name;
                    }
                    else
                    {
                        site_parents.Add(site_name, parent_site);

                        if (!site_childs.ContainsKey(parent_site))
                            site_childs.Add(parent_site, new List<string>());
                        site_childs[parent_site].Add(site_name);
                    }


                }
                else if (type == "Process" && keywords.Length >= 8)
                {
                    //Process subscriber0 Is subscriber On site0 URL tcp://localhost:3337/sub

                    string uri = keywords[7];
                    string ip = parseURI(uri)[1];
                    string port = parseURI(uri)[2];
                    string serviceName = parseURI(uri)[3];
                    string process_type = keywords[3];
                    string site = keywords[5];
                    string processName = keywords[1];

                    switch (process_type)
                    {
                        case "publisher":
                            //create
                            Publisher p = pms[ip].createPublisher(processName, serviceName, site, Int32.Parse(port), myaddr);
                            //associate
                            all_publishers.Add(processName, p);
                            //add it to site publishers
                            if (!site_publishers.ContainsKey(site))
                                site_publishers.Add(site, new List<Publisher>());
                            site_publishers[site].Add(p);
                            if (ip == "localhost")
                                uri = uri.Replace("localhost", LocalIPAddress().ToString());
                            uri_processname.Add(uri, processName);
                            break;
                        case "broker":
                            Broker b = pms[ip].createBroker(processName, serviceName, site, Int32.Parse(port), myaddr);
                            all_brokers.Add(processName, b);
                            site_brokers.Add(site, b);
                            site_site.Add(site, new Site() { name = site, brokers = new List<Broker>() { b } });
                            if (ip == "localhost")
                                uri = uri.Replace("localhost", LocalIPAddress().ToString());
                            uri_processname.Add(uri, processName);
                            break;
                        case "subscriber":
                            Subscriber s = pms[ip].createSubscriber(processName, serviceName, site, Int32.Parse(port), myaddr);
                            all_subscribers.Add(processName, s);
                            if (!site_subscribers.ContainsKey(site))
                                site_subscribers.Add(site, new List<Subscriber>());
                            site_subscribers[site].Add(s);
                            if (ip == "localhost")
                                uri = uri.Replace("localhost", LocalIPAddress().ToString());
                            uri_processname.Add(uri, processName);
                            break;
                        default:
                            MessageBox.Show("Error parsing config.file!");
                            break;
                    }


                }
                else
                    MessageBox.Show("Error parsing config.file!");
            }
            foreach (KeyValuePair<string, Broker> entry in all_brokers)
            {
                entry.Value.setRoutingPolicy(rout);
                entry.Value.setOrderingPolicy(ord);
                entry.Value.setLoggingLevel(log);
            }

            foreach (KeyValuePair<string, Subscriber> entry in all_subscribers)
            {
                entry.Value.setOrderingPolicy(ord);
            }

                site_brokers[site_root].setIsRoot();

            //Set publishers brokers
            foreach (KeyValuePair<string, List<Publisher>> entry in site_publishers)
            {
                site_brokers[entry.Key].setPublishers(entry.Value);
                foreach (Publisher p in entry.Value)
                {
                    p.setSiteBroker(site_brokers[entry.Key]);
                }
            }
            // Set subscriber brokers
            foreach (KeyValuePair<string, List<Subscriber>> entry in site_subscribers)
            {
                site_brokers[entry.Key].setSubscribers(entry.Value);
                foreach (Subscriber s in entry.Value)
                {
                    s.setSiteBroker(site_brokers[entry.Key]);
                }
            }

            //Set parents and childs
            foreach (KeyValuePair<string, Broker> entry in site_brokers)
            {
                string site = entry.Key;
                Broker broker = entry.Value;

                if (site_childs.ContainsKey(site))
                {
                    List<Site> childs = new List<Site>();
                    foreach (string str in site_childs[site])
                    {
                        if (site_site.ContainsKey(str)) // empty sites
                            childs.Add(site_site[str]);

                    }
                    broker.setChildren(childs);

                }

                //assume we first read root site
                if (site != site_root)
                {
                    string ps = site_parents[site];
                    Site parentSite = site_site[ps];
                    entry.Value.setParent(parentSite);
                }
            }


        }


        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                string input_file = ConfigurationManager.AppSettings["input"];
                string[] lines = System.IO.File.ReadAllLines(input_file);
                if(runningInstructions == true)
                {
                    MessageBox.Show("Still running instructions from last click");
                }
                else
                {
                    //assuming only one UI thread uses the function
                    runningInstructions = true;
                    new Thread(() => readInput(lines)).Start();
                    runningInstructions = false;
                }
                
            }
            catch (FileNotFoundException fnf)
            {
                MessageBox.Show("input.file not found" + fnf);
            }

        }

        //TODO: remove warnings
        
        public void readInput(String[] lines)
        {
            foreach (String line in lines)
            {
                //logCommand(line);
               
                logCommandExternalThread(line);
                string[] keywords = line.Split(' ');
                string instructionType = keywords[0];
                if (instructionType == "Status" && keywords.Length >= 1)
                {
                    foreach (KeyValuePair<string, Broker> entry in all_brokers)
                    {
                        try
                        {
                            entry.Value.status();
                        }
                        catch (Exception)
                        {
                            logCommandExternalThread(entry.Key + " failed \r\n");
                        }
                    }

                    foreach (KeyValuePair<string, Publisher> entry in all_publishers)
                    {
                        try
                        {
                            entry.Value.status();
                        }
                        catch (Exception)
                        {
                            logCommandExternalThread(entry.Key + " failed \r\n");                            
                        }
                    }
                    foreach (KeyValuePair<string, Subscriber> entry in all_subscribers)
                    {
                        try
                        {
                            entry.Value.status();
                        }
                        catch (Exception)
                        {
                            logCommandExternalThread(entry.Key + " failed \r\n");
                        }
                    }

                }
                else if (instructionType == "Crash" && keywords.Length >= 2)
                {
                    try
                    {
                        if (all_brokers.ContainsKey(keywords[1]))
                            all_brokers[keywords[1]].crash();
                        if (all_publishers.ContainsKey(keywords[1]))
                            all_publishers[keywords[1]].crash();
                        if (all_subscribers.ContainsKey(keywords[1]))
                            all_subscribers[keywords[1]].crash();
                    }
                    catch (System.IO.IOException)
                    {
                        //PROCESS KILLED SUCESSFULLY

                    }


                }
                else if (instructionType == "Freeze" && keywords.Length >= 2)
                {

                    if (all_brokers.ContainsKey(keywords[1]))
                        all_brokers[keywords[1]].freeze();
                    if (all_publishers.ContainsKey(keywords[1]))
                        all_publishers[keywords[1]].freeze();
                    if (all_subscribers.ContainsKey(keywords[1]))
                        all_subscribers[keywords[1]].freeze();

                }
                else if (instructionType == "Unfreeze" && keywords.Length >= 2)
                {
                    if (all_brokers.ContainsKey(keywords[1]))
                        all_brokers[keywords[1]].unfreeze();
                    if (all_publishers.ContainsKey(keywords[1]))
                        all_publishers[keywords[1]].unfreeze();
                    if (all_subscribers.ContainsKey(keywords[1]))
                        all_subscribers[keywords[1]].unfreeze();

                }
                else if (instructionType == "Wait" && keywords.Length >= 2)
                {
                    Thread.Sleep(Int32.Parse(keywords[1]));
                }
                else if (keywords.Length >= 4 && keywords[0] == "Subscriber" && keywords[2] == "Subscribe")
                {
                    all_subscribers[keywords[1]].subscribe(keywords[3]);
                }
                else if (keywords.Length >= 4 && keywords[0] == "Subscriber" && keywords[2] == "Unsubscribe")
                {
                    all_subscribers[keywords[1]].unsubscribe(keywords[3]);
                }
                else if (keywords.Length >= 8 && keywords[0] == "Publisher" && keywords[2] == "Publish")
                {
                    //Publisher publisher0 Publish 1 Ontopic /desporto/futebol Interval 100
                    int quantity = int.Parse(keywords[3]);
                    int interval = int.Parse(keywords[7]);
                    string topic = keywords[5];
                    all_publishers[keywords[1]].publish(topic, "timestamps", quantity, interval);
                }
                else
                {
                    MessageBox.Show("Error on reading input");
                }

            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            String[] str = new string[1];
            str[0] = "Status";
            readInput(str);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            String[] str = new string[1];
            str[0] = "Wait " + textBox1.Text;
            readInput(str);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            String[] str = new string[1];
            str[0] = "Subscriber " + textBox2.Text + " Subscribe " + textBox3.Text;
            readInput(str);


        }

        private void button6_Click(object sender, EventArgs e)
        {
            String[] str = new string[1];
            str[0] = "Subscriber " + textBox4.Text + " Unsubscribe " + textBox5.Text;
            readInput(str);


        }

        private void button7_Click(object sender, EventArgs e)
        {
            String[] str = new string[1];
            str[0] = "Publisher " + textBox6.Text + " Publish " + textBox7.Text + " Ontopic " + textBox8.Text + " Interval " + textBox9.Text;
            readInput(str);
        }

        private void button8_Click(object sender, EventArgs e)
        {
            String[] str = new string[1];
            str[0] = "Crash " + textBox10.Text;
            readInput(str);
        }

        private void button9_Click(object sender, EventArgs e)
        {
            String[] str = new string[1];
            str[0] = "Freeze " + textBox11.Text;
            readInput(str);
        }

        private void button10_Click(object sender, EventArgs e)
        {

            String[] str = new string[1];
            str[0] = "Unfreeze " + textBox12.Text;
            readInput(str);
        }

        private IPAddress LocalIPAddress()
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                return null;
            }

            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());

            return host
                .AddressList
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
