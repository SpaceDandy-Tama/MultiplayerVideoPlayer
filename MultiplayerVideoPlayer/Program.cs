using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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

        public static bool Save2Temp = false;

        static readonly byte[] ManifestMagic = { 0x4D, 0x41, 0x4E, 0x49, 0x46, 0x45, 0x53, 0x54 }; // "MANIFEST"

        public static readonly Icon Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        public static HttpClient HttpClient = null;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>

        [STAThread] 
        static void Main(string[] args) => OldMain(args).GetAwaiter().GetResult();
        
        static async Task OldMain(string[] args)
        {
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

#if !DEBUG
                if (!ValidateFiles(false))
                {
                    await Bootstrap();
                    Application.Restart();
                    Environment.Exit(0);
                    return;
                }
#endif

                Form2 = new Launcher();
                Form2.ShowDialog();
                string[] argi = Form2.HandleArgs();

                if (argi == null || argi.Length == 0)
                    return;

                if (argi[0].Equals("-generateManifest"))
                {
                    GenerateManifest();
                    return;
                }
                else if (argi[0].Equals("-dumpManifest"))
                {
                    DumpManifest();
                    return;
                }
                else if (argi[0].Equals("-bootstrap"))
                {
                    await Bootstrap();
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
                if (args[0].Equals("-generateManifest"))
                {
                    GenerateManifest();
                    return;
                }
                else if (args[0].Equals("-dumpManifest"))
                {
                    DumpManifest();
                    return;
                }
                else if (args[0].Equals("-bootstrap"))
                {
                    await Bootstrap();
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
                if(!Program.Save2Temp)
                    Program.KeepTempFiles();
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
        private static bool HasManifestMagic()
        {
            string exePath = Application.ExecutablePath;

            FileInfo fi = new FileInfo(exePath);

            // File too small to contain magic
            if (fi.Length < ManifestMagic.Length)
                return false;

            byte[] tail = new byte[ManifestMagic.Length];

            using (FileStream fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Seek(-ManifestMagic.Length, SeekOrigin.End);
                fs.Read(tail, 0, tail.Length);
            }

            for (int i = 0; i < ManifestMagic.Length; i++)
            {
                if (tail[i] != ManifestMagic[i])
                    return false;
            }

            return true;
        }
        private static void RemoveManifest()
        {
            if (!HasManifestMagic())
                return;

            string exePath = Application.ExecutablePath;

            using (FileStream fs = new FileStream(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    // Move to the int just before the magic
                    fs.Seek(-(ManifestMagic.Length + sizeof(int)), SeekOrigin.End);

                    int manifestLength = br.ReadInt32();

                    // Truncate: remove [manifest][length][magic]
                    long newLength = fs.Length - ManifestMagic.Length - sizeof(int) - manifestLength;

                    fs.SetLength(newLength);
                }
            }
        }
        private static string GetChecksum(string filePath)
        {
            using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hashBytes = sha.ComputeHash(fs);
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }
        }
        private static void GenerateManifest()
        {
            if (HasManifestMagic())
                RemoveManifest();

            //This is needed because Path.GetRelativePath does not exist in .Net Framework 4.7.2 (.net standard 2.0)
            Func<string, string, string> GetRelativePath = (filespec, folder) =>
            {
                Uri pathUri = new Uri(filespec);

                if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    folder += Path.DirectorySeparatorChar;

                Uri folderUri = new Uri(folder);
                Uri relativeUri = folderUri.MakeRelativeUri(pathUri);

                string relativePath = Uri.UnescapeDataString(relativeUri.ToString());
                return relativePath.Replace('/', Path.DirectorySeparatorChar);
            };

            string workingDir = Directory.GetCurrentDirectory();
            string exePath = Application.ExecutablePath;

            FileManifest manifest = new FileManifest();
            foreach (string filePath in Directory.GetFiles(workingDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = GetRelativePath(filePath, workingDir);
                manifest.Files[relativePath] = GetChecksum(filePath);
            }

            byte[] manifestBytes = Encoding.UTF8.GetBytes(Tiny.Serializer.Serialize(manifest));

            NetCompression.LoggingType = LoggingType.None;
            MemoryStream ms = NetCompression.Compress(manifestBytes, CompressionLevel.Optimal, CompressionAlgorithm.Deflate);
            byte[] compressedManifestBytes = ms.ToArray();
            ms.Dispose();

            string copyPath = Path.Combine(workingDir, Path.GetFileNameWithoutExtension(exePath) + ".WithManifest.exe");
            File.Copy(exePath, copyPath, true);

            using (FileStream fs = new FileStream(copyPath, FileMode.Append, FileAccess.Write))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    bw.Write(compressedManifestBytes);
                    bw.Write(compressedManifestBytes.Length);
                    bw.Write(ManifestMagic);
                }
            }
        }
        private static string ReadManifest()
        {
            if (!HasManifestMagic())
            {
                return null;
            }

            string exePath = Application.ExecutablePath;

            byte[] compressedManifestBytes;

            using (FileStream fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    // Seek to the int just before the magic
                    fs.Seek(-(ManifestMagic.Length + sizeof(int)), SeekOrigin.End);

                    int manifestLength = br.ReadInt32();

                    // Seek to the start of the manifest
                    fs.Seek(-(ManifestMagic.Length + sizeof(int) + manifestLength), SeekOrigin.End);

                    compressedManifestBytes = br.ReadBytes(manifestLength);
                }
            }

            MemoryStream manifestStream = NetCompression.Decompress(compressedManifestBytes, CompressionAlgorithm.Deflate);
            byte[] manifestBytes = manifestStream.ToArray();
            manifestStream.Dispose();
            return Encoding.UTF8.GetString(manifestBytes);
        }
        private static void DumpManifest()
        {
            if (!HasManifestMagic())
            {
                MessageBox.Show("No manifest embedded in the executable", Application.ProductName);
                return;
            }

            string tinyManifest = ReadManifest();

            if (tinyManifest == null)
                return;

            string workingDir = Directory.GetCurrentDirectory();
            string manifestPath = Path.Combine(workingDir, "manifest.tiny");

            File.WriteAllText(manifestPath, tinyManifest);
        }
        private static bool ValidateFiles(bool performChecksum = true)
        {
            string exePath = Application.ExecutablePath;

            FileManifest manifest = Tiny.Deserializer.Deserialize<FileManifest>(ReadManifest());

            foreach (KeyValuePair<string, string> pair in manifest.Files)
            {
                if (!File.Exists(pair.Key))
                    return false;

                if (!performChecksum || pair.Key.Equals(Path.GetFileName(exePath), StringComparison.OrdinalIgnoreCase))
                    continue;

                if (GetChecksum(pair.Key) != pair.Value)
                    return false;
            }

            return true;
        }
        private static async Task DownloadFileAsync(string url, string destinationPath = null)
        {
            if(HttpClient == null)
                HttpClient = new HttpClient();

            using (HttpResponseMessage response = await HttpClient.GetAsync(url))
            {
                response.EnsureSuccessStatusCode();

                if (destinationPath == null)
                {
                    string workingDir = Directory.GetCurrentDirectory();
                    destinationPath = Path.Combine(workingDir, Path.GetFileName(url));
                }

                using (FileStream fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }
        }
        private static async Task FetchDependencies(Process console)
        {
            console.StandardInput.WriteLine("tinydl.exe https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe");

            string workingDir = Directory.GetCurrentDirectory();

            //THIS IS PAINFULLY SLOW SO WE FETCH VERSION AND BUILD LINK TO GYAN.DEV'S GITHUB MIRROR
            string downloadLink = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-full.7z";
            string versionLink = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-full.7z.ver";
            //string checksumLink = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-full.7z.sha256";

            string version;
            //string checksum;
            //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (WebClient wc = new WebClient())
            {
                version = wc.DownloadString(versionLink).Trim();
                //checksum = wc.DownloadString(checksumLink).Trim();
            }

            downloadLink = $"https://github.com/GyanD/codexffmpeg/releases/latest/download/ffmpeg-{version}-full_build.7z";

            console.StandardInput.WriteLine($"tinydl.exe {downloadLink} \"%TEMP%\\ffmpeg.7z\"");
            console.StandardInput.WriteLine("7zr.exe x \"%TEMP%\\ffmpeg.7z\" -o\"%TEMP%\" -y");
            console.StandardInput.WriteLine($"for /d %D in (\"%TEMP%\\ffmpeg-*\") do copy /Y \"%D\\bin\\ffmpeg.exe\" \"{workingDir}\\ffmpeg.exe\"");
        }
        private static async Task FetchCoreFiles(Process console)
        {
            string workingDir = Directory.GetCurrentDirectory();
            string fileVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;

            //console.StandardInput.WriteLine($"tinydl.exe https://spacewardgame.com/temp/mvp/MVPv{fileVersion}.7z \"%TEMP%\\MVPv{fileVersion}.7z\"");
            console.StandardInput.WriteLine($"tinydl.exe https://github.com/SpaceDandy-Tama/MultiplayerVideoPlayer/releases/download/v{fileVersion}/MVP_coreFiles.7z \"%TEMP%\\MVPv{fileVersion}.7z\"");
            console.StandardInput.WriteLine($"7zr.exe x \"%TEMP%\\MVPv{fileVersion}.7z\" -o\"%TEMP%\\MVPv{fileVersion}\" -y");
            console.StandardInput.WriteLine($"xcopy /C/H/R/S/Y/I \"%TEMP%\\MVPv{fileVersion}\\*\" \"{workingDir}\"");
        }
        private static async Task Bootstrap()
        {
            await DownloadFileAsync("https://spacewardgame.com/temp/bin/tinydl.exe");

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/K", // keep open
                UseShellExecute = false,
                RedirectStandardInput = true
            };

            Process cmd = Process.Start(psi);

            string workingDir = Directory.GetCurrentDirectory();
            cmd.StandardInput.WriteLine("cd " + workingDir);
            cmd.StandardInput.WriteLine("tinydl.exe https://www.7-zip.org/a/7zr.exe");

            await FetchDependencies(cmd);
            await FetchCoreFiles(cmd);

            HttpClient.Dispose();
            cmd.StandardInput.WriteLine("exit");
            cmd.WaitForExit();
            cmd.Dispose();
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
