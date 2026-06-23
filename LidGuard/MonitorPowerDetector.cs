using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LidGuard;

internal static class MonitorPowerDetector
{
    private const uint MonitorPowerModeOn = 0x01;
    private const uint MonitorPowerModeStandby = 0x02;
    private const uint MonitorPowerModeSuspend = 0x03;
    private const uint MonitorPowerModeOff = 0x04;

    internal static ExternalMonitorState GetState(
        IReadOnlyList<ActiveExternalDisplayDetector.ActiveExternalDisplay> activeExternalDisplays
    )
    {
        bool sawUnavailableMonitor = false;
        bool sawSuccessfulQuery = false;

        foreach (var display in activeExternalDisplays)
        {
            ExternalMonitorState displayState =
                GetStateForDisplay(display);

            AppLogger.LogDisplay(
                $"DDC/CI state for external display '{display.SourceDeviceName}' " +
                $"('{display.MonitorFriendlyDeviceName}') is {displayState}."
            );

            switch (displayState)
            {
                case ExternalMonitorState.On:
                    return ExternalMonitorState.On;
                case ExternalMonitorState.Off:
                    sawSuccessfulQuery = true;
                    sawUnavailableMonitor = true;
                    break;
                case ExternalMonitorState.Unknown:
                    break;
                case ExternalMonitorState.None:
                    break;
            }
        }

        if (sawSuccessfulQuery && sawUnavailableMonitor)
        {
            return ExternalMonitorState.Off;
        }

        return ExternalMonitorState.Unknown;
    }

    private static ExternalMonitorState GetStateForDisplay(
        ActiveExternalDisplayDetector.ActiveExternalDisplay display
    )
    {
        List<IntPtr> monitorHandles =
            GetMonitorHandles(display.SourceDeviceName);

        if (monitorHandles.Count == 0)
        {
            return ExternalMonitorState.Unknown;
        }

        bool sawSuccessfulQuery = false;
        bool sawUnavailableMonitor = false;

        foreach (IntPtr monitorHandle in monitorHandles)
        {
            ExternalMonitorState monitorState =
                GetStateForMonitorHandle(monitorHandle);

            switch (monitorState)
            {
                case ExternalMonitorState.On:
                    return ExternalMonitorState.On;
                case ExternalMonitorState.Off:
                    sawSuccessfulQuery = true;
                    sawUnavailableMonitor = true;
                    break;
                case ExternalMonitorState.Unknown:
                    break;
                case ExternalMonitorState.None:
                    break;
            }
        }

        if (sawSuccessfulQuery && sawUnavailableMonitor)
        {
            return ExternalMonitorState.Off;
        }

        return ExternalMonitorState.Unknown;
    }

    private static List<IntPtr> GetMonitorHandles(string sourceDeviceName)
    {
        List<IntPtr> monitorHandles = [];

        bool success = NativeMethods.EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (monitorHandle, _, _, _) =>
            {
                var monitorInfo = new NativeMethods.MonitorInfoEx
                {
                    CbSize = (uint)Marshal.SizeOf<NativeMethods.MonitorInfoEx>()
                };

                if (!NativeMethods.GetMonitorInfo(monitorHandle, ref monitorInfo))
                {
                    return true;
                }

                if (string.Equals(
                    monitorInfo.DeviceName,
                    sourceDeviceName,
                    StringComparison.OrdinalIgnoreCase
                ))
                {
                    monitorHandles.Add(monitorHandle);
                }

                return true;
            },
            IntPtr.Zero
        );

        if (!success)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Windows could not enumerate display monitors."
            );
        }

        return monitorHandles;
    }

    private static ExternalMonitorState GetStateForMonitorHandle(
        IntPtr monitorHandle
    )
    {
        if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(
                monitorHandle,
                out uint physicalMonitorCount
            ))
        {
            return ExternalMonitorState.Unknown;
        }

        if (physicalMonitorCount == 0)
        {
            return ExternalMonitorState.Unknown;
        }

        var physicalMonitors =
            new NativeMethods.PhysicalMonitor[physicalMonitorCount];

        if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(
                monitorHandle,
                physicalMonitorCount,
                physicalMonitors
            ))
        {
            return ExternalMonitorState.Unknown;
        }

        try
        {
            bool sawSuccessfulQuery = false;
            bool sawUnavailableMonitor = false;

            foreach (NativeMethods.PhysicalMonitor physicalMonitor in physicalMonitors)
            {
                if (!NativeMethods.GetVCPFeatureAndVCPFeatureReply(
                        physicalMonitor.Handle,
                        NativeMethods.MonitorPowerModeVcpCode,
                        out _,
                        out uint currentValue,
                        out _
                    ))
                {
                    continue;
                }

                sawSuccessfulQuery = true;

                switch (currentValue)
                {
                    case MonitorPowerModeOn:
                        return ExternalMonitorState.On;
                    case MonitorPowerModeStandby:
                    case MonitorPowerModeSuspend:
                    case MonitorPowerModeOff:
                        sawUnavailableMonitor = true;
                        break;
                }
            }

            if (sawSuccessfulQuery && sawUnavailableMonitor)
            {
                return ExternalMonitorState.Off;
            }

            return ExternalMonitorState.Unknown;
        }
        finally
        {
            NativeMethods.DestroyPhysicalMonitors(
                physicalMonitorCount,
                physicalMonitors
            );
        }
    }
}
