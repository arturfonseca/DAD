using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting;
using DADInterfaces;
using System.Collections;
using System.Threading;
using System.Configuration;

namespace PuppetMasterConsole
{
    static class Constants
    {
        public static string PuppetMasterURI { get { return "PuppetMaster"; } }
        public static string BrokerExecutableLocation { get { return ConfigurationManager.AppSettings["BrokerPath"]; } }
        public static string PublisherExecutableLocation { get { return ConfigurationManager.AppSettings["PublisherPath"]; } }
        public static string SubscriberExecutableLocation { get { return ConfigurationManager.AppSettings["SubscriberPath"]; } }

    }
    class PuppetMasterRemote:MarshalByRefObject, PuppetMaster
    {
        private List<Broker> brokers = new List<Broker>();
        private List<Publisher> publishers = new List<Publisher>();
        private List<Subscriber> subscribers = new List<Subscriber>();

        private string _uri = "";
        public string URI { get { return _uri; } set { _uri = value; } }

        static void Main(string[] args)
        {
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

            PuppetMasterRemote puppetMaster = new PuppetMasterRemote();
            //we need to register each remote object
            ObjRef o = RemotingServices.Marshal(puppetMaster, Constants.PuppetMasterURI, typeof(PuppetMaster));
            puppetMaster.URI = string.Format("{0}/{1}", channelURI, Constants.PuppetMasterURI);
            Console.WriteLine("Created PuppetMaster at \"{0}\"", puppetMaster.URI);
            // testing code
            Broker b = puppetMaster.createBroker("broker1", "a", "a", null);
            Console.WriteLine("Got broker object '{0}'",b.getURI()); 

            Console.WriteLine("Press any key");           
            Console.Read();
        }

        public Broker createBroker(string name, string site, string parent_site, List<string> children_sites)
        {
            //start processes
            Process p = new Process();
            p.StartInfo.FileName = Constants.BrokerExecutableLocation;
            p.StartInfo.Arguments = string.Format("{0} {1}",URI,name);
            p.Start();
            Broker b = null;
            lock (brokers)
            {
                while (true)
                {
                    b = brokers.Find(x => x.getName() == name);
                    if(b != null)
                        break;
                    Monitor.Wait(brokers);
                }
            }
            return b;
        }

        public Publisher createPublisher(string name, string site, string uri)
        {
            throw new NotImplementedException();
        }

        public Subscriber createSubscriber(string name, string site, string uri)
        {
            throw new NotImplementedException();
        }

        public List<Broker> getBrokers()
        {
            throw new NotImplementedException();
        }

        public List<Publisher> getPublishers()
        {
            throw new NotImplementedException();
        }

        public List<Subscriber> getSubscribers()
        {
            throw new NotImplementedException();
        }

        public void registerBroker(Broker b)
        {
            lock (brokers)
            {
                brokers.Add(b);
                Monitor.Pulse(brokers);
            }
            
            Console.WriteLine("registered broker");
        }

        public void registerPublisher(Publisher p)
        {
            throw new NotImplementedException();
        }

        public void registerSubscriber(Subscriber s)
        {
            throw new NotImplementedException();
        }
    }
}
