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
        private List<Broker> _brokers = new List<Broker>();
        private List<Publisher> _publishers = new List<Publisher>();
        private List<Subscriber> _subscribers = new List<Subscriber>();

        private string _uri = "";
        public string URI { get { return _uri; } set { _uri = value; } }

        static void Main(string[] args)
        {
            Console.WriteLine("Started PuppetMaster");
            string channelURI = Utility.setupChannel(45000);

            PuppetMasterRemote puppetMaster = new PuppetMasterRemote();
            //we need to register each remote object
            ObjRef o = RemotingServices.Marshal(puppetMaster, Constants.PuppetMasterURI, typeof(PuppetMaster));
            puppetMaster.URI = string.Format("{0}/{1}", channelURI, Constants.PuppetMasterURI);
            Console.WriteLine("Created PuppetMaster at \"{0}\"", puppetMaster.URI);


            // testing code
            Broker b1 = puppetMaster.createBroker("broker1","site1",3333);
            Publisher p1 = puppetMaster.createPublisher("publisher1","site1",3334);
            Subscriber s1 = puppetMaster.createSubscriber("subscriber1", "site1", 3335);
            Broker b2 = puppetMaster.createBroker("broker2", "site2", 3336);
            Subscriber s2 = puppetMaster.createSubscriber("subscriber2", "site2", 3337);
            Publisher p2 = puppetMaster.createPublisher("publisher2", "site2", 3338);

            // connect everything
            var site1 = new Site() { name = "site1", brokers = new List<Broker>() { b1 } };
            var site2 = new Site() { name = "site2", brokers = new List<Broker>() { b2 } };
            //site1
            p1.setSiteBroker(b1);
            s1.setSiteBroker(b1);
            b1.setPublishers(new List<Publisher> { p1 });
            b1.setSubscribers(new List<Subscriber> { s1 });            
            b1.setParent(site2);
            //site2
            s2.setSiteBroker(b2);
            p2.setSiteBroker(b2);
            b2.setSubscribers(new List<Subscriber>() { s2 });
            b2.setChildren(new List<Site>() { site1 });

            // make events happen
            p1.publish("tempo", "hoje chove");
            p2.publish("tempo", "hoje ha figados");
            Console.WriteLine("Press key to leave");
            Console.Read();
        }

        public Broker createBroker(string name,string site,int port)
        {
            //start processes
            Process p = new Process();
            p.StartInfo.FileName = Constants.BrokerExecutableLocation;
            Console.WriteLine("launching Broker at '{0}'",Constants.BrokerExecutableLocation);
            p.StartInfo.Arguments = string.Format("{0} {1} {2} {3}",URI,name,site,port);
            p.Start();
            Broker b = null;
            lock (_brokers)
            {
                while (true)
                {
                    b = _brokers.Find(x => x.getName() == name);
                    if(b != null)
                        break;
                    Monitor.Wait(_brokers);
                }
            }
            return b;
        }

        public Publisher createPublisher(string name,string site,int port)
        {
            //start processes
            Process p = new Process();
            p.StartInfo.FileName = Constants.PublisherExecutableLocation;
            Console.WriteLine("launching Publisher at '{0}'", Constants.PublisherExecutableLocation);
            p.StartInfo.Arguments = string.Format("{0} {1} {2} {3}",URI,name,site,port);
            p.Start();
            Publisher b = null;
            lock (_publishers)
            {
                while (true)
                {
                    b = _publishers.Find(x => x.getName() == name);
                    if (b != null)
                        break;
                    Monitor.Wait(_publishers);
                }
            }
            return b;
        }

        public Subscriber createSubscriber(string name,string site,int port)
        {
            //start processes
            Process p = new Process();
            p.StartInfo.FileName = Constants.SubscriberExecutableLocation;
            Console.WriteLine("launching Subscriber at '{0}'", Constants.SubscriberExecutableLocation);
            p.StartInfo.Arguments = string.Format("{0} {1} {2} {3}",URI,name,site,port);
            p.Start();
            Subscriber b = null;
            lock (_subscribers)
            {
                while (true)
                {
                    b = _subscribers.Find(x => x.getName() == name);
                    if (b != null)
                        break;
                    Monitor.Wait(_subscribers);
                }
            }
            return b;
        }

        public List<Broker> getBrokers()
        {
            return _brokers;
        }

        public List<Publisher> getPublishers()
        {
            return _publishers;
        }

        public List<Subscriber> getSubscribers()
        {
            return _subscribers;
        }

        public void registerBroker(Broker b)
        {
            lock (_brokers)
            {
                _brokers.Add(b);
                Monitor.Pulse(_brokers);
            }            
            Console.WriteLine("registered broker {0}",b.getURI());
        }

        public void registerPublisher(Publisher p)
        {
            lock (_publishers)
            {
                _publishers.Add(p);
                Monitor.Pulse(_publishers);
            }
            Console.WriteLine("registered publisher {0}",p.getURI());
        }

        public void registerSubscriber(Subscriber s)
        {
            lock (_subscribers)
            {
                _subscribers.Add(s);
                Monitor.Pulse(_subscribers);
            }
            Console.WriteLine("registered subscriber {0}",s.getURI());
        }

        public void reportEvent(string origin_uri,string e)
        {
            Console.WriteLine("[Event] from:\"{0}\" \"{1}\"",origin_uri,e);
        }
    }
}
