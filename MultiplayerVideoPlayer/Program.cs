using System;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;

namespace MultiplayerVideoPlayer
{
    internal static class Program
    {
        public static Form1 Form;
        public static NetworkManager NetworkManager;

        public const int DownloadTimeOutSeconds = 100;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static async Task Main(string[] args)
        {
            string filePath = null;
            string hostName = null;
            int port = -1;

            bool quit = false;

            if (args == null || args.Length == 0)
                quit = true;
            else
            {
                if (args[0].StartsWith("http", StringComparison.OrdinalIgnoreCase) == false && File.Exists(args[0]) == false)
                    quit = true;
                else
                    filePath = args[0];

                if (args.Length < 2 || !int.TryParse(args[1], out port))
                    port = 7243;

                string hostOnlyDir = Path.Combine(Application.StartupPath, "hostonly");
                string hostNameDir = "hostname";
                string[] fileNames = Directory.GetFiles(Application.StartupPath);
                foreach(string fileName in fileNames)
                {
                    if (Path.GetFileName(fileName).StartsWith(hostNameDir))
                    {
                        hostNameDir = Path.Combine(Application.StartupPath, fileName);
                        break;
                    }
                }

                if (File.Exists(hostOnlyDir))
                    hostName = null;
                else if (args.Length > 2)
                    hostName = args[2];
                else if (File.Exists(hostNameDir))
                    hostName = File.ReadAllText(hostNameDir);
            }

            if (quit)
            {
                MessageBox.Show("Nothing to play.", "Wrong Arguments");
                Application.Exit();
                return;
            }

            string titleName = Path.GetFileName(filePath);

            if (filePath.StartsWith("http", StringComparison.OrdinalIgnoreCase) || (filePath.Contains("youtube") || filePath.Contains("youtu.be")))
            {
                string fileName = await DownloadFromYoutube(filePath);
                if (fileName == null)
                    return;

                titleName = filePath;
                filePath = fileName;
            }

            StartNetworkManager(hostName, port);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(Form = new Form1(filePath, titleName));
        }

        private static async Task<string> DownloadFromYoutube(string link)
        {
            if(!File.Exists("yt-dlp.exe") && !File.Exists("ffmpeg.exe"))
            {
                MessageBox.Show("You need yt-dlp.exe and ffmpeg.exe to watch youtube links.");
                return null;
            }

            string arguments = "-f bestvideo*+bestaudio/best --merge-output-format mp4 --write-subs -o temp.mp4 " + link;
            System.Diagnostics.Process.Start(Path.Combine(Application.StartupPath, "yt-dlp.exe"), arguments);

            int seconds = 0;
            while (File.Exists(Path.Combine(Application.StartupPath, "temp.mp4")) == false)
            {
                await Task.Delay(1000);
                seconds++;
                if (seconds > DownloadTimeOutSeconds)
                    return null;
            }
            await Task.Delay(1000);

            return Path.Combine(Application.StartupPath, "temp.mp4");
        }

        public static void RemoveTempFiles()
        {
            string[] fileNames = Directory.GetFiles(Application.StartupPath);
            foreach (string fileName in fileNames)
            {
                if (Path.GetFileName(fileName).StartsWith("temp"))
                    File.Delete(fileName);
            }
        }

        private static async void StartNetworkManager(string hostName, int port)
        {
            while(Form == null)
                await Task.Delay(100);

            NetworkManager = new NetworkManager(hostName, port);
        }
    }
}
