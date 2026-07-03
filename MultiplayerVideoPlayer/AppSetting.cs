using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Tiny;

namespace MultiplayerVideoPlayer
{
    [System.Serializable]
    public class AppSetting
    {
        public static AppSetting Current;
        public static string FullPath = AppSetting.GetPath();
        private static string GetPath()
        {
            string appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            string directoryPath = Path.Combine(appDataPath, Application.CompanyName);
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
            return Path.Combine(directoryPath, $"{Application.ProductName}Settings.tiny");
        }
        
        public string LastPath = string.Empty;
        public List<string> PreviousHosts = new List<string>();
        public string TempDir = null;
        public string SaveDir = null;
        public bool StereoMixdown = true;

        public static AppSetting Load()
        {
            if (Current == null)
            {
                if (File.Exists(FullPath))
                {
                    string tiny = File.ReadAllText(FullPath);
                    Current = Deserializer.Deserialize<AppSetting>(tiny);
                    File.Delete(FullPath);
                    if (Current == null)
                    {
                        Current = new AppSetting();
                    }
                }
                else
                {
                    Current = new AppSetting();
                }

                Current.Save();
            }
            return Current;
        }

        public void Save()
        {
            string tiny = Serializer.Serialize(this);
            File.WriteAllText(FullPath, tiny);
        }
    }
}