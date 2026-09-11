using Microsoft.Win32;
using System;
using System.Windows.Forms;

namespace HeadsetBat
{
    internal static class AppSettings
    {
        private const string SettingsKeyPath = @"Software\HeadsetBattery";
        private const string ShowBatteryPercentageValue = "ShowBatteryPercentage";
        private const string LowBatteryThresholdValue = "LowBatteryThreshold";
        private const string LowBatteryNotificationsValue = "LowBatteryNotifications";
        private const string LanguageValue = "Language";
        private const string TrayThemeValue = "TrayTheme";
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

        public static byte LoadLowBatteryThreshold()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath))
                {
                    var value = key?.GetValue(LowBatteryThresholdValue) as int?;
                    return value == 10 || value == 20 || value == 30 || value == 40 ? (byte)value : (byte)30;
                }
            }
            catch
            {
                return 30;
            }
        }

        public static void SaveLowBatteryThreshold(byte threshold)
        {
            if (threshold != 10 && threshold != 20 && threshold != 30 && threshold != 40)
                throw new ArgumentOutOfRangeException(nameof(threshold));

            using (var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath))
                key.SetValue(LowBatteryThresholdValue, threshold, RegistryValueKind.DWord);
        }

        public static bool LoadLowBatteryNotifications()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath))
                    return !(key?.GetValue(LowBatteryNotificationsValue) is int value) || value != 0;
            }
            catch
            {
                return true;
            }
        }

        public static void SaveLowBatteryNotifications(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath))
                key.SetValue(LowBatteryNotificationsValue, enabled ? 1 : 0, RegistryValueKind.DWord);
        }
        public static string LoadLanguage()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath))
                {
                    var language = key?.GetValue(LanguageValue) as string;
                    return IsSupportedLanguage(language) ? language : string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void SaveLanguage(string language)
        {
            if (!IsSupportedLanguage(language))
                throw new ArgumentOutOfRangeException(nameof(language));

            using (var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath))
                key.SetValue(LanguageValue, language, RegistryValueKind.String);
        }

        private static bool IsSupportedLanguage(string language) =>
            language == "en" || language == "ru" || language == "de" ||
            language == "es" || language == "fr" || language == "zh";

        public static TrayTheme LoadTrayTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath))
                {
                    var value = key?.GetValue(TrayThemeValue);
                    return value is int theme && theme == (int)TrayTheme.Light ? TrayTheme.Light :
                           value is int darkTheme && darkTheme == (int)TrayTheme.Dark ? TrayTheme.Dark : TrayTheme.Auto;
                }
            }
            catch
            {
                return TrayTheme.Auto;
            }
        }

        public static void SaveTrayTheme(TrayTheme theme)
        {
            if (theme != TrayTheme.Auto && theme != TrayTheme.Light && theme != TrayTheme.Dark)
                throw new ArgumentOutOfRangeException(nameof(theme));

            using (var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath))
                key.SetValue(TrayThemeValue, (int)theme, RegistryValueKind.DWord);
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
