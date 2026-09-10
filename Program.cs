using System;
using System.Diagnostics;
using System.Linq;
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
                Debug.Assert(MenuText.For("ru").Exit == "Выход");
                Debug.Assert(MenuText.For("en").Settings == "Settings");
                Debug.Assert(AppSettings.GetStartupCommand(@"C:\Program Files\HeadsetBattery\HeadsetBattery.exe") == @"""C:\Program Files\HeadsetBattery\HeadsetBattery.exe""");
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

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApplicationContext());
        }
    }
}
