using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HeadsetBat
{
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private const string BuildVersion = "260911";
        private const string InactiveDeviceIndent = "\u00A0\u00A0\u00A0\u00A0";
        private static readonly Font VersionFont = new Font(SystemFonts.MenuFont, FontStyle.Bold);
        private static readonly MenuText Texts = MenuText.For(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        private readonly NotifyIcon trayIcon;
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly Timer refreshTimer = new Timer { Interval = 10000 };
        private bool refreshing;
        private bool showBatteryPercent = AppSettings.LoadShowBatteryPercentage();
        private bool startWithWindows = AppSettings.IsStartWithWindowsEnabled();
        private IReadOnlyList<Headset> headsets = Array.Empty<Headset>();

        public TrayApplicationContext()
        {
            trayIcon = new NotifyIcon
            {
                ContextMenuStrip = menu,
                Text = "Headset Battery",
                Visible = true,
                Icon = TrayIconFactory.CreateSpeaker()
            };
            menu.Opening += async (_, __) => await RefreshAsync();
            refreshTimer.Tick += async (_, __) => await RefreshAsync();
            refreshTimer.Start();
            _ = RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            if (refreshing)
                return;

            refreshing = true;
            AudioOutput output = null;
            var audioOutputKnown = false;
            try
            {
                output = AudioEndpoint.GetDefaultRender();
                audioOutputKnown = true;
                headsets = await BluetoothHeadsets.GetConnectedAsync();
                var activeHeadset = output == null ? null : headsets.FirstOrDefault(x => x.ContainerId == output.ContainerId);
                SetIcon(activeHeadset, output);
                RebuildMenu(activeHeadset, output);
            }
            catch (Exception error)
            {
                if (audioOutputKnown)
                    SetIcon(null, output);
                SetToolTip("Headset Battery: " + error.Message);
                RebuildMenu(null, output);
            }
            finally
            {
                refreshing = false;
            }
        }

        private void SetIcon(Headset activeHeadset, AudioOutput output)
        {
            var oldIcon = trayIcon.Icon;
            if (output == null)
                trayIcon.Icon = TrayIconFactory.CreateNoAudioOutput();
            else if (activeHeadset == null)
                trayIcon.Icon = TrayIconFactory.CreateSpeaker();
            else
                trayIcon.Icon = output.IsHandsFree
                    ? TrayIconFactory.CreateHeadset(activeHeadset.BatteryPercent, showBatteryPercent)
                    : TrayIconFactory.CreateHeadphones(activeHeadset.BatteryPercent, showBatteryPercent);
            oldIcon?.Dispose();

            if (output == null)
                SetToolTip("No audio output device");
            else
                SetToolTip(activeHeadset == null
                    ? string.IsNullOrWhiteSpace(output.Name) ? "Speakers" : output.Name
                    : $"{(output.IsHandsFree ? "Headset" : "Headphones")}: {activeHeadset.Name}" +
                      (activeHeadset.BatteryPercent.HasValue ? $" — {Texts.Battery}: {activeHeadset.BatteryPercent}%" : string.Empty));
        }

        private void SetToolTip(string text)
        {
            text = text.Replace('\r', ' ').Replace('\n', ' ');
            trayIcon.Text = text.Length <= 63 ? text : text.Substring(0, 63);
        }

        private void RebuildMenu(Headset activeHeadset, AudioOutput output)
        {
            menu.Items.Clear();
            menu.Items.Add(new ToolStripLabel("Headset Battery Monitor, v. " + BuildVersion) { Enabled = false, Font = VersionFont });
            menu.Items.Add(new ToolStripSeparator());
            if (headsets.Count == 0)
            {
                menu.Items.Add(new ToolStripLabel(Texts.NoConnectedHeadphones) { ForeColor = Color.Black, Padding = new Padding(0, 2, 0, 2) });
            }
            else
            {
                foreach (var headset in headsets)
                {
                    var text = headset.Name + (headset.BatteryPercent.HasValue ? $" — {headset.BatteryPercent}%" : " — " + Texts.NoBatteryInfo);
                    var isActive = headset == activeHeadset;
                    menu.Items.Add(new ToolStripLabel((isActive ? "● " : InactiveDeviceIndent) + text)
                    {
                        ForeColor = Color.Black,
                        Padding = new Padding(0, 2, 0, 2)
                    });
                }
            }

            menu.Items.Add(new ToolStripSeparator());
            var showPercentItem = new ToolStripMenuItem(Texts.ShowBatteryPercentage) { CheckOnClick = true, Checked = showBatteryPercent };
            showPercentItem.CheckedChanged += (_, __) =>
            {
                if (showPercentItem.Checked == showBatteryPercent)
                    return;

                try
                {
                    AppSettings.SaveShowBatteryPercentage(showPercentItem.Checked);
                    showBatteryPercent = showPercentItem.Checked;
                    SetIcon(activeHeadset, output);
                }
                catch (Exception error)
                {
                    showPercentItem.Checked = showBatteryPercent;
                    SetToolTip("Headset Battery: " + error.Message);
                }
            };
            var startWithWindowsItem = new ToolStripMenuItem(Texts.StartWithWindows) { CheckOnClick = true, Checked = startWithWindows };
            startWithWindowsItem.CheckedChanged += (_, __) =>
            {
                if (startWithWindowsItem.Checked == startWithWindows)
                    return;

                try
                {
                    AppSettings.SetStartWithWindows(startWithWindowsItem.Checked);
                    startWithWindows = startWithWindowsItem.Checked;
                }
                catch (Exception error)
                {
                    startWithWindowsItem.Checked = startWithWindows;
                    SetToolTip("Headset Battery: " + error.Message);
                }
            };
            var settingsItem = new ToolStripMenuItem(Texts.Settings);
            settingsItem.DropDownItems.Add(showPercentItem);
            settingsItem.DropDownItems.Add(startWithWindowsItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(new ToolStripMenuItem(Texts.Exit, null, (_, __) => ExitThread()));
        }

        protected override void ExitThreadCore()
        {
            refreshTimer.Stop();
            trayIcon.Visible = false;
            trayIcon.Icon?.Dispose();
            trayIcon.Dispose();
            menu.Dispose();
            refreshTimer.Dispose();
            base.ExitThreadCore();
        }
    }

    internal sealed class MenuText
    {
        private MenuText(string settings, string showBatteryPercentage, string startWithWindows, string noConnectedHeadphones, string noBatteryInfo, string battery, string exit)
        {
            Settings = settings;
            ShowBatteryPercentage = showBatteryPercentage;
            StartWithWindows = startWithWindows;
            NoConnectedHeadphones = noConnectedHeadphones;
            NoBatteryInfo = noBatteryInfo;
            Battery = battery;
            Exit = exit;
        }

        public string Settings { get; }
        public string ShowBatteryPercentage { get; }
        public string StartWithWindows { get; }
        public string NoConnectedHeadphones { get; }
        public string NoBatteryInfo { get; }
        public string Battery { get; }
        public string Exit { get; }

        public static MenuText For(string language)
        {
            switch (language)
            {
                case "ru": return new MenuText("Настройки", "Показывать проценты заряда", "Запускать вместе с Windows", "Подключенные наушники не найдены", "нет данных о заряде", "Заряд", "Выход");
                case "de": return new MenuText("Einstellungen", "Akkuladung in Prozent anzeigen", "Mit Windows starten", "Keine verbundenen Kopfhörer gefunden", "keine Akkuinformationen", "Akku", "Beenden");
                case "es": return new MenuText("Configuración", "Mostrar porcentaje de batería", "Iniciar con Windows", "No se encontraron auriculares conectados", "sin información de batería", "Batería", "Salir");
                case "fr": return new MenuText("Paramètres", "Afficher le pourcentage de batterie", "Démarrer avec Windows", "Aucun casque connecté trouvé", "aucune information sur la batterie", "Batterie", "Quitter");
                case "zh": return new MenuText("设置", "显示电池百分比", "随 Windows 启动", "未找到已连接的耳机", "无电池信息", "电量", "退出");
                default: return new MenuText("Settings", "Show battery percentage", "Start with Windows", "Connected headphones not found", "no battery info", "Battery", "Exit");
            }
        }
    }
}
