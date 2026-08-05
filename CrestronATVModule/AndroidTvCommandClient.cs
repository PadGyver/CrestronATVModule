using System;
using System.IO;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace AndroidTvLib
{
    public class AndroidTvCommandClient
    {
        private const int ReadTimeoutMs = 60000;
        private const int MinReconnectDelayMs = 2000;
        private const int MaxReconnectDelayMs = 30000;

        private readonly object _lock = new object();

        private TcpClient _tcp;
        private SslStream _ssl;
        private Thread _readThread;

        private string _ip;
        private X509Certificate2 _cert;

        private volatile bool _stopping;
        private volatile bool _connected;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<bool> PowerStateChanged;
        public event Action<string> CurrentAppChanged;

        public bool IsConnected => _connected;

        public void Connect(string ip, X509Certificate2 clientCert)
        {
            _ip = ip;
            _cert = clientCert;
            _stopping = false;
            DoConnect();
        }

        public void Disconnect()
        {
            _stopping = true;
            Cleanup();
            _connected = false;
        }

        private void DoConnect()
        {
            lock (_lock)
            {
                Cleanup();

                _tcp = new TcpClient(_ip, 6466);
                _tcp.ReceiveTimeout = ReadTimeoutMs;

                _ssl = new SslStream(_tcp.GetStream(), false, (s, c, ch, e) => true);
                _ssl.AuthenticateAsClient(_ip,
                    new X509CertificateCollection { _cert },
                    SslProtocols.Tls12, false);
                _ssl.ReadTimeout = ReadTimeoutMs;
            }

            _readThread = new Thread(ReadLoop) { IsBackground = true };
            _readThread.Start();
        }

        private void Cleanup()
        {
            try { _ssl?.Close(); } catch { }
            try { _tcp?.Close(); } catch { }
            _ssl = null;
            _tcp = null;
        }

        private void ReadLoop()
        {
            while (!_stopping)
            {
                try
                {
                    SslStream ssl;
                    lock (_lock) { ssl = _ssl; }
                    if (ssl == null) return;

                    int len = ssl.ReadByte();
                    if (len < 0) throw new IOException("Stream closed by remote host");

                    var buf = new byte[len];
                    int read = 0;
                    while (read < len)
                    {
                        int r = ssl.Read(buf, read, len - read);
                        if (r == 0) throw new IOException("Stream closed by remote host");
                        read += r;
                    }
                    HandleMessage(buf);
                }
                catch (Exception)
                {
                    HandleDisconnect();
                    return;
                }
            }
        }

        private void HandleDisconnect()
        {
            if (_stopping) return;

            bool wasConnected = _connected;
            _connected = false;
            Cleanup();

            if (wasConnected)
                Disconnected?.Invoke();

            ScheduleReconnect();
        }

        private void ScheduleReconnect()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                int delay = MinReconnectDelayMs;
                while (!_stopping)
                {
                    Thread.Sleep(delay);
                    if (_stopping) return;

                    try
                    {
                        DoConnect();
                        return;
                    }
                    catch
                    {
                        delay = Math.Min(delay * 2, MaxReconnectDelayMs);
                    }
                }
            });
        }

        private void HandleMessage(byte[] msg)
        {
            if (msg.Length == 0) return;

            if (msg.Length >= 4 && msg[0] == 66)
            {
                SendPong();
                return;
            }
            if (msg.Length >= 2 && msg[0] == 18 && msg[1] == 0)
            {
                SendSecondConfig();
                return;
            }
            if (msg.Length >= 4 && msg[0] == 194 && msg[1] == 2)
            {
                bool isOn = msg[msg.Length - 1] == 1;
                PowerStateChanged?.Invoke(isOn);
                return;
            }
            if (msg.Length >= 2 && msg[0] == 162 && msg[1] == 1)
            {
                string app = ExtractPackageName(msg);
                if (!string.IsNullOrEmpty(app))
                    CurrentAppChanged?.Invoke(app);
                return;
            }
            if (msg.Length >= 2 && msg[0] == 10)
            {
                SendFirstConfig();
                return;
            }
        }

        private string ExtractPackageName(byte[] msg)
        {
            int bestStart = -1, bestLen = 0;
            int i = 0;
            while (i < msg.Length)
            {
                if (IsPrintable(msg[i]))
                {
                    int start = i;
                    while (i < msg.Length && IsPrintable(msg[i])) i++;
                    int len = i - start;
                    if (len > bestLen)
                    {
                        bestLen = len;
                        bestStart = start;
                    }
                }
                else
                {
                    i++;
                }
            }
            if (bestStart >= 0 && bestLen >= 3)
                return Encoding.ASCII.GetString(msg, bestStart, bestLen);
            return null;
        }

        private bool IsPrintable(byte b)
        {
            return b >= 0x20 && b <= 0x7E;
        }

        private void SendFramed(byte[] payload)
        {
            try
            {
                SslStream ssl;
                lock (_lock) { ssl = _ssl; }
                if (ssl == null) return;

                var framed = ProtoWriter.Frame(payload);
                ssl.Write(framed, 0, framed.Length);
            }
            catch (Exception)
            {
                HandleDisconnect();
            }
        }

        private void SendFirstConfig()
        {
            byte[] payload = {
                10,34,8,238,4,18,29,24,1,34,1,49,42,15,
                97,110,100,114,111,105,116,118,45,114,101,109,111,116,101,
                50,5,49,46,48,46,48
            };
            SendFramed(payload);
        }

        private void SendSecondConfig()
        {
            byte[] payload = { 18, 3, 8, 238, 4 };
            SendFramed(payload);
            _connected = true;
            Connected?.Invoke();
        }

        private void SendPong()
        {
            byte[] payload = { 74, 2, 8, 25 };
            SendFramed(payload);
        }

        public void SendKey(int keyCode, bool longPress = false)
        {
            SendKeyEvent(keyCode, 1);
            Thread.Sleep(longPress ? 400 : 40);
            SendKeyEvent(keyCode, 2);
        }

        private void SendKeyEvent(int keyCode, int action)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(82); ms.WriteByte(4); ms.WriteByte(8);
                var codeVarint = ProtoWriter.WriteVarint((ulong)keyCode);
                ms.Write(codeVarint, 0, codeVarint.Length);
                ms.WriteByte(16); ms.WriteByte((byte)action);
                var arr = ms.ToArray();
                SendFramed(arr);
            }
        }

        public void LaunchApp(string deepLink)
        {
            var linkBytes = Encoding.UTF8.GetBytes(deepLink);
            using (var inner = new MemoryStream())
            {
                ProtoWriter.WriteBytesField(inner, 1, linkBytes);
                var innerBytes = inner.ToArray();
                using (var ms = new MemoryStream())
                {
                    ms.WriteByte(210); ms.WriteByte(5);
                    var lenB = ProtoWriter.WriteVarint((ulong)innerBytes.Length);
                    ms.Write(lenB, 0, lenB.Length);
                    ms.Write(innerBytes, 0, innerBytes.Length);
                    var arr = ms.ToArray();
                    SendFramed(arr);
                }
            }
        }
    }
}