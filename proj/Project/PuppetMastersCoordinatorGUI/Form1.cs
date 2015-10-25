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
        List<Broker> all_brokers;
        List<Publisher> all_publishers;
        List<Subscriber> all_subscribers;
        //Vars
        String site_root;
        RoutingPolicy rout;
        OrderingPolicy ord;

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
            TcpChannel channel = new TcpChannel();
            ChannelServices.RegisterChannel(channel, true);
            pms = new Dictionary<String, PuppetMaster>();
            string[] lines = System.IO.File.ReadAllLines(@"./../../../puppermaster.file");
            foreach (string line in lines)
            {
                PuppetMaster obj = (PuppetMaster)Activator.GetObject(typeof(PuppetMaster), line);
                string ip = parseURI(line)[1];
                pms.Add(ip, obj);
            }


        }


        private void button1_Click(object sender, EventArgs e)
        {
            site_site = new Dictionary<string, Site>();
            site_parents = new Dictionary<string, string>();
            site_childs = new Dictionary<string, List<string>>();
            site_brokers = new Dictionary<String, Broker>();
            site_publishers = new Dictionary<String, List<Publisher>>();
            site_subscribers = new Dictionary<String, List<Subscriber>>();
            all_brokers = new List<Broker>();
            all_publishers = new List<Publisher>();
            all_subscribers = new List<Subscriber>();
            rout = RoutingPolicy.flooding;
            ord = OrderingPolicy.fifo;
            getPMs();

            string[] lines = System.IO.File.ReadAllLines(@"./../../../config.file");


            foreach (string line in lines)
            {
                string[] keywords = line.Split(' ');

                if (keywords[0] == "RoutingPolicy" && keywords.Length >= 2)
                {
                    if (keywords[1] == "filter")
                        rout = RoutingPolicy.filter;


                }
                else if (keywords[0] == "Ordering" && keywords.Length >= 2)
                {
                    if (keywords[1] == "no")
                        ord = OrderingPolicy.no;
                    if (keywords[1] == "total")
                        ord = OrderingPolicy.total;


                }
                else if (keywords[0] == "Site" && keywords.Length >= 4)
                {
                    if (keywords[3] == "none")
                        site_root = keywords[1];
                    else
                    {
                        site_parents.Add(keywords[1], keywords[3]);
                        if (!site_childs.ContainsKey(keywords[3]))
                            site_childs.Add(keywords[3], new List<string>());
                        site_childs[keywords[3]].Add(keywords[1]);
                    }


                }
                else if (keywords[0] == "Process" && keywords.Length >= 8)
                {
                    string ip = parseURI(keywords[7])[1];
                    string port = parseURI(keywords[7])[2];
                    string name = parseURI(keywords[7])[3];

                    switch (keywords[3])
                    {
                        case "publisher":
                            Publisher p = pms[ip].createPublisher(name, keywords[5], Int32.Parse(port));
                            all_publishers.Add(p);
                            if (!site_publishers.ContainsKey(keywords[5]))
                                site_publishers.Add(keywords[5], new List<Publisher>());
                            site_publishers[keywords[5]].Add(p);
                            break;
                        case "broker":
                            Broker b = pms[ip].createBroker(name, keywords[5], Int32.Parse(port));
                            all_brokers.Add(b);
                            site_brokers.Add(keywords[5], b);
                            site_site.Add(keywords[5], new Site() { name = keywords[5], brokers = new List<Broker>() { b } });
                            break;
                        case "subscriber":
                            Subscriber s = pms[ip].createSubscriber(name, keywords[5], Int32.Parse(port));
                            all_subscribers.Add(s);
                            if (!site_subscribers.ContainsKey(keywords[5]))
                                site_subscribers.Add(keywords[5], new List<Subscriber>());
                            site_subscribers[keywords[5]].Add(s);
                            break;
                        default:
                            MessageBox.Show("Error parsing config.file!");
                            break;
                    }


                }
                else
                    MessageBox.Show("Error parsing config.file!");
            }
            foreach (Broker b in all_brokers)
            {
                b.setRoutingPolicy(rout);
                b.setOrderingPolicy(ord);
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

            // foreach (KeyValuePair<string, PuppetMaster> entry in pms)
            //   MessageBox.Show("PM @ " + entry.Key + " " + entry.Value.status());
            foreach(Subscriber s in site_subscribers["site0"])
                s.subscribe("/tempo/lisboa");

            foreach (Publisher p in site_publishers["site0"])
            {
                p.publish("/tempo/lisboa", "chove");
                p.publish("/tempo/porto", "neve");
            }
            MessageBox.Show("Phase2");
            foreach (Subscriber s in site_subscribers["site0"])
            {
                s.unsubscribe("/tempo/lisboa");
                s.subscribe("/tempo/*");
            }
                
            foreach (Publisher p in site_publishers["site0"])
            {
                p.publish("/tempo/lisboa", "chove");
                p.publish("/tempo/porto", "neve");
            }



        }
    }
}
