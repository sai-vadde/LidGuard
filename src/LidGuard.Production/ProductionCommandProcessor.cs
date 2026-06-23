using System;
using System.IO;

namespace LidGuard.Production;

internal static class ProductionCommandProcessor
{
    internal static bool TryHandle(
        string[] args,
        out int exitCode
    )
    {
        exitCode = 0;

        if (args.Length == 0)
        {
            return false;
        }

        InstallCommandRequest request = ParseRequest(args);

        try
        {
            ClearReport(request.ReportPath);

            switch (request.Command)
            {
                case "--register-startup":
                    StartupRegistrationManager.RegisterCurrentExecutable(
                        request.Scope
                    );
                    UpdateStartupState(
                        request.Scope,
                        startupRegistered: true
                    );
                    return true;

                case "--unregister-startup":
                    StartupRegistrationManager.Unregister(request.Scope);
                    UpdateStartupState(
                        request.Scope,
                        startupRegistered: false
                    );
                    return true;

                case "--apply-install-power-policy":
                    PowerPolicyService.ApplyDoNothingToAllSchemes(request.Scope);
                    return true;

                case "--restore-install-power-policy":
                    PowerPolicyService.RestorePowerPolicy(request.Scope);
                    return true;

                default:
                    throw new ArgumentException(
                        $"Unknown command '{request.Command}'."
                    );
            }
        }
        catch (Exception exception)
        {
            exitCode = 1;
            WriteFailureReport(request.ReportPath, exception.Message);
            LogCommandFailure(exception);
            return true;
        }
    }

    private static void ClearReport(string? reportPath)
    {
        if (string.IsNullOrWhiteSpace(reportPath) ||
            !File.Exists(reportPath))
        {
            return;
        }

        File.Delete(reportPath);
    }

    private static void LogCommandFailure(Exception exception)
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
            // Installer command diagnostics must never throw back out.
        }
    }

    private static InstallCommandRequest ParseRequest(string[] args)
    {
        if (args.Length is < 2 or > 3)
        {
            throw new ArgumentException(
                "Installer commands require a command, an install scope, and an optional report file path."
            );
        }

        return new InstallCommandRequest(
            args[0],
            ParseScope(args[1]),
            args.Length == 3 ? args[2] : null
        );
    }

    private static AppInstallScope ParseScope(string scopeArgument)
    {
        return scopeArgument switch
        {
            "all-users" => AppInstallScope.AllUsers,
            "current-user" => AppInstallScope.CurrentUser,
            _ => throw new ArgumentException(
                $"Unknown install scope '{scopeArgument}'."
            )
        };
    }

    private static void UpdateStartupState(
        AppInstallScope scope,
        bool startupRegistered
    )
    {
        InstallState? state = InstallStateStore.Load(scope);

        if (state is null && !startupRegistered)
        {
            return;
        }

        state ??= new InstallState
        {
            InstallScope = scope
        };

        state.InstallScope = scope;
        state.StartupScope = scope;
        state.StartupRegistered = startupRegistered;

        InstallStateStore.Save(scope, state);
    }

    private static void WriteFailureReport(
        string? reportPath,
        string message
    )
    {
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        string? folder = Path.GetDirectoryName(reportPath);

        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(reportPath, message);
    }

    private sealed record InstallCommandRequest(
        string Command,
        AppInstallScope Scope,
        string? ReportPath
    );
}
