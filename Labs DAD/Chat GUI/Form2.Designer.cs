namespace WindowsFormsApplication1
{
    partial class Form2
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panel1 = new System.Windows.Forms.Panel();
            this.button1 = new System.Windows.Forms.Button();
            this._port = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this._ip = new System.Windows.Forms.TextBox();
            this.panel2 = new System.Windows.Forms.Panel();
            this._log = new System.Windows.Forms.TextBox();
            this._send_input = new System.Windows.Forms.TextBox();
            this._nick = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this._nick);
            this.panel1.Controls.Add(this.label3);
            this.panel1.Controls.Add(this.button1);
            this.panel1.Controls.Add(this._port);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this._ip);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(659, 27);
            this.panel1.TabIndex = 0;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(342, 1);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 4;
            this.button1.Text = "connect";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.connect_click);
            // 
            // _port
            // 
            this._port.Location = new System.Drawing.Point(296, 4);
            this._port.Name = "_port";
            this._port.Size = new System.Drawing.Size(40, 20);
            this._port.TabIndex = 3;
            this._port.Text = "yyyyy";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.label2.Location = new System.Drawing.Point(262, 7);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(28, 15);
            this.label2.TabIndex = 2;
            this.label2.Text = "Port";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.label1.Location = new System.Drawing.Point(144, 7);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(19, 15);
            this.label1.TabIndex = 1;
            this.label1.Text = "IP";
            this.label1.Click += new System.EventHandler(this.label1_Click);
            // 
            // _ip
            // 
            this._ip.Location = new System.Drawing.Point(169, 4);
            this._ip.Name = "_ip";
            this._ip.Size = new System.Drawing.Size(87, 20);
            this._ip.TabIndex = 0;
            this._ip.Text = "xxx.xxx.xxx.xxx";
            this._ip.TextChanged += new System.EventHandler(this.textBox1_TextChanged);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this._log);
            this.panel2.Controls.Add(this._send_input);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(0, 27);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(659, 253);
            this.panel2.TabIndex = 1;
            // 
            // _log
            // 
            this._log.Cursor = System.Windows.Forms.Cursors.IBeam;
            this._log.Dock = System.Windows.Forms.DockStyle.Fill;
            this._log.Location = new System.Drawing.Point(0, 0);
            this._log.Multiline = true;
            this._log.Name = "_log";
            this._log.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this._log.Size = new System.Drawing.Size(659, 233);
            this._log.TabIndex = 2;
            this._log.TextChanged += new System.EventHandler(this.textBox3_TextChanged);
            // 
            // _send_input
            // 
            this._send_input.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._send_input.Location = new System.Drawing.Point(0, 233);
            this._send_input.Name = "_send_input";
            this._send_input.Size = new System.Drawing.Size(659, 20);
            this._send_input.TabIndex = 1;
            this._send_input.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.send_click);
            // 
            // _nick
            // 
            this._nick.Location = new System.Drawing.Point(38, 4);
            this._nick.MaxLength = 10;
            this._nick.Name = "_nick";
            this._nick.Size = new System.Drawing.Size(100, 20);
            this._nick.TabIndex = 5;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 7);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(29, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Nick";
            // 
            // Form2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(659, 280);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Name = "Form2";
            this.Text = "Form2";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox _ip;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TextBox _port;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.TextBox _send_input;
        private System.Windows.Forms.TextBox _log;
        private System.Windows.Forms.TextBox _nick;
        private System.Windows.Forms.Label label3;
    }
}