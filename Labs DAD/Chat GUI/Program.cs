using Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApplication1
{
    delegate void recebeMsgDelegate(string a, string b);

    public class ChatClientRemote : MarshalByRefObject, IChatClientRemote
    {
        private Form2 _form;
        public ChatClientRemote(Form2 form)
        {
            _form = form;
        }

        public void recebeMsg(string nick, string msg)
        {
            _form.Invoke(new recebeMsgDelegate(_form.recebeMsg), new object[] { nick, msg });
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
            Application.Run(new Form2());
        }
    }
}
