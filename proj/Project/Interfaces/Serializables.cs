using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DADInterfaces
{
    public static class EventType
    {
        public static string PubEvent = "PubEvent";
        public static string BroEvent = "BroEvent";
        public static string SubEvent = "SubEvent";
    }

    [Serializable]
    public class Site
    {
        public string name;
        public List<Broker> brokers;
    }


    /* Messages */
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
            return string.Format("[SubscribeMessage] uri='{0}' seqnum='{1}' topic='{2}'", uri, seqnum, topic);
        }
    }
    [Serializable]
    public class UnsubscribeMessage : SubscribeMessage
    {
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
    public class PropagatedUnsubscribeMessage : PropagatedSubcribeMessage
    {
        public PropagatedUnsubscribeMessage(UnsubscribeMessage msg, string site) : base(msg, site)
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
        public string publisherURI;
        // used to avoid duplicates

        public int total_seqnum;
        public string topic;
        public string content;

        public override string ToString()
        {
            return string.Format("[PublishMessage] publisher_uri:'{0}' topic:'{1}' content :'{2}' total_seqnum :'{3}'",
                publisherURI, topic, content, total_seqnum);
        }
    }
    [Serializable]
    public class PropagatedPublishMessage : PublishMessage
    {
        public string origin_site;
        public PropagatedPublishMessage(PublishMessage msg, string site)
        {
            publisherURI = msg.publisherURI;
            total_seqnum = msg.total_seqnum;
            topic = msg.topic;
            content = msg.content;
            origin_site = site;
        }
        public override string ToString()
        {
            return String.Format(
                "[PropagatedPublishMessage] publisher_uri:'{0}', total_seqnum:'{1}', topic:'{2}' content:'{3}', source_site:'{4}'",
                publisherURI, total_seqnum, topic, content, origin_site);
        }
    }
}
