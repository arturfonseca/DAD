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
        //sender URI
        public string sender;
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
    }

    public interface Broker: Node
    {
        void setParent(Site parent_site);
        void setChildren(List<Site> child_sites);
        void setPublishers(List<Publisher> site_publishers);
        void setSubscriber(List<Subscriber> site_subscribers);

        void subscribe(SubscribeMessage msg);
        void unsubscribe(SubscribeMessage msg);
        void publish(PublishMessage msg);        
    }
    public interface Publisher: Node
    {
        void setSiteBroker(Broker site_broker);
        void subscribe(string topic);
        void unsubscribe(string topic);
    }
    public interface Subscriber: Node
    {
        void setSiteBroker(Broker site_broker);
        void publish(string topic, string msg);
    }

    public interface PuppetMaster
    {
        List<Broker> getBrokers();
        List<Subscriber> getSubscribers();
        List<Publisher> getPublishers();

        Broker createBroker(string name);
        Publisher createPublisher(string name);
        Subscriber createSubscriber(string name);

        // When a PuppetMaster createX it returns a remoteObjectX
        // But the only way to get a remoteObjectX is to wait
        // for the created process to call remoteObjectX on the PuppetMasterRemote
        // which the process activated using the URI given in main arguments
        void registerBroker(Broker b);
        void registerPublisher(Publisher p);
        void registerSubscriber(Subscriber s);
    }

    public static class Utility
    {
        public static string setupChannel()
        {
            //create process channel
            BinaryServerFormatterSinkProvider ssp = new BinaryServerFormatterSinkProvider();
            BinaryClientFormatterSinkProvider csp = new BinaryClientFormatterSinkProvider();
            ssp.TypeFilterLevel = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;
            IDictionary props = new Hashtable();
            props["port"] = 0;
            TcpChannel channel = new TcpChannel(props, csp, ssp);
            ChannelServices.RegisterChannel(channel, true);

            // print uris
            ChannelDataStore cds = (ChannelDataStore)channel.ChannelData;
            string channelURI = cds.ChannelUris[0];
            Console.WriteLine("Opened remoting channel at \"{0}\"", channelURI);
            return channelURI;
        }
    }

}
