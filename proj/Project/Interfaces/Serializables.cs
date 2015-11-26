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
        public string interested_site;
        public override string ToString()
        {
            return string.Format("[SubscribeMessage] subscriber_uri:'{0}' topic='{1}' interested_site='{2}' seqnum='{3}'", uri, topic, interested_site, seqnum);
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
    public class PropagatedUnsubscribeMessage : SubscribeMessage
    {
       
        public override string ToString()
        {
            return string.Format("[PropagatedUnsubscribeMessage] seqnum='{0}' topic='{1}' interested_site='{2}'", seqnum, topic, interested_site);
        }
    }

    [Serializable]
    public class PublishMessage
    {
        //sender URI
        public int origin_seqnum;
        public string publisherURI;
        public int seqnum;
        public string topic;
        public string content;
        public string origin_site;

        public PublishMessage() { }
        public PublishMessage(PublishMessage msg, string site)
        {
            publisherURI = msg.publisherURI;
            seqnum = msg.seqnum;
            origin_seqnum = msg.origin_seqnum;
            topic = msg.topic;
            content = msg.content;
            origin_site = site;
        }

        public override string ToString()
        {
            string origin_site = "none";
            if (this.origin_site != null)
                origin_site = this.origin_site;
            return String.Format(

                "[PublishMessage] publisher_uri:'{0}', source_site:'{1}', topic:'{2}' content:'{3}', seqnum:'{4}', origin_seqnum:'{5}'",
                publisherURI, origin_site,topic,"",seqnum,origin_seqnum );
        }
    }
}
