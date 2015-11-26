using DADInterfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

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
        private string _processName;
        private OrderingPolicy _orderingPolicy;
        private List<FIFOstruct> _fifostructs = new List<FIFOstruct>();
        private SubscriberForm _form;
        private object _eventnumLock = new object();
        private int _eventnum = 0;

        public SubscriberRemote(SubscriberForm form,PuppetMaster pm, string name, string site, string coordinatorURI,string processName)
        {
            _form = form;
            _serviceName = name;
            _pm = pm;
            _site = site;
            _orderingPolicy = OrderingPolicy.fifo;
            _processName = processName;
            c = (ICoordinator)Activator.GetObject(typeof(ICoordinator), coordinatorURI);

        }


        public override string ToString()
        {
            return string.Format("[Subscriber] name:{0} uri:{1} site:{2}", _processName, _uri, _site);
        }



        public override object InitializeLifetimeService()
        {
            return null;
        }

        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            //must be called in this order
            SubscriberForm form = new SubscriberForm(args);
            Application.Run(form);
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
            log("[STATUS] Trying to get broker status");
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
            log("[STATUS] Broker is alive:" + _alive);
            log("[STATUS] Subscribing: " + subscribedTopics);
            log("[STATUS] Freeze:" + _freezed);

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
            lock (_eventnumLock)
            {
                p.eventnum = _eventnum;
                _eventnum++;
            }
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
                //this lock
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
                            c.reportEvent(EventType.SubEvent, getURI(), _msg.publisherURI, _msg.topic, _msg.originalSeqnum);
                            log(string.Format("{0}", _msg));

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
                lock (c)
                {
                    c.reportEvent(EventType.SubEvent, getURI(), m.publisherURI, m.topic, m.seqnum);
                    log(string.Format("{0}", m));

                }
                
            }


           
        }

        public void imAlive()
        {

        }

        void log(string e)
        {
            _form.log(e);
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
                if (_subscribedTopics.Contains(topic))
                {
                    return;
                }
                _subscribedTopics.Add(topic);
                SubscribeMessage msg = new SubscribeMessage() { sub = this, seqnum = _seqnum, topic = topic, uri = getURI() };
                log(msg.ToString());
                SubscribeDelegate pd = new SubscribeDelegate(_broker.subscribe);
                pd.BeginInvoke(msg,null,null);
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
                log(msg.ToString());
                UnsubscribeDelegate pd = new UnsubscribeDelegate(_broker.unsubscribe);
                pd.BeginInvoke(msg, null, null);
                _seqnum += 1;
            }
        }
    }
}
