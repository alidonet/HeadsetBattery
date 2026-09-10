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
            "System.Devices.BatteryLife"
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
                            BatteryPercent = GetBattery(info)
                        });
                    }
                }
                catch
                {
                    // A connected Bluetooth endpoint is not necessarily a BluetoothDevice object.
                }
            }

            ApplyHfpBattery(result);
            var missingBattery = new HashSet<Guid>(result.Where(x => !x.BatteryPercent.HasValue).Select(x => x.ContainerId));
            var batteryByContainer = await GetBleBatteryFallbackAsync(missingBattery);
            foreach (var headset in result.Where(x => !x.BatteryPercent.HasValue))
                if (batteryByContainer.TryGetValue(headset.ContainerId, out var battery))
                    headset.BatteryPercent = battery;

            return result.OrderBy(x => x.Name).ToArray();
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

        private static async Task<Dictionary<Guid, byte>> GetBleBatteryFallbackAsync(ISet<Guid> requiredContainers)
        {
            var result = new Dictionary<Guid, byte>();
            if (requiredContainers.Count == 0)
                return result;

            try
            {
                var selector = BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
                var devices = await DeviceInformation.FindAllAsync(selector, RequestedProperties).AsTask();
                foreach (var info in devices)
                {
                    if (TryGetContainerId(info, out var containerId) && requiredContainers.Contains(containerId))
                    {
                        var value = GetBattery(info) ?? await ReadBleBatteryAsync(info.Id);
                        if (value.HasValue)
                            result[containerId] = value.Value;
                    }
                }
            }
            catch
            {
                // Some classic devices do not expose a BLE endpoint; the system property is still used.
            }

            return result;
        }

        private static async Task<byte?> ReadBleBatteryAsync(string deviceId)
        {
            try
            {
                using (var device = await BluetoothLEDevice.FromIdAsync(deviceId).AsTask())
                {
                    if (device == null)
                        return null;

                    var services = await device.GetGattServicesForUuidAsync(GattServiceUuids.Battery).AsTask();
                    if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
                        return null;

                    using (var service = services.Services[0])
                    {
                        var characteristics = await service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel).AsTask();
                        if (characteristics.Status != GattCommunicationStatus.Success || characteristics.Characteristics.Count == 0)
                            return null;

                        var read = await characteristics.Characteristics[0].ReadValueAsync(BluetoothCacheMode.Cached).AsTask();
                        if (read.Status != GattCommunicationStatus.Success || read.Value.Length == 0)
                            return null;

                        var reader = DataReader.FromBuffer(read.Value);
                        return reader.ReadByte();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetContainerId(DeviceInformation info, out Guid id)
        {
            id = Guid.Empty;
            return info.Properties.TryGetValue("System.Devices.Aep.ContainerId", out var value) && value is Guid containerId &&
                   (id = containerId) != Guid.Empty;
        }

        private static byte? GetBattery(DeviceInformation info)
        {
            return info.Properties.TryGetValue("System.Devices.BatteryLife", out var value) && value is byte battery && battery <= 100
                ? battery
                : (byte?)null;
        }
    }
}
