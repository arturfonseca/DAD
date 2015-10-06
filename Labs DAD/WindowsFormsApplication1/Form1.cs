using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Interfaces;
namespace WindowsFormsApplication1
{
    delegate void recebeMsgDelegate(string a, string b);

    public class ChatClientRemote : MarshalByRefObject, IChatClientRemote
    {
        private Form1 _form;
        public ChatClientRemote(Form1 form)
        {
            _form = form;
        }

        public void recebeMsg(string nick, string msg)
        {
            _form.Invoke(new recebeMsgDelegate(_form.recebeMsg), new object[] { nick, msg });
        }
    }


    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox3_TextChanged_1(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        public void recebeMsg(string nick, string msg)
        {

        }
    }
}
