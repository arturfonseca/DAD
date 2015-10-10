using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Interfaces;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

namespace RemotingSample
{
    public delegate void recebeMsgDelegate(string nick, string msg);

    public class ChatClientRemote :MarshalByRefObject, IClient
    {
        
        private Form1 _form;
        
        public ChatClientRemote(Form1 form1)
        {
            _form = form1;
        }

        public void recebeMsg(string nick, string msg)
        {
            
            _form.BeginInvoke(new recebeMsgDelegate(_form.recebeMsg),new object[]{nick, msg});
        }

    }
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
