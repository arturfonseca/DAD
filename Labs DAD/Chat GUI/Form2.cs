using Interfaces;
using System;
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
            
        private void connect_click(object sender, EventArgs e)
        {
            //TODO input validation and sanitation            
            TcpChannel channel = new TcpChannel();
            ChannelServices.RegisterChannel(channel, false);
            try
            {
                string uri = string.Format("tcp://{0}:{1}/{2}", _ip.Text, _port.Text, "ChatServer");
                IChatServerRemote server = (IChatServerRemote)Activator.GetObject(typeof(IChatServerRemote), uri);
                ChatClientRemote client = new ChatClientRemote(this);
                RemotingServices.Marshal(client);
                _client = client;
                IChatSessionRemote session = server.regista(_nick.Text, client);
                _session = session;

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
