﻿using DADInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;

namespace PublisherConsole
{
    class PublisherRemote:MarshalByRefObject,Publisher
    {
        private PuppetMaster _pm;
        private string _name;        
        private string _site;
        private string _uri;
        private Broker _broker;
        private int _total_seqnum;
        private Dictionary<string, int> _topics_seqnum = new Dictionary<string, int>();

        public PublisherRemote(PuppetMaster pm,string name,string site)
        {
            _name = name;
            _pm = pm;
            _site = site;
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Started Publisher");
            int nargs = 4;
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

            string channelURI = Utility.setupChannel(port);

            // get the puppetMaster that started this process
            PuppetMaster pm = (PuppetMaster)Activator.GetObject(typeof(PuppetMaster), puppetMasterURI);
            PublisherRemote publisher = new PublisherRemote(pm,name, site);
            //we need to register each remote object
            ObjRef o = RemotingServices.Marshal(publisher, name, typeof(Publisher));
            publisher.setURI(string.Format("{0}/{1}", channelURI, name));
            Console.WriteLine("Created Publisher at \"{0}\"", publisher.getURI());

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
            throw new NotImplementedException();
        }

        public void freeze()
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
        
        public void unfreeze()
        {
            throw new NotImplementedException();
        }

        public void publish(string topic, string content)
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

            var msg = new PublishMessage() { senderURI = getURI(), total_seqnum=total_seqnum, topic_seqnum = topic_seqnum, topic = topic, content = content};
            log(string.Format("[publish] {0}", msg));
            // TODO make all calls assyncs
            publishDelegate pd = new publishDelegate(_broker.publish);
            IAsyncResult res = pd.BeginInvoke(msg, null, null);
            // TODO wait for response...
            // synchronous way _broker.publish(msg);           
        }

        void log(string e)
        {
            _pm.reportEvent(getURI(), e);
            Console.WriteLine(e);
        }
    }
}