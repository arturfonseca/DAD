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
        private OrderingPolicy _orderingPolicy;
        private RoutingPolicy _routingPolicy;
        private LoggingLevel _loggingLevel;
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
        private Object _receivedLock = new object();
        private Object _receivedTOLock = new object();
        private Site _parentSite;
        private List<Site> _childSites = new List<Site>();

        // translation from siteName to string
        private Dictionary<string, Site> _nameToSite = new Dictionary<string, Site>();

        // uri to subscriber
        private Dictionary<string, Subscriber> _uriToSubs = new Dictionary<string, Subscriber>();
        // uri to Publisher
        private Dictionary<string, Publisher> _uriToPubs = new Dictionary<string, Publisher>();
        private Dictionary<String, HashSet<int>> receivedMsg = new Dictionary<String, HashSet<int>>();
        private Dictionary<String, HashSet<int>> receivedMsgTOupdate = new Dictionary<String, HashSet<int>>();

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
        //Used in subscritions  <site.name, ListofTopicsPropagated>
        Dictionary<string, Dictionary<string, int>> _siteToPropagatedSub = new Dictionary<string, Dictionary<string, int>>();
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
        private object _sequencerSeqnumLock = new object();
        private int _sequencerSeqnum = 0;
        private object _TOseqnumLock = new object();
        private int _TOSeqnum = 0;
        private object _TOUpdatesLock = new object();
        private List<TOUpdate> _TOUpdates = new List<TOUpdate>();
        private object _TOWaitingLock = new object();
        private List<TOUpdate> _TOWaiting = new List<TOUpdate>();
        private object _TOQueueLock = new object();
        private List<PublishMessage> _TOQueue = new List<PublishMessage>();
        private Dictionary<string, int> _TOSubscriberFIFO = new Dictionary<string, int>();
        private object _TORejectedLock = new object();
        private List<PublishMessage> _TORejected = new List<PublishMessage>();

        private Site mySite;
        //private List<PublishMessage> _totalOrderQueue = new List<PublishMessage>();

        // Event counter is used to identify a thread of execution in the log of concurrent(intervaled) prints
        private int _eventnum = 0;
        private Object _eventnumLock = new Object();

        public int getEventnum()
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
        }

        public void setParent(Site parent_site)
        {
            _parentSite = parent_site;
            _nameToSite.Add(_parentSite.name, _parentSite);
        }

        public void setPublishers(List<Publisher> site_publishers)
        {
            foreach (Publisher p in site_publishers)
            {
                _uriToPubs.Add(p.getURI(), p);

            }
        }

        public void setSubscribers(List<Subscriber> site_subscribers)
        {
            foreach (Subscriber s in site_subscribers)
            {
                var uri = s.getURI();
                _uriToSubs.Add(uri, s);
                _TOSubscriberFIFO.Add(uri, 0);
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
            ret += ("[STATUS] MyURI: " + getURI()) + Environment.NewLine;
            ret += ("[STATUS] ProcessName: " + getProcessName()) + Environment.NewLine;
            ret += ("[STATUS] Site: " + getSite()) + Environment.NewLine;
            ret += ("[STATUS] MyURI: " + getURI()) + Environment.NewLine;
            ret += ("[STATUS] Freeze:" + _freezed) + Environment.NewLine;
            ret += pubStatus();
            ret += subStatus();
            ret += parentStatus();
            ret += childsStatus();
            ret += "[STATUS] <<<END>>>" + Environment.NewLine;
            log(ret);
            return "OK";
        }

        private string pubStatus()
        {
            bool _alive;
            string ret = "";
            ret += ("[STATUS] Trying to get  Publishers status") + Environment.NewLine;
            foreach (KeyValuePair<string, Publisher> entry in _uriToPubs)
            {
                String s = entry.Key;
                Publisher p = entry.Value;
                try
                {
                    p.imAlive();
                    _alive = true;
                    ret += ("         " + s + " is alive:" + _alive) + Environment.NewLine;

                }
                catch (Exception)
                {
                    _alive = false;
                    ret += ("         " + s + " is alive:" + _alive) + Environment.NewLine;
                }
            }
            return ret;
        }

        private string subStatus()
        {
            string ret = "";
            bool _alive;
            ret += ("[STATUS] Trying to get  Subscribers status") + Environment.NewLine;
            foreach (KeyValuePair<string, Subscriber> entry in _uriToSubs)
            {
                String s = entry.Key;
                Subscriber p = entry.Value;
                try
                {
                    p.imAlive();
                    _alive = true;
                    ret += ("         " + s + " is alive:" + _alive) + Environment.NewLine;

                }
                catch (Exception)
                {
                    _alive = false;
                    ret += ("         " + s + " is alive:" + _alive) + Environment.NewLine;
                }
                ret += Environment.NewLine;
            }
            foreach (var kv in _topicSubscribers)
            {
                string buf = "";
                string topic = kv.Key;
                List<string> subscribers = kv.Value;
                buf += string.Format("         Topic:{0} Subscribers({1}):", topic, subscribers.Count);
                buf += string.Join(",", subscribers);
                ret += buf + Environment.NewLine;
            }
            return ret;
        }

        private string parentStatus()
        {
            string ret = "";
            if (_isRoot)
            {
                ret += ("[STATUS] I'm root") + Environment.NewLine;
                return ret;
            }


            ret += ("[STATUS] Trying to get  Parent status") + Environment.NewLine;
            bool _alive;
            try
            {
                _parentSite.brokers[0].imAlive();
                _alive = true;
                ret += ("         " + _parentSite.brokers[0].getURI() + " is alive:" + _alive) + Environment.NewLine;

            }
            catch (Exception)
            {
                _alive = false;
                ret += ("         " + "Parent" + " is alive:" + _alive) + Environment.NewLine;
            }
            return ret;

        }
        private string childsStatus()
        {
            bool _alive;
            string ret = "";
            ret += ("[STATUS] Trying to get  Childs status") + Environment.NewLine;
            foreach (Site s in _childSites)
            {
                Broker b = s.brokers[0];
                try
                {
                    b.imAlive();
                    _alive = true;
                    ret += ("         " + b.getURI() + " is alive:" + _alive) + Environment.NewLine;
                }
                catch (Exception)
                {
                    _alive = false;
                    ret += ("         " + b.getURI() + " is alive:" + _alive) + Environment.NewLine;
                }
            }
            ret += ("[STATUS] Printing topics and interested child sites") + Environment.NewLine;

            foreach (var kv in _topicSites)
            {
                string buf = "";
                string topic = kv.Key;
                List<string> sites = kv.Value;
                buf += string.Format("         Topic:{0} Sites({1}):", topic, sites.Count);
                buf += string.Join(",", sites);
                ret += buf + Environment.NewLine;
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

        public void updateTO(TOUpdate msg)
        {
            lock (_receivedTOLock)
            {
                if (receivedMsgTOupdate.ContainsKey(msg.topic))
                {
                    if (!receivedMsgTOupdate[msg.topic].Contains(msg.seqnum))
                        receivedMsgTOupdate[msg.topic].Add(msg.seqnum);
                    else
                        return;
                }
                else
                {
                    receivedMsgTOupdate.Add(msg.topic, new HashSet<int>());
                    receivedMsgTOupdate[msg.topic].Add(msg.seqnum);
                }
            }


            int en = getEventnum();
            log(en, string.Format("[TOUpdate] {0}", msg));
            //Propagate msg to childs
            lock (_childSites)
            {
                foreach (var childsite in _childSites)
                {
                    log(en, string.Format("[TOUpdate] sent to {0}", childsite));
                    foreach (var broker in childsite.brokers)
                    {
                        new updateTODelegate(broker.updateTO).BeginInvoke(msg, null, null);
                    }
                }
            }
            //////////////////
            lock (_TOUpdatesLock)
            {
                _TOUpdates.Add(msg);
                _TOUpdates = _TOUpdates.OrderBy(m => m.seqnum).ToList();
                var x = _TOUpdates.Select(m => string.Format("({0},{1})", m.topic, m.seqnum));
                log(en, string.Format("[TOUpdate] TOSeqnum:{0} TOUpdate:{1}", _TOSeqnum, string.Join(",", x)));
                while (_TOUpdates.Count != 0 && _TOSeqnum == _TOUpdates[0].seqnum)
                {
                    if (_topicSubscribers.Keys.Any(t => equivalentTopic(msg.topic, t)))
                    {
                        log(en, string.Format("[TOUpdate] saved update"));
                        lock (_TOWaitingLock)
                        {
                            _TOWaiting.Add(msg);
                        }
                    }
                    else
                    {
                        log(en, string.Format("[TOUpdate] discarded update because not interested in topic"));
                    }
                    _TOSeqnum++;
                    _TOUpdates.RemoveAt(0);

                }
                x = _TOUpdates.Select(m => string.Format("({0},{1})", m.topic, m.seqnum));
                log(en, string.Format("[TOUpdate] TOUpdate:{0}", string.Join(",", x)));
            }

            TODeliver(en);
        }

        private void TODeliver(int en)
        {
            log(en, string.Format("[TODeliver] start"));
            lock (_TOWaitingLock)
            {
                lock (_TOQueueLock)
                {
                    var x = _TOWaiting.Select(m => string.Format("({0},{1})", m.topic, m.seqnum));
                    log(en, string.Format("[TODeliver] TOWaiting:{0}", string.Join(",", x)));
                    var y = _TOQueue.Select(n => string.Format("({0},{1})", n.topic, n.seqnum));
                    log(en, string.Format("[TODeliver] TOQueue:{0}", string.Join(",", y)));

                    _TOWaiting = _TOWaiting.OrderBy(m => m.seqnum).ToList();
                    _TOQueue = _TOQueue.OrderBy(m => m.seqnum).ToList();

                    while (_TOQueue.Count != 0 && _TOWaiting.Count != 0)
                    {
                        var msg = _TOQueue[0];
                        var upt = _TOWaiting[0];
                        if (msg.seqnum != upt.seqnum)
                            break;
                        _TOQueue.RemoveAt(0);
                        _TOWaiting.RemoveAt(0);
                        lock (_topicSubscribers)
                        {
                            foreach (var subscribedTopic in _topicSubscribers.Keys)
                            {
                                if (!equivalentTopic(msg.topic, subscribedTopic))
                                    continue;
                                foreach (var subUri in _topicSubscribers[subscribedTopic])
                                {
                                    log(en, string.Format("[TODeliver] sent to {0}", subUri));
                                    var sub = _uriToSubs[subUri];
                                    ReceiveDelegate rd = new ReceiveDelegate(sub.receive);
                                    msg.seqnum = _TOSubscriberFIFO[subUri]++;
                                    rd.BeginInvoke(msg, null, null);
                                }
                            }
                        }
                    }

                    x = _TOWaiting.Select(m => string.Format("({0},{1})", m.topic, m.seqnum));
                    log(en, string.Format("[TODeliver] TOWaiting:{0}", string.Join(",", x)));
                    y = _TOQueue.Select(n => string.Format("({0},{1})", n.topic, n.seqnum));
                    log(en, string.Format("[TODeliver] TOQueue:{0}", string.Join(",", y)));

                }
            }
        }

        public void updateNetwork(int en, string topic, int seqnum)
        {
            var update = new TOUpdate() { originSite = _site, topic = topic, seqnum = seqnum };
            log(en, string.Format("[updateNetwork] Sending {0}", update));
            updateTO(update);
            //new updateTODelegate(this.updateTO).BeginInvoke(update, null, null);
        }

        public TOSeqnumRequest generateTOSeqnum(string topic)
        {
            TOSeqnumRequest req = null;
            int en = getEventnum();
            if (isSequencer())
            {
                log(en, "generateTOSeqnum: I am sequencer");
                lock (_sequencerSeqnumLock)
                {
                    req = new TOSeqnumRequest() { sequencerURI = _processName, seqnum = _sequencerSeqnum };
                    _sequencerSeqnum++;

                    foreach (Broker b in mySite.getBrokers())
                    {
                        try
                        {
                            if (b.getProcessName() != _processName)
                            {
                                b.setSeqNumber(_sequencerSeqnum);
                                b.updateNetwork(b.getEventnum(), topic, req.seqnum);
                            }

                        }
                        catch (Exception) { }
                    }

                }
                updateNetwork(en, topic, req.seqnum);
            }
            else
            {
                log(en, "generateTOSeqnum: Requesting to parent");
                foreach (Broker b in _parentSite.getBrokers())
                {
                    try
                    {
                        req = b.generateTOSeqnum(topic);
                        break;
                    }
                    catch (Exception)
                    {
                    }
                }

            }
            log(en, req.ToString());
            return req;

        }

        public void subscribe(SubscribeMessage msg)
        {
            string origin_site = msg.interested_site;
            int en = getEventnum();

            if (origin_site == null)
            {
                lock (_topicSubscribers)
                {
                    if (!_topicSubscribers.ContainsKey(msg.topic))
                    {
                        _topicSubscribers.Add(msg.topic, new List<string>());
                    }
                    //Discart possible duplicates
                    if (_topicSubscribers[msg.topic].Contains(msg.uri))
                        return;
                    else
                    {
                        _topicSubscribers[msg.topic].Add(msg.uri);
                    }

                }
            }
            else
            {
                lock (_topicSites)
                {
                    if (!_topicSites.ContainsKey(msg.topic))
                    {
                        _topicSites.Add(msg.topic, new List<string>());
                    }
                    //Discart possible duplicates
                    if (_topicSites[msg.topic].Contains(msg.interested_site))
                        return;
                    else
                    {
                        _topicSites[msg.topic].Add(msg.interested_site);
                    }
                }
            }
            //Doon't print discarted msg
            if (origin_site == null)
            {
                log(en, "[Subscribe] received" + msg.ToString());
            }
            else
            {
                log(en, "[Propagate subscribe] received" + msg.ToString());
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
                                _siteToPropagatedSub.Add(_parentSite.name, new Dictionary<string, int>());
                            }

                            if (!_siteToPropagatedSub[_parentSite.name].ContainsKey(msg.topic))
                            {
                                _siteToPropagatedSub[_parentSite.name].Add(msg.topic, 1);
                                log(en, string.Format("[Subscribe] Sending {0} to parent site {1}", msg, _parentSite.name));
                                foreach (var b in _parentSite.brokers)
                                {
                                    SubscribeDelegate sd = new SubscribeDelegate(b.subscribe);
                                    sd.BeginInvoke(msg, null, null);
                                }
                            }
                            else
                            {
                                var num = _siteToPropagatedSub[_parentSite.name][msg.topic];
                                num++;
                                _siteToPropagatedSub[_parentSite.name][msg.topic] = num;

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
                                    _siteToPropagatedSub.Add(s.name, new Dictionary<string, int>());
                                }

                                if (!_siteToPropagatedSub[s.name].ContainsKey(msg.topic))
                                {
                                    _siteToPropagatedSub[s.name].Add(msg.topic, 1);
                                    log(en, string.Format("[Subscribe] Sending {0} to child site {1}", msg, s.name));
                                    foreach (var b in s.brokers)
                                    {
                                        SubscribeDelegate sd = new SubscribeDelegate(b.subscribe);
                                        sd.BeginInvoke(msg, null, null);
                                    }
                                }
                                else
                                {
                                    var num = _siteToPropagatedSub[s.name][msg.topic];
                                    num++;
                                    _siteToPropagatedSub[s.name][msg.topic] = num;

                                }
                            }
                        }

                    }
                }
            }

            /* if (origin_site == null)
             {
                 log(en, "Subscribe finished");
             }
             else
             {
                 log(en, "Propagate subscribe finished");
             }*/
        }

        public void unsubscribe(UnsubscribeMessage msg)
        {
            string origin_site = msg.interested_site;
            int en = getEventnum();

            //We should only propagate the unsubscriveMessage if there is no more sites 
            //or subscribers that subscribe it

            if (origin_site == null)
            {
                lock (_topicSubscribers)
                {
                    if (_topicSubscribers.ContainsKey(msg.topic))
                    {
                        if (_topicSubscribers[msg.topic].Contains(msg.uri))
                        {
                            _topicSubscribers[msg.topic].Remove(msg.uri);
                        }
                        else
                        {
                            //discart duplicated message
                            return;
                        }

                        if (_topicSubscribers[msg.topic].Count == 0)
                        {
                            _topicSubscribers.Remove(msg.topic);
                        }
                    }
                    else
                    {
                        //If you unsubscribe something you are not subscribed does not make sense propagate the message
                        return;
                    }
                }
            }
            else
            {
                lock (_topicSites)
                {
                    if (_topicSites.ContainsKey(msg.topic))
                    {
                        if (_topicSites[msg.topic].Contains(msg.interested_site))
                        {
                            _topicSites[msg.topic].Remove(msg.interested_site);
                        }
                        else
                        {
                            //discart duplicated message
                            return;
                        }

                        if (_topicSites[msg.topic].Count == 0)
                        {
                            _topicSites.Remove(msg.topic);
                        }
                    }
                    else
                    {
                        //If you unsubscribe something you are not subscribed does not make sense propagate the message
                        return;
                    }
                }
            }

            //Doon't print discarted msg
            if (origin_site == null)
            {
                log(en, "[Unsubscribe] received" + msg.ToString());
            }
            else
            {
                log(en, "[Propagate unsubscribe] received" + msg.ToString());
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
                            if (_siteToPropagatedSub.ContainsKey(_parentSite.name))
                            {

                                var num = _siteToPropagatedSub[_parentSite.name][msg.topic];
                                num--;

                                _siteToPropagatedSub[_parentSite.name][msg.topic] = num;

                                if (_siteToPropagatedSub[_parentSite.name][msg.topic] == 0)
                                {
                                    _siteToPropagatedSub[_parentSite.name].Remove(msg.topic);
                                }

                                //NO More interested sites in this topic can propagate to parent
                                if (!_siteToPropagatedSub[_parentSite.name].ContainsKey(msg.topic))
                                {
                                    log(en, string.Format("[Unsubscribe] Sending {0} to parent site {1}", msg, _parentSite.name));
                                    foreach (var b in _parentSite.brokers)
                                    {
                                        UnsubscribeDelegate sd = new UnsubscribeDelegate(b.unsubscribe);
                                        sd.BeginInvoke(msg, null, null);
                                    }
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
                                if (_siteToPropagatedSub.ContainsKey(s.name))
                                {

                                    var num = _siteToPropagatedSub[s.name][msg.topic];
                                    num--;

                                    _siteToPropagatedSub[s.name][msg.topic] = num;

                                    if (_siteToPropagatedSub[s.name][msg.topic] == 0)
                                    {
                                        _siteToPropagatedSub[s.name].Remove(msg.topic);
                                    }

                                    //NO More interested sites in this topic can propagate to child
                                    if (!_siteToPropagatedSub[s.name].ContainsKey(msg.topic))
                                    {
                                        log(en, string.Format("[Unsubscribe] Sending {0} to site {1}", msg, s.name));
                                        foreach (var b in s.brokers)
                                        {
                                            UnsubscribeDelegate sd = new UnsubscribeDelegate(b.unsubscribe);
                                            sd.BeginInvoke(msg, null, null);
                                        }
                                    }
                                }
                            }
                        }

                    }
                }
            }

        }

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

        public void publishWork(PublishMessage receivingMessage)
        {
            // FLOODING implementation
            // TODO discart if duplicate message
            // TODO make all calls assyncs 


            //TODO LOCKS HERE

            lock (_receivedLock)
            {
                if (receivedMsg.ContainsKey(receivingMessage.publisherName))
                {
                    if (!receivedMsg[receivingMessage.publisherName].Contains(receivingMessage.originalSeqnum))
                        receivedMsg[receivingMessage.publisherName].Add(receivingMessage.originalSeqnum);
                    else
                        return;
                }
                else
                {
                    receivedMsg.Add(receivingMessage.publisherName, new HashSet<int>());
                    receivedMsg[receivingMessage.publisherName].Add(receivingMessage.originalSeqnum);
                }
            }


            int en = getEventnum();
            log(en, "Processing " + receivingMessage);
            if (_orderingPolicy == OrderingPolicy.total)
            {
                deliver(en, receivingMessage);
                routing(en, receivingMessage);
            }
            else if (_orderingPolicy == OrderingPolicy.fifo)
            {
                //FIFO
                lock (_fifostructs)
                {

                    int index = _fifostructs.FindIndex(item => item._publhisherURI == receivingMessage.publisherURI);
                    if (index < 0)
                    {
                        // element does not exists
                        _fifostructs.Add(new FIFOstruct(receivingMessage.publisherURI, 0));
                        //getIndex Now
                        index = _fifostructs.FindIndex(item => item._publhisherURI == receivingMessage.publisherURI);
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
                    fifo.listOfmessages.Add(receivingMessage);
                    fifo.listOfmessages = fifo.listOfmessages.OrderBy(item => item.seqnum).ToList();
                    var queueList = fifo.listOfmessages.Select(x => "" + x.seqnum);
                    var qlstr = string.Join(",", queueList);
                    log(en, string.Format("FIFO seqnum:{0} queue:{1}", fifo._seq_num, qlstr));
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
                deliver(en, receivingMessage);
                routing(en, receivingMessage);
            }
        }


        private void deliver(int en, PublishMessage rmsg)
        {
            var msg = new PublishMessage(rmsg, _site);
            // to avoid sending two times, we use a list
            List<string> sentURIs = new List<string>();

            // Dictionary of delegates per subcribers
            Dictionary<string, ReceiveDelegate> remoteDelegate = new Dictionary<string, ReceiveDelegate>();

            List<string> receivingSubscribers = new List<string>();
            // First phase we filter site subscribers and fillin
            lock (_topicSubscribers)
            {
                foreach (var subscribedTopic in _topicSubscribers.Keys)
                {
                    if (!equivalentTopic(msg.topic, subscribedTopic))
                        continue;
                    if (_orderingPolicy == OrderingPolicy.total)
                    {
                        lock (_TOQueue)
                        {
                            _TOQueue.Add(msg);
                        }
                        break;
                    }
                    foreach (var uri in _topicSubscribers[subscribedTopic]) // subscriber uri
                    {
                        if (receivingSubscribers.Contains(uri))
                            continue;
                        receivingSubscribers.Add(uri);
                    }
                }
            }

            if (_orderingPolicy == OrderingPolicy.total)
            {
                TODeliver(en);
                return;
            }


            foreach (var uri in receivingSubscribers)
            {
                Subscriber s = _uriToSubs[uri];
                // TODO assync
                ReceiveDelegate rd = new ReceiveDelegate(s.receive);
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
                        var x = msg.seqnum;
                        msg.seqnum = fifo._seq_num;
                        fifo._seq_num++;
                        log(en, string.Format("FIFO Deliver.For subscriber {2} mapping seqnum from {0} to {1}", x, msg.seqnum, uri));
                        rd.BeginInvoke(msg, null, null);
                    }
                }
                else
                { // No ordering
                    log(en, "NO ordering. no seqnum mapping");
                    rd.BeginInvoke(msg, null, null);
                    log(en, string.Format("Delivered message to {0}", uri));
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
                                log(en, string.Format("[Flooding] to child site {0}", site.name));
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
                        log(en, string.Format("[Flooding] to parent site {0}", _parentSite.name));
                        d.BeginInvoke(sendingMessage, null, null);
                        if (_loggingLevel == LoggingLevel.full)
                            c.reportEvent(EventType.BroEvent, getURI(), sendingMessage.publisherURI, sendingMessage.topic, receivedMessage.seqnum);
                    }
                }
            }

        }

        private void filter(int en, PublishMessage receivedMessage, PublishMessage sendingMessage)
        {
            log(en, "[Filter] Routing");
            var sentSites = new List<string>();
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
                        if (sentSites.Contains(site_name))
                        {
                            continue;
                        }
                        sentSites.Add(site_name);

                        var site = _nameToSite[site_name];
                        if (_orderingPolicy == OrderingPolicy.fifo)
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
                                log(en, string.Format("[Filter][FIFO] For site {2}, mapping seqnum from {0} to {1}",
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
                                    log(en, "Routing: received " + receivedMessage);
                                    log(en, "Routing sending " + sendingMessage);
                                    d.BeginInvoke(sendingMessage, null, null);
                                    log(en, string.Format("[Filter][TOTAL] routed to site {0}", site.name));
                                }
                                else if (_orderingPolicy == OrderingPolicy.fifo)
                                {
                                    d.BeginInvoke(sendingMessage, null, null);
                                    log(en, string.Format("[Filter][FIFO] routed to site {0}", site.name));
                                }
                                else
                                {
                                    d.BeginInvoke(sendingMessage, null, null);
                                    log(en, string.Format("[FILTER][NO ORDER] routed to {0}", site.name));
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
            log(en, "Routing: received " + receivedMessage);
            log(en, "Routing sending " + sendingMessage);
            if (_routingPolicy == RoutingPolicy.flooding)
            {
                flooding(en, receivedMessage, sendingMessage);
            }
            else // routing policy is filtering
            {
                filter(en, receivedMessage, sendingMessage);
            }
        }

        private void flooding(PublishMessage msg) { }



        public string getServiceName()
        {
            return _serviceName;
        }

        public string getProcessName()
        {
            return _processName;
        }
        public void setSeqNumber(int s)
        {
            _sequencerSeqnum = s;
        }

        public void setMySite(Site s)
        {
            mySite = s;
        }
    }
}
