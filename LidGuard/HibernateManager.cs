using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LidGuard;

internal static class HibernateManager
{
    internal static void Hibernate()
    {
        AppLogger.LogHibernate("Requesting Windows hibernation.");

        bool success = NativeMethods.SetSuspendState(
            hibernate: true,
            forceCritical: false,
            disableWakeEvent: false
        );

        if (!success)
        {
            AppLogger.LogHibernate("Windows rejected the hibernation request.");

            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Windows rejected the hibernation request."
            );
        }

        AppLogger.LogHibernate("Windows accepted the hibernation request.");
    }
}
