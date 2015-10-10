using DADInterfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
using System.Threading.Tasks;

namespace BrokerConsole
{
    class BrokerRemote:MarshalByRefObject, Broker
    {
        private string brokerName;
        private string uri;

        public BrokerRemote(string brokerName,string puppetMasterURI)
        {
            this.brokerName = brokerName;
        }

        public string URI { get { return uri; } set { uri = value; } }

        public string getName() { return brokerName; }

        static void Main(string[] args)
        {
            if(args.Length != 2)
            {
                Console.WriteLine("Excepted 2 arguments, got {0}", args.Length);
                Console.Read();
                return;
            }
            string puppetMasterURI = args[0];
            string brokerName = args[1];

            //create process channel
            BinaryServerFormatterSinkProvider ssp = new BinaryServerFormatterSinkProvider();
            BinaryClientFormatterSinkProvider csp = new BinaryClientFormatterSinkProvider();
            ssp.TypeFilterLevel = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;
            IDictionary props = new Hashtable();
            props["port"] = 0;
            TcpChannel channel = new TcpChannel(props, csp, ssp);
            ChannelServices.RegisterChannel(channel, true);

            // print uris
            ChannelDataStore cds = (ChannelDataStore)channel.ChannelData;
            string channelURI = cds.ChannelUris[0];
            Console.WriteLine("Opened remoting channel at \"{0}\"", channelURI);

            BrokerRemote broker = new BrokerRemote(brokerName,puppetMasterURI);
            //we need to register each remote object
            ObjRef o = RemotingServices.Marshal(broker, brokerName, typeof(Broker));
            broker.URI = string.Format("{0}/{1}", channelURI, brokerName);
            Console.WriteLine("Created Broker at \"{0}\"", broker.URI);

            PuppetMaster pm = (PuppetMaster)Activator.GetObject(typeof(PuppetMaster), puppetMasterURI);
            pm.registerBroker(broker);
            Console.WriteLine("Just registered at puppetMaster");
            Console.Read();
        }

        public void crash()
        {
            throw new NotImplementedException();
        }

        public void freeze()
        {
            throw new NotImplementedException();
        }

        public string getURI()
        {
            return URI;
        }

        public void setChildren(string child_site, List<string> uri)
        {
            throw new NotImplementedException();
        }

        public void setParent(string uri)
        {
            throw new NotImplementedException();
        }

        public void setSiteBrokers(List<string> uris)
        {
            throw new NotImplementedException();
        }

        public string status()
        {
            throw new NotImplementedException();
        }

        public void unfreeze()
        {
            throw new NotImplementedException();
        }
    }
}
