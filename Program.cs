using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace HeadsetBat
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Contains("--self-test"))
            {
                Debug.Assert(Headset.IsHeadset(BluetoothMinor.Headphones));
                Debug.Assert(!Headset.IsHeadset(BluetoothMinor.Loudspeaker));
                AudioEndpoint.GetDefaultRender();
                Debug.Assert(MenuText.For("ru").Exit == "Закрыть");
                Debug.Assert(MenuText.For("en").Settings == "Settings");
                Debug.Assert(MenuText.For("pt").Settings == "Configurações");
                Debug.Assert(MenuText.For("ja").Settings == "設定");
                Debug.Assert(MenuText.For("ko").Settings == "설정");
                Debug.Assert(AppSettings.GetStartupCommand(@"C:\Program Files\HeadsetBattery\HeadsetBattery.exe") == @"""C:\Program Files\HeadsetBattery\HeadsetBattery.exe""");
                using (var audioWatcher = new AudioOutputWatcher())
                {
                    Debug.Assert(audioWatcher != null);
                }
                Debug.Assert(BluetoothHeadsets.CreateConnectedDeviceWatcher() != null);
                using (TrayIconFactory.CreateSpeaker())
                using (TrayIconFactory.CreateNoAudioOutput())
                using (TrayIconFactory.CreateHeadphones(100, false))
                using (TrayIconFactory.CreateHeadphones(60, false))
                using (TrayIconFactory.CreateHeadphones(30, false))
                using (TrayIconFactory.CreateHeadset(60, false))
                using (TrayIconFactory.CreateHeadset(100, true))
                {
                }
                foreach (var headset in HfpBatteryReader.ReadConnected())
                    Debug.Assert(headset.ContainerId != Guid.Empty && headset.Percent <= 100);
                return;
            }

            using (var instanceMutex = new Mutex(true, @"Local\HeadsetBattery", out var isFirstInstance))
            {
                if (!isFirstInstance)
                    return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApplicationContext());
            }
        }
    }
}
