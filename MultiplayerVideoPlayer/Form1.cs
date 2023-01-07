using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace MultiplayerVideoPlayer
{
    public partial class Form1 : Form
    {
        private bool IsNetworked => Program.NetworkManager.Manager.ConnectedPeersCount > 0;
        private bool IsAuthoritative => Program.NetworkManager.IsServer;
        private NetworkManager NetworkManager => Program.NetworkManager;
        public VideoView VideoView => videoView1;

        public LibVLC LibVLC;
        public MediaPlayer MediaPlayer;
        public Media Media;
        public MediaTrack[] AudioTracks;
        public MediaTrack[] SubtitleTracks;
        public int CurrentAudioTrack;
        public int CurrentSubtitleTrack;

        public string TitleBase = "MVP";
        public string Title = "MVP";
        public string FileName;

        private bool IsFullscreen;
        private Point WindowLocation;
        private Size WindowSize;
        private bool ShiftHold;
        private bool CtrlHold;
        private bool AltHold;

        public Form1(string filePath, string titleName)
        {
            InitializeComponent();

            Core.Initialize();

            FormClosing += new FormClosingEventHandler(this.Form1_FormClosing);
            KeyDown += new KeyEventHandler(this.Common_KeyDown);
            KeyUp += new KeyEventHandler(this.Common_KeyUp);
            PreviewKeyDown += new PreviewKeyDownEventHandler(this.Common_PreviewKeyDown);
            VideoView.KeyDown += new KeyEventHandler(this.Common_KeyDown);
            VideoView.KeyUp += new KeyEventHandler(this.Common_KeyUp);
            VideoView.PreviewKeyDown += new PreviewKeyDownEventHandler(this.Common_PreviewKeyDown);

            FileName = titleName;

            LibVLC = new LibVLC();
            MediaPlayer = new MediaPlayer(LibVLC);
            VideoView.MediaPlayer = MediaPlayer;
            Media = new Media(LibVLC, new Uri(filePath));
            MediaPlayer.Play(Media);
            MediaPlayer.Playing += MediaPlayer_Playing;

            this.Text = $"{Title} - {FileName}";
        }

        #region Controls
        private void PlayPauseToggle()
        {
            if (!IsNetworked || (IsNetworked && IsAuthoritative))
            {
                if (MediaPlayer.IsPlaying)
                    MediaPlayer.Pause();
                else if (MediaPlayer.WillPlay)
                    MediaPlayer.Play();
                else
                {
                    long time = MediaPlayer.Time;
                    MediaPlayer.Play(Media);
                    MediaPlayer.Time = time;
                }
            }

            if (IsNetworked)
            {
                if (MediaPlayer.IsPlaying)
                    NetworkManager.SendPause(MediaPlayer.Time);
                else
                    NetworkManager.SendContinue(MediaPlayer.Time);
            }
        }
        
        private void Skip(long milliseconds)
        {
            //Time is in milliseconds
            //Position is normalized
            if (!IsNetworked || (IsNetworked && IsAuthoritative))
                MediaPlayer.Time += milliseconds;

            if (IsNetworked)
            {
                if (MediaPlayer.IsPlaying)
                {
                    NetworkManager.SendSkip(milliseconds);
                }
                else
                {
                    NetworkManager.SendSeek(NetworkManager.IsServer ? MediaPlayer.Time : MediaPlayer.Time + milliseconds);
                }
            }
        }

        private void ChapterNext()
        {
            if (!IsNetworked || (IsNetworked && IsAuthoritative))
                MediaPlayer.NextChapter();

            if (IsNetworked)
            {
                int chapter = MediaPlayer.Chapter;
                if (NetworkManager.IsClient)
                {
                    chapter++;
                    if (chapter >= MediaPlayer.ChapterCount)
                        chapter = MediaPlayer.ChapterCount - 1;
                }
                NetworkManager.SendChapterSkip(chapter);
            }
        }

        private void ChapterPrevious()
        {
            if (!IsNetworked || (IsNetworked && IsAuthoritative))
                MediaPlayer.PreviousChapter();

            if (IsNetworked)
            {
                int chapter = MediaPlayer.Chapter;
                if (NetworkManager.IsClient)
                {
                    chapter--;
                    if (chapter < 0)
                        chapter = 0;
                }
                NetworkManager.SendChapterSkip(chapter);
            }
        }

        private void VolumeUp()
        {
            int volume = MediaPlayer.Volume;
            volume += 10;
            if (volume > 100)
                volume = 100;
            MediaPlayer.Volume = volume;
        }

        private void VolumeDown()
        {
            int volume = MediaPlayer.Volume;
            volume -= 10;
            if (volume < 0)
                volume = 0;
            MediaPlayer.Volume = volume;
        }

        private void FullscreenToggle()
        {
            if (IsFullscreen)
            {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.Location = WindowLocation;
                this.Size = WindowSize;

                IsFullscreen = false;
            }
            else
            {
                WindowLocation = this.Location;
                WindowSize = this.Size;

                this.FormBorderStyle = FormBorderStyle.None;
                this.Location = new Point(0, 0);
                this.Size = new Size(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);

                IsFullscreen = true;
            }
        }

        private void SubtitleCycle()
        {
            CurrentSubtitleTrack++;
            if (CurrentSubtitleTrack >= SubtitleTracks.Length)
                CurrentSubtitleTrack = -1;
            MediaPlayer.SetSpu(CurrentSubtitleTrack > -1 ? SubtitleTracks[CurrentSubtitleTrack].Id : CurrentSubtitleTrack);
            string subtitle = "Disabled";
            if(CurrentSubtitleTrack > -1)
                subtitle = SubtitleTracks[CurrentSubtitleTrack].Description + " " + SubtitleTracks[CurrentSubtitleTrack].Language;
            MessageBox.Show(subtitle, "Subtitle Track");
        }

        private void AudioCycle()
        {
            CurrentAudioTrack++;
            if (CurrentAudioTrack >= AudioTracks.Length)
                CurrentAudioTrack = 0;
            MediaPlayer.SetAudioTrack(AudioTracks[CurrentAudioTrack].Id);
            string subtitle = AudioTracks[CurrentAudioTrack].Description + " " + AudioTracks[CurrentAudioTrack].Language;
            MessageBox.Show(subtitle, "Audio Track");
        }
        #endregion

        #region Events
        // By default, KeyDown does not fire for the ARROW keys
        private void Common_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ShiftKey)
                ShiftHold = true;
            else if (e.KeyCode == Keys.ControlKey)
                CtrlHold = true;
            else if (e.KeyCode == Keys.Menu)
                AltHold = true;
            else if (e.KeyCode == Keys.Space)
                PlayPauseToggle();
            else if (e.KeyCode == Keys.Left)
                Skip(GetModifiedSkipTime(-1000));
            else if (e.KeyCode == Keys.Right)
                Skip(GetModifiedSkipTime(1000));
            else if (e.KeyCode == Keys.PageDown)
                ChapterNext();
            else if (e.KeyCode == Keys.PageUp)
                ChapterPrevious();
            else if (e.KeyCode == Keys.Up)
                VolumeUp();
            else if (e.KeyCode == Keys.Down)
                VolumeDown();
            else if (e.KeyCode == Keys.F11 || (AltHold && e.KeyCode == Keys.Enter))
                FullscreenToggle();
            else if (e.KeyCode == Keys.S)
                SubtitleCycle();
            else if (e.KeyCode == Keys.A)
                AudioCycle();
        }

        private void Common_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ShiftKey)
                ShiftHold = false;
            else if (e.KeyCode == Keys.ControlKey)
                CtrlHold = false;
            else if (e.KeyCode == Keys.Menu)
                AltHold = false;
        }

        // PreviewKeyDown is where you preview the key.
        // Do not put any logic here, instead use the
        // KeyDown event after setting IsInputKey to true.
        private void Common_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            e.IsInputKey = true;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            NetworkManager.IsClosing = true;

            MediaPlayer?.Stop();
            MediaPlayer?.Media?.Dispose();
            Media?.Dispose();
            MediaPlayer?.Dispose();
            LibVLC?.Dispose();

            Program.RemoveTempFiles();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            TimeSpan timeSpan = TimeSpan.FromMilliseconds(MediaPlayer.Time);
            this.Text = $"{Title} | {FileName} - {timeSpan.ToString("hh':'mm':'ss")}";
        }

        private void MediaPlayer_Playing(object sender, EventArgs e)
        {
            if (AudioTracks == null && SubtitleTracks == null)
                PopulateTrackFields();
        }
        #endregion

        #region HelperFunctions
        private long GetModifiedSkipTime(long milliseconds)
        {
            if (ShiftHold)
                milliseconds *= 2;
            else if (CtrlHold)
                milliseconds *= 5;
            else if (AltHold)
                milliseconds *= 10;

            if (milliseconds < 0 && MediaPlayer.Time + milliseconds < 0)
                milliseconds = -MediaPlayer.Time;
            else if (milliseconds > 0 && MediaPlayer.Time + milliseconds > Media.Duration)
                milliseconds = Media.Duration - MediaPlayer.Time;

            return milliseconds;
        }

        private void PopulateTrackFields()
        {
            MediaTrack[] mediaTracks = MediaPlayer.Media.Tracks;
            List<MediaTrack> audios = new List<MediaTrack>();
            List<MediaTrack> subs = new List<MediaTrack>();
            foreach (MediaTrack mediaTrack in mediaTracks)
            {
                if (mediaTrack.TrackType == TrackType.Audio)
                    audios.Add(mediaTrack);
                else if (mediaTrack.TrackType == TrackType.Text)
                    subs.Add(mediaTrack);
            }
            AudioTracks = audios.ToArray();
            SubtitleTracks = subs.ToArray();
            CurrentAudioTrack = 0;
            CurrentSubtitleTrack = -1;
        }
        #endregion
    }
}
