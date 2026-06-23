using System;
using System.IO;
using System.Reflection;
using LidGuard;

namespace LidGuard.Production;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ProductionPaths.EnsureRuntimeDirectories();

        try
        {
            if (ProductionCommandProcessor.TryHandle(args, out int commandExitCode))
            {
                return commandExitCode;
            }

            return LidGuardApplication.Run(CreateRuntimeOptions());
        }
        catch (Exception exception)
        {
            LogStartupFailure(exception);
            Console.Error.WriteLine(exception.Message);

            return 1;
        }
    }

    private static void LogStartupFailure(Exception exception)
    {
        try
        {
            string logPath = Path.Combine(ProductionPaths.LogRoot, "errors.txt");
            string entry =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [error] [production] Unhandled exception in the production host entry point.{Environment.NewLine}" +
                $"{exception}{Environment.NewLine}" +
                $"{new string('-', 80)}{Environment.NewLine}";

            File.AppendAllText(logPath, entry);
        }
        catch
        {
            // Startup diagnostics must never throw back out.
        }
    }

    private static LidGuardRuntimeOptions CreateRuntimeOptions()
    {
        return new LidGuardRuntimeOptions
        {
            AppVersion = ResolveVersion(),
            ActivationSignalName = @"Local\LidGuard.ShowSettings",
            CreateApplicationContext = () =>
                new ProductionApplicationContext(@"Local\LidGuard.ShowSettings"),
            EnableLogRetention = true,
            LogRoot = ProductionPaths.LogRoot,
            Mode = AppRuntimeMode.Production,
            MutexName = @"Local\LidGuard",
            SettingsPath = ProductionPaths.SettingsPath
        };
    }

    private static string ResolveVersion()
    {
        return Assembly.GetExecutingAssembly()
                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion ?? "production";
    }
}
