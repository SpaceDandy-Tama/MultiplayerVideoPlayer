using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MultiplayerVideoPlayer
{
    public class TcpFileSender
    {
        public static async Task SendAsync(int serverPort, string filePath, int chunkSize = 64 * 1024)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, serverPort);
            listener.Start();

            try
            {
                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();

                    // Handle each client in its own task (fire & forget)
                    _ = Task.Run(() => HandleClientAsync(client, filePath, chunkSize));
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task HandleClientAsync(TcpClient client, string filePath, int chunkSize)
        {
            using (client)
            using (NetworkStream networkStream = client.GetStream())
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                string fileName = Path.GetFileName(filePath);
                // Send filename first
                byte[] fileNameBytes = Encoding.UTF8.GetBytes(fileName);
                byte[] fileNameLengthBytes = BitConverter.GetBytes(fileNameBytes.Length);
                await networkStream.WriteAsync(fileNameLengthBytes, 0, fileNameLengthBytes.Length);
                await networkStream.WriteAsync(fileNameBytes, 0, fileNameBytes.Length);

                // Send file length (8 bytes for long)
                long fileLength = fs.Length;
                byte[] fileLengthBytes = BitConverter.GetBytes(fileLength);
                await networkStream.WriteAsync(fileLengthBytes, 0, fileLengthBytes.Length);

                // Send the file in chunks asynchronously
                byte[] buffer = new byte[chunkSize];
                int totalBytes = 0;
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    totalBytes += bytesRead;
                    await networkStream.WriteAsync(buffer, 0, bytesRead);
#if DEBUG
                    await Task.Delay(50);
#endif
                }
            }
        }
    }
}
