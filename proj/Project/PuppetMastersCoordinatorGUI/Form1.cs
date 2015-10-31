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
        //Vars
        String site_root;
        RoutingPolicy rout;
        OrderingPolicy ord;
        LoggingLevel log;
        string myaddr;
        string log_path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);


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
            //string[] lines = System.IO.File.ReadAllLines(@"./../../../puppermaster.file");
            foreach (string line in lines)
            {
                PuppetMaster obj = (PuppetMaster)Activator.GetObject(typeof(PuppetMaster), line);
                string ip = parseURI(line)[1];
                pms.Add(ip, obj);
            }


        }

        public void log_(string str)
        {
           
            textBox13.Text += str + "\r\n";
            //System.IO.StreamWriter file =  new System.IO.StreamWriter(log_path);
            //file.WriteLine(str);
            //file.Close();
        }


        private void button1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("LOG PATH" + log_path);
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
                }
                else if (type == "LoggingLevel" && keywords.Length >= 2)
                {
                    if (keywords[1] == "full")
                        log = LoggingLevel.full;
                }
                else if (type == "Ordering" && keywords.Length >= 2)
                {
                    if (keywords[1] == "no")
                        ord = OrderingPolicy.no;
                    if (keywords[1] == "total")
                        ord = OrderingPolicy.total;


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
                    string name = parseURI(uri)[3];
                    string process_type = keywords[3];
                    string site = keywords[5];
                    string process_name = keywords[1];

                    switch (process_type)
                    {
                        case "publisher":
                            //create
                            Publisher p = pms[ip].createPublisher(name, site, Int32.Parse(port),myaddr);
                            //associate
                            all_publishers.Add(process_name, p);
                            //add it to site publishers
                            if (!site_publishers.ContainsKey(site))
                                site_publishers.Add(site, new List<Publisher>());
                            site_publishers[site].Add(p);
                            break;
                        case "broker":
                            Broker b = pms[ip].createBroker(name, site, Int32.Parse(port),myaddr);
                            all_brokers.Add(process_name, b);
                            site_brokers.Add(site, b);
                            site_site.Add(site, new Site() { name = site, brokers = new List<Broker>() { b } });
                            break;
                        case "subscriber":
                            Subscriber s = pms[ip].createSubscriber(name, site, Int32.Parse(port),myaddr);
                            all_subscribers.Add(process_name, s);
                            if (!site_subscribers.ContainsKey(site))
                                site_subscribers.Add(site, new List<Subscriber>());
                            site_subscribers[site].Add(s);
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
                if (site_childs.ContainsKey(entry.Key))
                {
                    List<Site> childs = new List<Site>();
                    foreach (string str in site_childs[entry.Key])
                    {
                        if (site_site.ContainsKey(str)) // empty sites
                            childs.Add(site_site[str]);

                    }
                    entry.Value.setChildren(childs);

                }



                if (entry.Key != site_root)
                {
                    Site parentSite = site_site[site_parents[entry.Key]];
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
                readInput(lines);
            }
            catch (FileNotFoundException fnf)
            {
                MessageBox.Show("input.file not found" + fnf);
            }

        }


        public void readInput(String[] lines)
        {
            foreach (String line in lines)
            {
                log_(line);
                string[] keywords = line.Split(' ');
                if (keywords[0] == "Status" && keywords.Length >= 1)
                {

                }
                else if (keywords[0] == "Crash" && keywords.Length >= 2)
                {

                }
                else if (keywords[0] == "Freeze" && keywords.Length >= 2)
                {

                }
                else if (keywords[0] == "Unfreeze" && keywords.Length >= 2)
                {

                }
                else if (keywords[0] == "Wait" && keywords.Length >= 2)
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
                    string msg = "some msg" + DateTime.Now.ToString();
                    all_publishers[keywords[1]].publish(keywords[5], msg, quantity, interval);
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

    }
}
