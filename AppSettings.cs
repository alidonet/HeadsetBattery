using Microsoft.Win32;
using System;
using System.Windows.Forms;

namespace HeadsetBat
{
    internal static class AppSettings
    {
        private const string SettingsKeyPath = @"Software\HeadsetBattery";
        private const string ShowBatteryPercentageValue = "ShowBatteryPercentage";
        private const string StartupKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupValue = "HeadsetBattery";

        public static bool LoadShowBatteryPercentage()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath))
                    return key?.GetValue(ShowBatteryPercentageValue) is int value && value != 0;
            }
            catch
            {
                return false;
            }
        }

        public static void SaveShowBatteryPercentage(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath))
                key.SetValue(ShowBatteryPercentageValue, enabled ? 1 : 0, RegistryValueKind.DWord);
        }

        public static bool IsStartWithWindowsEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath))
                    return key?.GetValue(StartupValue) is string value && !string.IsNullOrWhiteSpace(value);
            }
            catch
            {
                return false;
            }
        }

        public static void SetStartWithWindows(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(StartupKeyPath))
            {
                if (enabled)
                    key.SetValue(StartupValue, GetStartupCommand(Application.ExecutablePath), RegistryValueKind.String);
                else
                    key.DeleteValue(StartupValue, false);
            }
        }

        internal static string GetStartupCommand(string executablePath) => "\"" + executablePath + "\"";
    }
}
