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
        // personal information
        private string _serviceName;
        private string _site;
        private string _uri;
        private bool _isRoot;
        private string _processName;
        private Form1 _form;
        // site information
        private List<Publisher> _publishers = new List<Publisher>();

        // Subscribers
        private List<Subscriber> _subscribers = new List<Subscriber>();
        // uri to subscriber
        private Dictionary<string, Subscriber> _uriToSubs = new Dictionary<string, Subscriber>();
        // uri to Publisher
        private Dictionary<string, Publisher> _uriToPubs = new Dictionary<string, Publisher>();
        // key = topic value= "List of subscribers that subscribed to that topic"
        Dictionary<string, List<string>> _topicSubscribers = new Dictionary<string, List<string>>();

        // Child sites
        private List<Site> _childSites = new List<Site>();
        // site_name to Site
        private Dictionary<string, Site> _nameToSite = new Dictionary<string, Site>();
        // key = topic, value = list of interested sites
        Dictionary<string, List<string>> _topicSites = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> _topicChildSites = new Dictionary<string, List<string>>();
        // can be null
        private Object _parentSiteLock = new object();
        private Site _parentSite;
        private ICoordinator c;
        private int seq;
        private string _coordinatorURI;


        ///////////FIFO/////////
        private Dictionary<string, List<FIFOstruct>> _siteToFifoStruct = new Dictionary<string, List<FIFOstruct>>();
        private Dictionary<string, List<FIFOstruct>> _subToFifoStruct = new Dictionary<string, List<FIFOstruct>>();
        private List<FIFOstruct> _fifostructs = new List<FIFOstruct>();

        /// <summary>
        /// Freezed variables
        /// </summary>
        private bool _freezed = false;
        private object _freezedLock = new object();
        private List<PublishMessage> _freezedPublishMessages = new List<PublishMessage>();
        private List<PublishMessage> _freezedPropagatedPublishMessages = new List<PublishMessage>();


        public BrokerRemote(Form1 form, PuppetMaster pm, string uri, string name, string site, string addr, string processName)
        {
            _form = form;
            _uri = uri;
            _serviceName = name;
            _pm = pm;
            _site = site;
            _orderingPolicy = OrderingPolicy.fifo;
            _routingPolicy = RoutingPolicy.flooding;
            seq = 0;
            _coordinatorURI = addr;
            _processName = processName;
        }

        private bool isSequencer()
        {
            return _parentSite == null;
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
            _nameToSite.Add(_parentSite.name, _parentSite);
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
            log("[STATUS] MyURI: " + getURI());
            log("[STATUS] ProcessName: " + getProcessName());
            log("[STATUS] Site: " + getSite());
            log("[STATUS] MyURI: " + getURI());
            pubStatus();
            subStatus();
            parentStatus();
            childsStatus();
            log("[STATUS] Freeze:" + _freezed);


            return "OK";
        }

        private void pubStatus()
        {
            bool _alive;
            log("[STATUS] Trying to get  Publishers status");
            foreach (KeyValuePair<string, Publisher> entry in _uriToPubs)
            {
                String s = entry.Key;
                Publisher p = entry.Value;
                try
                {
                    p.imAlive();
                    _alive = true;
                    log("         " + s + " is alive:" + _alive);

                }
                catch (Exception)
                {
                    _alive = false;
                    log("         " + s + " is alive:" + _alive);
                }
            }
        }

        private void subStatus()
        {
            bool _alive;
            log("[STATUS] Trying to get  Subscribers status");
            foreach (KeyValuePair<string, Subscriber> entry in _uriToSubs)
            {
                String s = entry.Key;
                Subscriber p = entry.Value;
                try
                {
                    p.imAlive();
                    _alive = true;
                    log("         " + s + " is alive:" + _alive);

                }
                catch (Exception)
                {
                    _alive = false;
                    log("         " + s + " is alive:" + _alive);
                }
            }
        }

        private void parentStatus()
        {
            if (_isRoot)
            {
                log("[STATUS] I'm root");
                return;
            }


            log("[STATUS] Trying to get  Parent status");
            bool _alive;
            try
            {
                _parentSite.brokers[0].imAlive();
                _alive = true;
                log("         " + _parentSite.brokers[0].getURI() + " is alive:" + _alive);

            }
            catch (Exception)
            {
                _alive = false;
                log("         " + _parentSite.brokers[0].getURI() + " is alive:" + _alive);
            }

        }
        private void childsStatus()
        {
            bool _alive;
            log("[STATUS] Trying to get  Childs status");
            foreach (Site s in _childSites)
            {
                Broker b = s.brokers[0];
                try
                {
                    b.imAlive();
                    _alive = true;
                    log("         " + b.getURI() + " is alive:" + _alive);
                }
                catch (Exception)
                {
                    _alive = false;
                    log("         " + b.getURI() + " is alive:" + _alive);
                }
            }
            log("[STATUS] Printing topics and interested child sites");

            foreach(var kv in _topicSites)
        {
                string buf = "";
                string topic = kv.Key;
                List<string> childs = kv.Value;
                buf += string.Format("         Topic:'{0}' Sites({1}):",topic,childs.Count);
                buf += string.Join(",", childs);
                log(buf);
                }
                }

        // *end* Puppet Master functions

        void log(string e)
        {
            _form.log(e);
        }

        public int getTotalOrderSequenceNumber()
        {
            return 0;
        }

        public void subscribe(SubscribeMessage msg)
        {
            log(string.Format("[Subscribe] Received event '{0}'", msg));
            // To solve duplicate propagatedSubscribedMessages
            bool isNewTopic = false;
            bool canPropagateToParent = false;

            lock (_topicSubscribers)
            {
                if (!_topicSubscribers.ContainsKey(msg.topic))
                {
                    //This broker didn't send a propagatedSubscribedMessage with this topic till now
                    if (!_topicSites.ContainsKey(msg.topic))
                    {
                        isNewTopic = true;
                    }
                    _topicSubscribers.Add(msg.topic, new List<string>());
                    
                }
                _topicSubscribers[msg.topic].Add(msg.uri);
            }

            lock (_topicChildSites)
            {
                
                    if (!_topicChildSites.ContainsKey(msg.topic))
                    {
                        //This broker didn't send a propagatedSubscribedMessage with this topic till now
                        if (!_topicChildSites.ContainsKey(msg.topic))
                        {
                            canPropagateToParent = true;
                        }

                      _topicChildSites.Add(msg.topic, new List<string>());

                    }
                    _topicChildSites[msg.topic].Add(msg.uri);
                
            }
            //Only send a propagatedSubscribedMessage per Topic
            if (canPropagateToParent)
            {
                PropagatedSubcribeMessage pmsg = new PropagatedSubcribeMessage(msg, _site);
                // propagate subscribe to parent, taking advantage of tree strucure
                lock (_parentSiteLock)
                {
                    if (_parentSite != null)
                    {
                        foreach (Broker b in _parentSite.brokers)
                        {
                            //TODO assyncronous
                            log(string.Format("[subscribe] sending '{0}' to parent site '{1}'", pmsg, _parentSite.name));
                            b.propagateSubscribe(pmsg);
                        }
                    }
                }
            }
            if (isNewTopic)
            {
                PropagatedSubcribeMessage pmsg = new PropagatedSubcribeMessage(msg, _site);
                lock (_childSites)
                {
                    foreach (var s in _childSites)
                    {
                        lock (s)
                        {
                            
                            foreach (var b in s.brokers)
                            {
                                log(string.Format("[subscribe] sending '{0}' to child site '{1}'", pmsg, s.name));
                                //TODO assync
                                b.propagateSubscribe(pmsg);
                            }

                        }
                    }
                }
            }

        }

        public void propagateSubscribe(PropagatedSubcribeMessage msg)
        {
            string origin_site = msg.interested_site;
            log(string.Format("[propagateSubscribe] Received event '{0}'", msg));


            // To solve duplicate propagatedSubscribedMessages
            bool isNewTopic = false;
            bool canPropagateToParent = false;

            lock (_topicSites)
            {
                if (!_topicSites.ContainsKey(msg.topic))
                {
                    //This broker didn't send a propagatedSubscribedMessage with this topic till now
                    if (!_topicSubscribers.ContainsKey(msg.topic))
                    {
                        isNewTopic = true;
                    }
                    _topicSites.Add(msg.topic, new List<string>());
                   
                }
                _topicSites[msg.topic].Add(msg.interested_site);
            }

            lock (_topicChildSites)
            {
               
                if (_parentSite != null)
                {
                    if (_parentSite.name != origin_site)
                    {

                        if (!_topicChildSites.ContainsKey(msg.topic))
                        {
                            //This broker didn't send a propagatedSubscribedMessage with this topic till now
                            if (!_topicSubscribers.ContainsKey(msg.topic))
                            {
                                canPropagateToParent = true;
                            }

                            _topicChildSites.Add(msg.topic, new List<string>());

                        }
                        _topicChildSites[msg.topic].Add(msg.interested_site);
                    }
                }
            }

            //Only send a propagatedSubscribedMessage per Topic
            if (canPropagateToParent)
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
                                log(string.Format("[propagateSubscribe] sending '{0}' to parent site '{1}'", msg, _parentSite.name));
                                //TODO assync
                                b.propagateSubscribe(msg);
                            }
                        }
                    }
                }
            }

            if (isNewTopic)
            {
                msg.interested_site = _site;
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
                                   
                                    log(string.Format("[propagateSubscribe] sending '{0}' to child site '{1}'", msg, s.name));
                                    //TODO assync
                                    b.propagateSubscribe(msg);
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
                PropagatedUnsubscribeMessage pmsg = new PropagatedUnsubscribeMessage(msg, _site);
                // propagate unsubscribe only to parent, taking advantage of tree strucure
                lock (_parentSiteLock)
                {
                    if (_parentSite != null)
                    {
                        foreach (Broker b in _parentSite.brokers)
                        {
                            log(string.Format("[Unsubscribe] sending '{0}' to parent site '{1}'", pmsg, _parentSite.name));
                            // TODO assyncronous
                            b.propagateUnsubscribe(pmsg);
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
                                log(string.Format("[Unsubscribe] sending '{0}' to child site '{1}'", pmsg, s.name));
                                //TODO assync
                                b.propagateUnsubscribe(pmsg);
                            }
                        }
                    }
                }
            }
            else
            {
                if (remainingOneSubscrition)
                {
                    PropagatedUnsubscribeMessage pmsg = new PropagatedUnsubscribeMessage(msg, _site);
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
                                    log(string.Format("[Unsubscribe] sending '{0}' to site '{1}'", pmsg, s_name));
                                    //TODO assync
                                    b.propagateUnsubscribe(pmsg);
                                }
                            }
                        }
                    }
                }
            }
        }
       
        public void propagateUnsubscribe(PropagatedUnsubscribeMessage msg)
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
            catch(Exception e)
            {
                log(e.ToString());
            }


        }

        public void publishWork(PublishMessage msg)
        {
            // FLOODING implementation
            // TODO discart if duplicate message
            // TODO make all calls assyncs
            string buf = "";            
            if (_orderingPolicy == OrderingPolicy.total)
            {
                log(string.Format("[Publish TOTAL] Received event '{0}'", msg));
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
                    //log(string.Format("[Publish FIFO]{4}\r\n  Pub:{0} Remaining:{1} next seqnum:{2}. lowest seqnum:{3} ", fifo._publhisherURI, fifo.listOfmessages.Count(), fifo._seq_num, fifo.listOfmessages[0].seqnum, msg));
                    log(string.Format("[Publish FIFO]{0}", msg));
                    log(string.Format("fifo.seqnum:{0} Waitng list:", fifo._seq_num));
                    foreach (var s in fifo.listOfmessages)
                      {
                        log(string.Format(" seqnum:{0}", s.seqnum));
                    }
                    foreach (PublishMessage _msg in fifo.listOfmessages.ToList())
                    {
                        if (_msg.seqnum == fifo._seq_num)
                        {
                            //Prepare to send the msg to interested sites
                            log(string.Format("Sending {0}",_msg.seqnum));
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
                log(string.Format("[Publish] Received event '{0}'", msg));
                //TODO DISCARD DUPLICATES
                deliver(msg);
                routing(msg);
            }
        }

        private void deliver(PublishMessage msg)
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
                    foreach (var uri in _topicSubscribers[subscribedTopic])
                {
                        if (sentURIs.Contains(uri))
                            continue;
                        sentURIs.Add(uri);
                        Subscriber s = _uriToSubs[uri];
                        // TODO assync
                        ReceiveDelegate rd = new ReceiveDelegate(s.receive);
                        remoteDelegate.Add(uri, rd);
                        //MUDAR ISTO...
                        // c.reportEvent(EventType.SubEvent, uri, msg.publisherURI, msg.topic, msg.total_seqnum);

                        //Begin FIFO
                        if(_orderingPolicy == OrderingPolicy.total)
                        {
                            throw new Exception("not implemented");
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
                                msg.seqnum = fifo._seq_num;
                                fifo._seq_num++;                                
                    }

                        }
                        else
                        { // No ordering
                            ;
                    }
                        rd.BeginInvoke(msg, null, null);
                        log(string.Format("[Deliver] sent event '{0}' to '{1}'", msg, uri));

                }
            }
            }
        }

        private void flooding(PublishMessage receivedMessage,PublishMessage sendingMessage)
            {
                lock (_childSites)
                {
                foreach (var site in _childSites)
                {
                    lock (site)
                    {
                        if (site.name != receivedMessage.origin_site)
                        {
                            foreach (var broker in site.brokers)
                            {
                                // TODO broker.getURI() is slow, we should use a cache
                                PublishDelegate d = new PublishDelegate(broker.publish);
                                d.BeginInvoke(sendingMessage, null, null);
                                log(string.Format("[Flooding Routing] sent event '{0}' to '{1}'", receivedMessage, broker.getURI()));
                                if (_loggingLevel == LoggingLevel.full)
                                    c.reportEvent(EventType.BroEvent, getURI(), receivedMessage.publisherURI, receivedMessage.topic, receivedMessage.seqnum);
                            }
                            }
                        }
                    }
                }

                lock (_parentSiteLock)
                {
                if (_parentSite != null && _parentSite.name != receivedMessage.origin_site)
                    {
                        foreach (Broker broker in _parentSite.brokers)
                        {
                            // TODO broker.getURI() is slow, we should use a cache                        
                        PublishDelegate d = new PublishDelegate(broker.publish);
                        log(string.Format("[Routing] sent event '{0}' to parent broker '{1}'", sendingMessage, broker.getURI()));
                        d.BeginInvoke(sendingMessage, null, null);

                            if (_loggingLevel == LoggingLevel.full)
                            c.reportEvent(EventType.BroEvent, getURI(), sendingMessage.publisherURI, sendingMessage.topic, receivedMessage.seqnum);


                        }
                    }
                }

            }

        private void filter(PublishMessage receivedMessage, PublishMessage sendingMessage)
            {
            log(string.Format("[filter]\r\n   receivedMsg:{0}\r\n   sendingMsg:{1}",receivedMessage,sendingMessage));
                lock (_topicSites)
                {
                    foreach (var subscribedTopic in _topicSites.Keys)
                    {
                    if (!equivalentTopic(receivedMessage.topic, subscribedTopic))
                            continue;
                        foreach (var site_name in _topicSites[subscribedTopic])
                        {
                        if (site_name == receivedMessage.origin_site)
                                continue;
                            var site = _nameToSite[site_name];
                        if (_orderingPolicy == OrderingPolicy.total)
                        {
                            throw new Exception("not implemented");
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
                                    //create a new message for each site interested sites with possibly a different seqnum
                                sendingMessage.seqnum = fifo._seq_num;
                                //fifo.listOfmessages.Add(pmsg);
                                    fifo._seq_num++;
                                }
                            }
                           
                            lock (site)
                            {
                                foreach (var broker in site.brokers)
                                {
                                PublishDelegate d = new PublishDelegate(broker.publish);
                                    // using broker.getURI() increases network traffic
                                if(_orderingPolicy == OrderingPolicy.total)
                                        {
                                    throw new Exception("not impl");
                                }else if (_orderingPolicy == OrderingPolicy.fifo)
                                            {
                                    d.BeginInvoke(sendingMessage, null, null);
                                    log(string.Format("[filtering routing -FIFO] sent event '{0}' to '{1}'", sendingMessage, broker.getURI()));
                                    }
                                    else
                                    {
                                    d.BeginInvoke(sendingMessage, null, null);
                                    log(string.Format("[filtering routing] sent event '{0}' to '{1}'", sendingMessage, broker.getURI()));                                    
                                }
                                        if (_loggingLevel == LoggingLevel.full)
                                    c.reportEvent(EventType.BroEvent, getURI(), sendingMessage.publisherURI, sendingMessage.topic, receivedMessage.seqnum);
                            }
                        }

                                    }
                                }
                            }
        }
        private void routing(PublishMessage receivedMessage)
        {
            PublishMessage sendingMessage = new PublishMessage(receivedMessage, _site);

            if (_routingPolicy == RoutingPolicy.flooding)
            {
                flooding(receivedMessage, sendingMessage);
                    }
            else // routing policy is filtering
            {
                filter(receivedMessage, sendingMessage);
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
