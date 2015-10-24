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

        public Form1()
        {
            InitializeComponent();
        }

        Dictionary<String, PuppetMaster> pms;

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
                    PuppetMaster obj = (PuppetMaster)Activator.GetObject(typeof(PuppetMaster),line);
                    string ip = parseURI(line)[1];
                    pms.Add(ip, obj);
            }
            

        }
        

        private void button1_Click(object sender, EventArgs e)
        {

            getPMs();

            string[] lines = System.IO.File.ReadAllLines(@"./../../../config.file");


            foreach (string line in lines)
            {
                string[] keywords = line.Split(' ');

                if (keywords[0] == "RoutingPolicy" && keywords.Length >= 2)
                {


                }
                else if (keywords[0] == "Ordering" && keywords.Length >= 2)
                {


                }
                else if (keywords[0] == "Site" && keywords.Length >= 4)
                {


                }
                else if (keywords[0] == "Process" && keywords.Length >= 8)
                {
                    string ip=parseURI(keywords[7])[1];
                    string port = parseURI(keywords[7])[2];
                    string name = parseURI(keywords[7])[3];

                    switch (keywords[3])
                    {
                        case "publisher":
                            
                            //creat publisher(keywords[0],keywords[5], port);

                            break;
                        case "broker":
                            
                            break;
                        case "subscriber":
                            
                            break;
                        default:
                            MessageBox.Show("Error parsing config.file!");
                            break;
                    }


                }
                else
                    MessageBox.Show("Error parsing config.file!");
            }              

    
        }

        private void button2_Click(object sender, EventArgs e)
        {
            foreach (KeyValuePair<string, PuppetMaster> entry in pms)
            {
                MessageBox.Show("PM @ " + entry.Key+" " + entry.Value.status()  );
                
            }
            
            //pms["localhost"].test1(pms["localhost"]);
        }
    }
}
