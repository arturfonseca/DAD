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
        void regista(string nick, IChatClientRemote myremote);
        void enviaMsg(string nick, string msg);
    }
}
