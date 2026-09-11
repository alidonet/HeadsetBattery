using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace HeadsetBat
{
    internal enum BluetoothMinor
    {
        WearableHeadset = 1,
        HandsFree = 2,
        Loudspeaker = 5,
        Headphones = 6,
        PortableAudio = 7
    }

    internal sealed class Headset
    {
        public string Name { get; set; }
        public Guid ContainerId { get; set; }
        public byte? BatteryPercent { get; set; }
        public bool IsCharging { get; set; }
        public IReadOnlyList<byte> BatteryLevels { get; private set; } = Array.Empty<byte>();

        public void SetBatteryLevels(IEnumerable<byte> levels)
        {
            var values = levels.Where(value => value <= 100).ToArray();
            if (values.Length == 0)
                return;

            BatteryLevels = values;
            BatteryPercent = values.Min();
        }

        public static bool IsHeadset(BluetoothMinor minor) =>
            minor == BluetoothMinor.WearableHeadset ||
            minor == BluetoothMinor.HandsFree ||
            minor == BluetoothMinor.Headphones ||
            minor == BluetoothMinor.PortableAudio;
    }

    internal static class BluetoothHeadsets
    {
        private static readonly string[] RequestedProperties =
        {
            "System.Devices.Aep.ContainerId",
            "System.Devices.BatteryLife",
            "System.Devices.ChargingState"
        };

        public static async Task<IReadOnlyList<Headset>> GetConnectedAsync()
        {
            var selector = BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
            var devices = await DeviceInformation.FindAllAsync(selector, RequestedProperties).AsTask();
            var result = new List<Headset>();

            foreach (var info in devices)
            {
                try
                {
                    using (var device = await BluetoothDevice.FromIdAsync(info.Id).AsTask())
                    {
                        if (device?.ClassOfDevice == null ||
                            device.ClassOfDevice.MajorClass != BluetoothMajorClass.AudioVideo ||
                            !Headset.IsHeadset((BluetoothMinor)device.ClassOfDevice.MinorClass) ||
                            !TryGetContainerId(info, out var containerId))
                            continue;

                        result.Add(new Headset
                        {
                            Name = string.IsNullOrWhiteSpace(info.Name) ? device.Name : info.Name,
                            ContainerId = containerId,
                            BatteryPercent = GetBattery(info),
                            IsCharging = IsCharging(info)
                        });
                    }
                }
                catch
                {
                    // A connected Bluetooth endpoint is not necessarily a BluetoothDevice object.
                }
            }

            ApplyHfpBattery(result);
            var batteryByContainer = await GetBleBatteryLevelsAsync(new HashSet<Guid>(result.Select(x => x.ContainerId)));
            foreach (var headset in result)
            {
                if (!batteryByContainer.TryGetValue(headset.ContainerId, out var battery))
                    continue;

                headset.IsCharging |= battery.IsCharging;
                if (battery.Levels.Count > 1 || !headset.BatteryPercent.HasValue)
                    headset.SetBatteryLevels(battery.Levels);
            }

            return result.OrderBy(x => x.Name).ToArray();
        }

        public static DeviceWatcher CreateConnectedDeviceWatcher()
        {
            var selector = BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
            return DeviceInformation.CreateWatcher(selector, RequestedProperties);
        }

        private static void ApplyHfpBattery(IEnumerable<Headset> headsets)
        {
            foreach (var battery in HfpBatteryReader.ReadConnected())
            {
                var headset = headsets.FirstOrDefault(x => IsSameHeadset(x.Name, battery.Name));
                if (headset == null)
                    continue;

                headset.ContainerId = battery.ContainerId;
                headset.BatteryPercent = battery.Percent;
            }
        }

        private static bool IsSameHeadset(string first, string second)
        {
            first = TrimHfpSuffix(first);
            second = TrimHfpSuffix(second);
            return first.Equals(second, StringComparison.OrdinalIgnoreCase) ||
                   first.StartsWith(second, StringComparison.OrdinalIgnoreCase) ||
                   second.StartsWith(first, StringComparison.OrdinalIgnoreCase);
        }

        private static string TrimHfpSuffix(string name)
        {
            const string suffix = " Hands-Free";
            var index = name.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? name.Substring(0, index).Trim() : name.Trim();
        }

        private static async Task<Dictionary<Guid, BleBatteryInfo>> GetBleBatteryLevelsAsync(ISet<Guid> requiredContainers)
        {
            var result = new Dictionary<Guid, BleBatteryInfo>();
            if (requiredContainers.Count == 0)
                return result;

            try
            {
                var selector = BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
                var devices = await DeviceInformation.FindAllAsync(selector, RequestedProperties).AsTask();
                foreach (var info in devices)
                {
                    if (!TryGetContainerId(info, out var containerId) || !requiredContainers.Contains(containerId))
                        continue;

                    if (!result.TryGetValue(containerId, out var battery))
                        result[containerId] = battery = new BleBatteryInfo();
                    battery.IsCharging |= IsCharging(info);

                    var levels = await ReadBleBatteryLevelsAsync(info.Id);
                    if (levels.Count == 0 && GetBattery(info).HasValue)
                        levels.Add(GetBattery(info).Value);
                    battery.Levels.AddRange(levels);
                }
            }
            catch
            {
                // Some classic devices do not expose a BLE endpoint; the system property is still used.
            }

            return result;
        }

        private sealed class BleBatteryInfo
        {
            public List<byte> Levels { get; } = new List<byte>();
            public bool IsCharging { get; set; }
        }

        private static async Task<List<byte>> ReadBleBatteryLevelsAsync(string deviceId)        {
            var levels = new List<byte>();
            try
            {
                using (var device = await BluetoothLEDevice.FromIdAsync(deviceId).AsTask())
                {
                    if (device == null)
                        return levels;

                    var services = await device.GetGattServicesForUuidAsync(GattServiceUuids.Battery).AsTask();
                    if (services.Status != GattCommunicationStatus.Success)
                        return levels;

                    foreach (var service in services.Services)
                    using (service)
                    {
                        var characteristics = await service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel).AsTask();
                        if (characteristics.Status != GattCommunicationStatus.Success)
                            continue;

                        foreach (var characteristic in characteristics.Characteristics)
                        {
                            var read = await characteristic.ReadValueAsync(BluetoothCacheMode.Cached).AsTask();
                            if (read.Status == GattCommunicationStatus.Success && read.Value.Length > 0)
                            {
                                var reader = DataReader.FromBuffer(read.Value);
                                var value = reader.ReadByte();
                                if (value <= 100)
                                    levels.Add(value);
                            }
                        }
                    }
                }
            }
            catch
            {
                // The device may not allow direct GATT access while used for audio.
            }

            return levels;
        }

        private static bool TryGetContainerId(DeviceInformation info, out Guid id)
        {
            id = Guid.Empty;
            return info.Properties.TryGetValue("System.Devices.Aep.ContainerId", out var value) && value is Guid containerId &&
                   (id = containerId) != Guid.Empty;
        }

        private static bool IsCharging(DeviceInformation info)
        {
            return info.Properties.TryGetValue("System.Devices.ChargingState", out var value) && value is byte state && state == 1;
        }

        private static byte? GetBattery(DeviceInformation info)        {
            return info.Properties.TryGetValue("System.Devices.BatteryLife", out var value) && value is byte battery && battery <= 100
                ? battery
                : (byte?)null;
        }
    }
}
