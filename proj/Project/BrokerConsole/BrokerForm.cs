﻿using DADInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrokerConsole
{
    

    public partial class BrokerForm : Form
    {
        public BrokerForm(string[] args)
        {
            InitializeComponent();
            logFormat("Started Process, pid=\"{0}\"", Process.GetCurrentProcess().Id);
            Text = "Broker ";
            int nargs = 6;
            if (args.Length != nargs)
            {
                logFormat("Expected {0} arguments, got {1}", nargs, args.Length);
                return;
            }
            string puppetMasterURI = args[0];
            string name = args[1];
            string site = args[2];
            int port = int.Parse(args[3]);
            string coordinatorURI = args[4];
            string processName = args[5];
            Text = string.Format("{0} {1}",processName,site);
                        
            string channelURI = Utility.setupChannel(port);

            // get the puppetMaster that started this process
            PuppetMaster pm = (PuppetMaster)Activator.GetObject(typeof(PuppetMaster), puppetMasterURI);
            string uri = string.Format("{0}/{1}", channelURI, name);
            BrokerRemote broker = new BrokerRemote(this, pm, uri, name, site, coordinatorURI, processName);
            //we need to register each remote object
            ObjRef o = RemotingServices.Marshal(broker, name, typeof(Broker));
            log(broker.ToString());
            //now that broker is created and marshalled
            //send remote to puppetMaster which is Monitor.waiting for the remote  
            pm.registerBroker(broker);
            logFormat("registered at puppetMaster");    
            
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        public void log(string str)
        {
            if (InvokeRequired)
            {
                Invoke(new stringIn(log), new object[] { str });
            }
            else
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException("lol?");
                }
                logTextBox.AppendText(str + "\r\n");
            }
            
        }
      

        public void logFormat(string f,params object[] args)
        {
            if (args != null && args.Length > 0)
                log(string.Format(f, args));
            else
                log(f);           
        }

        public void setTitle(string t)
        {
            Text = t;
        }

        private void logTextBox_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
