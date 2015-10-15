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
using System.Collections;

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
            _server.enviaMsg(this, msg);
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

        public void enviaMsg(ChatSessionRemote session, string msg)
        {
            Console.WriteLine("Sending message \"{0}:{1}\"", session.getNick(), msg);
            foreach (ChatSessionRemote s in sessions){
                if(s != session)
                {
                    s.recebeMsg(s.getNick(), msg);
                    Console.WriteLine("Sent to \"{0}\"", s.getNick());                    
                }
            }
            
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
            //create process channel
            BinaryServerFormatterSinkProvider ssp = new BinaryServerFormatterSinkProvider();            
            BinaryClientFormatterSinkProvider csp = new BinaryClientFormatterSinkProvider();
            ssp.TypeFilterLevel = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;
            IDictionary props = new Hashtable();
            props["port"] = 8787;
            TcpChannel channel = new TcpChannel(props, csp, ssp);
            ChannelServices.RegisterChannel(channel, false);

            // print uris
            Console.WriteLine("Opened channel at uris:");
            ChannelDataStore cds = (ChannelDataStore)channel.ChannelData;
            foreach(string url in cds.ChannelUris)
            {
                Console.WriteLine(" - '{0}'", url);
            }
            
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
