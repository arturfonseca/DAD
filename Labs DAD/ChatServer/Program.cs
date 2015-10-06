using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
using System.Threading.Tasks;
using Interfaces;
using System.Runtime.Remoting;

namespace ChatServer
{
    class ChatSessionRemote : MarshalByRefObject, IChatSessionRemote
    {
        private string _nick;
        private ChatServerRemote _server;
        private IChatClientRemote _client;

        public string getNick()
        {
            return _nick;
        }

        public ChatSessionRemote(ChatServerRemote server, IChatClientRemote client, string nick)
        {
            _server = server;
            _nick = nick;
            _client = client;
        }

        public void enviaMsg(string msg) //called by the client
        {
            _server.enviaMsg(_nick, msg);
        }

        internal void recebeMsg(string nick, string msg)
        {
            _client.recebeMsg(nick, msg);
        }
    }

    class ChatServerRemote : MarshalByRefObject, IChatServerRemote
    {
        private List<ChatSessionRemote> sessions;

        public ChatServerRemote()
        {
            sessions = new List<ChatSessionRemote>();
        }

        public void enviaMsg(string nick, string msg)
        {            
            foreach(ChatSessionRemote s in sessions){
                if(s.getNick() != nick)
                {
                    s.recebeMsg(nick, msg);
                }
            }
            Console.WriteLine("{0}:{1}", nick, msg);
        }

        public IChatSessionRemote regista(string nick, IChatClientRemote myremote)
        {
            ChatSessionRemote session = new ChatSessionRemote(this, myremote, nick);
            RemotingServices.Marshal(session);
            Console.WriteLine("used \"{0}\" connected", nick);
            sessions.Add(session);
            return session;
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            TcpChannel channel = new TcpChannel(8787);
            Console.WriteLine("Opened channel at uris:");
            ChannelDataStore cds = (ChannelDataStore)channel.ChannelData;
            foreach(string url in cds.ChannelUris)
            {
                Console.WriteLine(" - '{0}'", url);
            }
            ChannelServices.RegisterChannel(channel, false);
            ChatServerRemote cs = new ChatServerRemote();
            RemotingServices.Marshal(cs, "ChatServer", typeof(IChatServerRemote));
            Console.WriteLine("Server started. enter \"chatquit\" to exit");
            while (true)
            {
                if (Console.ReadLine() == "chatquit")
                    break;
            }
            Console.WriteLine("Exiting....");

        }
    }
}
