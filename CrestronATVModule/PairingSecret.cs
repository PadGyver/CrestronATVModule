using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
namespace AndroidTvLib
{
    public static class PairingSecret
    {
        public static byte[] Compute(X509Certificate2 clientCert, X509Certificate2 serverCert, string code)
        {
            var clientRsa = clientCert.GetRSAPublicKey();
            var serverRsa = serverCert.GetRSAPublicKey();

            var cp = clientRsa.ExportParameters(false);
            var sp = serverRsa.ExportParameters(false);

            byte[] clientMod = StripLeadingZero(cp.Modulus);
            byte[] clientExp = StripLeadingZero(cp.Exponent);
            byte[] serverMod = StripLeadingZero(sp.Modulus);
            byte[] serverExp = StripLeadingZero(sp.Exponent);

            string last4 = code.Substring(code.Length - 4);
            byte[] codeBytes = HexStringToBytes(last4);

            using (var ms = new MemoryStream())
            {
                ms.Write(clientMod, 0, clientMod.Length);
                ms.Write(clientExp, 0, clientExp.Length);
                ms.Write(serverMod, 0, serverMod.Length);
                ms.Write(serverExp, 0, serverExp.Length);
                ms.Write(codeBytes, 0, codeBytes.Length);

                using (var sha256 = SHA256.Create())
                {
                    return sha256.ComputeHash(ms.ToArray());
                }
            }
        }

        private static byte[] StripLeadingZero(byte[] b)
            => (b.Length > 1 && b[0] == 0x00) ? b.Skip(1).ToArray() : b;

        private static byte[] HexStringToBytes(string hex)
        {
            var result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return result;
        }
    }
}