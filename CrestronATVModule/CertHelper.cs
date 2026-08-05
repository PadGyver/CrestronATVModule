using Crestron.SimplSharp.CrestronIO;
using System.Security.Cryptography.X509Certificates;

namespace AndroidTvLib
{
    public static class CertHelper
    {
        private const string CertPath = @"\NVRAM\atvremote_client.pfx";
        private const string CertPassword = "atvremote";

        public static X509Certificate2 GetOrCreateClientCert()
        {
            if (!File.Exists(CertPath))
                throw new FileNotFoundException(
                    "Certificat client manquant. Copiez atvremote_client.pfx dans " + CertPath);

            byte[] pfxBytes;
            using (var fs = new FileStream(CertPath, FileMode.Open, FileAccess.Read))
            {
                pfxBytes = new byte[fs.Length];
                fs.Read(pfxBytes, 0, (int)fs.Length);
            }

            return new X509Certificate2(pfxBytes, CertPassword,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
        }
    }
}