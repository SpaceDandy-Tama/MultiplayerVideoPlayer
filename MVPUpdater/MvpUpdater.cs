using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MVPUpdater
{
    public partial class MvpUpdater : Form
    {
        public MvpUpdater()
        {
            InitializeComponent();

            PerformUpdate();
        }

        private async void PerformUpdate()
        {
            DeleteOldFilesAndDirs();
            progressBar1.Value = 10;
            string updatePackage = await DownloadUpdatePackage("MVPv6.zip");
            progressBar1.Value += 10;
            ExtractPackage(updatePackage);
            progressBar1.Value = 100;
            LaunchMainApplicationToDeleteUpdater();
        }

        private void DeleteOldFilesAndDirs()
        {
            string[] oldFiles = Directory.GetFiles(Application.StartupPath);
            foreach (string file in oldFiles)
            {
                if (file.Equals(Application.ExecutablePath) == false)
                    File.Delete(file);
            }
            string[] oldDirs = Directory.GetDirectories(Application.StartupPath);
            foreach (string dir in oldDirs)
                Directory.Delete(dir, true);
        }

        private async Task<string> DownloadUpdatePackage(string fileName)
        {
            string url = "https://spacewardgame.com/temp/MVP/" + fileName;
            string path = Path.Combine(Application.StartupPath, fileName);

            if (File.Exists(path))
                File.Delete(path);

            HttpClient httpClient = new HttpClient();

            using (HttpResponseMessage response = await httpClient.GetAsync(url))
            {
                using (Stream stream = await response.Content.ReadAsStreamAsync())
                {
                    byte[] buffer = new byte[(int)(stream.Length / 60)];
                    using (FileStream fileStream = new FileStream(path, FileMode.CreateNew))
                    {
                        while (stream.Position < stream.Length)
                        {
                            await stream.ReadAsync(buffer, 0, buffer.Length);
                            await fileStream.WriteAsync(buffer, 0, buffer.Length);
                            progressBar1.Value++;
                        }
                    }
                }
            }

            return path;
        }

        private void ExtractPackage(string updatePackage)
        {
            ZipFile.ExtractToDirectory(updatePackage, Application.StartupPath);
            File.Delete(updatePackage);
        }

        private void LaunchMainApplicationToDeleteUpdater()
        {
            System.Diagnostics.Process.Start(Path.Combine(Application.StartupPath, "MultiplayerVideoPlayer.exe"), "-deleteUpdater");
            Application.Exit();
        }
    }
}