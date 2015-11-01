using DADInterfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SubscriberConsole
{
    class SubscriberRemote : MarshalByRefObject, Subscriber
    {
        private PuppetMaster _pm;
        private string _name;
        private string _site;
        private string _uri;
        private Broker _broker;
        // used to order messages
        private int _seqnum = 0;
        private List<string> _subscribedTopics = new List<string>();
        private Object thisLock = new Object();
        private ICoordinator c;
        private bool _freezed = false;
        private List<ThreadStart> _freezedThreads = new List<ThreadStart>();

        public SubscriberRemote(PuppetMaster pm, string name, string site, string addr)
        {
            _name = name;
            _pm = pm;
            _site = site;
            c = (ICoordinator)Activator.GetObject(typeof(ICoordinator), addr);
            
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Started Subscriber, pid=\"{0}\"", Process.GetCurrentProcess().Id);
            int nargs = 5;
            if (args.Length != nargs)
            {
                Console.WriteLine("Expected {0} arguments, got {1}", nargs, args.Length);
                Console.Read();
                return;
            }
            string puppetMasterURI = args[0];
            string name = args[1];
            string site = args[2];
            int port = int.Parse(args[3]);
            string addr = args[4];

            string channelURI = Utility.setupChannel(port);

            // get the puppetMaster that started this process
            PuppetMaster pm = (PuppetMaster)Activator.GetObject(typeof(PuppetMaster), puppetMasterURI);
            SubscriberRemote subscriber = new SubscriberRemote(pm, name, site, addr);
            //we need to register each remote object
            ObjRef o = RemotingServices.Marshal(subscriber, name, typeof(Subscriber));
            subscriber.setURI(string.Format("{0}/{1}", channelURI, name));
            Console.WriteLine("Created Subscriber at site:\"{0}\" uri:\"{1}\"", site, subscriber.getURI());

            //now that broker is created and marshalled
            //send remote to puppetMaster which is Monitor.waiting for the remote            
            pm.registerSubscriber(subscriber);
            Console.WriteLine("Just registered at puppetMaster");
            Console.WriteLine("Press key to leave");
            Console.Read();
        }

        public void setSiteBroker(Broker site_broker)
        {
            _broker = site_broker;
        }

        public string getURI()
        {
            return _uri;
        }

        public void setURI(string uri)
        {
            _uri = uri;
        }

        public string status()
        {
            string subscribedTopics="";
            foreach (string t in _subscribedTopics)
                subscribedTopics += t+"; ";
            Console.WriteLine("[STATUS] Freeze:" + _freezed + " Subscribing: " + subscribedTopics);
            return "OK";
        }

        public string getName()
        {
            return _name;
        }

        public string getSite()
        {
            return _site;
        }

        public void crash()
        {
            Process.GetCurrentProcess().Kill();
        }


        public void freeze()
        {
            lock (_freezedThreads)
            {
                _freezed = true;
            }
        }

        public void unfreeze()
        {
            lock (_freezedThreads)
            {
                if (!_freezed)
                {
                    return;
                }
                foreach (var ts in _freezedThreads)
                    new Thread(ts).Start();
                _freezedThreads.Clear();
                _freezed = false;
            }
        }


        public void subscribe(string topic)
        {
            lock (thisLock)
            {
                // TODO LOG
                if (_subscribedTopics.Contains(topic))
                {
                    // do nothing
                    return;
                }
                _subscribedTopics.Add(topic);
                // TODO make all calls assyncs
                SubscribeMessage msg = new SubscribeMessage() { sub = this, seqnum = _seqnum, topic = topic, uri = getURI() };
                log(string.Format("Subscribe. '{0}'", msg));

                _broker.subscribe(msg);
                _seqnum += 1;
            }


        }

        public void unsubscribe(string topic)
        {
            lock (thisLock)
            {
                if (!_subscribedTopics.Contains(topic))
                {
                    // do nothing or throw exception?
                    return;
                }
                _subscribedTopics.Remove(topic);
                // TODO LOG
                // TODO make all calls assyncs
                UnsubscribeMessage msg = new UnsubscribeMessage() { sub = this, seqnum = _seqnum, topic = topic, uri = getURI() };
                log(string.Format("Unsubscribe. '{0}'", msg));
                _broker.unsubscribe(msg);
                _seqnum += 1;
            }



        }

        public void receive(string topic, string content)
        {
            ThreadStart x = () => receive_job(topic, content);
            lock (_freezedThreads)
            {
                if (_freezed)
                {
                    _freezedThreads.Add(x);
                }
                else
                {
                    new Thread(x).Start();
                }
            }            
           }
        

            

        public void receive_job(string topic, string content)
        {
            //TEM DE FICAR AQUI O LOG
           // c.reportEvent(EventType.SubEvent, uri, msg.senderURI, topic, msg.total_seqnum);
            log(string.Format("Received. topic:'{0}' content:'{1}'", topic, content));
        }



        void log(string e)
        {
           // _pm.reportEvent(getURI(), e);
            Console.WriteLine(e);
        }
    }
}
