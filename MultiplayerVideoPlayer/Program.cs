using System;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Net.Http;

namespace MultiplayerVideoPlayer
{
    internal static class Program
    {
        public static MvpMain Form;
        public static NetworkManager NetworkManager;

        public const int DownloadTimeOutSeconds = 60;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static async Task Main(string[] args)
        {
            if(ValidateFiles() == false)
            {
                await Update();
                return;
            }

            string filePath = null;
            string hostName = null;
            int port = -1;

            bool quit = false;

            if (args == null || args.Length == 0)
                quit = true;
            else
            {
                if (args[0].Equals("-update"))
                {
                    await Update();
                    return;
                }
                else if (args[0].Equals("-deleteUpdater"))
                {
                    await Task.Delay(1000);
                    await Update(delete: true);
                    return;
                }
                else if (args[0].StartsWith("http", StringComparison.OrdinalIgnoreCase) == false && File.Exists(args[0]) == false)
                    quit = true;
                else
                    filePath = args[0];

                if (args.Length < 2 || !int.TryParse(args[1], out port))
                    port = 7243;

                string hostOnlyDir = Path.Combine(Application.StartupPath, "hostonly");
                string hostNameDir = "hostname";
                string[] fileNames = Directory.GetFiles(Application.StartupPath);
                foreach (string fileName in fileNames)
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
            Application.Run(Form = new MvpMain(filePath, titleName));
        }

        private static async void StartNetworkManager(string hostName, int port)
        {
            while (Form == null || !Form.MediaPlayer.IsPlaying)
                await Task.Delay(100);

            NetworkManager = new NetworkManager(hostName, port);
        }

        #region Update Related Methods
        private static bool ValidateFiles()
        {
            //Checksum later?
            string[] requiredFiles = new string[]
            {
                "LibVLCSharp.dll",
                "LibVLCSharp.WinForms.dll",
                "LiteNetLib.dll",
                "MultiplayerVideoPlayer.exe",
                "System.Buffers.dll",
                "System.Memory.dll",
                "System.Numerics.Vectors.dll",
                "System.Runtime.CompilerServices.Unsafe.dll",
                "Xamarin.Forms.Core.dll",
                "Xamarin.Forms.Platform.dll",
                "Xamarin.Forms.Xaml.dll"
            };
            string[] requiredDirs = new string[]
            {
                "libvlc",
            };

            foreach(string requiredFile in requiredFiles)
            {
                if (!File.Exists(requiredFile))
                    return false;
            }
            foreach(string requiredDir in requiredDirs)
            {
                if (!Directory.Exists(requiredDir))
                    return false;
            }

            return true;
        }
        private static async Task Update(bool delete = false)
        {
            string url = "https://spacewardgame.com/temp/MVP/MVPUpdater.exe";
            string path = Path.Combine(Application.StartupPath, "MVPUpdater.exe");

            if (File.Exists(path))
                File.Delete(path);

            if (delete)
                return;

            HttpClient httpClient = new HttpClient();

            using (Stream stream = await httpClient.GetStreamAsync(url))
            {
                using (FileStream fileStream = new FileStream(path, FileMode.CreateNew))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }

            System.Diagnostics.Process.Start(path);
        }
        #endregion

        #region Download Related Methods
        private static async Task<string> DownloadFromYoutube(string link)
        {
            if(!File.Exists("yt-dlp.exe") && !File.Exists("ffmpeg.exe"))
            {
                MessageBox.Show("You need yt-dlp.exe and ffmpeg.exe to watch youtube links.");
                return null;
            }

            string tempDir = Path.Combine(Application.StartupPath, "temp");
            if(Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            string arguments = $"-f bestvideo*+bestaudio/best --merge-output-format mp4 --write-subs -P \"{tempDir}\" -o \"%(uploader)s - %(title)s.%(ext)s\" " + link;
            System.Diagnostics.Process.Start(Path.Combine(Application.StartupPath, "yt-dlp.exe"), arguments);

            int seconds = 0;
            string resultFileName = null;
            while (resultFileName == null)
            {
                await Task.Delay(1000);

                seconds++;
                if (seconds > DownloadTimeOutSeconds)
                    return null;

                string[] fileNames = Directory.GetFiles(tempDir);
                foreach(string fileName in fileNames)
                {
                    if (fileName.EndsWith(".mp4"))
                        resultFileName = fileName;
                }
            }

            await Task.Delay(1000);

            return resultFileName;
        }

        public static void RemoveTempFiles()
        {
            string tempDir = Path.Combine(Application.StartupPath, "temp");
            Directory.Delete(tempDir, true);
        }

        public static void KeepTempFiles()
        {
            string videoPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "MVP Downloads");
            if (Directory.Exists(videoPath) == false)
                Directory.CreateDirectory(videoPath);

            string tempDir = Path.Combine(Application.StartupPath, "temp");

            string[] tempFileNames = Directory.GetFiles(tempDir);
            foreach (string fileName in tempFileNames)
            {
                string destFileName = Path.Combine(videoPath, Path.GetFileName(fileName));
                if (File.Exists(destFileName))
                    File.Delete(destFileName);
                File.Move(fileName, destFileName);
            }

            RemoveTempFiles();
        }
        #endregion
    }
}
