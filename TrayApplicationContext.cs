using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Enumeration;

namespace HeadsetBat
{
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private const string BuildVersion = "1.0.2";
        private const string InactiveDeviceIndent = "\u00A0\u00A0\u00A0\u00A0";
        private static readonly TimeSpan LowBatteryNotificationCooldown = TimeSpan.FromMinutes(3);
        private static readonly Font VersionFont = new Font(SystemFonts.MenuFont, FontStyle.Bold);
        private static readonly Image LogoImage = LoadMenuImage("Logo_tiny.png");
        private static readonly Image SettingsImage = LoadMenuImage("settings-2.png");
        private static readonly Image UpdatesImage = LoadMenuImage("cloud-backup.png");
        private static readonly Image CloseImage = LoadMenuImage("x.png");
        private string language = ResolveLanguage(AppSettings.LoadLanguage());
        private MenuText Texts => MenuText.For(language);
        private readonly NotifyIcon trayIcon;
        private readonly ContextMenuStrip menu = new ContextMenuStrip { AutoClose = true, Renderer = new HeaderMenuRenderer() };
        private readonly Control dispatcher = new Control();
        private readonly Timer refreshTimer = new Timer { Interval = 60000 };
        private AudioOutputWatcher audioOutputWatcher;
        private DeviceWatcher bluetoothWatcher;
        private bool refreshing;
        private bool refreshRequested;
        private bool suppressNextMenuOpening;
        private bool showBatteryPercent = AppSettings.LoadShowBatteryPercentage();
        private bool startWithWindows = AppSettings.IsStartWithWindowsEnabled();
        private byte lowBatteryThreshold = AppSettings.LoadLowBatteryThreshold();
        private bool lowBatteryNotifications = AppSettings.LoadLowBatteryNotifications();
        private TrayTheme trayTheme = AppSettings.LoadTrayTheme();
        private readonly HashSet<Guid> connectedHeadsetIds = new HashSet<Guid>();
        private bool connectedHeadsetsKnown;
        private Guid notificationContainerId;
        private int? lastNotificationMilestone;
        private DateTime lastNotificationUtc = DateTime.MinValue;
        private IReadOnlyList<Headset> headsets = Array.Empty<Headset>();

        public TrayApplicationContext()
        {
            TrayIconFactory.Theme = trayTheme;
            trayIcon = new NotifyIcon
            {
                ContextMenuStrip = menu,
                Text = "Headset Battery",
                Visible = true,
                Icon = TrayIconFactory.CreateSpeaker()
            };
            dispatcher.CreateControl();
            menu.Opening += (_, args) =>
            {
                if (!suppressNextMenuOpening)
                    return;

                suppressNextMenuOpening = false;
                args.Cancel = true;
            };
            trayIcon.MouseDown += (_, args) =>
            {
                if (args.Button == MouseButtons.Right && menu.Visible)
                {
                    suppressNextMenuOpening = true;
                    menu.Close();
                }
            };
            trayIcon.MouseUp += (_, args) =>
            {
                if (args.Button != MouseButtons.Left)
                    return;

                if (menu.Visible)
                    menu.Close();
                else
                    menu.Show(Cursor.Position);
            };
            refreshTimer.Tick += async (_, __) => await RefreshAsync();
            StartSystemWatchers();
            refreshTimer.Start();
            _ = RefreshAsync();
        }

        private static Image LoadMenuImage(string name)
        {
            using (var stream = typeof(TrayApplicationContext).Assembly.GetManifestResourceStream("HeadsetBat.Assets." + name))
            using (var image = Image.FromStream(stream))
                return new Bitmap(image);
        }

        private async Task RefreshAsync()        {
            if (refreshing)
            {
                refreshRequested = true;
                return;
            }

            do
            {
                refreshRequested = false;
                refreshing = true;
                AudioOutput output = null;
                var audioOutputKnown = false;
                try
                {
                    output = AudioEndpoint.GetDefaultRender();
                    audioOutputKnown = true;
                    headsets = await BluetoothHeadsets.GetConnectedAsync();
                    NotifyConnectedHeadsets();
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
            while (refreshRequested);
        }

        private void StartSystemWatchers()
        {
            try
            {
                audioOutputWatcher = new AudioOutputWatcher();
                audioOutputWatcher.DefaultRenderChanged += RequestRefresh;
            }
            catch
            {
                audioOutputWatcher?.Dispose();
                audioOutputWatcher = null;
            }

            try
            {
                bluetoothWatcher = BluetoothHeadsets.CreateConnectedDeviceWatcher();
                bluetoothWatcher.Added += OnBluetoothDeviceAdded;
                bluetoothWatcher.Updated += OnBluetoothDeviceUpdated;
                bluetoothWatcher.Removed += OnBluetoothDeviceRemoved;
                bluetoothWatcher.Start();
            }
            catch
            {
                StopBluetoothWatcher();
            }
        }

        private void RequestRefresh()
        {
            if (dispatcher.IsDisposed || !dispatcher.IsHandleCreated)
                return;

            try
            {
                dispatcher.BeginInvoke((Action)(() => _ = RefreshAsync()));
            }
            catch (InvalidOperationException)
            {
                // The application is shutting down.
            }
        }

        private void OnBluetoothDeviceAdded(DeviceWatcher sender, DeviceInformation args) => RequestRefresh();
        private void OnBluetoothDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate args) => RequestRefresh();
        private void OnBluetoothDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args) => RequestRefresh();

        private void StopBluetoothWatcher()
        {
            if (bluetoothWatcher == null)
                return;

            bluetoothWatcher.Added -= OnBluetoothDeviceAdded;
            bluetoothWatcher.Updated -= OnBluetoothDeviceUpdated;
            bluetoothWatcher.Removed -= OnBluetoothDeviceRemoved;
            if (bluetoothWatcher.Status == DeviceWatcherStatus.Started || bluetoothWatcher.Status == DeviceWatcherStatus.EnumerationCompleted)
                bluetoothWatcher.Stop();
            bluetoothWatcher = null;
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
                    ? TrayIconFactory.CreateHeadset(activeHeadset.BatteryPercent, showBatteryPercent, lowBatteryThreshold, activeHeadset.IsCharging)
                    : TrayIconFactory.CreateHeadphones(activeHeadset.BatteryPercent, showBatteryPercent, lowBatteryThreshold, activeHeadset.IsCharging);
            oldIcon?.Dispose();

            if (output == null)
                SetToolTip("No audio output device");
            else
                SetToolTip(activeHeadset == null
                    ? string.IsNullOrWhiteSpace(output.Name) ? "Speakers" : output.Name
                    : $"{(output.IsHandsFree ? "Headset" : "Headphones")}: {activeHeadset.Name}" +
                      (activeHeadset.BatteryPercent.HasValue ? $" — {Texts.Battery}: {activeHeadset.BatteryPercent}%" : string.Empty) + (activeHeadset.IsCharging ? ", " + Texts.Charging : string.Empty));

            UpdateLowBatteryNotification(activeHeadset);
        }

        private void UpdateLowBatteryNotification(Headset activeHeadset)
        {
            if (activeHeadset == null || activeHeadset.ContainerId == Guid.Empty)
            {
                ResetLowBatteryNotification();
                return;
            }

            if (notificationContainerId != activeHeadset.ContainerId)
            {
                notificationContainerId = activeHeadset.ContainerId;
                lastNotificationMilestone = null;
                lastNotificationUtc = DateTime.MinValue;
            }

            if (!lowBatteryNotifications || activeHeadset.IsCharging || !activeHeadset.BatteryPercent.HasValue)
            {
                if (activeHeadset.IsCharging)
                    lastNotificationMilestone = null;
                return;
            }

            var battery = activeHeadset.BatteryPercent.Value;
            if (battery > lowBatteryThreshold)
            {
                lastNotificationMilestone = null;
                return;
            }

            var milestone = lowBatteryThreshold - (lowBatteryThreshold - battery) / 10 * 10;
            if (milestone < 10 ||
                lastNotificationMilestone.HasValue && milestone >= lastNotificationMilestone.Value ||
                DateTime.UtcNow - lastNotificationUtc < LowBatteryNotificationCooldown)
                return;

            ShowBatteryNotification(activeHeadset, ToolTipIcon.Warning);
            lastNotificationMilestone = milestone;
            lastNotificationUtc = DateTime.UtcNow;
        }

        private void NotifyConnectedHeadsets()
        {
            var current = new HashSet<Guid>(headsets.Where(x => x.ContainerId != Guid.Empty).Select(x => x.ContainerId));
            if (connectedHeadsetsKnown && lowBatteryNotifications)
                foreach (var headset in headsets.Where(x => current.Contains(x.ContainerId) && !connectedHeadsetIds.Contains(x.ContainerId) && !x.IsCharging))
                    ShowConnectionNotification(headset);

            connectedHeadsetIds.Clear();
            connectedHeadsetIds.UnionWith(current);
            connectedHeadsetsKnown = true;
        }

        private void ShowBatteryNotification(Headset headset, ToolTipIcon icon)
        {
            if (headset?.BatteryPercent.HasValue != true)
                return;

            trayIcon.ShowBalloonTip(5000, string.Empty, $"{headset.Name} — {Texts.Battery}: {headset.BatteryPercent}%", icon);
        }

        private void ShowConnectionNotification(Headset headset)        {
            var battery = headset.BatteryPercent.HasValue ? $" — {Texts.Battery}: {headset.BatteryPercent}%" : string.Empty;
            trayIcon.ShowBalloonTip(5000, string.Empty, $"{headset.Name} — {Texts.Connected}{battery}", ToolTipIcon.Info);
        }

        private void ResetLowBatteryNotification()        {
            notificationContainerId = Guid.Empty;
            lastNotificationMilestone = null;
            lastNotificationUtc = DateTime.MinValue;
        }
        private void SetToolTip(string text)
        {
            text = text.Replace('\r', ' ').Replace('\n', ' ');
            trayIcon.Text = text.Length <= 63 ? text : text.Substring(0, 63);
        }

        private static string ResolveLanguage(string savedLanguage)
        {
            if (!string.IsNullOrEmpty(savedLanguage))
                return savedLanguage;

            switch (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
            {
                case "ru":
                case "de":
                case "es":
                case "fr":
                case "zh": return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                default: return "en";
            }
        }

        private string GetLanguageName(string code)        {
            switch (code)
            {
                case "en": return "English";
                case "ru": return "Русский";
                case "de": return "Deutsch";
                case "es": return "Español";
                case "fr": return "Français";
                case "zh": return "中文";
                default: return "English";
            }
        }

        private string FormatBatteryLevels(Headset headset)
        {
            if (headset.BatteryLevels.Count <= 1)
                return headset.BatteryPercent + "%" + (headset.IsCharging ? ", " + Texts.Charging : string.Empty);

            return string.Join(", ", headset.BatteryLevels.Select((value, index) => $"{Texts.Battery} {index + 1}: {value}%")) + (headset.IsCharging ? ", " + Texts.Charging : string.Empty);
        }
        private void RebuildMenu(Headset activeHeadset, AudioOutput output)
        {
            menu.Items.Clear();
            menu.Items.Add(new HeaderMenuItem("Headset Battery Monitor, v. " + BuildVersion) { Image = LogoImage, ImageScaling = ToolStripItemImageScaling.None, Font = VersionFont });
            menu.Items.Add(new ToolStripSeparator());
            if (headsets.Count == 0)
            {
                menu.Items.Add(new ToolStripLabel(Texts.NoConnectedHeadphones) { ForeColor = Color.Black, Padding = new Padding(0, 2, 0, 2) });
            }
            else
            {
                foreach (var headset in headsets)
                {
                    var text = headset.Name + (headset.BatteryPercent.HasValue ? " — " + FormatBatteryLevels(headset) : " — " + Texts.NoBatteryInfo);
                    var isActive = headset == activeHeadset;
                    menu.Items.Add(new ToolStripLabel((isActive ? "● " : InactiveDeviceIndent) + text)
                    {
                        ForeColor = Color.Black,
                        Padding = new Padding(0, 2, 0, 2)
                    });
                }
            }

            menu.Items.Add(new ToolStripSeparator());
            var indicatorItem = new ToolStripMenuItem(Texts.IconIndicator);
            var colorFillItem = new ToolStripMenuItem(Texts.ColorFill) { Checked = !showBatteryPercent };
            var percentageBadgeItem = new ToolStripMenuItem(Texts.PercentageBadge) { Checked = showBatteryPercent };
            Action<bool> selectIndicator = showPercentage =>
            {
                if (showPercentage == showBatteryPercent)
                    return;

                try
                {
                    AppSettings.SaveShowBatteryPercentage(showPercentage);
                    showBatteryPercent = showPercentage;
                    colorFillItem.Checked = !showPercentage;
                    percentageBadgeItem.Checked = showPercentage;
                    SetIcon(activeHeadset, output);
                }
                catch (Exception error)
                {
                    SetToolTip("Headset Battery: " + error.Message);
                }
            };
            colorFillItem.Click += (_, __) => selectIndicator(false);
            percentageBadgeItem.Click += (_, __) => selectIndicator(true);
            indicatorItem.DropDownItems.Add(colorFillItem);
            indicatorItem.DropDownItems.Add(percentageBadgeItem);
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
            var thresholdItem = new ToolStripMenuItem(Texts.LowBatteryThreshold);
            var thresholdItems = new List<ToolStripMenuItem>();
            foreach (var threshold in new byte[] { 40, 30, 20, 10 })
            {
                var value = threshold;
                var item = new ToolStripMenuItem(value + "%") { CheckOnClick = false, Checked = value == lowBatteryThreshold };
                item.Click += (_, __) =>
                {
                    if (value == lowBatteryThreshold)
                        return;

                    try
                    {
                        AppSettings.SaveLowBatteryThreshold(value);
                        lowBatteryThreshold = value;
                        foreach (var thresholdOption in thresholdItems)
                            thresholdOption.Checked = thresholdOption == item;
                        ResetLowBatteryNotification();
                        SetIcon(activeHeadset, output);
                    }
                    catch (Exception error)
                    {
                        SetToolTip("Headset Battery: " + error.Message);
                    }
                };
                thresholdItems.Add(item);
                thresholdItem.DropDownItems.Add(item);
            }

            var lowBatteryNotificationsItem = new ToolStripMenuItem(Texts.NotifyBatteryLevel) { CheckOnClick = true, Checked = lowBatteryNotifications };
            lowBatteryNotificationsItem.CheckedChanged += (_, __) =>
            {
                if (lowBatteryNotificationsItem.Checked == lowBatteryNotifications)
                    return;

                try
                {
                    AppSettings.SaveLowBatteryNotifications(lowBatteryNotificationsItem.Checked);
                    lowBatteryNotifications = lowBatteryNotificationsItem.Checked;
                    ResetLowBatteryNotification();
                    UpdateLowBatteryNotification(activeHeadset);
                }
                catch (Exception error)
                {
                    lowBatteryNotificationsItem.Checked = lowBatteryNotifications;
                    SetToolTip("Headset Battery: " + error.Message);
                }
            };

            var themeItem = new ToolStripMenuItem(Texts.Theme);
            var themeItems = new List<ToolStripMenuItem>();
            foreach (var option in new[] { TrayTheme.Auto, TrayTheme.Light, TrayTheme.Dark })
            {
                var value = option;
                var item = new ToolStripMenuItem(value == TrayTheme.Auto ? Texts.Auto : value == TrayTheme.Light ? Texts.Light : Texts.Dark) { Checked = value == trayTheme };
                item.Click += (_, __) =>
                {
                    if (value == trayTheme)
                        return;
                    AppSettings.SaveTrayTheme(value);
                    trayTheme = value;
                    TrayIconFactory.Theme = value;
                    foreach (var themeOption in themeItems)
                        themeOption.Checked = themeOption == item;
                    SetIcon(activeHeadset, output);
                };
                themeItems.Add(item);
                themeItem.DropDownItems.Add(item);
            }

            var testNotificationItem = new ToolStripMenuItem(Texts.TestNotification, null, (_, __) => ShowBatteryNotification(activeHeadset, ToolTipIcon.Info))
            { Enabled = activeHeadset?.BatteryPercent.HasValue == true };

            var languageItem = new ToolStripMenuItem(Texts.Language);
            foreach (var code in new[] { "en", "ru", "de", "es", "fr", "zh" })
            {
                var value = code;
                var item = new ToolStripMenuItem(GetLanguageName(value)) { Checked = value == language };
                item.Click += (_, __) =>
                {
                    if (value == language)
                        return;

                    try
                    {
                        var location = menu.Bounds.Location;
                        AppSettings.SaveLanguage(value);
                        language = value;
                        SetIcon(activeHeadset, output);
                        menu.Close();
                        RebuildMenu(activeHeadset, output);
dispatcher.BeginInvoke((Action)(() =>
                        {
                            menu.Show(location);
                            var settings = menu.Items.OfType<ToolStripMenuItem>().First(x => x.Text == Texts.Settings);
                            settings.ShowDropDown();
                            settings.DropDownItems.OfType<ToolStripMenuItem>().First(x => x.Text == Texts.Language).ShowDropDown();
                        }));
                    }
                    catch (Exception error)
                    {
                        SetToolTip("Headset Battery: " + error.Message);
                    }
                };
                languageItem.DropDownItems.Add(item);
            }

            var settingsItem = new ToolStripMenuItem(Texts.Settings) { Image = SettingsImage, ImageScaling = ToolStripItemImageScaling.None };
            settingsItem.DropDownItems.Add(themeItem);
            settingsItem.DropDownItems.Add(indicatorItem);
            settingsItem.DropDownItems.Add(new ToolStripSeparator());
            settingsItem.DropDownItems.Add(thresholdItem);
            settingsItem.DropDownItems.Add(lowBatteryNotificationsItem);
            settingsItem.DropDownItems.Add(testNotificationItem);
            settingsItem.DropDownItems.Add(new ToolStripSeparator());
            settingsItem.DropDownItems.Add(startWithWindowsItem);
            settingsItem.DropDownItems.Add(languageItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(new ToolStripMenuItem(Texts.CheckForUpdates, UpdatesImage, (_, __) => Process.Start(new ProcessStartInfo("https://github.com/alidonet/HeadsetBattery/releases/latest") { UseShellExecute = true })) { ImageScaling = ToolStripItemImageScaling.None });
            menu.Items.Add(new ToolStripMenuItem(Texts.Exit, CloseImage, (_, __) => ExitThread()) { ImageScaling = ToolStripItemImageScaling.None });
        }

        protected override void ExitThreadCore()
        {
            refreshTimer.Stop();
            StopBluetoothWatcher();
            audioOutputWatcher?.Dispose();
            dispatcher.Dispose();
            trayIcon.Visible = false;
            trayIcon.Icon?.Dispose();
            trayIcon.Dispose();
            menu.Dispose();
            refreshTimer.Dispose();
            base.ExitThreadCore();
        }
    }

    internal sealed class HeaderMenuItem : ToolStripMenuItem
    {
        public HeaderMenuItem(string text) : base(text)
        {
        }

        protected override void OnClick(EventArgs e)
        {
        }
    }

    internal sealed class HeaderMenuRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item is HeaderMenuItem)
            {
                e.Graphics.FillRectangle(SystemBrushes.Menu, e.Item.Bounds);
                return;
            }

            base.OnRenderMenuItemBackground(e);
        }
    }

    internal sealed class MenuText    {
        private MenuText(string settings, string startWithWindows, string lowBatteryThreshold, string notifyLowBattery, string noConnectedHeadphones, string noBatteryInfo, string battery, string exit, string language, string iconIndicator, string colorFill, string percentageBadge, string notifyBatteryLevel, string testNotification, string charging, string connected, string theme, string auto, string light, string dark, string checkForUpdates)
        {
            Settings = settings;
            StartWithWindows = startWithWindows;
            LowBatteryThreshold = lowBatteryThreshold;
            NotifyLowBattery = notifyLowBattery;
            NoConnectedHeadphones = noConnectedHeadphones;
            NoBatteryInfo = noBatteryInfo;
            Battery = battery;
            Exit = exit;
            Language = language;
            IconIndicator = iconIndicator;
            ColorFill = colorFill;
            PercentageBadge = percentageBadge;
            NotifyBatteryLevel = notifyBatteryLevel;
            TestNotification = testNotification;
            Charging = charging;
            Connected = connected;
            Theme = theme;
            Auto = auto;
            Light = light;
            Dark = dark;
            CheckForUpdates = checkForUpdates;
        }

        public string Settings { get; }
        public string StartWithWindows { get; }
        public string LowBatteryThreshold { get; }
        public string NotifyLowBattery { get; }
        public string NoConnectedHeadphones { get; }
        public string NoBatteryInfo { get; }
        public string Battery { get; }
        public string Exit { get; }
        public string Language { get; }
        public string IconIndicator { get; }
        public string ColorFill { get; }
        public string PercentageBadge { get; }
        public string NotifyBatteryLevel { get; }
        public string TestNotification { get; }
        public string Charging { get; }
        public string Connected { get; }
        public string Theme { get; }
        public string Auto { get; }
        public string Light { get; }
        public string Dark { get; }
        public string CheckForUpdates { get; }

        public static MenuText For(string language)
        {
            switch (language)
            {
                case "ru": return new MenuText("Настройки", "Запускать вместе с Windows", "Порог низкого заряда", "Уведомлять о низком заряде", "Подключенные наушники не найдены", "нет данных о заряде", "Заряд", "Закрыть", "Язык", "Вид индикатора", "Заливка цветом", "Проценты на значке", "Уведомлять об уровне заряда", "Тестовое уведомление", "зарядка", "Подключено", "Тема", "Авто", "Светлый трей", "Тёмный трей", "Проверить обновления");
                case "de": return new MenuText("Einstellungen", "Mit Windows starten", "Niedriger Akkustand", "Bei niedrigem Akkustand benachrichtigen", "Keine verbundenen Kopfhörer gefunden", "keine Akkuinformationen", "Akku", "Schließen", "Sprache", "Anzeige im Symbol", "Farbfüllung", "Prozentanzeige", "Über Akkustand benachrichtigen", "Testbenachrichtigung", "Wird geladen", "Verbunden", "Design", "Auto", "Heller Infobereich", "Dunkler Infobereich", "Nach Updates suchen");
                case "es": return new MenuText("Configuración", "Iniciar con Windows", "Umbral de batería baja", "Notificar batería baja", "No se encontraron auriculares conectados", "sin información de batería", "Batería", "Cerrar", "Idioma", "Indicador del icono", "Relleno de color", "Porcentaje en el icono", "Notificar nivel de batería", "Notificación de prueba", "cargando", "Conectado", "Tema", "Auto", "Bandeja clara", "Bandeja oscura", "Buscar actualizaciones");
                case "fr": return new MenuText("Paramètres", "Démarrer avec Windows", "Seuil de batterie faible", "Notifier en cas de batterie faible", "Aucun casque connecté trouvé", "aucune information sur la batterie", "Batterie", "Fermer", "Langue", "Indicateur d'icône", "Remplissage en couleur", "Pourcentage sur l'icône", "Notifier le niveau de batterie", "Notification de test", "en charge", "Connecté", "Thème", "Auto", "Zone claire", "Zone sombre", "Rechercher des mises à jour");
                case "zh": return new MenuText("设置", "随 Windows 启动", "低电量阈值", "低电量时通知", "未找到已连接的耳机", "无电池信息", "电量", "关闭", "语言", "图标指示器", "颜色填充", "图标上的百分比", "通知电池电量", "测试通知", "充电中", "已连接", "主题", "自动", "浅色托盘", "深色托盘", "检查更新");
                default: return new MenuText("Settings", "Start with Windows", "Low battery threshold", "Notify when battery is low", "Connected headphones not found", "no battery info", "Battery", "Close", "Language", "Icon indicator", "Color fill", "Percentage badge", "Notify battery level", "Test notification", "charging", "Connected", "Theme", "Auto", "Light tray", "Dark tray", "Check for updates");
            }
        }
    }
}
