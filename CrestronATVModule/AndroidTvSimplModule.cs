using System;
using Crestron.SimplSharp;

namespace AndroidTvLib
{
    public class AndroidTvSimplModule
    {
        private readonly PairingClient _pairingClient = new PairingClient();
        private readonly AndroidTvCommandClient _cmdClient = new AndroidTvCommandClient();

        private string _ip = "";
        private ushort _pendingKeyCode;
        private bool _pairingStarted;

        // ===== DELEGATES (Sorties / Feedback vers SIMPL Windows) =====
        public delegate void BoolOutputSigDelegate(ushort value);
        public delegate void UShortOutputSigDelegate(ushort value);
        public delegate void StringOutputSigDelegate(SimplSharpString value);

        public BoolOutputSigDelegate Fb_Connected { get; set; }
        public BoolOutputSigDelegate Fb_PairingRequired { get; set; }
        public BoolOutputSigDelegate Fb_PowerOn { get; set; }
        public BoolOutputSigDelegate Fb_Error { get; set; }
        public StringOutputSigDelegate Fb_CurrentApp { get; set; }
        public StringOutputSigDelegate Fb_StatusText { get; set; }

        public AndroidTvSimplModule()
        {
            _cmdClient.Connected += () => Fb_Connected?.Invoke(1);
            _cmdClient.PowerStateChanged += isOn => Fb_PowerOn?.Invoke(isOn ? (ushort)1 : (ushort)0);
            _cmdClient.CurrentAppChanged += app => Fb_CurrentApp?.Invoke(app);
        }

        // ===== ENTREES (Digital-In depuis SIMPL Windows) =====
        public void Connect(ushort value)
        {
            if (value == 0) return;
            Fb_StatusText?.Invoke("Connexion en cours...");
            CrestronInvoke.BeginInvoke(_ => TryConnect());
        }

        public void Pair(ushort value)
        {
            if (value == 0) return;
            CrestronInvoke.BeginInvoke(_ => TryPair());
        }

        public void SendSelectedKey(ushort value)
        {
            if (value == 0) return;
            _cmdClient.SendKey(_pendingKeyCode);
        }

        public void LaunchApp(ushort value)
        {
            if (value == 0) return;
            _cmdClient.LaunchApp(_appLink);
        }

        public void VolumeUp(ushort value) { if (value != 0) _cmdClient.SendKey(24); }
        public void VolumeDown(ushort value) { if (value != 0) _cmdClient.SendKey(25); }
        public void Power(ushort value) { if (value != 0) _cmdClient.SendKey(26); }
        public void Home(ushort value) { if (value != 0) _cmdClient.SendKey(3); }
        public void Back(ushort value) { if (value != 0) _cmdClient.SendKey(4); }
        public void Up(ushort value) { if (value != 0) _cmdClient.SendKey(19); }
        public void Down(ushort value) { if (value != 0) _cmdClient.SendKey(20); }
        public void Left(ushort value) { if (value != 0) _cmdClient.SendKey(21); }
        public void Right(ushort value) { if (value != 0) _cmdClient.SendKey(22); }
        public void Select(ushort value) { if (value != 0) _cmdClient.SendKey(23); }

        // ===== ENTREES (Serial-In depuis SIMPL Windows) =====
        public void SetIpAddress(string value) { _ip = value; }

        private string _pairingCode = "";
        public void SetPairingCode(string value) { _pairingCode = value; }

        private string _appLink = "";
        public void SetAppLink(string value) { _appLink = value; }

        // ===== ENTREES (Analog-In depuis SIMPL Windows) =====
        public void SetKeyCode(ushort value) { _pendingKeyCode = value; }

        // ===== Logique interne =====
        private void TryConnect()
        {
            try
            {
                var cert = CertHelper.GetOrCreateClientCert();
                _cmdClient.Connect(_ip, cert);
                Fb_StatusText?.Invoke("Connecté");
            }
            catch (Exception ex)
            {
                Fb_Error?.Invoke(1);
                Fb_StatusText?.Invoke("Erreur: " + ex.Message);
            }
        }

        private void TryPair()
        {
            if (!_pairingStarted)
            {
                Fb_PairingRequired?.Invoke(1);
                bool ok = _pairingClient.StartPairing(_ip, out string err);

                if (ok)
                {
                    _pairingStarted = true;
                    Fb_StatusText?.Invoke("Code affiché sur le boîtier, saisissez-le puis relancez Pair");
                }
                else
                {
                    Fb_PairingRequired?.Invoke(0);
                    Fb_Error?.Invoke(1);
                    Fb_StatusText?.Invoke("Erreur: " + err);
                }
            }
            else
            {
                bool ok = _pairingClient.SubmitCode(_pairingCode, out string err);

                Fb_PairingRequired?.Invoke(0);
                _pairingStarted = false;

                if (ok)
                {
                    Fb_StatusText?.Invoke("Appairé");
                }
                else
                {
                    Fb_Error?.Invoke(1);
                    Fb_StatusText?.Invoke("Échec: " + err);
                }
            }
        }
    }
}