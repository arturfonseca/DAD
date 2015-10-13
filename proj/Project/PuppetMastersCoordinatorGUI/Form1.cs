using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PuppetMastersCoordinatorGUI
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string[] lines = System.IO.File.ReadAllLines(@"C:\Users\Artur Fonseca\Desktop\testinput.txt");

            
            foreach (string line in lines)
            {
               if (line.Contains("ORDERING"))
                {
                    line.Split(' ');
                    MessageBox.Show(line[1]);

                }
               
            }
           
        }
    }
}
