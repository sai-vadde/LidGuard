using System;
using System.Threading;
using System.Windows.Forms;

namespace LidGuard;

public static class LidGuardApplication
{
    private static Mutex? _singleInstanceMutex;

    public static int Run(LidGuardRuntimeOptions options)
    {
        try
        {
            AppEnvironment.Initialize(options);
            AppLogger.Initialize();
            AppLogger.LogStartup(
                $"LidGuard starting in {options.Mode} mode. Version: {options.AppVersion}."
            );

            _singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                name: options.MutexName,
                createdNew: out bool isFirstInstance
            );

            if (!isFirstInstance)
            {
                AppLogger.LogStartup(
                    "Another instance is already running."
                );

                SignalExistingInstance(options.ActivationSignalName);
                return 0;
            }

            AppLogger.LogStartup("Single-instance lock acquired.");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppLogger.LogStartup("Windows Forms application configured.");
            AppLogger.LogStartup("Creating listener context.");

            using ApplicationContext context =
                options.CreateApplicationContext?.Invoke() ??
                new LidListenerContext();

            AppLogger.LogStartup("Entering message loop.");
            Application.Run(context);
            AppLogger.LogStartup("Message loop exited.");

            return 0;
        }
        catch (Exception exception)
        {
            try
            {
                AppLogger.LogStartup(
                    "Startup failed before the application could finish initializing."
                );
                AppLogger.LogError(
                    "startup",
                    "Unhandled startup exception.",
                    exception
                );
            }
            catch
            {
                // Startup diagnostics must never throw back out.
            }

            return 1;
        }
    }

    private static void SignalExistingInstance(string? activationSignalName)
    {
        if (string.IsNullOrWhiteSpace(activationSignalName))
        {
            AppLogger.LogStartup(
                "No activation signal name was configured, so the second instance will exit without notifying the running instance."
            );
            return;
        }

        try
        {
            using var activationEvent = EventWaitHandle.OpenExisting(
                activationSignalName
            );

            activationEvent.Set();
            AppLogger.LogStartup(
                "The running instance was notified to show its UI."
            );
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            AppLogger.LogStartup(
                "The activation signal was not available, so the running instance could not be notified."
            );
        }
    }
}
