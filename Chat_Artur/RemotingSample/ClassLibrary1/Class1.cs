using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    public interface IServer
    {
        void enviaMsg(string msg);
        void registar(string port);
    }

    public interface IClient
    {
       void recebeMsg(string nick, string msg);
    }

   
}
