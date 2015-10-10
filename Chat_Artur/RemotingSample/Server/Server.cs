using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using Interfaces;
using System.Collections.Generic;
using System.Collections;

namespace RemotingSample
{


    public class ServerLaucher
    {
        static void Main(string[] args)
        {

            IDictionary RemoteChannelProperties = new Hashtable();
            RemoteChannelProperties["port"] = "8086";
            RemoteChannelProperties["name"] = "tcp1";
            TcpChannel chan = new TcpChannel(RemoteChannelProperties, null, null);
            ChannelServices.RegisterChannel(chan, true);

            Server server = new Server();
            RemotingServices.Marshal(server, "ChatServer", typeof(Server));
            System.Console.WriteLine("<enter> para sair...");
            System.Console.ReadLine();
        }


    }

    public class Server : MarshalByRefObject, IServer
    {
        Dictionary<string, IClient> clients;

        public Server()
        {
            clients = new Dictionary<string, IClient>();

        }
        public void enviaMsg(string msg)
        {

            foreach (KeyValuePair<string, IClient> entry in clients)
            {
                entry.Value.recebeMsg("User", msg);
            }

        }



        public void registar(string port)
        {
            Random rnd = new Random();
            IDictionary RemoteChannelProperties = new Hashtable();
            RemoteChannelProperties["port"] = 0;
            RemoteChannelProperties["name"] = "tcp" + rnd.Next(1000, 7000);
            TcpChannel chan = new TcpChannel(RemoteChannelProperties, null, null);
            ChannelServices.RegisterChannel(chan, true);

            IClient client_obj = (IClient)Activator.GetObject(
                typeof(IClient),
                "tcp://localhost:" + port + "/ChatClient");
            clients.Add(port, client_obj);
            Console.WriteLine("Registed user:" + port);

        }
    }
}