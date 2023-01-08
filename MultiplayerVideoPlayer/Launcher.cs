using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using System.Threading;

namespace MultiplayerVideoPlayer
{
    public partial class Launcher : Form
    {
        private string Link => textBox1.Text;
        private string Port => textBox3.Text;
        private string IP => textBox4.Text;
        private string UpdateCommand = "-update";
        private string[] Args;
        private string Filter = "Video Files (*.mkv, *.mp4, *.webm,)|*.mkv;*.mp4;*.webm";

        public Launcher()
        {
            InitializeComponent();
            string clipBoard = Clipboard.GetText();
            if(clipBoard != null && clipBoard.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                textBox1.Text = clipBoard;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Args = new string[2] { Link, Port };
            Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Args = new string[3] { Link, Port, IP };
            Close();
        }      

        private void button3_Click(object sender, EventArgs e)
        {
            Args = new string[1] { UpdateCommand };
            Close();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            openFileDialog1.InitialDirectory = Application.StartupPath;
            openFileDialog1.Filter = Filter;
            openFileDialog1.ShowDialog();
            string filename;
            if (openFileDialog1.FileName != "openFileDialog1")
                filename = openFileDialog1.FileName;
            else
                filename = "";

            textBox1.Text = filename;
        }

        public string[] HandleArgs() => Args;

        
    }
}
