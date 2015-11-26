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

    [Serializable]
    public class TOSeqnumRequest
    {
        public string sequencerURI;
        public int seqnum;
        public override string ToString()
        {
            return string.Format("[TOSeqnumRequest] sequencer:'{0}' seqnum:'{1}' topic:'{2}'", sequencerURI,seqnum);
        }
    }

    /* Messages */
    [Serializable]
    public class SubscribeMessage
    {
        public string uri = null;
        public Subscriber sub = null;
        // used to avoid duplicates
        public int seqnum = 0;
        public string topic = null;
        public string interested_site=null;
        public override string ToString()
        {
            return string.Format("[SubscribeMessage] uri:'{0}' topic='{1}' interested_site='{2}' seqnum='{3}'", uri, topic, interested_site, seqnum);
        }
    }
    [Serializable]
    public class UnsubscribeMessage : SubscribeMessage
    {
        public override string ToString()
        {
            return string.Format("[SubscribeMessage] uri:'{0}' seqnum:'{1}' topic:'{2}'", uri, seqnum, topic);
        }
    }

    [Serializable]
    public class PropagatedUnsubscribeMessage : SubscribeMessage
    {
       
        public override string ToString()
        {
            return string.Format("[PropagatedUnsubscribeMessage] seqnum:'{0}' topic:'{1}' interested_site:'{2}'", seqnum, topic, interested_site);
        }
    }

    [Serializable]
    public class PublishMessage
    {
        public string publisherName = "";
        public int originalSeqnum;
        //sender URI
        public string publisherURI;
        public int seqnum;
        public string topic;
        public string content;
        public string originSite = null;
        public int eventnum;

        public PublishMessage() { }
        public PublishMessage(PublishMessage msg, string site)
        {
            publisherURI = msg.publisherURI;
            seqnum = msg.seqnum;
            originalSeqnum = msg.originalSeqnum;
            topic = msg.topic;
            content = msg.content;
            originSite = site;
        }

        public override string ToString()
        {
            string origin_site = "none";
            if (this.originSite != null)
                origin_site = this.originSite;
            return String.Format(

                "[PublishMessage] publisher:{0} source_site:{1} topic:{2} content:{3} seqnum:{4} originalSeqnum:{5}",
                publisherName, origin_site,topic,"",seqnum,originalSeqnum );
        }
    }
}
