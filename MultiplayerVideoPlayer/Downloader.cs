using MultiplayerVideoPlayer.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MultiplayerVideoPlayer
{
    public partial class Downloader: Form
    {
        public Downloader()
        {
            InitializeComponent();

            progressBar1.Value = 0;

            this.Icon = Program.Icon;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            int percent = (int)((TcpFileReceiver.TotalRead * 100L) / TcpFileReceiver.FileLength);
            progressBar1.Value = Math.Min(percent, 100);

            if (TcpFileReceiver.Done)
                Close();
        }

        //play
        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Yes;
        }

        //test
        private void button2_Click(object sender, EventArgs e)
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo(Path.GetFullPath(TcpFileReceiver.SavePath))
            {
                UseShellExecute = true // Required to open with default app
            };
            process.Start();
        }

        private void Downloader_FormClosed(object sender, FormClosedEventArgs e)
        {
            if(e.CloseReason == CloseReason.UserClosing)
                DialogResult = DialogResult.OK;
        }
    }
}
