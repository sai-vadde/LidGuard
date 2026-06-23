using System;
using System.Runtime.InteropServices;

namespace LidGuard;

internal static class NativeMethods
{
    internal const int WM_DISPLAYCHANGE = 0x007E;
    internal const int WM_POWERBROADCAST = 0x0218;
    internal const int PBT_POWERSETTINGCHANGE = 0x8013;
    internal const int DEVICE_NOTIFY_WINDOW_HANDLE = 0;

    internal const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    internal const byte MonitorPowerModeVcpCode = 0xD6;
 
    [DllImport("user32.dll")]
    internal static extern int QueryDisplayConfig(
        uint flags,
        ref uint numberOfPathArrayElements,
        [Out] DisplayConfigPathInfo[] pathInfoArray,
        ref uint numberOfModeInfoArrayElements,
        [Out] DisplayConfigModeInfo[] modeInfoArray,
        IntPtr currentTopologyId
    );

    [DllImport("user32.dll")]
    internal static extern int DisplayConfigGetDeviceInfo(
        ref DisplayConfigTargetDeviceName requestPacket
    );

    [DllImport("user32.dll")]
    internal static extern int DisplayConfigGetDeviceInfo(
        ref DisplayConfigSourceDeviceName requestPacket
    );

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc callback,
        IntPtr dwData
    );

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(
        IntPtr hMonitor,
        ref MonitorInfoEx monitorInfo
    );

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
        IntPtr hMonitor,
        out uint numberOfPhysicalMonitors
    );

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetPhysicalMonitorsFromHMONITOR(
        IntPtr hMonitor,
        uint physicalMonitorArraySize,
        [Out] PhysicalMonitor[] physicalMonitorArray
    );

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyPhysicalMonitors(
        uint physicalMonitorArraySize,
        [In] PhysicalMonitor[] physicalMonitorArray
    );

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetVCPFeatureAndVCPFeatureReply(
        IntPtr physicalMonitorHandle,
        byte vcpCode,
        out uint vcpCodeType,
        out uint currentValue,
        out uint maximumValue
    );

    internal delegate bool MonitorEnumProc(
        IntPtr hMonitor,
        IntPtr hdcMonitor,
        IntPtr lprcMonitor,
        IntPtr dwData
    );

    internal enum DisplayConfigDeviceInfoType : uint
    {
        GetSourceName = 1,
        GetTargetName = 2
    }

    internal enum DisplayConfigVideoOutputTechnology : uint
    {
        Other = 0xFFFFFFFF,
        Hd15 = 0,
        SVideo = 1,
        CompositeVideo = 2,
        ComponentVideo = 3,
        Dvi = 4,
        Hdmi = 5,
        Lvds = 6,
        Djpn = 8,
        Sdi = 9,
        DisplayPortExternal = 10,
        DisplayPortEmbedded = 11,
        UdiExternal = 12,
        UdiEmbedded = 13,
        SdtvDongle = 14,
        Miracast = 15,
        IndirectWired = 16,
        IndirectVirtual = 17,
        Internal = 0x80000000
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Luid
    {
        internal uint LowPart;
        internal int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigRational
    {
        internal uint Numerator;
        internal uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigPathSourceInfo
    {
        internal Luid AdapterId;
        internal uint Id;
        internal uint ModeInfoIdx;
        internal uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigPathTargetInfo
    {
        internal Luid AdapterId;
        internal uint Id;
        internal uint ModeInfoIdx;
        internal DisplayConfigVideoOutputTechnology OutputTechnology;
        internal DisplayConfigRotation Rotation;
        internal DisplayConfigScaling Scaling;
        internal DisplayConfigRational RefreshRate;
        internal DisplayConfigScanLineOrdering ScanLineOrdering;

        [MarshalAs(UnmanagedType.Bool)]
        internal bool TargetAvailable;

        internal uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigPathInfo
    {
        internal DisplayConfigPathSourceInfo SourceInfo;
        internal DisplayConfigPathTargetInfo TargetInfo;
        internal uint Flags;
    }

    internal enum DisplayConfigModeInfoType : uint
    {
        Source = 1,
        Target = 2,
        DesktopImage = 3
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct DisplayConfigModeInfoUnion
    {
        [FieldOffset(0)]
        internal DisplayConfigTargetMode TargetMode;

        [FieldOffset(0)]
        internal DisplayConfigSourceMode SourceMode;

        [FieldOffset(0)]
        internal DisplayConfigDesktopImageInfo DesktopImageInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigModeInfo
    {
        internal DisplayConfigModeInfoType InfoType;
        internal uint Id;
        internal Luid AdapterId;
        internal DisplayConfigModeInfoUnion ModeInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigDeviceInfoHeader
    {
        internal DisplayConfigDeviceInfoType Type;
        internal uint Size;
        internal Luid AdapterId;
        internal uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DisplayConfigSourceDeviceName
    {
        internal DisplayConfigDeviceInfoHeader Header;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string ViewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DisplayConfigTargetDeviceName
    {
        internal DisplayConfigDeviceInfoHeader Header;
        internal DisplayConfigTargetDeviceNameFlags Flags;
        internal DisplayConfigVideoOutputTechnology OutputTechnology;
        internal ushort EdidManufactureId;
        internal ushort EdidProductCodeId;
        internal uint ConnectorInstance;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        internal string MonitorFriendlyDeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string MonitorDevicePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigTargetDeviceNameFlags
    {
        internal uint Value;
    }

    internal enum DisplayConfigRotation : uint
    {
        Identity = 1,
        Rotate90 = 2,
        Rotate180 = 3,
        Rotate270 = 4
    }

    internal enum DisplayConfigScaling : uint
    {
        Identity = 1,
        Centered = 2,
        Stretched = 3,
        AspectRatioCenteredMax = 4,
        Custom = 5,
        Preferred = 128
    }

    internal enum DisplayConfigScanLineOrdering : uint
    {
        Unspecified = 0,
        Progressive = 1,
        Interlaced = 2,
        InterlacedUpperFieldFirst = 2,
        InterlacedLowerFieldFirst = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfig2DRegion
    {
        internal uint Cx;
        internal uint Cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigVideoSignalInfo
    {
        internal ulong PixelRate;
        internal DisplayConfigRational HSyncFreq;
        internal DisplayConfigRational VSyncFreq;
        internal DisplayConfig2DRegion ActiveSize;
        internal DisplayConfig2DRegion TotalSize;
        internal uint VideoStandard;
        internal DisplayConfigScanLineOrdering ScanLineOrdering;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigTargetMode
    {
        internal DisplayConfigVideoSignalInfo TargetVideoSignalInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PointL
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigSourceMode
    {
        internal uint Width;
        internal uint Height;
        internal uint PixelFormat;
        internal PointL Position;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RectL
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigDesktopImageInfo
    {
        internal PointL PathSourceSize;
        internal RectL DesktopImageRegion;
        internal RectL DesktopImageClip;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfoEx
    {
        internal uint CbSize;
        internal RectL RcMonitor;
        internal RectL RcWork;
        internal uint DwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string DeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct PhysicalMonitor
    {
        internal IntPtr Handle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string Description;
    }

    internal static readonly Guid LidSwitchStateChangeGuid =
        new("ba3e0f4d-b817-4094-a2d1-d56379e6a0f3");

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr RegisterPowerSettingNotification(
        IntPtr recipientHandle,
        ref Guid powerSettingGuid,
        int flags
    );

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterPowerSettingNotification(
        IntPtr registrationHandle
    );

    [DllImport("user32.dll")]
    internal static extern int GetDisplayConfigBufferSizes(
        uint flags,
        out uint numberOfPathArrayElements,
        out uint numberOfModeInfoArrayElements
    );

    [DllImport("PowrProf.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetSuspendState(
        [MarshalAs(UnmanagedType.Bool)] bool hibernate,
        [MarshalAs(UnmanagedType.Bool)] bool forceCritical,
        [MarshalAs(UnmanagedType.Bool)] bool disableWakeEvent
    );

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct PowerBroadcastSetting
    {
        internal Guid PowerSetting;
        internal uint DataLength;
    }
}
