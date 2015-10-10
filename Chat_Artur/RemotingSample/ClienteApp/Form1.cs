using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels;
using System.Net.Sockets;
using Interfaces;
using System.Collections;


namespace RemotingSample
{
    public partial class Form1 : Form
    {
        IServer obj;
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            String _clientPort = textBox2.Text;
            //CLIENT->SERVER
            TcpChannel channel = new TcpChannel();
            ChannelServices.RegisterChannel(channel, true);

            obj = (IServer)Activator.GetObject(
               typeof(IServer),
               "tcp://localhost:8086/ChatServer");

            //CLIENT<-SERVER
            IDictionary RemoteChannelProperties = new Hashtable();
            RemoteChannelProperties["port"] = _clientPort;
            RemoteChannelProperties["name"] = "tcp" + _clientPort;
            TcpChannel chan = new TcpChannel(RemoteChannelProperties, null, null);
            ChannelServices.RegisterChannel(chan, true);
            ChatClientRemote client = new ChatClientRemote(this);
            RemotingServices.Marshal(client, "ChatClient", typeof(ChatClientRemote));


            obj.registar(_clientPort);
            
        }

        private void button2_Click(object sender, EventArgs e)
        {
            obj.enviaMsg(textBox3.Text);

        }
        public void recebeMsg(string a, string b)
        {

            textBox4.Text += "User: " + b + "\r\n";

        }


    }
}

