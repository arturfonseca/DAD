using DADInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PuppetMasterConsole
{
    public partial class Form1 : Form
    {
        private PuppetMasterRemote _pm;

        public Form1()
        {
            InitializeComponent();
            log("Started PuppetMaster");
            int port = int.Parse(ConfigurationManager.AppSettings["PuppetMasterPort"]);
            string channelURI = Utility.setupChannel(port);
            PuppetMasterRemote pm = new PuppetMasterRemote(channelURI,this);
            _pm = pm;
            //we need to register each remote object
            string service = ConfigurationManager.AppSettings["PuppetMasterService"];
            ObjRef o = RemotingServices.Marshal(pm, service, typeof(PuppetMaster));
            pm.URI = string.Format("{0}/{1}", channelURI, service);
            logFormat("Created PuppetMaster at \"{0}\"", pm.URI);
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

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            foreach (Process p in _pm.processes)
            {
                try
                {
                    p.Kill();
                }
                catch (System.InvalidOperationException) { }
            }
        }
    }
}
