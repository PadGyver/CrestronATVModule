using System;
using System.IO;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace AndroidTvLib
{
    public class PairingClient
    {
        private TcpClient _tcp;
        private SslStream _ssl;
        private X509Certificate2 _clientCert;
        private X509Certificate2 _serverCert;
        private bool _sessionOpen;

        public bool StartPairing(string ip, out string error)
        {
            error = null;
            try
            {
                _clientCert = CertHelper.GetOrCreateClientCert();
                _tcp = new TcpClient(ip, 6467);

                _ssl = new SslStream(_tcp.GetStream(), false, (sender, cert, chain, errs) =>
                {
                    _serverCert = new X509Certificate2(cert);
                    return true; // TOFU
                });

                _ssl.AuthenticateAsClient(ip,
                    new X509CertificateCollection { _clientCert },
                    SslProtocols.Tls12, false);

                SendFramed(BuildPairingRequest());
                ReadFramed();

                SendFramed(BuildPairingOption());
                ReadFramed();

                SendFramed(BuildPairingConfiguration());
                ReadFramed();

                _sessionOpen = true;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                ClosePairingSession();
                return false;
            }
        }

        public bool SubmitCode(string tvCode, out string error)
        {
            error = null;
            if (!_sessionOpen)
            {
                error = "Session de pairing non initialisée. Relancez Pair.";
                return false;
            }
            try
            {
                byte[] secret = PairingSecret.Compute(_clientCert, _serverCert, tvCode);
                SendFramed(BuildSecretMessage(secret));
                var resp = ReadFramed();
                return resp != null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                ClosePairingSession();
            }
        }

        private void ClosePairingSession()
        {
            _sessionOpen = false;
            try { _ssl?.Close(); } catch { }
            try { _tcp?.Close(); } catch { }
            _ssl = null;
            _tcp = null;
        }

        private byte[] BuildPairingRequest()
        {
            using (var ms = new MemoryStream())
            {
                ProtoWriter.WriteVarintField(ms, 1, 2);
                ProtoWriter.WriteVarintField(ms, 2, 200);
                using (var inner = new MemoryStream())
                {
                    ProtoWriter.WriteStringField(inner, 1, "info.kodono.assistant");
                    ProtoWriter.WriteStringField(inner, 2, "crestron-atv");
                    ProtoWriter.WriteBytesField(ms, 10, inner.ToArray());
                }
                return ms.ToArray();
            }
        }

        private byte[] BuildPairingOption()
        {
            using (var innerType = new MemoryStream())
            {
                ProtoWriter.WriteVarintField(innerType, 1, 3);
                ProtoWriter.WriteVarintField(innerType, 2, 6);
                using (var role = new MemoryStream())
                {
                    ProtoWriter.WriteBytesField(role, 1, innerType.ToArray());
                    ProtoWriter.WriteVarintField(role, 3, 1);
                    using (var ms = new MemoryStream())
                    {
                        ProtoWriter.WriteVarintField(ms, 1, 2);
                        ProtoWriter.WriteVarintField(ms, 2, 200);
                        ProtoWriter.WriteBytesField(ms, 20, role.ToArray());
                        return ms.ToArray();
                    }
                }
            }
        }

        private byte[] BuildPairingConfiguration()
        {
            using (var innerType = new MemoryStream())
            {
                ProtoWriter.WriteVarintField(innerType, 1, 3);
                ProtoWriter.WriteVarintField(innerType, 2, 6);
                using (var cfg = new MemoryStream())
                {
                    ProtoWriter.WriteBytesField(cfg, 1, innerType.ToArray());
                    ProtoWriter.WriteVarintField(cfg, 2, 1);
                    using (var ms = new MemoryStream())
                    {
                        ProtoWriter.WriteVarintField(ms, 1, 2);
                        ProtoWriter.WriteVarintField(ms, 2, 200);
                        ProtoWriter.WriteBytesField(ms, 30, cfg.ToArray());
                        return ms.ToArray();
                    }
                }
            }
        }

        private byte[] BuildSecretMessage(byte[] secret)
        {
            using (var secretMsg = new MemoryStream())
            {
                ProtoWriter.WriteBytesField(secretMsg, 1, secret);
                using (var ms = new MemoryStream())
                {
                    ProtoWriter.WriteVarintField(ms, 1, 2);
                    ProtoWriter.WriteVarintField(ms, 2, 200);
                    ProtoWriter.WriteBytesField(ms, 40, secretMsg.ToArray());
                    return ms.ToArray();
                }
            }
        }

        private void SendFramed(byte[] payload)
        {
            var framed = ProtoWriter.Frame(payload);
            _ssl.Write(framed, 0, framed.Length);
        }

        private byte[] ReadFramed()
        {
            int lenByte = _ssl.ReadByte();
            if (lenByte < 0) return null;
            var buf = new byte[lenByte];
            int read = 0;
            while (read < lenByte) read += _ssl.Read(buf, read, lenByte - read);
            return buf;
        }
    }
}