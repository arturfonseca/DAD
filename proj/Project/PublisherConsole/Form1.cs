using DADInterfaces;
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

namespace PublisherConsole
{
    public partial class Form1 : Form
    {
        public Form1(string[] args)
        {
            InitializeComponent();
            logFormat("Started Process, pid=\"{0}\"", Process.GetCurrentProcess().Id);
            Text = "Publisher ";
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
            string addr = args[4];
            string processName = args[5];
            string channelURI = Utility.setupChannel(port);
            Text = string.Format("{0} {1}", processName, site);

            // get the puppetMaster that started this process
            PuppetMaster pm = (PuppetMaster)Activator.GetObject(typeof(PuppetMaster), puppetMasterURI);
            PublisherRemote publisher = new PublisherRemote(this,pm, name, site, addr,processName);
            //we need to register each remote object
            ObjRef o = RemotingServices.Marshal(publisher, name, typeof(Publisher));
            publisher.setURI(string.Format("{0}/{1}", channelURI, name));
            log(publisher.ToString());
            //now that broker is created and marshalled
            //send remote to puppetMaster which is Monitor.waiting for the remote            
            pm.registerPublisher(publisher);
            logFormat("Just registered at puppetMaster");
            logFormat("Press key to leave");
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


        public void logFormat(string f, params object[] args)
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

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
