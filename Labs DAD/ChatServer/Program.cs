using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
using System.Threading.Tasks;
using Interfaces;
using System.Runtime.Remoting;

namespace ChatServer
{
    class ChatServerRemote : MarshalByRefObject, IChatServerRemote
    {
        private List<Tuple<string, IChatClientRemote>> clients;

        public ChatServerRemote()
        {
            clients = new List<Tuple<string, IChatClientRemote>>();
        }

        public void enviaMsg(string nick, string msg)
        {
            foreach(Tuple<string,IChatClientRemote> reg in clients){
                if(reg.Item1 != nick)
                {
                    reg.Item2.recebeMsg(nick, msg);
                }
            }
        }

        public void regista(string nick, IChatClientRemote myremote)
        {
            clients.Add(new Tuple<string, IChatClientRemote>(nick, myremote));
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            TcpChannel channel = new TcpChannel(8787);
            ChannelServices.RegisterChannel(channel, false);
            ChatServerRemote cs = new ChatServerRemote();
            RemotingServices.Marshal(cs, "ChatServer", typeof(ChatServerRemote));
            Console.WriteLine("Server started. enter chatquit to exit");
            while (true)
            {
                if (Console.ReadLine() == "chatquit")
                    break;
            }
            Console.WriteLine("Exiting....");

        }
    }
}
