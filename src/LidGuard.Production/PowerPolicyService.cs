using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LidGuard.Production;

internal static class PowerPolicyService
{
    private const string LidActionSettingGuid =
        "5ca83367-6e45-459f-a27b-476b1d01c936";
    private const string SubButtonsSubgroupGuid =
        "4f971e89-eebd-4455-a8de-9e59040e7347";

    private static readonly Regex AcValueRegex = new(
        @"Current AC Power Setting Index:\s*0x([0-9A-Fa-f]+)",
        RegexOptions.Compiled
    );

    private static readonly Regex DcValueRegex = new(
        @"Current DC Power Setting Index:\s*0x([0-9A-Fa-f]+)",
        RegexOptions.Compiled
    );

    private static readonly Regex SchemeGuidRegex = new(
        @"([0-9A-Fa-f-]{36})",
        RegexOptions.Compiled
    );

    private static readonly Regex PowerSettingGuidRegex = new(
        @"Power Setting GUID:\s*([0-9A-Fa-f-]{36})",
        RegexOptions.Compiled
    );

    internal static void ApplyDoNothingToAllSchemes(AppInstallScope scope)
    {
        InstallState state = InstallStateStore.Load(scope) ??
                             new InstallState
                             {
                                 InstallScope = scope
                             };

        if (state.PowerSchemes.Count == 0)
        {
            string activeSchemeId = GetActiveSchemeId();
            List<PowerSchemeState> schemes = [];

            foreach (string schemeId in GetSchemeIds())
            {
                (uint acValue, uint dcValue) =
                    GetLidActionValues(schemeId);

                schemes.Add(
                    new PowerSchemeState
                    {
                        SchemeId = schemeId,
                        AcLidAction = acValue,
                        DcLidAction = dcValue
                    }
                );
            }

            state.ActiveSchemeId = activeSchemeId;
            state.PowerSchemes = schemes;
            InstallStateStore.Save(scope, state);
        }

        foreach (PowerSchemeState scheme in state.PowerSchemes)
        {
            SetLidActionValue(
                scheme.SchemeId,
                isAcValue: true,
                0
            );
            SetLidActionValue(
                scheme.SchemeId,
                isAcValue: false,
                0
            );
        }

        if (!string.IsNullOrWhiteSpace(state.ActiveSchemeId))
        {
            SetActiveScheme(state.ActiveSchemeId);
        }
    }

    internal static void RestorePowerPolicy(AppInstallScope scope)
    {
        InstallState? state = InstallStateStore.Load(scope);

        if (state is null || state.PowerSchemes.Count == 0)
        {
            return;
        }

        foreach (PowerSchemeState scheme in state.PowerSchemes)
        {
            SetLidActionValue(
                scheme.SchemeId,
                isAcValue: true,
                scheme.AcLidAction
            );
            SetLidActionValue(
                scheme.SchemeId,
                isAcValue: false,
                scheme.DcLidAction
            );
        }

        if (!string.IsNullOrWhiteSpace(state.ActiveSchemeId))
        {
            SetActiveScheme(state.ActiveSchemeId);
        }

        InstallStateStore.Delete(scope);
    }

    private static (uint AcValue, uint DcValue) GetLidActionValues(
        string schemeId
    )
    {
        string output = RunPowerCfg(
            $"/qh {schemeId} {SubButtonsSubgroupGuid}"
        );
        string lidActionBlock = ExtractLidActionBlock(
            schemeId,
            output
        );

        Match acMatch = AcValueRegex.Match(lidActionBlock);
        Match dcMatch = DcValueRegex.Match(lidActionBlock);

        if (!acMatch.Success || !dcMatch.Success)
        {
            throw new InvalidOperationException(
                $"Windows exposed the Lid close action block for scheme '{schemeId}', but the AC/DC values could not be parsed.{Environment.NewLine}{Environment.NewLine}Powercfg block:{Environment.NewLine}{lidActionBlock}"
            );
        }

        uint acValue = uint.Parse(
            acMatch.Groups[1].Value,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture
        );
        uint dcValue = uint.Parse(
            dcMatch.Groups[1].Value,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture
        );

        return (acValue, dcValue);
    }

    private static string ExtractLidActionBlock(
        string schemeId,
        string subgroupOutput
    )
    {
        string[] lines = subgroupOutput.Split(
            ["\r\n", "\n"],
            StringSplitOptions.None
        );
        List<string> blockLines = [];
        bool foundTargetBlock = false;

        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd();
            Match settingMatch = PowerSettingGuidRegex.Match(line);

            if (settingMatch.Success)
            {
                if (foundTargetBlock)
                {
                    break;
                }

                foundTargetBlock = string.Equals(
                    settingMatch.Groups[1].Value,
                    LidActionSettingGuid,
                    StringComparison.OrdinalIgnoreCase
                );
            }

            if (foundTargetBlock)
            {
                blockLines.Add(line);
            }
        }

        if (blockLines.Count == 0)
        {
            throw new InvalidOperationException(
                $"Windows did not expose the Lid close action power setting for scheme '{schemeId}'.{Environment.NewLine}{Environment.NewLine}Powercfg subgroup output:{Environment.NewLine}{BuildDiagnosticExcerpt(subgroupOutput)}"
            );
        }

        return string.Join(Environment.NewLine, blockLines);
    }

    private static List<string> GetSchemeIds()
    {
        string output = RunPowerCfg("/list");
        List<string> schemeIds = [];

        foreach (string line in output.Split(
                     ["\r\n", "\n"],
                     StringSplitOptions.RemoveEmptyEntries
                 ))
        {
            Match match = SchemeGuidRegex.Match(line);

            if (match.Success)
            {
                schemeIds.Add(match.Groups[1].Value);
            }
        }

        if (schemeIds.Count == 0)
        {
            throw new InvalidOperationException(
                "Windows did not report any power schemes."
            );
        }

        return schemeIds;
    }

    private static string GetActiveSchemeId()
    {
        string output = RunPowerCfg("/getactivescheme");
        Match match = SchemeGuidRegex.Match(output);

        if (!match.Success)
        {
            throw new InvalidOperationException(
                "Windows did not report the active power scheme."
            );
        }

        return match.Groups[1].Value;
    }

    private static string BuildDiagnosticExcerpt(string output)
    {
        const int maxLength = 2000;

        if (output.Length <= maxLength)
        {
            return output.Trim();
        }

        return output[..maxLength].TrimEnd() +
               Environment.NewLine +
               "... [truncated]";
    }

    private static string RunPowerCfg(string arguments)
    {
        using var process = Process.Start(
            new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = arguments,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        );

        if (process is null)
        {
            throw new InvalidOperationException(
                "Windows could not start powercfg.exe."
            );
        }

        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string errorMessage = string.IsNullOrWhiteSpace(standardError)
                ? "Windows did not return any additional details."
                : standardError.Trim();

            if (process.ExitCode == 5 ||
                errorMessage.Contains(
                    "Access is denied",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                throw new InvalidOperationException(
                    "Windows denied access while LidGuard was changing lid-close power settings. Run setup as an administrator and try again."
                );
            }

            throw new InvalidOperationException(
                $"powercfg.exe exited with code {process.ExitCode}: {errorMessage}"
            );
        }

        return standardOutput;
    }

    private static void SetActiveScheme(string schemeId)
    {
        RunPowerCfg($"/setactive {schemeId}");
    }

    private static void SetLidActionValue(
        string schemeId,
        bool isAcValue,
        uint value
    )
    {
        string modeSwitch = isAcValue ? "/setacvalueindex" : "/setdcvalueindex";

        RunPowerCfg(
            $"{modeSwitch} {schemeId} {SubButtonsSubgroupGuid} {LidActionSettingGuid} {value.ToString(CultureInfo.InvariantCulture)}"
        );
    }
}
