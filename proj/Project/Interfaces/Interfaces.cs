using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DADInterfaces
{
    public interface Node
    {
        string getURI();
        string status();
        void setSiteBrokers(List<string> uris);

        void crash();
        void freeze();
        void unfreeze();
        
    }
    public interface Broker: Node
    {
        void setParent(string uri);
        void setChildren(string child_site, List<string> uri);
        string getName();
    }
    public interface Publisher: Node
    {
        void setSiteBroker(string uri);
        void subscribe(string topic);
        void unsubscribe(string topic);
    }
    public interface Subscriber: Node
    {
        void setSiteBroker(string uri);
        void publish(string topic, string msg);
    }

    public interface PuppetMaster
    {
        List<Broker> getBrokers();
        List<Subscriber> getSubscribers();
        List<Publisher> getPublishers();

        Broker createBroker(string name,string site,string parent_site,List<string> children_sites);
        Publisher createPublisher(string name, string site,string uri);
        Subscriber createSubscriber(string name, string site, string uri);

        void registerBroker(Broker b);
        void registerPublisher(Publisher p);
        void registerSubscriber(Subscriber s);
    }

}
