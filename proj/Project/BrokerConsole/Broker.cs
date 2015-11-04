using DADInterfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace BrokerConsole
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

    class BrokerRemote : MarshalByRefObject, Broker
    {
        private PuppetMaster _pm;
        // personal information
        private string _serviceName;
        private string _site;
        private string _uri;
        private bool _isRoot;
        // site information
        private List<Publisher> _publishers = new List<Publisher>();



        // Subscribers
        private List<Subscriber> _subscribers = new List<Subscriber>();
        // uri to subscriber
        private Dictionary<string, Subscriber> _uriToSubs = new Dictionary<string, Subscriber>();
        private Dictionary<string, Publisher> _uriToPubs = new Dictionary<string, Publisher>();
        // key = topic value= subscribers uri
        Dictionary<string, List<string>> _topicSubscribers = new Dictionary<string, List<string>>();
        // key = subscriber_uri, value = seqnum
        Dictionary<string, int> _subscribersSeqNums = new Dictionary<string, int>();


        // Child sites
        private List<Site> _childSites = new List<Site>();
        // site_name to Site
        private Dictionary<string, Site> _nameToSite = new Dictionary<string, Site>();
        // key = topic, value = list of interested sites
        Dictionary<string, List<string>> _topicSites = new Dictionary<string, List<string>>();

        // can be null
        private Object _parentSiteLock = new object();
        private Site _parentSite;
        private ICoordinator c;
        private int seq;
        private string _coordinatorURI;

        private OrderingPolicy _orderingPolicy;
        private RoutingPolicy _routingPolicy;
        private LoggingLevel _loggingLevel;


        ///////////FIFO/////////

        //string= site.URI
        private Dictionary<string, List<FIFOstruct>> _siteToFifoStruct = new Dictionary<string, List<FIFOstruct>>();
        private List<FIFOstruct> _fifostructs = new List<FIFOstruct>();
        private bool _freezed = false;
        private object _freezedLock = new object();
        private List<PublishMessage> _freezedPublishMessages = new List<PublishMessage>();
        private List<PropagatedPublishMessage> _freezedPropagatedPublishMessages = new List<PropagatedPublishMessage>();
        private static string _processName;

        public BrokerRemote(PuppetMaster pm, string uri, string name, string site, string addr)
        {
            _uri = uri;
            _serviceName = name;
            _pm = pm;
            _site = site;
            _orderingPolicy = OrderingPolicy.fifo;
            _routingPolicy = RoutingPolicy.flooding;
            seq = 0;
            _coordinatorURI = addr;
            // c = (ICoordinator)Activator.GetObject(typeof(ICoordinator), addr);

        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Started Broker process, pid=\"{0}\"", Process.GetCurrentProcess().Id);
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
            string uri = string.Format("{0}/{1}", channelURI, name);
            BrokerRemote broker = new BrokerRemote(pm, uri, name, site, coordinatorURI);
            //we need to register each remote object
            ObjRef o = RemotingServices.Marshal(broker, name, typeof(Broker));
            Console.WriteLine("Instanciated Broker name:'{0}' site:'{1}' uri:'{2}'", name, site, uri);

            //now that broker is created and marshalled
            //send remote to puppetMaster which is Monitor.waiting for the remote  
            pm.registerBroker(broker);
            Console.WriteLine("Just registered at puppetMaster");
            Console.WriteLine("Press key to leave");
            Console.Read();
        }

        public void imAlive()
        {

        }

        public void setOrderingPolicy(OrderingPolicy p)
        {
            _orderingPolicy = p;
        }
        public void setRoutingPolicy(RoutingPolicy p)
        {
            _routingPolicy = p;
        }
        public void setLoggingLevel(LoggingLevel l)
        {
            _loggingLevel = l;
            if (l == LoggingLevel.full)
            {
                c = (ICoordinator)Activator.GetObject(typeof(ICoordinator), _coordinatorURI);
            }
        }

        public string getSite()
        {
            return _site;
        }

        public string getURI()
        {
            return _uri;
        }

        public void setChildren(List<Site> child_sites)
        {
            _childSites = child_sites;
            foreach (Site s in child_sites)
            {
                _nameToSite.Add(s.name, s);
            }
        }

        public void setParent(Site parent_site)
        {
            _parentSite = parent_site;
        }




        public void setPublishers(List<Publisher> site_publishers)
        {
            _publishers = site_publishers;
            foreach (Publisher p in site_publishers)
            {
                _uriToPubs.Add(p.getURI(), p);

            }
        }

        public void setSubscribers(List<Subscriber> site_subscribers)
        {
            _subscribers = site_subscribers;
            foreach (Subscriber s in site_subscribers)
            {
                _uriToSubs.Add(s.getURI(), s);

            }
        }

        public void setIsRoot()
        {
            _isRoot = true;
        }

        public bool getIsRoot()
        {
            return _isRoot;
        }

        // Puppet Master functions
        public void crash()
        {
            Process.GetCurrentProcess().Kill();
        }

        public string status()
        {
            Console.WriteLine("[STATUS] MyURI: " + getURI());
            Console.WriteLine("[STATUS] ProcessName: " + getProcessName());
            Console.WriteLine("[STATUS] Site: " + getSite());
            Console.WriteLine("[STATUS] MyURI: " + getURI());
            pubStatus();
            subStatus();
            parentStatus();
            childsStatus();
            Console.WriteLine("[STATUS] Freeze:" + _freezed);


            return "OK";
        }

        private void pubStatus()
        {
            bool _alive;
            Console.WriteLine("[STATUS] Trying to get  Publishers status");
            foreach (KeyValuePair<string, Publisher> entry in _uriToPubs)
            {
                String s = entry.Key;
                Publisher p = entry.Value;
                try
                {
                    p.imAlive();
                    _alive = true;
                    Console.WriteLine("         " + s + " is alive:" + _alive);

                }
                catch (Exception)
                {
                    _alive = false;
                    Console.WriteLine("         " + s + " is alive:" + _alive);
                }
            }
        }

        private void subStatus()
        {
            bool _alive;
            Console.WriteLine("[STATUS] Trying to get  Subscribers status");
            foreach (KeyValuePair<string, Subscriber> entry in _uriToSubs)
            {
                String s = entry.Key;
                Subscriber p = entry.Value;
                try
                {
                    p.imAlive();
                    _alive = true;
                    Console.WriteLine("         " + s + " is alive:" + _alive);

                }
                catch (Exception)
                {
                    _alive = false;
                    Console.WriteLine("         " + s + " is alive:" + _alive);
                }
            }
        }

        private void parentStatus()
        {
            if (_isRoot)
            {
                Console.WriteLine("[STATUS] I'm root");
                return;
            }


            Console.WriteLine("[STATUS] Trying to get  Parent status");
            bool _alive;
            try
            {
                _parentSite.brokers[0].imAlive();
                _alive = true;
                Console.WriteLine("         " + _parentSite.brokers[0].getURI() + " is alive:" + _alive);

            }
            catch (Exception)
            {
                _alive = false;
                Console.WriteLine("         " + _parentSite.brokers[0].getURI() + " is alive:" + _alive);
            }

        }
        private void childsStatus()
        {
            bool _alive;
            Console.WriteLine("[STATUS] Trying to get  Childs status");
            foreach (Site s in _childSites)
            {
                Broker b = s.brokers[0];
                try
                {
                    b.imAlive();
                    _alive = true;
                    Console.WriteLine("         " + b.getURI() + " is alive:" + _alive);
                }
                catch (Exception)
                {
                    _alive = false;
                    Console.WriteLine("         " + b.getURI() + " is alive:" + _alive);
                }
            }
        }

        // *end* Puppet Master functions

        /*
         *  Business logic
         */

        /*
        private bool isDuplicate(SubscribeMessage msg)
        {
            lock (_subscribersSeqNums)
            {
                if (!_subscribersSeqNums.ContainsKey(msg.uri))
                {
                    _subscribersSeqNums.Add(msg.uri, 0);
                }
                if (msg.seqnum < _subscribersSeqNums[msg.uri])
                {
                    // its a duplicated request, discard
                    log(string.Format("[isDuplicate] detected duplicated, discarding. expected seqnum:'{0}', msg:'{1}'", _subscribersSeqNums[msg.uri], msg));
                    return true;
                }
                // if its waiting for 0 and receives 4, then next is 5, is this desired?
                _subscribersSeqNums[msg.uri] = msg.seqnum + 1;
            }

            return false;
        }
        */

        void log(string e)
        {
            // TODO use assynchronous call
            _pm.reportEvent(getURI(), e);
            Console.WriteLine(e);
        }

        public void subscribe(SubscribeMessage msg)
        {
            log(string.Format("[Subscribe] Received event '{0}'", msg));
            //if (isDuplicate(msg)) return;
            // should we have FIFO order here?
            lock (_topicSubscribers)
            {
                if (!_topicSubscribers.ContainsKey(msg.topic))
                {
                    _topicSubscribers.Add(msg.topic, new List<string>());
                }
                _topicSubscribers[msg.topic].Add(msg.uri);
            }
            PropagatedSubcribeMessage pmsg = new PropagatedSubcribeMessage(msg, _site);
            // propagate subscribe only to parent, taking advantage of tree strucure
            lock (_parentSiteLock)
            {
                if (_parentSite != null)
                {
                    foreach (Broker b in _parentSite.brokers)
                    {
                        //TODO assyncronous
                        log(string.Format("[subscribe] senting '{0}' to parent site '{1}'", pmsg, _parentSite.name));
                        b.propagateSubscribe(pmsg);
                    }
                }
            }

        }

        public void propagateSubscribe(PropagatedSubcribeMessage msg)
        {
            log(string.Format("[propagateSubscribe] Received event '{0}'", msg));
            // TODO deal with duplicate messages. using which seqnum?...
            lock (_topicSites)
            {
                if (!_topicSites.ContainsKey(msg.topic))
                {
                    _topicSites.Add(msg.topic, new List<string>());
                }
                _topicSites[msg.topic].Add(msg.interested_site);
            }
            msg.interested_site = _site;
            lock (_parentSiteLock)
            {
                if (_parentSite != null)
                {
                    foreach (var b in _parentSite.brokers)
                    {
                        log(string.Format("[propagateSubscribe] senting '{0}' to parent site '{1}'", msg, _parentSite.name));
                        //TODO assync
                        b.propagateSubscribe(msg);
                    }
                }
            }

        }

        public void unsubscribe(UnsubscribeMessage msg)
        {
            log(string.Format("[Unsubscribe] Received event '{0}'", msg));
            //if (isDuplicate(msg)) return;
            // should we have FIFO order here?
            lock (_topicSubscribers)
            {
                if (_topicSubscribers.ContainsKey(msg.topic))
                {
                    _topicSubscribers[msg.topic].Remove(msg.uri);
                    if (_topicSubscribers[msg.topic].Count == 0)
                    {
                        _topicSubscribers.Remove(msg.topic);
                    }
                }
            }
            PropagatedUnsubscribeMessage pmsg = new PropagatedUnsubscribeMessage(msg, _site);
            // propagate unsubscribe only to parent, taking advantage of tree strucure
            lock (_parentSiteLock)
            {
                if (_parentSite != null)
                {
                    foreach (Broker b in _parentSite.brokers)
                    {
                        log(string.Format("[Unsubscribe] senting '{0}' to parent site '{1}'", pmsg, _parentSite.name));
                        // TODO assyncronous
                        b.propagateUnsubscribe(pmsg);
                    }
                }
            }
        }

        public void propagateUnsubscribe(PropagatedUnsubscribeMessage msg)
        {
            log(string.Format("[propagateUnsubscribe] Received event '{0}'", msg));
            // TODO deal with duplicate messages. using which seqnum?...
            lock (_topicSites)
            {
                if (_topicSites.ContainsKey(msg.topic))
                {
                    _topicSites[msg.topic].Remove(msg.interested_site);
                    if (_topicSites[msg.topic].Count == 0)
                    {
                        _topicSites.Remove(msg.topic);
                    }
                }
            }
            msg.interested_site = _site;
            lock (_parentSiteLock)
            {
                if (_parentSite != null)
                {
                    foreach (var b in _parentSite.brokers)
                    {
                        log(string.Format("[subscribe] senting '{0}' to parent site '{1}'", msg, _parentSite.name));
                        //TODO assync
                        b.propagateSubscribe(msg);
                    }
                }
            }

        }

        private void deliver(PublishMessage msg)
        {
            // to avoid sending two times, we use a list
            List<string> sentUris = new List<string>();

            // List of delegates that know the interested subcribers
            List<ReceiveDelegate> remoteDelegates = new List<ReceiveDelegate>();

            // First phase we filter site subscribers and fillin
            lock (_topicSubscribers)
            {
                foreach (var subscribedTopic in _topicSubscribers.Keys)
                {
                    if (!equivalentTopic(msg.topic, subscribedTopic))
                        continue;
                    foreach (var uri in _topicSubscribers[subscribedTopic])
                    {
                        if (sentUris.Contains(uri))
                            continue;
                        Subscriber s = _uriToSubs[uri];
                        // TODO assync
                        ReceiveDelegate rd = new ReceiveDelegate(s.receive);
                        remoteDelegates.Add(rd);
                        //MUDAR ISTO...
                        // c.reportEvent(EventType.SubEvent, uri, msg.publisherURI, msg.topic, msg.total_seqnum);
                        log(string.Format("[Deliver] sent event '{0}' to '{1}'", msg, uri));

                    }
                }
            }
            List<IAsyncResult> results = new List<IAsyncResult>();

            foreach (ReceiveDelegate subDelegate in remoteDelegates)
            {
                IAsyncResult result = subDelegate.BeginInvoke(msg, null, null);
                results.Add(result);
            }
            List<WaitHandle> handlesLst = new List<WaitHandle>();
            //TODO ASK PROFESSOR IF WE NEED TO RESEND LOST MESSAGES
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
                    return;

                foreach (var msg in _freezedPublishMessages)
                {
                    publishWork(msg);
                }
                _freezedPublishMessages.Clear();

                foreach (var msg in _freezedPropagatedPublishMessages)
                {
                    propagatePublishWork(msg);
                }
                _freezedPropagatedPublishMessages.Clear();

                _freezed = false;
            }
        }


        public void propagatePublish(PropagatedPublishMessage msg)
        {
            bool freezed = false;
            lock (_freezedLock)
            {
                if (_freezed)
                {
                    freezed = true;
                    log(string.Format("[propagatePublish] freezed"));
                    _freezedPropagatedPublishMessages.Add(msg);
                }
            }
            if (!freezed)
            {
                propagatePublishWork(msg);
            }

        }
        public void publish(PublishMessage msg)
        {
            bool freezed = false;

            lock (_freezedLock)
            {
                if (_freezed)
                {
                    freezed = true;
                    log(string.Format("[Publish] freezed"));
                    _freezedPublishMessages.Add(msg);
                }
            }
            if (!freezed)
            {
                publishWork(msg);
            }

        }

        public void publishWork(PublishMessage msg)
        {
            // FLOODING implementation
            // TODO discart if duplicate message
            // TODO make all calls assyncs

            log(string.Format("[Publish] Received event '{0}'", msg));


            if (_orderingPolicy == OrderingPolicy.fifo)
            {
                //FIFO
                lock (_fifostructs)
                {

                    int index = _fifostructs.FindIndex(item => item._publhisherURI == msg.publisherURI);

                    if (index < 0)
                    {
                        // element does not exists
                        _fifostructs.Add(new FIFOstruct(msg.publisherURI, 0));
                        //getIndex Now
                        index = _fifostructs.FindIndex(item => item._publhisherURI == msg.publisherURI);
                    }
                    var fifo = _fifostructs[index];
                    //TODO Verify duplicates
                    fifo.listOfmessages.Add(msg);
                    fifo.listOfmessages.OrderBy(item => item.seqnum).ToList();

                    //DEBUG ListOfMessages
                    foreach (PublishMessage _msg in fifo.listOfmessages)
                    {
                        log(string.Format("[ListOfMessage] Publisher: '{0}' msg '{1}'", _msg.publisherURI, _msg));
                    }

                    foreach (PublishMessage _msg in fifo.listOfmessages.ToList())
                    {

                        if (_msg.seqnum == fifo._seq_num)
                        {
                            //Prepare to send the msg to interested sites
                            deliver(_msg);
                            routing(_msg);

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
                deliver(msg);
                routing(msg);
            }



        }
        public void propagatePublishWork(PropagatedPublishMessage msg)
        {

            log(string.Format("[propagatePublish] Received event '{0}'", msg));
            if (_orderingPolicy == OrderingPolicy.fifo)
            {
                //FIFO
                lock (_fifostructs)
                {

                    int index = _fifostructs.FindIndex(item => item._publhisherURI == msg.publisherURI);

                    if (index < 0)
                    {
                        // element does not exists
                        _fifostructs.Add(new FIFOstruct(msg.publisherURI, 0));
                        //getIndex Now
                        index = _fifostructs.FindIndex(item => item._publhisherURI == msg.publisherURI);
                    }
                    var fifo = _fifostructs[index];
                    //TODO Verify duplicates
                    fifo.listOfmessages.Add(msg);
                    fifo.listOfmessages.OrderBy(item => item.seqnum).ToList();

                    //DEBUG ListOfMessages
                    foreach (PublishMessage _msg in fifo.listOfmessages)
                    {
                        log(string.Format("[ListOfMessage] seqnum: '{0}' '{1}'", fifo._seq_num, _msg));
                    }

                    foreach (PublishMessage _msg in fifo.listOfmessages.ToList())
                    {

                        if (_msg.seqnum == fifo._seq_num)
                        {
                            //Prepare to send the msg to interested sites
                            deliver(_msg);
                            propagatingRouting(new PropagatedPublishMessage(_msg, msg.origin_site));

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

                deliver(msg);
                propagatingRouting(msg);
            }
        }



        private void routing(PublishMessage msg)
        {
            PropagatedPublishMessage pmsg = new PropagatedPublishMessage(msg, _site);
            pmsg.origin_site = _site;

            seq++;
            Console.WriteLine(seq);

            if (_routingPolicy == RoutingPolicy.flooding)
            {
                lock (_childSites)
                {
                    foreach (var s in _childSites)
                    {
                        lock (s)
                        {
                            foreach (var broker in s.brokers)
                            {
                                // TODO broker.getURI() is slow, we should use a cache
                                log(string.Format("[Flooding Routing] sent event '{0}' to '{1}'", msg, broker.getURI()));

                                PropagatePublishDelegate d = new PropagatePublishDelegate(broker.propagatePublish);
                                d.BeginInvoke(pmsg, null, null);

                                if (_loggingLevel == LoggingLevel.full)
                                    c.reportEvent(EventType.BroEvent, getURI(), msg.publisherURI, msg.topic, msg.seqnum);
                            }
                        }

                    }
                }

            }
            else // routing policy is filtering
            {
                List<string> sentSites = new List<string>();

                lock (_topicSites)
                {
                    foreach (var subscribedTopic in _topicSites.Keys)
                    {
                        if (!equivalentTopic(msg.topic, subscribedTopic))
                            continue;
                        foreach (var site_name in _topicSites[subscribedTopic])
                        {

                            if (sentSites.Contains(site_name))
                                continue;
                            var site = _nameToSite[site_name];
                            //translate seq_num of the message to send to child_sites

                            if (_orderingPolicy == OrderingPolicy.fifo)
                            {
                                lock (_siteToFifoStruct)
                                {

                                    if (!_siteToFifoStruct.ContainsKey(site_name))
                                    {
                                        _siteToFifoStruct.Add(site_name, new List<FIFOstruct>());
                                    }

                                    int index = _siteToFifoStruct[site_name].FindIndex(item => item._publhisherURI == pmsg.publisherURI);

                                    if (index < 0)
                                    {
                                        // element does not exists
                                        _siteToFifoStruct[site_name].Add(new FIFOstruct(msg.publisherURI, 0));
                                        //getIndex Now
                                        index = _siteToFifoStruct[site_name].FindIndex(item => item._publhisherURI == pmsg.publisherURI);
                                    }
                                    var fifo = _siteToFifoStruct[site_name][index];

                                    //create a new message for each site interested sites with possibly a different seqnum
                                    PropagatedPublishMessage local_pmsg = new PropagatedPublishMessage(msg, _site);
                                    local_pmsg.origin_site = _site;

                                    local_pmsg.seqnum = fifo._seq_num;
                                    fifo.listOfmessages.Add(local_pmsg);

                                    fifo._seq_num++;

                                    //It's not necessary to register the message in the listOfmessages because the message is imediately sent
                                    //But you could have to do that way with an assync approach
                                }
                            }

                            lock (site)
                            {
                                foreach (var broker in site.brokers)
                                {
                                    // using broker.getURI() increases network traffic
                                    if (_orderingPolicy == OrderingPolicy.fifo)
                                    {
                                        int index = _siteToFifoStruct[site_name].FindIndex(item => item._publhisherURI == pmsg.publisherURI);
                                        if (index >= 0)
                                        {
                                            foreach (PropagatedPublishMessage _pmsg in _siteToFifoStruct[site.name][index].listOfmessages.ToList())
                                            {
                                                PropagatePublishDelegate d = new PropagatePublishDelegate(broker.propagatePublish);
                                                log(string.Format("[filtering routing] sent event '{0}' to '{1}'", _pmsg, broker.getURI()));
                                                d.BeginInvoke(_pmsg, null, null);

                                                if (_loggingLevel == LoggingLevel.full)
                                                    c.reportEvent(EventType.BroEvent, getURI(), _pmsg.publisherURI, _pmsg.topic, msg.seqnum);

                                                _siteToFifoStruct[site.name][index].listOfmessages.Remove(_pmsg);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        PropagatePublishDelegate d = new PropagatePublishDelegate(broker.propagatePublish);
                                        log(string.Format("[filtering routing] sent event '{0}' to '{1}'", pmsg, broker.getURI()));
                                        d.BeginInvoke(pmsg, null, null);

                                        if (_loggingLevel == LoggingLevel.full)
                                            c.reportEvent(EventType.BroEvent, getURI(), pmsg.publisherURI, pmsg.topic, msg.seqnum);

                                    }
                                }
                            }

                        }
                    }
                }

            }

            // send to parent site brokers
            // always send publish to parent, doesnt matter if interested in topic
            lock (_parentSiteLock)
            {
                if (_parentSite != null)
                {
                    foreach (Broker broker in _parentSite.brokers)
                    {
                        // TODO broker.getURI() is slow, we should use a cache                        
                        PropagatePublishDelegate d = new PropagatePublishDelegate(broker.propagatePublish);
                        log(string.Format("[Routing] sent event '{0}' to parent broker '{1}'", pmsg, broker.getURI()));
                        d.BeginInvoke(pmsg, null, null);

                        if (_loggingLevel == LoggingLevel.full)
                            c.reportEvent(EventType.BroEvent, getURI(), pmsg.publisherURI, pmsg.topic, msg.seqnum);


                    }
                }
            }

        }

        private bool equivalentTopic(string publishTopic, string subscribedTopic)
        {
            if (string.Compare(publishTopic, subscribedTopic) == 0)
                return true;

            // example if publishTopic = "/a/b/c/d"
            // and subscriberTopic = "/a/b/*"            
            if (subscribedTopic[subscribedTopic.Length - 1] == '*')
            {
                // prefix = "/a/b/
                var prefix = subscribedTopic.Substring(0, subscribedTopic.Length - 1);
                // true because "/a/b/c/d" starts with "/a/b/"
                if (publishTopic.StartsWith(prefix))
                    return true;
            }
            return false;
        }

        private void propagatingRouting(PropagatedPublishMessage msg)
        {
            string origin_site = msg.origin_site;
            msg.origin_site = _site;

            if (_routingPolicy == RoutingPolicy.flooding)
            {
                lock (_childSites)
                {
                    foreach (var s in _childSites)
                    {
                        lock (s)
                        {
                            if (s.name != origin_site)
                            {
                                foreach (var broker in s.brokers)
                                {
                                    // TODO broker.getURI() is slow, we should use a cache                                    
                                    PropagatePublishDelegate d = new PropagatePublishDelegate(broker.propagatePublish);
                                    log(string.Format("[propagatingRouting] flooding. sent event '{0}' to '{1}'", msg, broker.getURI()));
                                    d.BeginInvoke(msg, null, null);

                                    if (_loggingLevel == LoggingLevel.full)
                                        c.reportEvent(EventType.BroEvent, getURI(), msg.publisherURI, msg.topic, msg.seqnum);


                                }
                            }
                        }
                    }
                }
            }
            else // routing policy is filtering
            {
                List<string> sentSites = new List<string>();
                lock (_topicSubscribers)
                {
                    foreach (var subscribedTopic in _topicSites.Keys)
                    {
                        if (!equivalentTopic(msg.topic, subscribedTopic))
                            continue;
                        foreach (var site_name in _topicSites[subscribedTopic])
                        {
                            if (sentSites.Contains(site_name))
                                continue;
                            if (site_name == origin_site)
                                continue;
                            var site = _nameToSite[site_name];
                            if (_orderingPolicy == OrderingPolicy.fifo)
                            {
                                //FIFO
                                //translate seq_num of the message to send to child_sites
                                lock (_siteToFifoStruct)
                                {
                                    if (!_siteToFifoStruct.ContainsKey(site_name))
                                    {
                                        _siteToFifoStruct.Add(site_name, new List<FIFOstruct>());
                                    }
                                    int index = _siteToFifoStruct[site_name].FindIndex(item => item._publhisherURI == msg.publisherURI);
                                    if (index < 0)
                                    {
                                        // element does not exists
                                        _siteToFifoStruct[site_name].Add(new FIFOstruct(msg.publisherURI, 0));
                                        //getIndex Now
                                        index = _siteToFifoStruct[site_name].FindIndex(item => item._publhisherURI == msg.publisherURI);
                                    }
                                    var fifo = _siteToFifoStruct[site_name][index];
                                    //create a new message for each site interested sites with possibly a different seqnum
                                    PropagatedPublishMessage local_pmsg = new PropagatedPublishMessage(msg, _site);
                                    local_pmsg.origin_site = _site;
                                    local_pmsg.seqnum = fifo._seq_num;
                                    fifo.listOfmessages.Add(local_pmsg);
                                    fifo._seq_num++;
                                }
                            }

                            lock (site)
                            {
                                foreach (var broker in site.brokers)
                                {
                                    if (_orderingPolicy == OrderingPolicy.fifo)
                                    {
                                        int index = _siteToFifoStruct[site_name].FindIndex(item => item._publhisherURI == msg.publisherURI);
                                        if (index >= 0)
                                        {
                                            foreach (PropagatedPublishMessage _pmsg in _siteToFifoStruct[site.name][index].listOfmessages.ToList())
                                            {



                                                PropagatePublishDelegate d = new PropagatePublishDelegate(broker.propagatePublish);
                                                log(string.Format("[filtering routing] sent event '{0}' to '{1}'", _pmsg, broker.getURI()));
                                                d.BeginInvoke(_pmsg, null, null);

                                                if (_loggingLevel == LoggingLevel.full)
                                                    c.reportEvent(EventType.BroEvent, getURI(), _pmsg.publisherURI, _pmsg.topic, _pmsg.seqnum);
                                                _siteToFifoStruct[site.name][index].listOfmessages.Remove(_pmsg);

                                            }
                                        }
                                    }
                                    else
                                    {

                                        // using broker.getURI() increases network traffic
                                        log(string.Format("sent '{0}' to '{1}'", msg, broker.getURI()));
                                        PropagatePublishDelegate d = new PropagatePublishDelegate(broker.propagatePublish);
                                        d.BeginInvoke(msg, null, null);

                                        if (_loggingLevel == LoggingLevel.full)
                                            c.reportEvent(EventType.BroEvent, getURI(), msg.publisherURI, msg.topic, msg.seqnum);
                                        //broker.propagatePublish(msg);
                                    }

                                }
                            }

                        }
                    }
                }


            }

            // send to parent site brokers
            // always send publish to parent, doesnt matter if interested in topic
            lock (_parentSiteLock)
            {
                if (_parentSite != null && _parentSite.name != origin_site)
                {
                    log(string.Format("[Sending to parent site] msg: '{0}' to parent site '{1}' origin_site '{2}'", msg, _parentSite.name, origin_site));
                    foreach (Broker broker in _parentSite.brokers)
                    {
                        // TODO broker.getURI() is slow, we should use a cache
                        log(string.Format("Sent '{0}' to parent broker '{1}'", msg, broker.getURI()));
                        PropagatePublishDelegate d = new PropagatePublishDelegate(broker.propagatePublish);
                        d.BeginInvoke(msg, null, null);
                        c.reportEvent(EventType.BroEvent, getURI(), msg.publisherURI, msg.topic, msg.seqnum);
                    }
                }
            }

        }

        public string getServiceName()
        {
            return _serviceName;
        }

        public string getProcessName()
        {
            return _processName;
        }
    }
}
