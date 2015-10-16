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
    
    public interface Node
    {
        string getURI();
        void setURI(string uri);
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
    }
    [Serializable]
    public class UnsubscribeMessage: SubscribeMessage {}

    [Serializable]
    public class PublishMessage
    {
        //sender URI
        public string sender;
        // used to avoid duplicates
        public int seqnum;
        public string topic;
        public string content;
        public string last_broker;
        public override string ToString()
        {
            return String.Format("sender:{0}, seqnum:{1}, topic:{2}, content:{3}, last_broker:{4}", sender, seqnum, topic, content, last_broker);
        }
    }

    public delegate void subscriberDelegate(SubscribeMessage msg);
    public delegate void unsubscribeDelegate(SubscribeMessage msg);
    public delegate void publishDelegate(PublishMessage msg);
    public interface Broker: Node
    {
        void setParent(Site parent_site);
        void setChildren(List<Site> child_sites);
        void setPublishers(List<Publisher> site_publishers);
        void setSubscribers(List<Subscriber> site_subscribers);

        void subscribe(SubscribeMessage msg);
        void unsubscribe(UnsubscribeMessage msg);
        void publish(PublishMessage msg);

        void setIsRoot(bool v);
        bool getIsRoot();
    }
    public interface Publisher: Node
    {
        void setSiteBroker(Broker site_broker);
        void publish(string topic, string msg);
        
    }
    public interface Subscriber: Node
    {
        void setSiteBroker(Broker site_broker);
        void subscribe(string topic);
        void unsubscribe(string topic);
        void receive(string topic, string content);
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

        // used by all processes created by the puppet master to report events
        void reportEvent(string origin_uri, string e);
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
