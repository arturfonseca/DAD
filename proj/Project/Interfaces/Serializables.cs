using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DADInterfaces
{
    [Serializable]
    public class TOUpdate
    {
        public string originSite;
        public string topic;
        public int seqnum;
        public override string ToString()
        {
            return string.Format("[TOUpdate] originsite:{0} topic:{1} seqnum:{2}",originSite,topic,seqnum);
        }
    }
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
        public override string ToString()
        {
            return string.Format("[Site] name:{0} #brokers:{1}",name,brokers.Count);
        }
    }

    [Serializable]
    public class TOSeqnumRequest
    {
        public string sequencerURI;
        public int seqnum;
        public override string ToString()
        {
            return string.Format("[TOSeqnumRequest] sequencer:{0} seqnum:{1}", sequencerURI,seqnum);
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
        public int eventnum;
        public int originalSeqnum;
        public int seqnum;
        public string topic;
        public string content;
        public string originSite = null;
        //sender URI
        public string publisherURI;     
        

        public PublishMessage() { }
        public PublishMessage(PublishMessage msg, string site)
        {
            publisherName = msg.publisherName;
            originalSeqnum = msg.originalSeqnum;
            publisherURI = msg.publisherURI;          
            seqnum = msg.seqnum;            
            topic = msg.topic;
            content = msg.content;
            originSite = site;
            eventnum = msg.eventnum;
        }

        public override string ToString()
        {
            string origin_site = "none";
            if (this.originSite != null)
                origin_site = this.originSite;
            var fmtstr = "[PublishMessage] publisher:{0} eventnum:{1} originalSeqnum:{2} seqnum:{3} topic:{4} content:{5} originSite:{6}";
            return String.Format(fmtstr,publisherName,eventnum,originalSeqnum,seqnum,topic,"<content>",originSite);
        }
    }
}
