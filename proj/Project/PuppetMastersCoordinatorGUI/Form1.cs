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
        Dictionary<String, String> site_parents;
        Dictionary<String, List<String>> site_childs;
        Dictionary<String, List<Broker>> site_brokers;
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
            site_parents = new Dictionary<string, string>();
            site_childs = new Dictionary<string, List<string>>();
            site_brokers = new Dictionary<String, List<Broker>>();
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
                    site_brokers.Add(keywords[1], new List<Broker>());
                    site_publishers.Add(keywords[1], new List<Publisher>());
                    site_subscribers.Add(keywords[1], new List<Subscriber>());
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
                            site_publishers[ip].Add(p);
                            break;
                        case "broker":
                            Broker b = pms[ip].createBroker(name, keywords[5], Int32.Parse(port));
                            all_brokers.Add(b);
                            site_brokers[ip].Add(b);
                            break;
                        case "subscriber":
                            Subscriber s = pms[ip].createSubscriber(name, keywords[5], Int32.Parse(port));
                            all_subscribers.Add(s);
                            site_subscribers[ip].Add(s);
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

            /* CODE TO TEST TREE
            MessageBox.Show("Root is "+site_root);

            foreach (KeyValuePair<string, string> entry in site_parents)
            {
                MessageBox.Show(entry.Key+" father is "+entry.Value);

            }
            foreach (KeyValuePair<string, List<string>> entry in site_childs)
            {
                foreach(String c in entry.Value)
                MessageBox.Show("Father: "+entry.Key+" Child: "+c);

            }
            */

        }

        private void button2_Click(object sender, EventArgs e)
        {
            foreach (KeyValuePair<string, PuppetMaster> entry in pms)
            {
                MessageBox.Show("PM @ " + entry.Key + " " + entry.Value.status());

            }

            //pms["localhost"].test1(pms["localhost"]);
        }
    }
}
