using System.Net;
using System.Net.Sockets;
using TWC.I2.MsgEncode;
using TWC.I2.MsgEncode.FEC;
using TWC.I2.MsgEncode.ProcessingSteps;
using TWC.Msg;

namespace i2Receiver.Communication
{
    public class UDPSender
    {
        private string tempDir = Path.Combine(AppContext.BaseDirectory, "temp");
        private readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        IPAddress ipAddress;
        IPEndPoint ipEndPoint;
        UdpClient udpClient;

        public UDPSender(string destIp, int destPort, string interfaceIp)
        {
            ipAddress = IPAddress.Parse(destIp);
            ipEndPoint = new IPEndPoint(ipAddress, destPort);

            udpClient = new UdpClient();
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind((EndPoint)new IPEndPoint(IPAddress.Parse(interfaceIp), 7787));
            udpClient.JoinMulticastGroup(ipAddress, 64);
            Log.Info($"UDP connection established @ {destIp}:{destPort}");
        }

        public void SendCommand(string command, string? headendId = null)
        {
            string tempFile = Path.Combine(tempDir, Guid.NewGuid().ToString());
            File.WriteAllText(tempFile, "");
            SendFile(tempFile, command, false, headendId);
            File.Delete(tempFile);
        }

        public void SendFile(string fileName, string command, bool gZipEncode = true, string? headendId = null)
        {
            string tempFile = Path.Combine(tempDir, Guid.NewGuid().ToString() + ".i2m");
            string fecTempFile = Path.Combine(tempDir, Guid.NewGuid().ToString() + ".i2m");
            File.Copy(fileName, tempFile);

            List<IMsgEncodeStep> steps = new List<IMsgEncodeStep>();
            ExecMsgEncodeStep execMsgEncodeStep = new ExecMsgEncodeStep(command);
            steps.Add(execMsgEncodeStep);

            if (gZipEncode)
            {
                steps.Add(new GzipMsgEncoderDecoder());
            }

            if (headendId != null)
            {
                steps.Add(new CheckHeadendIdMsgEncodeStep(headendId));
            }

            MsgEncoder encoder = new MsgEncoder(steps);
            encoder.Encode(tempFile);

            FecEncoder fecEncoder = FecEncoder.Create(FecEncoding.None, (ushort)DgPacket.MAX_PAYLOAD_SIZE, 1, 2);
            Stream inputStream = (Stream)File.OpenRead(tempFile);

            using (Stream oStream = (Stream)File.OpenWrite(fecTempFile))
            {
                fecEncoder.Encode(inputStream, oStream);
            }

            I2Msg msg = new I2Msg(fecTempFile);
            msg.Id = (uint)GetUnixTimestampMillis();
            msg.Start();
            uint count = msg.CalcMsgPacketCount();

            uint packets = 0;
            while (packets < count)
            {
                byte[] b = msg.GetNextPacket();
                udpClient.Send(b, b.Length, ipEndPoint);
                packets++;
                Thread.Sleep(2);
            }

            // Disposal
            msg.Dispose();
            inputStream.Close();
            File.Delete(tempFile);
            File.Delete(fecTempFile);
        }

        public long GetUnixTimestampMillis()
        {
            return (long)(DateTime.UtcNow - unixEpoch).TotalMilliseconds;
        }
    }
}
