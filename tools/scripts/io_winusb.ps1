$code = @"
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Text;
public static class WinUsbIo {
    [StructLayout(LayoutKind.Sequential)] public struct USB_INTERFACE_DESCRIPTOR { public byte bLength; public byte bDescriptorType; public byte bInterfaceNumber; public byte bAlternateSetting; public byte bNumEndpoints; public byte bInterfaceClass; public byte bInterfaceSubClass; public byte bInterfaceProtocol; public byte iInterface; }
    [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)] static extern SafeFileHandle CreateFile(string fileName, uint access, uint share, IntPtr sec, uint creation, uint flags, IntPtr template);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_Initialize(SafeFileHandle deviceHandle, out IntPtr interfaceHandle);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_Free(IntPtr interfaceHandle);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_WritePipe(IntPtr interfaceHandle, byte PipeID, byte[] Buffer, uint BufferLength, out uint LengthTransferred, IntPtr Overlapped);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_ReadPipe(IntPtr interfaceHandle, byte PipeID, byte[] Buffer, uint BufferLength, out uint LengthTransferred, IntPtr Overlapped);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_SetPipePolicy(IntPtr interfaceHandle, byte PipeID, uint PolicyType, uint ValueLength, ref uint Value);
    public static string TryIo(string path, byte outPipe, byte inPipe, string cmd) {
        var h = CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
        if (h.IsInvalid) return "CreateFile failed: " + Marshal.GetLastWin32Error();
        IntPtr usb;
        if (!WinUsb_Initialize(h, out usb)) return "WinUsb_Initialize failed: " + Marshal.GetLastWin32Error();
        try {
            uint timeout = 1000;
            WinUsb_SetPipePolicy(usb, inPipe, 3, 4, ref timeout);
            var data = Encoding.ASCII.GetBytes(cmd + "\n");
            uint written;
            bool ok = WinUsb_WritePipe(usb, outPipe, data, (uint)data.Length, out written, IntPtr.Zero);
            if (!ok) return "Write failed: " + Marshal.GetLastWin32Error();
            byte[] buf = new byte[2048];
            uint read;
            ok = WinUsb_ReadPipe(usb, inPipe, buf, (uint)buf.Length, out read, IntPtr.Zero);
            if (!ok) return "Read failed: " + Marshal.GetLastWin32Error();
            return "W=" + written + " R=" + read + " Data=" + Encoding.ASCII.GetString(buf,0,(int)read).Trim();
        } finally { WinUsb_Free(usb); h.Close(); }
    }
}
"@
Add-Type -TypeDefinition $code
$path='\\?\USB#VID_04CC&PID_121B#2408010006#{0040d94d-be36-48b1-9605-0efb33d5c206}'
'Pair01=' + [WinUsbIo]::TryIo($path, 0x01, 0x81, '*IDN?')
'Pair02=' + [WinUsbIo]::TryIo($path, 0x02, 0x82, '*IDN?')
'Cross1=' + [WinUsbIo]::TryIo($path, 0x01, 0x82, '*IDN?')
'Cross2=' + [WinUsbIo]::TryIo($path, 0x02, 0x81, '*IDN?')
