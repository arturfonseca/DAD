using Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApplication1
{
    enum chatState { On, Off};
    public partial class Form2 : Form
    {
        private IChatSessionRemote _session;
        private ChatClientRemote _client;
        private chatState _chatState = chatState.Off;

        public Form2()
        {
            InitializeComponent();
        }

        private void writeLine(string s)
        {
            _log.Text += String.Format("[{0}]{1}\r\n", DateTime.Now.ToString("HH:mm:ss"), s);
        }

        public void recebeMsg(string nick, string msg)
        {
            writeLine(string.Format("{0}:{1}", nick, msg));
        }

        public void Log(string msg)
        {
            recebeMsg("System", msg);
        }
            
        private void connect_click(object sender, EventArgs e)
        {
            // TODO only one channel can be created, avoid created one each time button is clicked
            //create process channel
            BinaryServerFormatterSinkProvider ssp = new BinaryServerFormatterSinkProvider();
            BinaryClientFormatterSinkProvider csp = new BinaryClientFormatterSinkProvider();
            ssp.TypeFilterLevel = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;
            IDictionary props = new Hashtable();
            props["port"] = 0; // 0 means choose a random port
            TcpChannel channel = new TcpChannel(props, csp, ssp);
            ChannelServices.RegisterChannel(channel, true);

            // print uris
            Log("Opened channel at uris:");
            ChannelDataStore cds = (ChannelDataStore)channel.ChannelData;
            foreach (string url in cds.ChannelUris)
            {
               Log(string.Format(" - '{0}'", url));
            }

            //TODO input validation and sanitation          
            try
            {
                string uri = string.Format("tcp://{0}:{1}/{2}", _ip.Text, _port.Text, "ChatServer");
                Log(String.Format("Trying to connect to {0}", uri));
                IChatServerRemote server = (IChatServerRemote)Activator.GetObject(typeof(IChatServerRemote), uri);
                ChatClientRemote client = new ChatClientRemote(this);
                RemotingServices.Marshal(client);
                _client = client;
                IChatSessionRemote session = server.regista(_nick.Text, client);
                _session = session;
                _chatState = chatState.On;

            }
            catch(Exception exc)
            {
                recebeMsg("System", "Failed to connect to server");
                recebeMsg("System", exc.ToString());                
                return;
            }
                       
        }

        private void send_click(object sender, KeyPressEventArgs e)
        {            
            if(e.KeyChar == (char)Keys.Enter){
                e.Handled = true;
                if (chatState.Off == _chatState)
                {
                    recebeMsg("System", "Not connected. Can not send message");
                    return;
                }
                if (_send_input.Text == "") return;
                _session.enviaMsg(_send_input.Text);
                recebeMsg(_nick.Text, _send_input.Text);
                _send_input.Clear();
                
            }
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }


    }
}
