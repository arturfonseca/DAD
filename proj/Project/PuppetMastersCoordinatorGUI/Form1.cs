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
using PuppetMasterConsole;
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

        private Dictionary<String,PuppetMasterRemote> getPMs()
        {
            TcpChannel channel = new TcpChannel();
            ChannelServices.RegisterChannel(channel, false);
            Dictionary<String, PuppetMasterRemote> dic = new Dictionary<String, PuppetMasterRemote>();
              string[] lines = System.IO.File.ReadAllLines(@"./../../../puppermaster.file");
            foreach (string line in lines)
            {
                string[] tokens = line.Split('/');
                if (tokens.Length != 3)
                {
                    MessageBox.Show("Error parsing puppetmaster.file!");
                }
                else
                {
                    /*
                     PuppetMasterRemote obj = (PuppetMasterRemote)Activator.GetObject(
               typeof(PuppetMasterRemote),
               "tcp://"+tokens[0]+":" + tokens[1] + "/"+tokens[2]);
                    dic.Add(tokens[0], obj);
                     */
                    MessageBox.Show("tcp://" + tokens[0] + ":" + tokens[1] + "/" + tokens[2]);
                }
            }
            return dic;

        }
        

        private void button1_Click(object sender, EventArgs e)
        {

            Dictionary<String, PuppetMasterRemote> pms = getPMs();

               string[] lines = System.IO.File.ReadAllLines(@"./../../../config.file");


            foreach (string line in lines)
            {
                string[] keywords = line.Split(' ');

                if (keywords[0]=="RoutingPolicy" && keywords.Length >=2)
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
                    string[] temp = keywords[7].Split('/');
                    string remoteName = temp[3];
                    string aux = temp[2];
                    string ip = aux.Split(':')[0];
                    string port = aux.Split(':')[1];
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
    }
}
