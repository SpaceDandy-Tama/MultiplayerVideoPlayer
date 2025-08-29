using System;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Net.Http;
using LibVLCSharp.Shared;
using System.Threading;

namespace MultiplayerVideoPlayer
{
    internal static class Program
    {
        public static MvpMain Form;
        public static Launcher Form2;
        public static Downloader Form3;
        public static NetworkManager NetworkManager;

        public const int DownloadTimeOutSeconds = 60;
        public static bool TempFilesDownloaded = false;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>

        [STAThread] 
        static void Main(string[] args) => OldMain(args).GetAwaiter().GetResult();
        
        static async Task OldMain(string[] args)
        {
            /*
            if(ValidateFiles() == false)
            {
                await Update();
                return;
            }
            */

            string filePath = null;
            string hostName = null;
            int port = -1;

            bool noLinkOrFile = false;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);


            if (args == null || args.Length == 0)
            {
                /* Checks if there are any arguments to the program, if not launchers the launcher for setup,
                 * returns the values from the launcher to use as arguments, the launcher determines the directroy/url, port and if you are client or host
                 * if there are no arguements from the launcher returns
                 * checks if the 1st arguemnt is valid if not quits
                 * checks the port if not vlaid defaults to 7243
                 * if there is a 3rd arguements sets as client with the ip on 3rd argument as host                 
                */

                Form2 = new Launcher();
                Form2.ShowDialog();
                string[] argi = Form2.HandleArgs();

                if(argi == null || argi.Length == 0)
                    return;

                if (argi[0].Equals("-update"))
                {
                    await Update();
                    return;
                }

                if (argi[0].StartsWith("http") || File.Exists(argi[0]))
                    filePath = argi[0];
                else
                    noLinkOrFile = true;

                if (argi.Length < 2 || !int.TryParse(argi[1], out port))
                    port = 9200;

                if (argi.Length > 2)
                    hostName = argi[2];
            }
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
                else if (args[0].StartsWith("http") || File.Exists(args[0]))
                    filePath = args[0];
                else
                    noLinkOrFile = true;

                if (args.Length < 2 || !int.TryParse(args[1], out port))
                    port = 9200;

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

            if (noLinkOrFile)
            {
                if (!string.IsNullOrEmpty(hostName))
                {
                    TcpFileReceiver.ReceiveAsync(hostName, port);
                    Form3 = new Downloader();
                    DialogResult result = Form3.ShowDialog();
                    //if(result == DialogResult.Yes)
                    //{
                    //    Form3.Dispose();
                    //    Form3 = new Downloader();
                    //    Form3.Show();
                    //}
                    filePath = TcpFileReceiver.SavePath;
                }
                else
                {
                    Application.Exit();
                    return;
                }
            }

            string titleName = Path.GetFileName(filePath);

            if (!string.IsNullOrEmpty(filePath))
            {
                if (filePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = await DownloadFromYoutube(filePath);
                    if (fileName == null)
                        return;

                    titleName = filePath;
                    filePath = fileName;
                }
            }

            PlayMedia(filePath, hostName, port, titleName);
        }

        // New method to prevent code repetation,
        // if the program launcher with arguments, regular checks are made if the arguements came from the launcher and they are valid they are directly pushed to the video client
        public static void PlayMedia(string filePath, string hostName, int port, string titleName)
        {
            StartNetworkManager(hostName, port, filePath);
            TcpFileSender.SendAsync(port, filePath);
            Application.Run(Form = new MvpMain(filePath, titleName));
        }

        private static async Task StartNetworkManager(string hostName, int port, string filePath)
        {
            if (NetworkManager.IsInitialized)
                return;

            while (Form == null ||!Form.MediaPlayer.IsPlaying)
                await Task.Delay(100);

            NetworkManager = new NetworkManager(hostName, port, filePath);
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

            string arguments = $"-f bestvideo*+bestaudio/best --merge-output-format mp4 --write-subs --sub-lang \"en-US,en-GB,tr\" -P \"{tempDir}\" -o \"%(uploader)s - %(title)s.%(ext)s\" " + link;
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
            TempFilesDownloaded = true;

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
