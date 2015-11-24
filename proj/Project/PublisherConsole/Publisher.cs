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
        private int _total_seqnum;
        private Object thisLock = new Object();
        private ICoordinator c;
        private bool _freezed = false;
        private List<ThreadStart> _freezedThreads = new List<ThreadStart>();
        private string _processName;
        private Form1 _form;

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
        delegate void publish_delegate(string topic, string content, int quantity, int interval);

        private void publish_work(string topic, string content)
        {
            lock (thisLock)
            {
                int total_seqnum = _total_seqnum;
                _total_seqnum += 1;
                string cc = "";
                cc = string.Format("[Content]PublisherURI:'{0}' seqnum:{1} timestamp:{2}", getURI(), total_seqnum, DateTime.Now.ToString());

                var msg = new PublishMessage() { publisherURI = getURI(), seqnum = total_seqnum, origin_seqnum = total_seqnum, topic = topic, content = cc };

                // TODO make all calls assyncs
                log(string.Format("[publish] {0}", msg));
                c.reportEvent(EventType.PubEvent, getURI(), getURI(), topic, total_seqnum);
                PublishDelegate d = new PublishDelegate(_broker.publish);
                d.BeginInvoke(msg,null,null);
                //_broker.publish(msg);
            }

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
    }
}
