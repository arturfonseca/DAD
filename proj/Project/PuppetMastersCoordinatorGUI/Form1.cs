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


namespace PuppetMastersCoordinatorGUI
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        

        private void button1_Click(object sender, EventArgs e)
        {

           
            
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
                            MessageBox.Show("Error!");
                            break;
                    }


                }
                else
                    MessageBox.Show("Error!");
            }              

    
        }
    }
}
