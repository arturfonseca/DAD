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
   public class PuppetMasterRemote:MarshalByRefObject, PuppetMaster
    {
        private List<Broker> _brokers = new List<Broker>();
        private List<Publisher> _publishers = new List<Publisher>();
        private List<Subscriber> _subscribers = new List<Subscriber>();
        public List<Process> processes = new List<Process>();

        private string _uri = "";
        private string _channel_uri;
        public string URI { get { return _uri; } set { _uri = value; } }

        public PuppetMasterRemote(string channelURI)
        {
            _channel_uri = channelURI;
        }
        public override object InitializeLifetimeService()
        {
            return null;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Started PuppetMaster");
            int port = int.Parse(ConfigurationManager.AppSettings["PuppetMasterPort"]);
            string channelURI = Utility.setupChannel(port);
            PuppetMasterRemote pm = new PuppetMasterRemote(channelURI);
            //we need to register each remote object
            string service = ConfigurationManager.AppSettings["PuppetMasterService"];
            ObjRef o = RemotingServices.Marshal(pm, service, typeof(PuppetMaster));
            pm.URI = string.Format("{0}/{1}", channelURI, service);
            Console.WriteLine("Created PuppetMaster at \"{0}\"", pm.URI);

            test1(pm);
            //test2(pm);
            Console.WriteLine("Press key to leave");
            Console.Read();
            // quick way to close all windows
            foreach(Process p in pm.processes)
            {
                try {
                    p.Kill();
                }
                catch (System.InvalidOperationException) { }
            }
        }

        public static void test2(PuppetMaster pm)
        {
            // testing code
            Broker b1 = pm.createBroker("broker1", "site1", 3333);
            Publisher p1 = pm.createPublisher("publisher1", "site1", 3334);
            Subscriber s1 = pm.createSubscriber("subscriber1", "site1", 3335);
            Broker b2 = pm.createBroker("broker2", "site2", 3336);
            Subscriber s2 = pm.createSubscriber("subscriber2", "site2", 3337);
            Publisher p2 = pm.createPublisher("publisher2", "site2", 3338);

            // connect everything
            var site1 = new Site() { name = "site1", brokers = new List<Broker>() { b1 } };
            var site2 = new Site() { name = "site2", brokers = new List<Broker>() { b2 } };
            //site1
            p1.setSiteBroker(b1);
            s1.setSiteBroker(b1);
            b1.setPublishers(new List<Publisher> { p1 });
            b1.setSubscribers(new List<Subscriber> { s1 });
            b1.setParent(site2);
            b1.setRoutingPolicy(RoutingPolicy.filter);
            //site2
            s2.setSiteBroker(b2);
            p2.setSiteBroker(b2);
            b2.setSubscribers(new List<Subscriber>() { s2 });
            b2.setChildren(new List<Site>() { site1 });
            Console.WriteLine("I'm executing test2");
            b2.setIsRoot();
            b2.setRoutingPolicy(RoutingPolicy.filter);

            // make events happen
            Console.WriteLine("All processes created and setup upped, debug now if you wish, press any key to start events");
            Console.ReadLine();
            s1.subscribe("arroz");
           
            s2.subscribe("batata");
            p1.publish("batata", "batata");
            p2.publish("arroz", "arroz");

            
        }

        public static void test1(PuppetMaster pm)
        {
            // testing code
            Broker b1 = pm.createBroker("broker1", "site1", 3333);
            Publisher p1 = pm.createPublisher("publisher1", "site1", 3334);
            Subscriber s1 = pm.createSubscriber("subscriber1", "site1", 3335);
            // connect everything           
            //site1
            p1.setSiteBroker(b1);
            s1.setSiteBroker(b1);
            b1.setPublishers(new List<Publisher> { p1 });
            b1.setSubscribers(new List<Subscriber> { s1 });
            // make events happen
            Console.WriteLine("All processes created and setup upped, debug now if you wish, press any key to start events");
            Console.ReadLine();
            s1.subscribe("/tempo/lisboa");
            p1.publish("/tempo/lisboa", "chove");
            p1.publish("/tempo/porto", "neve");
            Console.WriteLine("phase 1 complete. press key to continue");
            Console.ReadLine();
            s1.subscribe("/tempo/*");
            p1.publish("/tempo/porto", "neve");
        }

        public Broker createBroker(string name,string site,int port)
        {
            //start processes
            Process p = new Process();
            p.StartInfo.FileName = ConfigurationManager.AppSettings["BrokerPath"];
            var arg = string.Format("{0} {1} {2} {3}", URI, name, site, port);                       
            p.StartInfo.Arguments = arg;
            Console.WriteLine("launching Broker executable '{0}' ", p.StartInfo.FileName);
            p.Start();
            processes.Add(p);
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
            p.StartInfo.FileName = ConfigurationManager.AppSettings["PublisherPath"];
            Console.WriteLine("launching Publisher at '{0}'", p.StartInfo.FileName);
            p.StartInfo.Arguments = string.Format("{0} {1} {2} {3}",URI,name,site,port);
            p.Start();
            processes.Add(p);
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
            p.StartInfo.FileName = ConfigurationManager.AppSettings["SubscriberPath"];
            Console.WriteLine("launching Subscriber at '{0}'", p.StartInfo.FileName);
            p.StartInfo.Arguments = string.Format("{0} {1} {2} {3}",URI,name,site,port);
            p.Start();
            processes.Add(p);
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
