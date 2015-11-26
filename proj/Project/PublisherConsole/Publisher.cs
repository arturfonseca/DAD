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

namespace PublisherConsole
{
    class PublisherRemote : MarshalByRefObject, Publisher
    {
        private PuppetMaster _pm;
        private string _serviceName;
        private string _site;
        private string _uri;
        private Broker _broker;
        private int _eventnum = 0;
        private Object _eventnumLock = new Object();
        private ICoordinator c;
        private bool _freezed = false;
        private List<ThreadStart> _freezedThreads = new List<ThreadStart>();
        private string _processName;
        private Form1 _form;
        private OrderingPolicy _orderingPolicy;

        public PublisherRemote(Form1 form,PuppetMaster pm, string name, string site, string addr, string processName)
        {
            _form = form;
            _serviceName = name;
            _pm = pm;
            _site = site;
            _processName = processName;
            c = (ICoordinator)Activator.GetObject(typeof(ICoordinator), addr);
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
            Form1 form = new Form1(args);
            Application.Run(form);

        }

        public void setURI(string v)
        {
            _uri = v;
        }

        public void crash()
        {
            Process.GetCurrentProcess().Kill();
        }






        public string getSite()
        {
            return _site;
        }

        public string getURI()
        {
            return _uri;
        }

        public void setSiteBroker(Broker site_broker)
        {
            _broker = site_broker;
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
            log("[STATUS] Broker is alive:" + _alive);
            log("[STATUS] Freeze: " + _freezed);

            return "OK";
        }


        public void imAlive()
        {

        }

        private int getEventnum()
        {
            lock (_eventnumLock){
                int ret = _eventnum;
                _eventnum++;
                return ret;
            }
        }
        private void publish_work(string topic, string content)
        {
            int eventnum = getEventnum();
            var cc = string.Format("publisher name {0}. seqnum {1}", _processName, eventnum);
            var msg = new PublishMessage() { publisherURI = getURI(), seqnum = eventnum, origin_seqnum = eventnum, topic = topic, content = cc, origin_site = null };

            if (_orderingPolicy == OrderingPolicy.total)
            {
                TOSeqnumRequest req = _broker.getTotalOrderSequenceNumber();
                log(eventnum,req.ToString());
                msg.seqnum = req.seqnum;
            }            
            // TODO make all calls assyncs
            log(eventnum,msg);
            c.reportEvent(EventType.PubEvent, _processName, _processName, topic, msg.seqnum);
            PublishDelegate d = new PublishDelegate(_broker.publish);
            d.BeginInvoke(msg,null,null);
        }

        private void publish_job(string topic, string content, int quantity, int interval)
        {
            //we assume quantity and interval positive
            for (int i = 0; i < quantity; i++)
            {
                publish_work(topic, content);
                Thread.Sleep(interval);
            }
        }


        public void publish(string topic, string content, int quantity, int interval)
        {

            // interval in milliseconds
            // we dont remove thread gracefully
            ThreadStart x = () => publish_job(topic, content, quantity, interval);
            lock (_freezedThreads)
            {
                log(string.Format("[Publish] freezed? {0}", _freezed ? "yes" : "no"));
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


        void log(int s, object e)
        {
            _form.log(string.Format("[job:{0}]{1}",s,e));
        }
        void log(string e)
        {
            _form.log(e);
        }

        public string getProcessName()
        {
            return _processName;
        }

        public string getServiceName()
        {
            return _serviceName;
        }

        public void setOrderingPolicy(OrderingPolicy p)
        {
            _orderingPolicy = p;
        }
    }
}
