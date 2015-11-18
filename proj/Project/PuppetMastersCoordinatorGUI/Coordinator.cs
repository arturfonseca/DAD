using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DADInterfaces;

namespace PuppetMastersCoordinatorGUI
{
    public delegate void recebeMsgDelegate(string type, string uri1, string uri2, string topic, int seqnum);
    public class CoordinatorRem : MarshalByRefObject, ICoordinator
    {
        Form1 _form;
        public CoordinatorRem(Form1 form1)
        {
            _form = form1;
        }


        public void reportEvent(string type, string uri1, string uri2, string topic, int seqnum)
        {
            _form.BeginInvoke(new recebeMsgDelegate(_form.reportEvent), new object[] { type,uri1,uri2,topic,seqnum });
        }
    }




    static class Coordinator
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