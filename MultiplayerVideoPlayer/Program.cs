using System;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Net.Http;
using LibVLCSharp.Shared;
using System.Threading;
using System.Diagnostics;

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
            string quality = null;
            bool downloadOnly = false;

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

                if (argi == null || argi.Length == 0)
                    return;

                if (argi[0].Equals("-update"))
                {
                    await Update();
                    return;
                }
                else
                {
                    quality = argi[0].Remove(argi[0].Length - 1);
                }

                if (argi[1].StartsWith("http") || File.Exists(argi[1]))
                    filePath = argi[1];
                else
                    noLinkOrFile = true;

                if (argi.Length == 2)
                    downloadOnly = true;

                if (argi.Length < 3 || !int.TryParse(argi[2], out port))
                    port = 9200;

                if (argi.Length > 3)
                    hostName = argi[3];
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
                else
                {
                    quality = args[0].Remove(args[0].Length - 1);
                }

                if (args[1].StartsWith("http") || File.Exists(args[1]))
                    filePath = args[1];
                else
                    noLinkOrFile = true;

                if (args.Length == 2)
                    downloadOnly = true;

                if (args.Length < 3 || !int.TryParse(args[2], out port))
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
                else if (args.Length > 3)
                    hostName = args[3];
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
                    string fileName = await DownloadFromYoutube(filePath, quality);
                    if (fileName == null)
                        return;

                    titleName = filePath;
                    filePath = fileName;
                }
            }

            if (downloadOnly)
            {
                DialogResult result = MessageBox.Show("Do you want to keep this video?", "Downloaded Video", MessageBoxButtons.YesNo);

                if (result == DialogResult.Yes)
                    Program.KeepTempFiles();
                else
                    Program.RemoveTempFiles();
            }
            else
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
        private static async Task<string> DownloadFromYoutube(string link, string quality)
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

            //--write-subs writes the subs as external subs, doesn't work together with --embed-subs
            string arguments;
            if(link.Contains("youtube") || link.Contains("youtu.be"))
                arguments = $"-f bestvideo[height<={quality}]+bestaudio --merge-output-format mp4 --audio-multistreams --sub-lang \"en-US,en-GB,tr\" --embed-subs --embed-metadata --embed-thumbnail --embed-chapters --write-description -P \"{tempDir}\" -o \"%(uploader)s - %(title)s.%(ext)s\" \"{link}\"";
            else
                arguments = $"-f bestvideo+bestaudio/best --merge-output-format mp4 -P \"{tempDir}\" -o \"%(uploader)s - %(title)s.%(ext)s\" \"{link}\"";

            System.Diagnostics.Process.Start(Path.Combine(Application.StartupPath, "yt-dlp.exe"), arguments).WaitForExit(int.MaxValue);

            bool fallbackAttempted = false;
            string resultFileName = null;
            while (resultFileName == null && !fallbackAttempted)
            {
                await Task.Delay(100);

                string[] fileNames = Directory.GetFiles(tempDir);
                foreach(string fileName in fileNames)
                {
                    if (fileName.EndsWith(".mp4"))
                        resultFileName = fileName;
                    else if (fileName.EndsWith(".description"))
                        File.Move(fileName, $"{fileName}.txt");
                }

                if (resultFileName == null)
                {
                    fallbackAttempted = true;
                    arguments = $"--force-generic-extractor --merge-output-format mp4 -P \"{tempDir}\" -o \"%(uploader)s - %(title)s.%(ext)s\" \"{link}\"";
                    System.Diagnostics.Process.Start(Path.Combine(Application.StartupPath, "yt-dlp.exe"), arguments).WaitForExit(int.MaxValue);
                }
            }

            await Task.Delay(100);
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
