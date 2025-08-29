using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MultiplayerVideoPlayer
{
    public class TcpFileReceiver
    {
        public static async Task<string> ReceiveAsync(string ip, int port, int chunkSize = 64 * 1024)
        {
            using (TcpClient client = new TcpClient())
            {
                // Connect asynchronously
                await client.ConnectAsync(ip, port);

                using (NetworkStream networkStream = client.GetStream())
                {
                    // --- Read filename length (4 bytes) ---
                    byte[] nameLengthBuffer = new byte[4];
                    int read = 0;
                    while (read < nameLengthBuffer.Length)
                    {
                        int bytesRead = await networkStream.ReadAsync(nameLengthBuffer, read, nameLengthBuffer.Length - read);
                        if (bytesRead == 0)
                            throw new Exception("Client disconnected while sending filename length.");
                        read += bytesRead;
                    }
                    int fileNameLength = BitConverter.ToInt32(nameLengthBuffer, 0);

                    // --- Read filename ---
                    byte[] nameBuffer = new byte[fileNameLength];
                    read = 0;
                    while (read < fileNameLength)
                    {
                        int bytesRead = await networkStream.ReadAsync(nameBuffer, read, fileNameLength - read);
                        if (bytesRead == 0)
                            throw new Exception("Client disconnected while sending filename.");
                        read += bytesRead;
                    }
                    string savePath = Encoding.UTF8.GetString(nameBuffer);

                    using (FileStream fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, chunkSize, useAsync: true))
                    {

                        // Read file length (8 bytes for long)
                        byte[] lengthBuffer = new byte[8];
                        read = 0;
                        while (read < lengthBuffer.Length)
                        {
                            int bytesRead = await networkStream.ReadAsync(lengthBuffer, read, lengthBuffer.Length - read);
                            if (bytesRead == 0)
                                throw new Exception("Client disconnected while sending file length.");
                            read += bytesRead;
                        }
                        long fileLength = BitConverter.ToInt64(lengthBuffer, 0);

                        // Read the file data in chunks asynchronously
                        byte[] buffer = new byte[chunkSize];
                        long totalRead = 0;
                        while (totalRead < fileLength)
                        {
                            int bytesToRead = (int)Math.Min(chunkSize, fileLength - totalRead);
                            int bytesRead = await networkStream.ReadAsync(buffer, 0, bytesToRead);
                            if (bytesRead == 0)
                                throw new Exception("Client disconnected while sending file data.");

                            await fs.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;
                        }
                    }

                    return savePath;
                }
            }
        }
    }
}
