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
    class FIFOstruct
    {
        public string _publhisherURI;
        public int _seq_num;
        public List<PublishMessage> listOfmessages = new List<PublishMessage>();

        public FIFOstruct(string publisher, int seq_num)
        {
            _publhisherURI = publisher;
            _seq_num = seq_num;
        }

        public FIFOstruct() { }
    }

    class SubscriberRemote : MarshalByRefObject, Subscriber
    {
        private PuppetMaster _pm;
        private string _serviceName;
        private string _site;
        private string _uri;
        private Broker _broker;
        // used to order messages
        private int _seqnum = 0;
        private List<string> _subscribedTopics = new List<string>();
        private Object thisLock = new Object();
        private ICoordinator c;
        private bool _freezed = false;
        private List<PublishMessage> _freezedReceives = new List<PublishMessage>();
        private object _freezedLock = new object();
        private static string _processName;
        private OrderingPolicy _orderingPolicy;
        private List<FIFOstruct> _fifostructs = new List<FIFOstruct>();

        public SubscriberRemote(PuppetMaster pm, string name, string site, string coordinatorURI)
        {
            _serviceName = name;
            _pm = pm;
            _site = site;
            _orderingPolicy = OrderingPolicy.fifo;
            c = (ICoordinator)Activator.GetObject(typeof(ICoordinator), coordinatorURI);

        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Started Subscriber, pid=\"{0}\"", Process.GetCurrentProcess().Id);
            int nargs = 6;
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
            string coordinatorURI = args[4];
            string processName = args[5];
            _processName = processName;

            string channelURI = Utility.setupChannel(port);

            // get the puppetMaster that started this process
            PuppetMaster pm = (PuppetMaster)Activator.GetObject(typeof(PuppetMaster), puppetMasterURI);
            SubscriberRemote subscriber = new SubscriberRemote(pm, name, site, coordinatorURI);
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
            bool _alive;
            Console.WriteLine("[STATUS] Trying to get broker status");
            try
            {
                _broker.imAlive();
                _alive = true;
            }
            catch (Exception)
            {
                _alive = false;
            }
            string subscribedTopics = "";
            foreach (string t in _subscribedTopics)
                subscribedTopics += "\""+t + "\" ";
            Console.WriteLine("[STATUS] Broker is alive:" + _alive);
            Console.WriteLine("[STATUS] Subscribing: " + subscribedTopics);
            Console.WriteLine("[STATUS] Freeze:" + _freezed);

            return "OK";
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
            lock (_freezedLock)
            {
                _freezed = true;
            }
        }

        public void unfreeze()
        {
            lock (_freezedLock)
            {
                if (!_freezed)
                {
                    return;
                }
                foreach (var ts in _freezedReceives)
                    receive_job(ts);
                _freezed = false;
                _freezedReceives.Clear();
            }
        }


        

        

        public void receive(PublishMessage p)
        {
            bool freezed =false;
            lock (_freezedLock)
            {
                if (_freezed)
                {
                    freezed = true;
                    _freezedReceives.Add(p);
                }
                    

            }
            if (!freezed)
            {
                receive_job(p);
            }
        }




        public void receive_job(PublishMessage m)
        {
            if (_orderingPolicy == OrderingPolicy.fifo)
            {
                //FIFO
                lock (_fifostructs)
                {

                    int index = _fifostructs.FindIndex(item => item._publhisherURI == m.publisherURI);

                    if (index < 0)
                    {
                        // element does not exists
                        _fifostructs.Add(new FIFOstruct(m.publisherURI, 0));
                        //getIndex Now
                        index = _fifostructs.FindIndex(item => item._publhisherURI == m.publisherURI);
                    }
                    var fifo = _fifostructs[index];
                    //TODO Verify duplicates
                    fifo.listOfmessages.Add(m);
                    fifo.listOfmessages.OrderBy(item => item.seqnum);

                    foreach (PublishMessage _msg in fifo.listOfmessages.ToList())
                    {

                        if (_msg.seqnum == fifo._seq_num)
                        {
                            //Message needed received , can now print
                            c.reportEvent(EventType.SubEvent, getURI(), _msg.publisherURI, _msg.topic, _msg.origin_seqnum);
                            log(string.Format("Received. topic:'{0}' content:'{1}'", _msg.topic, _msg.content));

                            //Message sent , increment seq_num and delete delivered message
                            fifo._seq_num++;
                            fifo.listOfmessages.Remove(_msg);
                        }
                        else
                            break;
                    }
                }
            }
            else
            {
                //TEM DE FICAR AQUI O LOG
                c.reportEvent(EventType.SubEvent, getURI(), m.publisherURI, m.topic, m.seqnum);
                log(string.Format("Received. topic:'{0}' content:'{1}'", m.topic, m.content));
            }


           
        }

        public void imAlive()
        {

        }

        void log(string e)
        {
            // _pm.reportEvent(getURI(), e);
            Console.WriteLine(e);
        }

        public string getServiceName()
        {
            return _serviceName;
        }

        public string getProcessName()
        {
            return _processName;
        }

        public void setOrderingPolicy(OrderingPolicy ord)
        {
            _orderingPolicy = ord;
        }

        public void subscribe(string topic)
        {
            lock (thisLock)
            {
                // TODO LOG
                if (_subscribedTopics.Contains(topic))
                {
                    // do nothing, should we? keep-alive in the future? who knows...
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
    }
}
