using System;
using System.Runtime.InteropServices;

namespace HeadsetBat
{
    internal static class AudioEndpoint
    {
        private const int ErrorNotFound = unchecked((int)0x80070490);

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

        public static AudioOutput GetDefaultRender()
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            IMMDevice device = null;
            IPropertyStore store = null;
            var containerId = Guid.Empty;
            string endpointId = null;
            string endpointName = null;
            try
            {
                try
                {
                    enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out device);
                }
                catch (COMException error) when (error.HResult == ErrorNotFound)
                {
                    return null;
                }

                if (device == null)
                    return null;

                device.GetId(out endpointId);
                device.OpenPropertyStore(StorageAccessMode.Read, out store);
                var key = ContainerId;
                store.GetValue(ref key, out var value);
                try
                {
                    containerId = value.PointerValue != IntPtr.Zero
                        ? Marshal.PtrToStructure<Guid>(value.PointerValue)
                        : Guid.Empty;
                }
                finally
                {
                    PropVariantClear(ref value);
                }

                var nameKey = FriendlyName;
                store.GetValue(ref nameKey, out var name);
                try
                {
                    endpointName = name.ValueType == VarEnum.VT_LPWSTR
                        ? Marshal.PtrToStringUni(name.PointerValue)
                        : null;
                }
                finally
                {
                    PropVariantClear(ref name);
                }
            }
            finally
            {
                if (store != null) Marshal.FinalReleaseComObject(store);
                if (device != null) Marshal.FinalReleaseComObject(device);
                Marshal.FinalReleaseComObject(enumerator);
            }

            return new AudioOutput
            {
                ContainerId = containerId != Guid.Empty || string.IsNullOrWhiteSpace(endpointId)
                    ? containerId
                    : HfpBatteryReader.GetContainerId("SWD\\MMDEVAPI\\" + endpointId),
                Name = endpointName,
                IsHandsFree = !string.IsNullOrWhiteSpace(endpointName) &&
                    (endpointName.IndexOf("Hands-Free", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     endpointName.IndexOf("Handsfree", StringComparison.OrdinalIgnoreCase) >= 0)
            };
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant value);
    }

    internal sealed class AudioOutput
    {
        public Guid ContainerId { get; set; }
        public string Name { get; set; }
        public bool IsHandsFree { get; set; }
    }

    internal enum EDataFlow { eRender, eCapture, eAll }
    internal enum ERole { eConsole, eMultimedia, eCommunications }
    internal enum StorageAccessMode { Read = 0 }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PropVariant
    {
        [FieldOffset(0)] public VarEnum ValueType;
        [FieldOffset(8)] public IntPtr PointerValue;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumerator { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    internal interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out object devices);
        void GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        void RegisterEndpointNotificationCallback(IntPtr client);
        void UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    internal interface IMMDevice
    {
        void Activate(ref Guid iid, uint clsContext, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object result);
        void OpenPropertyStore(StorageAccessMode accessMode, out IPropertyStore properties);
        void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetState(out uint state);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    internal interface IPropertyStore
    {
        void GetCount(out uint count);
        void GetAt(uint index, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value);
        void Commit();
    }
}
