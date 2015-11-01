using DADInterfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PublisherConsole
{
    class PublisherRemote : MarshalByRefObject, Publisher
    {
        private PuppetMaster _pm;
        private string _name;
        private string _site;
        private string _uri;
        private Broker _broker;
        private int _total_seqnum;
        private Dictionary<string, int> _topics_seqnum = new Dictionary<string, int>();
        private Object thisLock = new Object();
        private ICoordinator c;
        private int seq;
        private bool _freeze_state=false;

        public PublisherRemote(PuppetMaster pm, string name, string site,string addr)
        {
            _name = name;
            _pm = pm;
            _site = site;
            c = (ICoordinator)Activator.GetObject(typeof(ICoordinator), addr);
            seq = 0;
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Started Publisher, pid=\"{0}\"", Process.GetCurrentProcess().Id);
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
            PublisherRemote publisher = new PublisherRemote(pm, name, site,addr);
            //we need to register each remote object
            ObjRef o = RemotingServices.Marshal(publisher, name, typeof(Publisher));
            publisher.setURI(string.Format("{0}/{1}", channelURI, name));
            Console.WriteLine("Created Publisher at site:\"{0}\" uri:\"{1}\"", site, publisher.getURI());

            //now that broker is created and marshalled
            //send remote to puppetMaster which is Monitor.waiting for the remote            
            pm.registerPublisher(publisher);
            Console.WriteLine("Just registered at puppetMaster");
            Console.WriteLine("Press key to leave");
            Console.Read();
        }

        public void setURI(string v)
        {
            _uri = v;
        }

        public void crash()
        {
            Process.GetCurrentProcess().Kill();
            
        }

       

        public void freeze()
        {
            _freeze_state = true;
            while (_freeze_state)
            {

            }

        }

        public void unfreeze()
        {
            _freeze_state = false;
        }

        public string getName()
        {
            return _name;
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
            Console.WriteLine("[STATUS: Freeze=" + _freeze_state);
            return "OK";
        }

        

        delegate void publish_delegate(string topic, string content, int quantity, int interval);

        private void publish_work(string topic, string content)
        {
            lock (thisLock)
            {
                int total_seqnum = _total_seqnum;
                _total_seqnum += 1;
                int topic_seqnum = 0;
                if (!_topics_seqnum.ContainsKey(topic))
                {
                    // obvious memory leak, because we never release tuples from dict
                    _topics_seqnum.Add(topic, 0);
                }
                topic_seqnum = _topics_seqnum[topic];
                _topics_seqnum[topic] += 1;
                string cc = "";
                if (content == "timestamps")
                {
                    cc = string.Format("[Content] seqnum:{0} timestamp:{1}", total_seqnum, DateTime.Now.ToString());
                }
                var msg = new PublishMessage() { senderURI = getURI(), total_seqnum = total_seqnum, topic = topic, content = cc };
                log(string.Format("[publish] {0}", msg));
                // TODO make all calls assyncs
                seq++;
                c.reportEvent("PubEvent", getURI(), getURI(), topic, seq);
                _broker.publish(msg);
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

            Thread t = new Thread(() => publish_job(topic, content, quantity, interval));
            t.Start();
            log(string.Format("[Publish Thread Started]"));
        }



        void log(string e)
        {
            _pm.reportEvent(getURI(), e);
            Console.WriteLine(e);
        }
    }
}
