using System;
using System.Runtime.InteropServices;
using System.Text;

class P {
    const uint DIGCF_PRESENT = 0x2;
    const uint DIGCF_DEVICEINTERFACE = 0x10;
    const int ERROR_NO_MORE_ITEMS = 259;

    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DATA {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Auto)]
    struct SP_DEVICE_INTERFACE_DETAIL_DATA {
        public int cbSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
        public string DevicePath;
    }

    [DllImport("setupapi.dll", SetLastError=true)]
    static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

    [DllImport("setupapi.dll", SetLastError=true)]
    static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid, uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

    [DllImport("setupapi.dll", CharSet=CharSet.Auto, SetLastError=true)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, IntPtr DeviceInterfaceDetailData, uint DeviceInterfaceDetailDataSize, out uint RequiredSize, IntPtr DeviceInfoData);

    [DllImport("setupapi.dll", CharSet=CharSet.Auto, SetLastError=true)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, ref SP_DEVICE_INTERFACE_DETAIL_DATA DeviceInterfaceDetailData, uint DeviceInterfaceDetailDataSize, out uint RequiredSize, IntPtr DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError=true)]
    static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    static void Main() {
        var guid = new Guid("0040D94D-BE36-48B1-9605-0EFB33D5C206");
        var info = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (info == IntPtr.Zero || info.ToInt64() == -1) {
            Console.WriteLine("GetClassDevs failed: " + Marshal.GetLastWin32Error());
            return;
        }
        try {
            uint index = 0;
            while (true) {
                var ifData = new SP_DEVICE_INTERFACE_DATA();
                ifData.cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>();
                bool ok = SetupDiEnumDeviceInterfaces(info, IntPtr.Zero, ref guid, index, ref ifData);
                if (!ok) {
                    int err = Marshal.GetLastWin32Error();
                    if (err == ERROR_NO_MORE_ITEMS) break;
                    Console.WriteLine("Enum failed: " + err);
                    break;
                }

                SetupDiGetDeviceInterfaceDetail(info, ref ifData, IntPtr.Zero, 0, out uint required, IntPtr.Zero);
                var detail = new SP_DEVICE_INTERFACE_DETAIL_DATA();
                detail.cbSize = IntPtr.Size == 8 ? 8 : 6;
                ok = SetupDiGetDeviceInterfaceDetail(info, ref ifData, ref detail, required, out required, IntPtr.Zero);
                Console.WriteLine($"Index {index} ok={ok} err={Marshal.GetLastWin32Error()} path={detail.DevicePath}");
                index++;
            }
        }
        finally {
            SetupDiDestroyDeviceInfoList(info);
        }
    }
}
