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
using System.Windows.Forms;

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
        private OrderingPolicy _orderingPolicy;
        private RoutingPolicy _routingPolicy;
        private LoggingLevel _loggingLevel;
        private PuppetMaster _pm;
        private ICoordinator c;
        private string _coordinatorURI;


        // personal information
        private string _serviceName;
        private string _site;
        private string _uri;
        private bool _isRoot;
        private string _processName;
        private BrokerForm _form;
        // site information
        /// <summary>
        /// site configuration
        /// </summary>
        private Object _parentSiteLock = new object();
        private Site _parentSite;
        private List<Publisher> _publishers = new List<Publisher>();
        private List<Subscriber> _subscribers = new List<Subscriber>();
        private List<Site> _childSites = new List<Site>();

        // translation from siteName to string
        private Dictionary<string, Site> _nameToSite = new Dictionary<string, Site>();

        // uri to subscriber
        private Dictionary<string, Subscriber> _uriToSubs = new Dictionary<string, Subscriber>();
        // uri to Publisher
        private Dictionary<string, Publisher> _uriToPubs = new Dictionary<string, Publisher>();

        /// <summary>
        /// Deliver Variables
        /// 
        /// key is the topic
        /// value is list of URIs of the site subscribers
        /// </summary>       
        Dictionary<string, List<string>> _topicSubscribers = new Dictionary<string, List<string>>();

        /// <summary>
        /// FILTERING
        /// </summary>        
        // key = topic, value = list of interested sites
        Dictionary<string, List<string>> _topicSites = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> _topicChildSites = new Dictionary<string, List<string>>();
        //Used in subscritions  <site.name, ListofTopicsPropagated>
        Dictionary<string, List<string>> _siteToPropagatedSub = new Dictionary<string, List<string>>();

        //FIFO
        // for each site list seqnum for each publisher
        private Dictionary<string, List<FIFOstruct>> _siteToFifoStruct = new Dictionary<string, List<FIFOstruct>>();
        // for each sub list seqnum for each publisher
        private Dictionary<string, List<FIFOstruct>> _subToFifoStruct = new Dictionary<string, List<FIFOstruct>>();
        // list seqnum for each publisher
        private List<FIFOstruct> _fifostructs = new List<FIFOstruct>();

        // FREEZED variables        
        private bool _freezed = false;
        private object _freezedLock = new object();
        private List<PublishMessage> _freezedPublishMessages = new List<PublishMessage>();
        private List<PublishMessage> _freezedPropagatedPublishMessages = new List<PublishMessage>();

        // TOTAL ORDER = TO
        private object _TOSeqnumLock = new object();
        private int _TOSeqnum = 0;
        private Dictionary<string, int> _siteTOSeqnum = new Dictionary<string, int>();
        private Dictionary<string, int> _subTOSeqnum = new Dictionary<string, int>();
        private List<PublishMessage> _totalOrderQueue = new List<PublishMessage>();

        // Event counter is used to identify a thread of execution in the log of concurrent(intervaled) prints
        private int _eventnum = 0;
        private Object _eventnumLock = new Object();

        private int getEventnum()
        {
            lock (_eventnumLock)
            {
                int ret = _eventnum;
                _eventnum++;
                return ret;
            }
        }

        public BrokerRemote(BrokerForm form, PuppetMaster pm, string uri, string name, string site, string addr, string processName)
        {
            _form = form;
            _uri = uri;
            _serviceName = name;
            _pm = pm;
            _site = site;
            _orderingPolicy = OrderingPolicy.fifo;
            _routingPolicy = RoutingPolicy.flooding;
            _coordinatorURI = addr;
            _processName = processName;
        }

        public override string ToString()
        {
            return string.Format("[Broker] name:{0} uri:{1} site:{2}", _processName, _uri, _site);
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
            BrokerForm form = new BrokerForm(args);
            Application.Run(form);
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
            if (_orderingPolicy == OrderingPolicy.total)
            {
                foreach (Site s in child_sites)
                {
                    _siteTOSeqnum.Add(s.name, 0);
                }
            }
        }

        public void setParent(Site parent_site)
        {
            _parentSite = parent_site;
            _nameToSite.Add(_parentSite.name, _parentSite);
            if (_orderingPolicy == OrderingPolicy.total)
            {
                _siteTOSeqnum.Add(_parentSite.name, 0);
            }
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
            if (_orderingPolicy == OrderingPolicy.total)
            {
                foreach (Subscriber s in site_subscribers)
                {
                    _subTOSeqnum.Add(s.getURI(), 0);
                }
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

        public void addChild(Site s) { }
        public void removeChild(Site s) { }
        public void addSubscriber(Subscriber s) { }
        public void removeSubscriber(Subscriber s) { }

        // Puppet Master functions
        public void crash()
        {
            Process.GetCurrentProcess().Kill();
        }

        public string status()
        {
            string ret = "";
            ret += "[STATUS] <<<START>>>\n";
            ret += ("[STATUS] MyURI: " + getURI()); ret += "\n";
            ret += ("[STATUS] ProcessName: " + getProcessName()); ret += "\n";
            ret += ("[STATUS] Site: " + getSite()); ret += "\n";
            ret += ("[STATUS] MyURI: " + getURI()); ret += "\n";
            ret += ("[STATUS] Freeze:" + _freezed); ret += "\n";
            ret += pubStatus();
            ret += subStatus();
            ret += parentStatus();
            ret += childsStatus();
            ret += "[STATUS] <<<START>>>\n";
            return "OK";
        }

        private string pubStatus()
        {
            bool _alive;
            string ret = "";
            ret += ("[STATUS] Trying to get  Publishers status"); ret += "\n";
            foreach (KeyValuePair<string, Publisher> entry in _uriToPubs)
            {
                String s = entry.Key;
                Publisher p = entry.Value;
                try
                {
                    p.imAlive();
                    _alive = true;
                    ret += ("         " + s + " is alive:" + _alive); ret += "\n";

                }
                catch (Exception)
                {
                    _alive = false;
                    ret += ("         " + s + " is alive:" + _alive); ret += "\n";
                }
            }
            return ret;
        }

        private string subStatus()
        {
            string ret = "";
            bool _alive;
            ret += ("[STATUS] Trying to get  Subscribers status"); ret += "\n";
            foreach (KeyValuePair<string, Subscriber> entry in _uriToSubs)
            {
                String s = entry.Key;
                Subscriber p = entry.Value;
                try
                {
                    p.imAlive();
                    _alive = true;
                    ret += ("         " + s + " is alive:" + _alive); ret += "\n";

                }
                catch (Exception)
                {
                    _alive = false;
                    ret += ("         " + s + " is alive:" + _alive); ret += "\n";
                }
                ret += "\n";
            }
            foreach (var kv in _topicSubscribers)
            {
                string buf = "";
                string topic = kv.Key;
                List<string> subscribers = kv.Value;
                buf += string.Format("         Topic:{0} Subscribers({1}):", topic, subscribers.Count);
                buf += string.Join(",", subscribers);
                ret += buf + "\n";
            }
            return ret;
        }

        private string parentStatus()
        {
            string ret = "";
            if (_isRoot)
            {
                ret += ("[STATUS] I'm root"); ret += "\n";
                return ret;
            }


            ret += ("[STATUS] Trying to get  Parent status"); ret += "\n";
            bool _alive;
            try
            {
                _parentSite.brokers[0].imAlive();
                _alive = true;
                ret += ("         " + _parentSite.brokers[0].getURI() + " is alive:" + _alive); ret += "\n";

            }
            catch (Exception)
            {
                _alive = false;
                ret += ("         " + _parentSite.brokers[0].getURI() + " is alive:" + _alive); ret += "\n";
            }
            return ret;

        }
        private string childsStatus()
        {
            bool _alive;
            string ret = "";
            ret += ("[STATUS] Trying to get  Childs status"); ret += "\n";
            foreach (Site s in _childSites)
            {
                Broker b = s.brokers[0];
                try
                {
                    b.imAlive();
                    _alive = true;
                    ret += ("         " + b.getURI() + " is alive:" + _alive); ret += "\n";
                }
                catch (Exception)
                {
                    _alive = false;
                    ret += ("         " + b.getURI() + " is alive:" + _alive); ret += "\n";
                }
            }
            ret += ("[STATUS] Printing topics and interested child sites");

            foreach (var kv in _topicSites)
            {
                string buf = "";
                string topic = kv.Key;
                List<string> sites = kv.Value;
                buf += string.Format("         Topic:{0} Sites({1}):", topic, sites.Count);
                buf += string.Join(",", sites);
                ret += buf + "\n";
            }
            return ret;
        }

        // *end* Puppet Master functions

        void log(string e)
        {
            _form.log(e);
        }
        void log(int s, object e)
        {
            _form.log(string.Format("[job {0}]{1}", s, e));
        }

        private bool isSequencer()
        {
            lock (_parentSiteLock)
            {
                return _parentSite == null;
            }
        }

        public int getTOSeqnum()
        {
            int ret = 0;
            lock (_TOSeqnumLock)
            {
                ret = _TOSeqnum;
            }
            return ret;
        }
        public TOSeqnumRequest generateTOSeqnum()
        {
            TOSeqnumRequest req = null;
            if (isSequencer())
            {
                lock (_TOSeqnumLock)
                {
                    req = new TOSeqnumRequest() { sequencerURI = _processName, seqnum = _TOSeqnum };
                    _TOSeqnum++;
                }
            }
            else
            {
                req = _parentSite.brokers[0].generateTOSeqnum();
            }
            return req;

        }

        public void subscribe(SubscribeMessage msg)
        {
            string origin_site = msg.interested_site;

            if (origin_site == null)
            {
                log(string.Format("[Subscribe] Received event '{0}'", msg));
                lock (_topicSubscribers)
                {
                    if (!_topicSubscribers.ContainsKey(msg.topic))
                    {
                        _topicSubscribers.Add(msg.topic, new List<string>());
                    }
                    _topicSubscribers[msg.topic].Add(msg.uri);
                }
            }
            else
            {
                log(string.Format("[propagateSubscribe] Received event '{0}'", msg));
                lock (_topicSites)
                {
                    if (!_topicSites.ContainsKey(msg.topic))
                    {
                        _topicSites.Add(msg.topic, new List<string>());
                    }
                    _topicSites[msg.topic].Add(msg.interested_site);
                }
            }
           

            msg.interested_site = _site;
            // propagate subscribe to parent, taking advantage of tree strucure
            lock (_parentSiteLock)
            {
                if (_parentSite != null)
                {
                    if (origin_site == null || _parentSite.name != origin_site)
                    {
                        lock (_siteToPropagatedSub)
                        {
                            if (!_siteToPropagatedSub.ContainsKey(_parentSite.name))
                            {
                                _siteToPropagatedSub.Add(_parentSite.name, new List<string>());
                            }

                            if (!_siteToPropagatedSub[_parentSite.name].Contains(msg.topic))
                            {
                                _siteToPropagatedSub[_parentSite.name].Add(msg.topic);
                                foreach (var b in _parentSite.brokers)
                                {
                                    //TODO assyncronous
                                    if (origin_site == null)
                                    {
                                        log(string.Format("[subscribe] sending '{0}' to parent site '{1}'", msg, _parentSite.name));
                                    }
                                    else
                                    {
                                        log(string.Format("[propagateSubscribe] sending '{0}' to parent site '{1}'", msg, _parentSite.name));
                                    }
                                    SubscribeDelegate sd = new SubscribeDelegate(b.subscribe);
                                    sd.BeginInvoke(msg, null, null);
                                }
                            }
                        }
                    }
                }
            }
           
            lock (_childSites)
            {         
                foreach (var s in _childSites)
                {
                  
                    lock (s)
                    {
                        if (origin_site == null || s.name != origin_site)
                        {
                            lock (_siteToPropagatedSub)
                            {
                                if (!_siteToPropagatedSub.ContainsKey(s.name))
                                {
                                    _siteToPropagatedSub.Add(s.name, new List<string>());
                                }

                                if (!_siteToPropagatedSub[s.name].Contains(msg.topic))
                                {
                                    _siteToPropagatedSub[s.name].Add(msg.topic);
                                    foreach (var b in s.brokers)
                                    {
                                        if (origin_site == null)
                                        {
                                            log(string.Format("[subscribe] sending '{0}' to child site '{1}'", msg, s.name));
                                        }
                                        else
                                        {
                                            log(string.Format("[propagateSubscribe] sending '{0}' to child site '{1}'", msg, s.name));
                                        }

                                        SubscribeDelegate sd = new SubscribeDelegate(b.subscribe);
                                        sd.BeginInvoke(msg, null, null);
                                    }
                                }
                            }
                        }

                    }
                }
            }

            
        }

        public void unsubscribe(UnsubscribeMessage msg)
        {
            log(string.Format("[Unsubscribe] Received event '{0}'", msg));
            //We should only propagate the unsubscriveMessage if there is no more sites 
            //or subscribers that subscribe it
            bool isLastTopic = false;
            bool remainingOneSubscrition = false;
            lock (_topicSubscribers)
            {
                if (_topicSubscribers.ContainsKey(msg.topic))
                {
                    if (_topicSubscribers[msg.topic].Contains(msg.uri))
                        _topicSubscribers[msg.topic].Remove(msg.uri);

                    if (_topicSubscribers[msg.topic].Count == 0)
                    {
                        if (!_topicSites.ContainsKey(msg.topic))
                        {
                            isLastTopic = true;
                        }
                        else
                        {
                            if (_topicSites[msg.topic].Count == 1)
                            {
                                //propagateUnsubcribe to only this broker
                                remainingOneSubscrition = true;
                            }
                        }

                        _topicSubscribers.Remove(msg.topic);
                    }
                }
                else
                {
                    //If you unsubscribe something you are not subscribed does not make sense propagate the message
                    return;
                }
            }
            if (isLastTopic)
            {
                msg.interested_site = _site;
                // propagate unsubscribe only to parent, taking advantage of tree strucure
                lock (_parentSiteLock)
                {
                    if (_parentSite != null)
                    {
                        foreach (Broker b in _parentSite.brokers)
                        {
                            log(string.Format("[Unsubscribe] sending '{0}' to parent site '{1}'", msg, _parentSite.name));
                            UnsubscribeDelegate usd = new UnsubscribeDelegate(b.unsubscribe);
                            usd.BeginInvoke(msg, null, null);
                        }
                    }
                }
                lock (_childSites)
                {
                    foreach (var s in _childSites)
                    {
                        lock (s)
                        {
                            foreach (var b in s.brokers)
                            {
                                log(string.Format("[Unsubscribe] sending '{0}' to child site '{1}'", msg, s.name));
                                UnsubscribeDelegate usd = new UnsubscribeDelegate(b.unsubscribe);
                                usd.BeginInvoke(msg, null, null);
                            }
                        }
                    }
                }
            }
            else
            {
                if (remainingOneSubscrition)
                {
                    msg.interested_site = _site;
                    var site_names = _topicSites[msg.topic];
                    //only exists 1 occurence
                    if (site_names.Count == 1)
                    {
                        foreach (var s_name in site_names)
                        {
                            var site = _nameToSite[s_name];
                            lock (site)
                            {
                                foreach (var b in site.brokers)
                                {
                                    log(string.Format("[Unsubscribe] sending '{0}' to site '{1}'", msg, s_name));
                                    UnsubscribeDelegate usd = new UnsubscribeDelegate(b.unsubscribe);
                                    usd.BeginInvoke(msg, null, null);
                                }
                            }
                        }
                    }
                }
            }
        }

        /*   
                public void propagateUnsubscribe(UnsubscribeMessage msg)

                {
                    log(string.Format("[propagateUnsubscribe] Received event '{0}'", msg));
                    //We should only propagate the unsubscriveMessage if there is no more sites 
                    //or subscribers that subscribe it
                    bool isLastTopic = false;
                    bool remainingOneSubscrition = false;
                    string origin_site = msg.interested_site;
                    lock (_topicSites)
                    {
                        if (_topicSites.ContainsKey(msg.topic))
                        {
                            if (_topicSites[msg.topic].Contains(msg.interested_site))
                                _topicSites[msg.topic].Remove(msg.interested_site);

                            if (_topicSites[msg.topic].Count == 0)
                            {
                                if (!_topicSubscribers.ContainsKey(msg.topic))
                                {
                                    isLastTopic = true;
                                }
                                _topicSites.Remove(msg.topic);
                            }
                            else
                            {
                                if (_topicSites[msg.topic].Count == 1)
                                {
                                    if (!_topicSubscribers.ContainsKey(msg.topic))
                                    {
                                        remainingOneSubscrition = true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            //If you unsubscribe something you are not subscribed does not make sense propagate the message
                            return;
                        }
                    }

                    if (isLastTopic)
                    {
                        msg.interested_site = _site;
                        lock (_parentSiteLock)
                        {
                            if (_parentSite != null)
                            {
                                if (_parentSite.name != origin_site)
                                {
                                    foreach (var b in _parentSite.brokers)
                                    {
                                        log(string.Format("[propagateUnsubscribe] sending '{0}' to parent site '{1}'", msg, _parentSite.name));
                                        //TODO assync
                                        b.propagateUnsubscribe(msg);
                                    }
                                }
                            }
                        }
                        lock (_childSites)
                        {
                            foreach (var s in _childSites)
                            {
                                lock (s)
                                {
                                    if (s.name != origin_site)
                                    {
                                        foreach (var b in s.brokers)
                                        {
                                            log(string.Format("[propagateUnsubscribe] sending '{0}' to child site '{1}'", msg, s.name));
                                            //TODO assync
                                            b.propagateUnsubscribe(msg);
                                        }

                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (remainingOneSubscrition)
                        {
                            msg.interested_site = _site;
                            var site_names = _topicSites[msg.topic];
                            //only exists 1 occurence
                            if (site_names.Count == 1)
                            {
                                foreach (var s_name in site_names)
                                {
                                    var site = _nameToSite[s_name];
                                    lock (site)
                                    {
                                        foreach (var b in site.brokers)
                                        {
                                            log(string.Format("[propagateUnsubscribe] sending '{0}' to site '{1}'", msg, s_name));
                                            //TODO assync
                                            b.propagateUnsubscribe(msg);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
        */



        public void freeze()
        {
            log("Freezed");
            lock (_freezedLock)
            {
                _freezed = true;
            }
        }

        public void unfreeze()
        {
            log("Unfreezed");
            lock (_freezedLock)
            {
                if (!_freezed)
                    return;

                foreach (var msg in _freezedPublishMessages)
                {
                    publishWork(msg);
                }
                _freezedPublishMessages.Clear();
                _freezed = false;
            }
        }


        public void publish(PublishMessage msg)
        {
            bool freezed = false;
            try
            {
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
            catch (Exception e)
            {
                log(e.ToString());
            }


        }

        public void publishWork(PublishMessage msg)
        {
            // FLOODING implementation
            // TODO discart if duplicate message
            // TODO make all calls assyncs        
            int en = getEventnum();
            log(en, "Processing " + msg);
            if (_orderingPolicy == OrderingPolicy.total)
            {
                lock (_totalOrderQueue)
                lock (_TOSeqnumLock)
                {
                    // TODO discard old messages
                    _totalOrderQueue.Add(msg);
                    _totalOrderQueue = _totalOrderQueue.OrderBy(m => m.seqnum).ToList();
                    log(en, string.Format("TOTAL seqnum:{0} queue:{1}", _TOSeqnum, _totalOrderQueue));
                    var tmp = new List<PublishMessage>();
                    foreach (var mi in _totalOrderQueue.ToList())
                    {

                        if (mi.seqnum == _TOSeqnum)
                        {
                            _totalOrderQueue.Remove(mi);
                            _TOSeqnum++;
                            tmp.Add(mi);
                        }
                        else { break; }

                    }
                    foreach (var mi in tmp)
                    {
                        deliver(en, mi);
                        routing(en, mi);
                    }
                }
                throw new Exception("Not implemented");
            }
            else if (_orderingPolicy == OrderingPolicy.fifo)
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
                    /// Discard duplicates
                    /*
                    if(msg.seqnum < fifo._seq_num)
                    {
                        log(string.Format("[Publish] Received duplicate message pub:{0} seqnum:{1}", msg.publisherURI, msg.seqnum));
                        return;
                    }
                    */
                    fifo.listOfmessages.Add(msg);
                    fifo.listOfmessages = fifo.listOfmessages.OrderBy(item => item.seqnum).ToList();
                    log(en, string.Format("FIFO seqnum:{0} queue:{1}", fifo._seq_num, string.Join(",", fifo.listOfmessages.Select(x => x.seqnum))));
                    foreach (PublishMessage _msg in fifo.listOfmessages.ToList())
                    {
                        if (_msg.seqnum == fifo._seq_num)
                        {
                            //Prepare to send the msg to interested sites                            
                            deliver(en, _msg);
                            routing(en, _msg);

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
                //TODO DISCARD DUPLICATES
                deliver(en, msg);
                routing(en, msg);
            }
        }

        private void deliver(int en, PublishMessage msg)
        {
            // to avoid sending two times, we use a list
            List<string> sentURIs = new List<string>();

            // Dictionary of delegates per subcribers
            Dictionary<string, ReceiveDelegate> remoteDelegate = new Dictionary<string, ReceiveDelegate>();

            // First phase we filter site subscribers and fillin
            lock (_topicSubscribers)
            {
                foreach (var subscribedTopic in _topicSubscribers.Keys)
                {
                    if (!equivalentTopic(msg.topic, subscribedTopic))
                        continue;
                    foreach (var uri in _topicSubscribers[subscribedTopic]) // subscriber uri
                    {
                        if (sentURIs.Contains(uri))
                            continue;
                        sentURIs.Add(uri);
                        Subscriber s = _uriToSubs[uri];
                        // TODO assync
                        ReceiveDelegate rd = new ReceiveDelegate(s.receive);
                        remoteDelegate.Add(uri, rd);
                        ///
                        /// Here we are going to map the current message's seqnum to
                        /// the seqnum that each site is expecting
                        ///
                        if (_orderingPolicy == OrderingPolicy.total)
                        {
                            lock (_subTOSeqnum)
                            {
                                var x = _subTOSeqnum[uri];
                                msg.seqnum = x;
                                _subTOSeqnum[uri]++;
                                log(en, string.Format("TOTAL Deliver.For subscriber {2} mapping seqnum from {0} to {1}", x, msg.seqnum, uri));
                            }
                        }
                        else if (_orderingPolicy == OrderingPolicy.fifo)
                        {
                            lock (_subToFifoStruct)
                            {
                                if (!_subToFifoStruct.ContainsKey(uri))
                                {
                                    _subToFifoStruct.Add(uri, new List<FIFOstruct>());
                                }
                                int index = _subToFifoStruct[uri].FindIndex(item => item._publhisherURI == msg.publisherURI);
                                if (index < 0)
                                {
                                    // element does not exists
                                    _subToFifoStruct[uri].Add(new FIFOstruct(msg.publisherURI, 0));
                                    //getIndex Now
                                    index = _subToFifoStruct[uri].FindIndex(item => item._publhisherURI == msg.publisherURI);
                                }
                                var fifo = _subToFifoStruct[uri][index];
                                //create a new message for each site interested sites with possibly a different seqnum   
                                var x = msg.seqnum;
                                msg.seqnum = fifo._seq_num;
                                fifo._seq_num++;
                                log(en, string.Format("FIFO Deliver.For subscriber {2} mapping seqnum from {0} to {1}", x, msg.seqnum, uri));
                            }
                        }
                        else
                        { // No ordering
                            log(en, "NO ordering. no seqnum mapping");
                        }
                        rd.BeginInvoke(msg, null, null);
                        log(en, string.Format("Delivered message to {0}", uri));

                    }
                }
            }
        }

        private void flooding(int en, PublishMessage receivedMessage, PublishMessage sendingMessage)
        {
            lock (_childSites)
            {
                foreach (var site in _childSites)
                {
                 
                    lock (site)
                    {
                        if (site.name != receivedMessage.originSite)
                        {
                            foreach (var broker in site.brokers)
                            {
                                PublishDelegate d = new PublishDelegate(broker.publish);
                                d.BeginInvoke(sendingMessage, null, null);
                                log(en, string.Format("Flooding to child site {0}", site.name));
                                if (_loggingLevel == LoggingLevel.full)
                                    c.reportEvent(EventType.BroEvent, getURI(), receivedMessage.publisherURI, receivedMessage.topic, receivedMessage.seqnum);
                            }
                        }
                    }
                }
            }

            lock (_parentSiteLock)
            {
                if (_parentSite != null && _parentSite.name != receivedMessage.originSite)
                {
                    foreach (Broker broker in _parentSite.brokers)
                    {
                        PublishDelegate d = new PublishDelegate(broker.publish);
                        log(en, string.Format("Flooding to parent site {0}", _parentSite.name));
                        d.BeginInvoke(sendingMessage, null, null);
                        if (_loggingLevel == LoggingLevel.full)
                            c.reportEvent(EventType.BroEvent, getURI(), sendingMessage.publisherURI, sendingMessage.topic, receivedMessage.seqnum);
                    }
                }
            }

        }

        private void filter(int en, PublishMessage receivedMessage, PublishMessage sendingMessage)
        {
            
            lock (_topicSites)
            {
                foreach (var subscribedTopic in _topicSites.Keys)
                {
                    if (!equivalentTopic(receivedMessage.topic, subscribedTopic))
                        continue;
                    foreach (var site_name in _topicSites[subscribedTopic])
                    {
                        if (site_name == receivedMessage.originSite)
                            continue;
                        var site = _nameToSite[site_name];
                        if (_orderingPolicy == OrderingPolicy.total)
                        {
                            lock (_siteTOSeqnum)
                            {
                                var x = _siteTOSeqnum[site_name];
                                _siteTOSeqnum[site_name]++;
                                sendingMessage.seqnum = x;
                                log(en, string.Format("TOTAL routing.For site {2}, mapping seqnum from {0} to {1}",
                                    receivedMessage.seqnum, sendingMessage.seqnum, site_name));
                            }
                        }
                        else if (_orderingPolicy == OrderingPolicy.fifo)
                        {
                            //translate seq_num of the message to send to child_sites
                            lock (_siteToFifoStruct)
                            {
                                if (!_siteToFifoStruct.ContainsKey(site_name))
                                {
                                    _siteToFifoStruct.Add(site_name, new List<FIFOstruct>());
                                }
                                int index = _siteToFifoStruct[site_name].FindIndex(item => item._publhisherURI == sendingMessage.publisherURI);
                                if (index < 0)
                                {
                                    // element does not exists
                                    _siteToFifoStruct[site_name].Add(new FIFOstruct(receivedMessage.publisherURI, 0));
                                    //getIndex Now
                                    index = _siteToFifoStruct[site_name].FindIndex(item => item._publhisherURI == sendingMessage.publisherURI);
                                }
                                var fifo = _siteToFifoStruct[site_name][index];
                                sendingMessage.seqnum = fifo._seq_num;
                                log(en, string.Format("FIFO routing.For site {2}, mapping seqnum from {0} to {1}",
                                    receivedMessage.seqnum, sendingMessage.seqnum, site_name));
                                fifo._seq_num++;
                            }
                        }

                        lock (site)
                        {
                            foreach (var broker in site.brokers)
                            {
                                PublishDelegate d = new PublishDelegate(broker.publish);
                                if (_orderingPolicy == OrderingPolicy.total)
                                {
                                    d.BeginInvoke(sendingMessage, null, null);
                                    log(en, string.Format("TOTAL routed to site {0}", site.name));
                                }
                                else if (_orderingPolicy == OrderingPolicy.fifo)
                                {
                                    d.BeginInvoke(sendingMessage, null, null);
                                    log(en, string.Format("FIFO routed to site {0}", site.name));
                                }
                                else
                                {
                                    d.BeginInvoke(sendingMessage, null, null);
                                    log(en, string.Format("NO order, routed to {0}", site.name));
                                }
                                if (_loggingLevel == LoggingLevel.full)
                                    c.reportEvent(EventType.BroEvent, getURI(), sendingMessage.publisherURI, sendingMessage.topic, receivedMessage.seqnum);
                            }
                        }

                    }
                }
            }
        }
        private void routing(int en, PublishMessage receivedMessage)
        {
            PublishMessage sendingMessage = new PublishMessage(receivedMessage, _site);

            if (_routingPolicy == RoutingPolicy.flooding)
            {
                log(en, "RoutingPolicy Flooding");
                flooding(en, receivedMessage, sendingMessage);
            }
            else // routing policy is filtering
            {
                log(en, "RoutingPolicy Filter");
                filter(en, receivedMessage, sendingMessage);
            }
        }

        private void flooding(PublishMessage msg) { }

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
