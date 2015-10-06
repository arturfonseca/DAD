using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    public interface IChatClientRemote
    {
        void recebeMsg(string nick, string msg);
    }

    public interface IChatServerRemote
    {
        IChatSessionRemote regista(string nick, IChatClientRemote myremote);        
    }

    public interface IChatSessionRemote
    {
        void enviaMsg(string msg);
        string getNick();
    }
}
