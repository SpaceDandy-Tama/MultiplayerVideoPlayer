using LibVLCSharp.Shared;
using MultiplayerVideoPlayer.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace MultiplayerVideoPlayer
{
    public partial class Launcher : Form
    {
        private string Link => textBox1.Text;
        private string Port => textBox3.Text;
        private string IP => comboBox2.Text;
        private string Quality => comboBox1.Text;
        private bool Save2Temp => checkBox1.Checked;
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

            this.Icon = Program.Icon;

            HandleComboBox();

            if (Program.AppSetting.LastPath == string.Empty || !Directory.Exists(Program.AppSetting.LastPath))
            {
                Program.AppSetting.LastPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                Program.AppSetting.Save();
            }

            checkBox1.Enabled = Program.AppSetting.TempDir != null;
        }

        //host
        private void button1_Click(object sender, EventArgs e)
        {
            Args = new string[3] { Quality, Link, Port };
            Close();
        }

        //join
        private void button2_Click(object sender, EventArgs e)
        {
            Args = new string[4] { Quality, Link, Port, IP };
            if(Program.AppSetting.PreviousHosts == null)
                Program.AppSetting.PreviousHosts = new List<string>();

            if (!Program.AppSetting.PreviousHosts.Contains(IP.ToString()))
            {
                DialogResult result = MessageBox.Show("This is your first time connecting to this IP! Do you want to save it for later?", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    Program.AppSetting.PreviousHosts.Add(IP.ToString());
                    Program.AppSetting.Save();
                }                
            }                
                            
            Close();
        }

        //download
        private void button5_Click(object sender, EventArgs e)
        {
            Program.Save2Temp = Save2Temp;
            Args = new string[2] { Quality, Link };
            Close();
        }

        //update
        private void button3_Click(object sender, EventArgs e)
        {
            Args = new string[1] { UpdateCommand };
            Close();
        }

        //select file
        private void button4_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = Filter;
            openFileDialog1.InitialDirectory = Program.AppSetting.LastPath;
            openFileDialog1.ShowDialog();
            string filename;
            if (openFileDialog1.FileName != "openFileDialog1")
            {
                filename = openFileDialog1.FileName;
                Program.AppSetting.LastPath = Path.GetDirectoryName(filename);
                Program.AppSetting.Save();
            }                
            else
                filename = "";

            textBox1.Text = filename;              
            
        }

        private void HandleComboBox()
        {
            if(Program.AppSetting.PreviousHosts != null && Program.AppSetting.PreviousHosts.Count > 0)
            {
                comboBox2.Items.AddRange(Program.AppSetting.PreviousHosts.ToArray());
                comboBox2.Text = comboBox2.Items[comboBox2.Items.Count-1].ToString();
            }
                
        }

        public string[] HandleArgs() => Args;

    }
}
