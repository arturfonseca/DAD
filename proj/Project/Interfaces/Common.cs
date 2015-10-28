using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
using System.Threading.Tasks;

namespace DADInterfaces
{
    public enum RoutingPolicy { flooding, filter};
    public enum OrderingPolicy { no, fifo, total};

    // A Node can be a Broker, Publisher or Subscriber
    public interface Node
    {
        string getURI();
        string status();
        string getName();
        string getSite();

        void crash();
        void freeze();
        void unfreeze();
    }

    [Serializable]
    public class Site
    {
        public string name;
        public List<Broker> brokers;
    }
    [Serializable]
    public class SubscribeMessage
    {
        public string uri;
        public Subscriber sub;
        // used to avoid duplicates
        public int seqnum;
        public string topic;
        public override string ToString()
        {
            return string.Format("[SubscribeMessage] uri='{0}' seqnum='{1}' topic='{2}'",uri,seqnum,topic);
        }
    }
    [Serializable]
    public class UnsubscribeMessage : SubscribeMessage {
        public override string ToString()
        {
            return string.Format("[SubscribeMessage] uri='{0}' seqnum='{1}' topic='{2}'", uri, seqnum, topic);
        }
    }

    [Serializable]
    // This message is sent from a Broker to a parent broker
    public class PropagatedSubcribeMessage
    {
        // used to avoid duplicates
        public int seqnum;
        public string topic;
        public string uri;
        // name of the interested child site
        public string interested_site;
        public PropagatedSubcribeMessage(SubscribeMessage msg, string site)
        {
            seqnum = msg.seqnum;
            topic = msg.topic;
            uri = msg.uri;
            interested_site = site;
        }
        public override string ToString()
        {
            return string.Format("[PropagatedSubcribeMessage] subscriber_uri:'{0}' topic='{1}' interested_site='{2}' seqnum='{3}'", uri, topic, interested_site, seqnum);
        }
    }
    [Serializable]
    public class PropagatedUnsubscribeMessage : PropagatedSubcribeMessage {
        public PropagatedUnsubscribeMessage(UnsubscribeMessage msg, string site): base(msg,site)
        {           
        }

        public override string ToString()
        {
            return string.Format("[PropagatedUnsubscribeMessage] seqnum='{0}' topic='{1}' interested_site='{2}'", seqnum, topic, interested_site);
        }
    } 

    [Serializable]
    public class PublishMessage
    {
        //sender URI
        public string senderURI;
        // used to avoid duplicates
        public int topic_seqnum;
        public int total_seqnum;
        public string topic;
        public string content;

        public override string ToString()
        {
            return string.Format("[PublishMessage] publisher_uri:'{0}' topic:'{1}' content :'{2}' total_seqnum :'{3}' topic_seqnum :'{4}'",
                senderURI, topic, content, topic_seqnum, total_seqnum);
        }
    }
    [Serializable]
    public class PropagatedPublishMessage: PublishMessage
    {
        public string origin_site;
        public PropagatedPublishMessage(PublishMessage msg, string site)
        {
            senderURI = msg.senderURI;
            topic_seqnum = msg.topic_seqnum;
            total_seqnum = msg.total_seqnum;
            topic = msg.topic;
            content = msg.content;
            origin_site = site;
        }
        public override string ToString()
        {
            return String.Format(
                "sender:{0}, topic_seqnum:'{1}', total_seqnum:'{2}', topic:'{3}', content:'{4}', source_site:'{5}'",
                senderURI, topic_seqnum, total_seqnum, topic, content, origin_site);
        }
    }

    public delegate void subscriberDelegate(SubscribeMessage msg);
    public delegate void unsubscribeDelegate(SubscribeMessage msg);
    public delegate void publishDelegate(PublishMessage msg);
    public interface Broker: Node
    {
        void setIsRoot();
        bool getIsRoot();
        void setOrderingPolicy(OrderingPolicy p);
        void setRoutingPolicy(RoutingPolicy p);

        void setParent(Site parent_site);
        void setChildren(List<Site> child_sites);
        void setPublishers(List<Publisher> site_publishers);
        void setSubscribers(List<Subscriber> site_subscribers);

        // methods called by Subscriber
        void subscribe(SubscribeMessage msg);
        void unsubscribe(UnsubscribeMessage msg);
        // method called by Publisher
        void publish(PublishMessage msg);
        // methods called by other Brokers
        void propagateSubscribe(PropagatedSubcribeMessage msg);
        void propagateUnsubscribe(PropagatedUnsubscribeMessage msg);
        void propagatePublish(PropagatedPublishMessage msg);
            
    }
    public interface Publisher: Node
    {
        void setSiteBroker(Broker site_broker);
        void publish(string topic, string msg, int quantity, int interval);
        
    }
    public interface Subscriber: Node
    {
        void setSiteBroker(Broker site_broker);
        void subscribe(string topic);
        void unsubscribe(string topic);
        void receive(string topic, string content);
    }

    public interface ICoordinator
    {
        void reportEvent(string type, string uri1, string uri2, string topic, string seqnum);
    }

    public interface PuppetMaster
    {
        List<Broker> getBrokers();
        List<Subscriber> getSubscribers();
        List<Publisher> getPublishers();

        Broker createBroker(string name,string site,int port);
        Publisher createPublisher(string name,string site,int port);
        Subscriber createSubscriber(string name,string site,int port);

        // When a PuppetMaster createX it returns a remoteObjectX
        // But the only way to get a remoteObjectX is to wait
        // for the created process to call remoteObjectX on the PuppetMasterRemote
        // which the process activated using the URI given in main arguments
        void registerBroker(Broker b);
        void registerPublisher(Publisher p);
        void registerSubscriber(Subscriber s);
        string status();

        // used by all processes created by the puppet master to report events
        // type = {PubEvent, BroEvent, SubEvent}
        void reportEvent(string type, string uri1, string uri2, string topic, string seqnum);
        void reportEvent(string a, String b);
    }

    public static class Utility
    {
        public static string setupChannel(int port)
        {
            //create process channel
            BinaryServerFormatterSinkProvider ssp = new BinaryServerFormatterSinkProvider();
            ssp.TypeFilterLevel = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;
            IDictionary props = new Hashtable();
            props["port"] = port;
            TcpChannel channel = new TcpChannel(props, null, ssp);
            ChannelServices.RegisterChannel(channel, true);

            // print uris
            ChannelDataStore cds = (ChannelDataStore)channel.ChannelData;
            string channelURI = cds.ChannelUris[0];
            Console.WriteLine("Opened remoting channel at \"{0}\"", channelURI);
            return channelURI;
        }
    }

}
