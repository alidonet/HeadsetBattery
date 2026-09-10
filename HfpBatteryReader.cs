using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace HeadsetBat
{
    internal sealed class HfpBattery
    {
        public string Name { get; set; }
        public Guid ContainerId { get; set; }
        public byte Percent { get; set; }
    }

    internal static class HfpBatteryReader
    {
        private const uint DigcfPresent = 0x2;
        private const uint DigcfAllClasses = 0x4;
        private const int ErrorNoMoreItems = 259;
        private const int ErrorInsufficientBuffer = 122;
        private static readonly IntPtr InvalidHandle = new IntPtr(-1);
        private static readonly PropertyKey BatteryLevel = new PropertyKey
        {
            FormatId = new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"),
            PropertyId = 2
        };
        private static readonly PropertyKey ContainerId = new PropertyKey
        {
            FormatId = new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"),
            PropertyId = 2
        };
        private static readonly PropertyKey FriendlyName = new PropertyKey
        {
            FormatId = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
            PropertyId = 14
        };

        public static IReadOnlyList<HfpBattery> ReadConnected()
        {
            var result = new List<HfpBattery>();
            var devices = SetupDiGetClassDevs(IntPtr.Zero, "BTHENUM", IntPtr.Zero, DigcfPresent | DigcfAllClasses);
            if (devices == InvalidHandle)
                return result;

            try
            {
                for (uint index = 0; ; index++)
                {
                    var device = new SP_DEVINFO_DATA { cbSize = Marshal.SizeOf<SP_DEVINFO_DATA>() };
                    if (!SetupDiEnumDeviceInfo(devices, index, ref device))
                    {
                        if (Marshal.GetLastWin32Error() == ErrorNoMoreItems)
                            break;
                        continue;
                    }

                    var instanceId = ReadInstanceId(devices, ref device);
                    var battery = ReadProperty(devices, ref device, BatteryLevel);
                    var container = ReadProperty(devices, ref device, ContainerId);
                    if (instanceId == null || instanceId.IndexOf("0000111E", StringComparison.OrdinalIgnoreCase) < 0 ||
                        battery == null || battery.Length != 1 || container == null || container.Length != 16)
                        continue;

                    var name = ReadProperty(devices, ref device, FriendlyName);
                    result.Add(new HfpBattery
                    {
                        Name = name == null ? instanceId : Encoding.Unicode.GetString(name).TrimEnd('\0'),
                        ContainerId = new Guid(container),
                        Percent = battery[0]
                    });
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devices);
            }

            return result;
        }

        public static Guid GetContainerId(string instanceId)
        {
            var devices = SetupDiCreateDeviceInfoList(IntPtr.Zero, IntPtr.Zero);
            if (devices == InvalidHandle)
                return Guid.Empty;

            try
            {
                var device = new SP_DEVINFO_DATA { cbSize = Marshal.SizeOf<SP_DEVINFO_DATA>() };
                if (!SetupDiOpenDeviceInfo(devices, instanceId, IntPtr.Zero, 0, ref device))
                    return Guid.Empty;

                var container = ReadProperty(devices, ref device, ContainerId);
                return container?.Length == 16 ? new Guid(container) : Guid.Empty;
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devices);
            }
        }

        private static string ReadInstanceId(IntPtr devices, ref SP_DEVINFO_DATA device)
        {
            var name = new StringBuilder(260);
            if (SetupDiGetDeviceInstanceId(devices, ref device, name, name.Capacity, out _))
                return name.ToString();
            return null;
        }

        private static byte[] ReadProperty(IntPtr devices, ref SP_DEVINFO_DATA device, PropertyKey key)
        {
            if (SetupDiGetDeviceProperty(devices, ref device, ref key, out _, null, 0, out var size, 0) ||
                Marshal.GetLastWin32Error() != ErrorInsufficientBuffer || size == 0)
                return null;

            var value = new byte[size];
            return SetupDiGetDeviceProperty(devices, ref device, ref key, out _, value, size, out _, 0) ? value : null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public int cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string enumerator, IntPtr parent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiCreateDeviceInfoList(IntPtr classGuid, IntPtr parent);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiOpenDeviceInfo(IntPtr devices, string instanceId, IntPtr hwndParent, uint openFlags, ref SP_DEVINFO_DATA device);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr devices, uint index, ref SP_DEVINFO_DATA device);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiGetDeviceInstanceId(IntPtr devices, ref SP_DEVINFO_DATA device, StringBuilder instanceId, int size, out int requiredSize);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDevicePropertyW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiGetDeviceProperty(IntPtr devices, ref SP_DEVINFO_DATA device, ref PropertyKey key, out uint propertyType, byte[] propertyBuffer, uint propertyBufferSize, out uint requiredSize, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr devices);
    }
}
