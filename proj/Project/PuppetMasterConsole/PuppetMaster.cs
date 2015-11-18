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
using System.Windows.Forms;

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
        private Form1 _form;

        public string URI { get { return _uri; } set { _uri = value; } }
        

        public PuppetMasterRemote(string channelURI,Form1 form)
        {
            _form = form;
            _channel_uri = channelURI;
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
            Form1 form = new Form1();
            Application.Run(form);           
           
        }

        void log(string e)
        {
            _form.log(e);
        }

        public Broker createBroker(string processName, string serviceName,string site,int port,string addr)
        {
            //start processes
            Process p = new Process();
            p.StartInfo.FileName = ConfigurationManager.AppSettings["BrokerPath"];
            var arg = string.Format("{0} {1} {2} {3} {4} {5}", URI, serviceName, site, port,addr, processName);                       
            p.StartInfo.Arguments = arg;
            log(string.Format("launching Broker executable '{0}' ", p.StartInfo.FileName));

            p.Start();
            processes.Add(p);
            Broker b = null;
            lock (_brokers)
            {
                while (true)
                {
                    b = _brokers.Find(x => x.getProcessName() == processName);
                    if(b != null)
                        break;
                    Monitor.Wait(_brokers);
                }
            }            
            return b;
        }

        public Publisher createPublisher(string processName, string serviceName, string site, int port, string addr)
        {
            //start processes
            Process p = new Process();
            p.StartInfo.FileName = ConfigurationManager.AppSettings["PublisherPath"];
            log(string.Format("launching Publisher at '{0}'", p.StartInfo.FileName));
            p.StartInfo.Arguments = string.Format("{0} {1} {2} {3} {4} {5}",URI,serviceName,site,port,addr, processName);

            p.Start();
            processes.Add(p);
            Publisher b = null;
            lock (_publishers)
            {
                while (true)
                {
                    b = _publishers.Find(x => x.getProcessName() == processName);
                    if (b != null)
                        break;
                    Monitor.Wait(_publishers);
                }
            }
            return b;
        }

        public Subscriber createSubscriber(string processName, string serviceName,string site,int port,string addr)
        {
            //start processes
            Process p = new Process();
            p.StartInfo.FileName = ConfigurationManager.AppSettings["SubscriberPath"];
            log(string.Format("launching Subscriber at '{0}'", p.StartInfo.FileName));
            p.StartInfo.Arguments = string.Format("{0} {1} {2} {3} {4} {5}",URI,serviceName,site,port,addr,processName);

            p.Start();
            processes.Add(p);
            Subscriber b = null;
            lock (_subscribers)
            {
                while (true)
                {
                    b = _subscribers.Find(x => x.getProcessName() == processName);
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
            log(string.Format("registered broker {0}",b.getURI()));
        }

        public void registerPublisher(Publisher p)
        {
            lock (_publishers)
            {
                _publishers.Add(p);
                Monitor.Pulse(_publishers);
            }
            log(string.Format("registered publisher {0}",p.getURI()));
        }

        public void registerSubscriber(Subscriber s)
        {
            lock (_subscribers)
            {
                _subscribers.Add(s);
                Monitor.Pulse(_subscribers);
            }
            log(string.Format("registered subscriber {0}", s.getURI()));
        }

        public void reportEvent(string origin_uri,string e)
        {
            log(string.Format("[Event] from:\"{0}\" \"{1}\"",origin_uri,e));
        }

        public String status()
        {
            return "OK!";
        }

        public void reportEvent(string type, string uri1, string uri2, string topic, string seqnum)
        {
            throw new NotImplementedException();
        }
    }
}
