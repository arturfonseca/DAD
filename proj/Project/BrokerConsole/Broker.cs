﻿using DADInterfaces;
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
        Dictionary<string, List<string>> _topicChildSites = new Dictionary<string, List<string>>();
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
        private Dictionary<string, List<FIFOstruct>> _subToFifoStruct = new Dictionary<string, List<FIFOstruct>>();
        private List<FIFOstruct> _fifostructs = new List<FIFOstruct>();
        private bool _freezed = false;
        private object _freezedLock = new object();
        private List<PublishMessage> _freezedPublishMessages = new List<PublishMessage>();
        private List<PropagatedPublishMessage> _freezedPropagatedPublishMessages = new List<PropagatedPublishMessage>();
        private string _processName;
        private Form1 _form;

        public BrokerRemote(Form1 form,PuppetMaster pm, string uri, string name, string site, string addr,string processName)
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
            _form.log(e);
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
                            log(string.Format("[subscribe] senting '{0}' to parent site '{1}'", pmsg, _parentSite.name));
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
                                log(string.Format("[subscribe] senting '{0}' to child site '{1}'", pmsg, s.name));
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
                                log(string.Format("[propagateSubscribe] senting '{0}' to parent site '{1}'", msg, _parentSite.name));
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
                                   
                                    log(string.Format("[propagateSubscribe] senting '{0}' to child site '{1}'", msg, s.name));
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
            lock (_topicSubscribers)
            {
                if (_topicSubscribers.ContainsKey(msg.topic))
                {
                    _topicSubscribers[msg.topic].Remove(msg.uri);
                    if (_topicSubscribers[msg.topic].Count == 0)
                    {
                        if (!_topicSites.ContainsKey(msg.topic))
                        {
                            isLastTopic = true;
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
                            log(string.Format("[Unsubscribe] senting '{0}' to parent site '{1}'", pmsg, _parentSite.name));
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
                                log(string.Format("[propagateSubscribe] senting '{0}' to parent site '{1}'", pmsg, s.name));
                                //TODO assync
                                b.propagateSubscribe(pmsg);
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
            string origin_site = msg.interested_site;
            lock (_topicSites)
            {
                if (_topicSites.ContainsKey(msg.topic))
                {
                    _topicSites[msg.topic].Remove(msg.interested_site);
                    if (_topicSites[msg.topic].Count == 0)
                    {
                        if (!_topicSubscribers.ContainsKey(msg.topic))
                        {
                            isLastTopic = true;
                        }
                        _topicSites.Remove(msg.topic);
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
                                log(string.Format("[subscribe] senting '{0}' to parent site '{1}'", msg, _parentSite.name));
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
                                    log(string.Format("[propagateSubscribe] senting '{0}' to parent site '{1}'", msg, s.name));
                                    //TODO assync
                                    b.propagateSubscribe(msg);
                                }

                            }
                        }
                    }
                }
            }
        }

        private void deliver(PublishMessage msg)
        {
            // to avoid sending two times, we use a list
            List<string> sentUris = new List<string>();

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
                        if (sentUris.Contains(uri))
                            continue;
                        Subscriber s = _uriToSubs[uri];
                        // TODO assync
                        ReceiveDelegate rd = new ReceiveDelegate(s.receive);
                        remoteDelegate.Add(uri, rd);
                        //MUDAR ISTO...
                        // c.reportEvent(EventType.SubEvent, uri, msg.publisherURI, msg.topic, msg.total_seqnum);
                        log(string.Format("[Deliver] sent event '{0}' to '{1}'", msg, uri));

                        //Begin FIFO
                        if (_orderingPolicy == OrderingPolicy.fifo)
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
                                PublishMessage local_msg = new PublishMessage();

                                //Creates a copy
                                local_msg.publisherURI = msg.publisherURI;
                                local_msg.seqnum = msg.seqnum;
                                local_msg.origin_seqnum = msg.origin_seqnum;
                                local_msg.topic = msg.topic;
                                local_msg.content = msg.content;
       

                                local_msg.seqnum = fifo._seq_num;
                                fifo.listOfmessages.Add(local_msg);

                                fifo._seq_num++;

                                //It's not necessary to register the message in the listOfmessages because the message is imediately sent
                                //But you could have to do that way with an assync approach
                            }

                        }
                        //end FIFO

                    }
                }
            }


            List<IAsyncResult> results = new List<IAsyncResult>();

            foreach (KeyValuePair<string,ReceiveDelegate> entry in remoteDelegate)
            {
                if (_orderingPolicy == OrderingPolicy.fifo)
                {
                    lock (_subToFifoStruct)
                    {
                        int index = _subToFifoStruct[entry.Key].FindIndex(item => item._publhisherURI == msg.publisherURI);
                        if (index >= 0)
                        {
                            foreach (PublishMessage _msg in _subToFifoStruct[entry.Key][index].listOfmessages.ToList())
                            {

                                IAsyncResult result = entry.Value.BeginInvoke(_msg, null, null);
                                results.Add(result);
                   
                                _subToFifoStruct[entry.Key][index].listOfmessages.Remove(_msg);
                            }
                        }
                    }
                }
                else
                {

                    IAsyncResult result = entry.Value.BeginInvoke(msg, null, null);
                    results.Add(result);
                }
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
                    fifo.listOfmessages.OrderBy(item => item.seqnum);
                   
                    //DEBUG ListOfMessages
                    /*  foreach (PublishMessage _msg in fifo.listOfmessages)
                      {
                          log(string.Format("[ListOfMessage] Publisher: '{0}' msg '{1}'", _msg.publisherURI, _msg));
                      }*/

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
                    fifo.listOfmessages.OrderBy(item => item.seqnum);

                    //DEBUG ListOfMessages
                   /* foreach (PublishMessage _msg in fifo.listOfmessages)
                    {
                        log(string.Format("[ListOfMessage] seqnum: '{0}' '{1}'", fifo._seq_num, _msg));
                    }*/

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
            log(seq.ToString());

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
                                                log(string.Format("[filtering routing -FIFO] sent event '{0}' to '{1}'", _pmsg, broker.getURI()));
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
                //Send po parent

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
                                                log(string.Format("[filtering routing -FIFO] sent event '{0}' to '{1}'", _pmsg, broker.getURI()));
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
