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

namespace BrokerConsole
{
	class BrokerRemote : MarshalByRefObject, Broker
	{
		private PuppetMaster _pm;
		// personal information
		private string _name;
		private string _site;
		private string _uri;
		private bool _isRoot;
		// site information
		private List<Publisher> _publishers = new List<Publisher>();


		// Subscribers
		private List<Subscriber> _subscribers = new List<Subscriber>();
		// uri to subscriber
		private Dictionary<string, Subscriber> _uriToSubs = new Dictionary<string, Subscriber>();

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



		private Site _parentSite;

		private OrderingPolicy _orderingPolicy;
		private RoutingPolicy _routingPolicy;

		public BrokerRemote(PuppetMaster pm, string uri, string name, string site)
		{
			_uri = uri;
			_name = name;
			_pm = pm;
			_site = site;
			_orderingPolicy = OrderingPolicy.fifo;
			_routingPolicy = RoutingPolicy.flooding;
		}

		public override object InitializeLifetimeService()
		{
			return null;
		}

		static void Main(string[] args)
		{
			Console.WriteLine("Started Broker, pid=\"{0}\"", Process.GetCurrentProcess().Id);
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
			string uri = string.Format("{0}/{1}", channelURI, name);
			BrokerRemote broker = new BrokerRemote(pm, uri, name, site);
			//we need to register each remote object
			ObjRef o = RemotingServices.Marshal(broker, name, typeof(Broker));
			Console.WriteLine("Created Broker at \"{0}\"", broker.getURI());

			//now that broker is created and marshalled
			//send remote to puppetMaster which is Monitor.waiting for the remote  
			pm.registerBroker(broker);
			Console.WriteLine("Just registered at puppetMaster");
			Console.WriteLine("Press key to leave");
			Console.Read();
		}

		public void setOrderingPolicy(OrderingPolicy p)
		{
			_orderingPolicy = p;
		}
		public void setRoutingPolicy(RoutingPolicy p)
		{
			_routingPolicy = p;
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
			throw new NotImplementedException();
		}

		public void freeze()
		{
			throw new NotImplementedException();
		}
		public string status()
		{
			throw new NotImplementedException();
		}

		public void unfreeze()
		{
			throw new NotImplementedException();
		}

		// *end* Puppet Master functions

		/*
		 *  Business logic
		 */

		private bool isDuplicate(SubscribeMessage msg)
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
			return false;
		}

		void log(string e)
		{
			// TODO use assynchronous call
			_pm.reportEvent(getURI(), e);
			Console.WriteLine(e);
		}

		public void subscribe(SubscribeMessage msg)
		{
			log(string.Format("[Subscribe] Received event '{0}'", msg));
			if (isDuplicate(msg)) return;
			// should we have FIFO order here?

			if (!_topicSubscribers.ContainsKey(msg.topic))
			{
				_topicSubscribers.Add(msg.topic, new List<string>());
			}
			_topicSubscribers[msg.topic].Add(msg.uri);
			PropagatedSubcribeMessage pmsg = new PropagatedSubcribeMessage(msg, _site);
			// propagate subscribe only to parent, taking advantage of tree strucure
			if (_parentSite != null)
			{
				foreach (Broker b in _parentSite.brokers)
				{
                    //TODO assyncronous
                    log(string.Format("[subscribe] senting '{0}' to parent site '{1}'",pmsg,_parentSite.name));
					b.propagateSubscribe(pmsg);
				}
			}
		}

        public void propagateSubscribe(PropagatedSubcribeMessage msg)
        {
            log(string.Format("[propagateSubscribe] Received event '{0}'", msg));
            // TODO deal with duplicate messages. using which seqnum?...
            if (!_topicSites.ContainsKey(msg.topic))
            {
                _topicSites.Add(msg.topic, new List<string>());
            }
            _topicSites[msg.topic].Add(msg.interested_site);
            msg.interested_site = _site;
            if(_parentSite != null)
            {
                foreach(var b in _parentSite.brokers)
                {
                    log(string.Format("[subscribe] senting '{0}' to parent site '{1}'", msg, _parentSite.name));
                    //TODO assync
                    b.propagateSubscribe(msg);
                }
            }
        }

        public void unsubscribe(UnsubscribeMessage msg)
		{
			log(string.Format("[Unsubscribe] Received event '{0}'", msg));
			if (isDuplicate(msg)) return;
			// should we have FIFO order here?

			if (_topicSubscribers.ContainsKey(msg.topic))
			{
				_topicSubscribers[msg.topic].Remove(msg.uri);
				if (_topicSubscribers[msg.topic].Count == 0)
				{
					_topicSubscribers.Remove(msg.topic);
				}
			}
			PropagatedUnsubscribeMessage pmsg = new PropagatedUnsubscribeMessage(msg, _site);
			// propagate unsubscribe only to parent, taking advantage of tree strucure
			foreach (Broker b in _parentSite.brokers)
			{
                log(string.Format("[subscribe] senting '{0}' to parent site '{1}'", pmsg, _parentSite.name));
                // TODO assyncronous
                b.propagateUnsubscribe(pmsg);
			}
		}

        public void propagateUnsubscribe(PropagatedUnsubscribeMessage msg)
        {
            log(string.Format("[propagateUnsubscribe] Received event '{0}'", msg));
            // TODO deal with duplicate messages. using which seqnum?...
            if (_topicSites.ContainsKey(msg.topic))
            {
                _topicSites[msg.topic].Remove(msg.interested_site);
                if (_topicSubscribers[msg.topic].Count == 0)
                {
                    _topicSubscribers.Remove(msg.topic);
                }
            }          
            msg.interested_site = _site;
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

        private void deliver(PublishMessage msg)
		{
			// send to current site subscribers
			// TODO suppot * wildcard on topic
			if (_topicSubscribers.ContainsKey(msg.topic))
			{
				foreach (string uri in _topicSubscribers[msg.topic])
				{
					Subscriber s = _uriToSubs[uri];
                    // TODO assync
                    log(string.Format("[Deliver] sent event '{0}' to '{1}'", msg, uri));
                    s.receive(msg.topic, msg.content);
					
				}
			}
		}

		

		public void publish(PublishMessage msg)
		{
			// FLOODING implementation
			// TODO discart if duplicate message
			// TODO make all calls assyncs

			log(string.Format("[Publish] Received event '{0}'", msg));
			deliver(msg);
			routing(msg);
		}
		public void propagatePublish(PropagatedPublishMessage msg)
		{
			log(string.Format("[propagatePublish] Received event '{0}'", msg));
			deliver(msg);
			propagatingRouting(msg);
		}



		private void routing(PublishMessage msg)
		{
			PropagatedPublishMessage pmsg = new PropagatedPublishMessage(msg, _site);

			if (_routingPolicy == RoutingPolicy.flooding)
			{
				foreach (var s in _childSites)
				{
					foreach (var b in s.brokers)
					{
						// TODO broker.getURI() is slow, we should use a cache
						log(string.Format("[Routing] flooding. sent event '{0}' to '{1}'", msg, b.getURI()));
						// TODO assync
						b.propagatePublish(pmsg);
					}
				}
			}
			else // routing policy is filtering
			{
				foreach (var pair in _topicSites)
				{
					foreach (var site in pair.Value)
					{
						Site s = _nameToSite[site];
						foreach (var broker in s.brokers)
						{
							// TODO broker.getURI() is slow, we should use a cache
							log(string.Format("[Routing] filtering. sent event '{0}' to '{1}'", msg, broker.getURI()));
							// TODO assync
							broker.propagatePublish(pmsg);

						}
					}
				}
			}

			// send to parent site brokers
			// always send publish to parent, doesnt matter if interested in topic
			if (_parentSite != null)
			{
				foreach (Broker b in _parentSite.brokers)
				{
					// TODO broker.getURI() is slow, we should use a cache
					log(string.Format("[Routing] sent event '{0}' to parent broker '{1}'", msg, b.getURI()));
					// TODO assync
					b.propagatePublish(pmsg);
				}
			}
		}

		private void propagatingRouting(PropagatedPublishMessage msg)
		{
			string origin_site = msg.origin_site;
			msg.origin_site = _site;
			if (_routingPolicy == RoutingPolicy.flooding)
			{
				foreach (var s in _childSites)
				{
					if (s.name != origin_site)
					{
						foreach (var b in s.brokers)
						{
							// TODO broker.getURI() is slow, we should use a cache
							log(string.Format("[propagatingRouting] flooding. sent event '{0}' to '{1}'", msg, b.getURI()));
							// TODO assync
							b.propagatePublish(msg);
						}
					}

				}
			}
			else // routing policy is filtering
			{
				foreach (var pair in _topicSites)
				{
					foreach (var site in pair.Value)
					{
						if (site != origin_site) // we dont send msg back to who sent it to us
						{
							Site s = _nameToSite[site];
							foreach (var broker in s.brokers)
							{
								// TODO broker.getURI() is slow, we should use a cache
								log(string.Format("[propagatingRouting] filtering. sent event '{0}' to '{1}'", msg, broker.getURI()));
								// TODO assync
								broker.propagatePublish(msg);

							}
						}
					}
				}
			}

			// send to parent site brokers
			// always send publish to parent, doesnt matter if interested in topic
			if (_parentSite != null && _parentSite.name != origin_site)
			{
				foreach (Broker b in _parentSite.brokers)
				{
					// TODO broker.getURI() is slow, we should use a cache
					log(string.Format("[Routing] sent event '{0}' to parent broker '{1}'", msg, b.getURI()));
					// TODO assync
					b.propagatePublish(msg);
				}
			}
		}
	}
}
