$code = @"
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class WinUsbProbe {
    [StructLayout(LayoutKind.Sequential)]
    public struct USB_INTERFACE_DESCRIPTOR {
        public byte bLength; public byte bDescriptorType; public byte bInterfaceNumber; public byte bAlternateSetting; public byte bNumEndpoints; public byte bInterfaceClass; public byte bInterfaceSubClass; public byte bInterfaceProtocol; public byte iInterface;
    }
    public enum USBD_PIPE_TYPE : int { Control, Isochronous, Bulk, Interrupt }
    [StructLayout(LayoutKind.Sequential)]
    public struct WINUSB_PIPE_INFORMATION { public USBD_PIPE_TYPE PipeType; public byte PipeId; public ushort MaximumPacketSize; public byte Interval; }
    [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
    static extern SafeFileHandle CreateFile(string fileName, uint access, uint share, IntPtr sec, uint creation, uint flags, IntPtr template);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_Initialize(SafeFileHandle deviceHandle, out IntPtr interfaceHandle);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_Free(IntPtr interfaceHandle);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_QueryInterfaceSettings(IntPtr interfaceHandle, byte alternateInterfaceNumber, out USB_INTERFACE_DESCRIPTOR usbAltInterfaceDescriptor);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_QueryPipe(IntPtr interfaceHandle, byte alternateInterfaceNumber, byte pipeIndex, out WINUSB_PIPE_INFORMATION pipeInformation);
    public static string Probe(string path) {
        var h = CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
        if (h.IsInvalid) return "CreateFile failed: " + Marshal.GetLastWin32Error();
        IntPtr usb;
        if (!WinUsb_Initialize(h, out usb)) return "WinUsb_Initialize failed: " + Marshal.GetLastWin32Error();
        try {
            USB_INTERFACE_DESCRIPTOR d;
            if (!WinUsb_QueryInterfaceSettings(usb, 0, out d)) return "QueryInterfaceSettings failed: " + Marshal.GetLastWin32Error();
            string text = string.Format("Endpoints={0} Class={1:X2} Sub={2:X2} Prot={3:X2}", d.bNumEndpoints, d.bInterfaceClass, d.bInterfaceSubClass, d.bInterfaceProtocol);
            for (byte i = 0; i < d.bNumEndpoints; i++) {
                WINUSB_PIPE_INFORMATION p;
                if (!WinUsb_QueryPipe(usb, 0, i, out p)) text += "\nPipe " + i + " failed: " + Marshal.GetLastWin32Error();
                else text += string.Format("\nPipe {0}: Type={1} Id=0x{2:X2} MaxPacket={3} Interval={4}", i, p.PipeType, p.PipeId, p.MaximumPacketSize, p.Interval);
            }
            return text;
        } finally { WinUsb_Free(usb); h.Close(); }
    }
}
"@
Add-Type -TypeDefinition $code
$path='\\?\USB#VID_04CC&PID_121B#2408010006#{0040d94d-be36-48b1-9605-0efb33d5c206}'
$result=[WinUsbProbe]::Probe($path)
Write-Host $result
