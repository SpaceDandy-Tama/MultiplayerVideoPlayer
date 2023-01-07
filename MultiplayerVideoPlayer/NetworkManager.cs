using LiteNetLib;
using LiteNetLib.Utils;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace MultiplayerVideoPlayer
{
    public enum NetworkDataEvent : byte
    {
        Connected,
        Paused,
        Continued,
        Seek,
        Skip,
        ChapterSkip,
    }

    public class NetworkManager
    {
        public static bool IsClosing;
        public static MediaPlayer MediaPlayer => Program.Form.MediaPlayer;

        public string HostName;
        public int Port;

        public EventBasedNetListener Listener;
        public NetManager Manager;
        public NetDataWriter DataWriter;

        public string SessionKey => Program.Form.FileName;
        public const int MaxConnections = 10;

        public bool IsInitialized;
        public bool IsServer;
        public bool IsClient;

        public NetworkManager(string hostName, int port)
        {
            HostName = hostName;
            Port = port;

            Listener = new EventBasedNetListener();
            Manager = new NetManager(Listener);
            DataWriter = new NetDataWriter();

            if (HostName == null || HostName == "")
                InitializeAsHost(Port);
            else
                InitializeAsClient(HostName, Port);

            PoolEvents();
        }

        private void InitializeAsHost(int port)
        {
            if (IsInitialized)
                return;

            Manager.Start(port);
            Listener.ConnectionRequestEvent += Listener_ConnectionRequestEvent;
            Listener.PeerConnectedEvent += Listener_PeerConnectedEvent;
            Listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;

            IsInitialized = true;
            IsServer = true;
            IsClient = false;

            Program.Form.Title = $"{Program.Form.TitleBase} Hosting on {Port}";
        }

        private void InitializeAsClient(string hostName, int port)
        {
            if (IsInitialized)
                return;

            Manager.Start();
            Manager.Connect(hostName, port, SessionKey);
            Listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;

            IsInitialized = true;
            IsServer = false;
            IsClient = true;

            Program.Form.Title = $"{Program.Form.TitleBase} Connecting to {HostName} : {Port}";
        }

        private async void PoolEvents()
        {
            while (!IsClosing)
            {
                Manager.PollEvents();
                await System.Threading.Tasks.Task.Delay(10);
            }

            Manager.Stop();
        }

        #region Events
        private void Listener_ConnectionRequestEvent(ConnectionRequest request)
        {
            if (Manager.ConnectedPeersCount < MaxConnections)
                request.AcceptIfKey(SessionKey);
            else
                request.Reject();
        }

        private void Listener_PeerConnectedEvent(NetPeer peer)
        {
            DataWriter.Reset();
            DataWriter.Put((byte)NetworkDataEvent.Connected);
            DataWriter.Put((long)MediaPlayer.Time);
            DataWriter.Put((bool)MediaPlayer.IsPlaying);
            peer.Send(DataWriter, DeliveryMethod.ReliableOrdered);
        }

        private void Listener_NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            NetworkDataEvent NetworkDataEvent = (NetworkDataEvent)reader.GetByte();
            if (NetworkDataEvent == NetworkDataEvent.Connected)
            {
                long time = reader.GetLong();
                bool isPlaying = false;
                if (IsServer)
                {

                }
                else
                {
                    if (isPlaying == false)
                        MediaPlayer.Pause();
                    MediaPlayer.Time = time;
                    Program.Form.Title = $"{Program.Form.TitleBase} Connected to {HostName} : {Port}";
                }
            }
            else if (NetworkDataEvent == NetworkDataEvent.Paused)
            {
                long time = reader.GetLong();
                Pause(time);
                if (IsServer)
                    SendPause(time);
            }
            else if (NetworkDataEvent == NetworkDataEvent.Continued)
            {
                long time = reader.GetLong();
                Continue(time);
                if (IsServer)
                    SendContinue(time);
            }
            else if (NetworkDataEvent == NetworkDataEvent.Skip)
            {
                long millisecondsDelta = reader.GetLong();
                Skip(millisecondsDelta);
                if (IsServer)
                    SendSeek(MediaPlayer.Time);
            }
            else if(NetworkDataEvent == NetworkDataEvent.Seek)
            {
                long time = reader.GetLong();
                Seek(time);
                if (IsServer)
                    SendSeek(time);
            }
            else if(NetworkDataEvent == NetworkDataEvent.ChapterSkip)
            {
                int chapter = reader.GetInt();
                ChapterSkip(chapter);
                if (IsServer)
                    SendChapterSkip(chapter);
            }
        }
        #endregion

        #region Actions
        public void Pause(long time)
        {
            if (!MediaPlayer.IsPlaying)
                return;

            MediaPlayer.Pause();
            MediaPlayer.Time = time;
        }
        public void SendPause(long time)
        {
            DataWriter.Reset();
            DataWriter.Put((byte)NetworkDataEvent.Paused);
            DataWriter.Put((long)time);
            SendWrittenPacket();
        }

        public void Continue(long time)
        {
            if (MediaPlayer.IsPlaying)
                return;

            MediaPlayer.Time = time;

            if (MediaPlayer.WillPlay)
                MediaPlayer.Play();
            else
            {
                MediaPlayer.Play(Program.Form.Media);
                MediaPlayer.Time = time;
            }
        }
        public void SendContinue(long time)
        {
            DataWriter.Reset();
            DataWriter.Put((byte)NetworkDataEvent.Continued);
            DataWriter.Put((long)time);
            SendWrittenPacket();
        }

        public void Skip(long millisecondsDelta)
        {
            MediaPlayer.Time += millisecondsDelta;
        }
        public void SendSkip(long millisecondsDelta)
        {
            DataWriter.Reset();
            DataWriter.Put((byte)NetworkDataEvent.Skip);
            DataWriter.Put((long)millisecondsDelta);
            SendWrittenPacket();
        }

        public void Seek(long time)
        {
            MediaPlayer.Time = time;
        }
        public void SendSeek(long time)
        {
            DataWriter.Reset();
            DataWriter.Put((byte)NetworkDataEvent.Seek);
            DataWriter.Put((long)time);
            SendWrittenPacket();
        }

        public void ChapterSkip(int chapter)
        {
            MediaPlayer.Chapter = chapter;
        }
        public void SendChapterSkip(int chapter)
        {
            DataWriter.Reset();
            DataWriter.Put((byte)NetworkDataEvent.ChapterSkip);
            DataWriter.Put((int)chapter);
            SendWrittenPacket();
        }
        #endregion

        #region HelperMethods
        private void SendWrittenPacket()
        {
            if (IsServer)
            {
                foreach (NetPeer peer in Manager.ConnectedPeerList)
                    peer.Send(DataWriter, DeliveryMethod.ReliableOrdered);
            }
            else if (IsClient)
                Manager.FirstPeer.Send(DataWriter, DeliveryMethod.ReliableOrdered);
        }
        #endregion
    }
}
