using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace KeyER;

/// <summary>
/// User-initiated, plain-text beta support report. Contains no source snippets,
/// document titles/paths, username, computer name, or raw exception messages.
/// </summary>
internal static class BetaSupportReport
{
    private static readonly Regex SafeLogLine = new(
        @"^\d{4}-\d{2}-\d{2}T[0-9:.+\-]+Z? ERROR [A-Za-z0-9_-]{1,48} [A-Za-z0-9_]{1,80} HRESULT=0x[0-9A-Fa-f]{8}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    internal static IReadOnlyList<string> FilterLogEntries(IEnumerable<string> lines,
        int maximum = 25)
    {
        var safe = new Queue<string>();
        var limit = Math.Clamp(maximum, 1, 50);
        foreach (var line in lines)
        {
            if (line.Length > 220 || !SafeLogLine.IsMatch(line)) continue;
            safe.Enqueue(line);
            if (safe.Count > limit) safe.Dequeue();
        }
        return safe.ToArray();
    }

    internal static string Build(AppSettings source, IEnumerable<string>? logEntries = null)
    {
        var settings = source.Clone();
        settings.Validate();
        var assembly = typeof(AppSettings).Assembly.GetName();

        var sb = new StringBuilder();
        sb.AppendLine("KeyER Studio — support report");
        sb.AppendLine("Created locally. Share only if you choose to.");
        sb.AppendLine();
        sb.AppendLine($"Version: {assembly.Version}");
        sb.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"OS platform: {RuntimeInformation.OSDescription}");
        sb.AppendLine($"OS architecture: {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"Settings schema: {settings.SettingsSchemaVersion}");
        sb.AppendLine($"Typing mode: {settings.Mode}");
        sb.AppendLine($"Number of profiles: {settings.Profiles.Count}");
        sb.AppendLine($"IDE verification: {settings.Profiles.Count(p => p.CodeIntegrity != CodeIntegrityMode.Off)} profiles enabled");
        sb.AppendLine($"Theme: {settings.Appearance}");
        sb.AppendLine($"High contrast: {SystemInformation.HighContrast}");
        sb.AppendLine();
        sb.AppendLine("Recent sanitized application errors:");
        var safe = FilterLogEntries(logEntries ?? LoadSafeLogs());
        if (safe.Count == 0) sb.AppendLine("None available.");
        foreach (var line in safe) sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine("No typed text, file paths, document contents, window titles,");
        sb.AppendLine("user/account names, hotkey values, or API credentials are included.");
        return sb.ToString();
    }

    private static IEnumerable<string> LoadSafeLogs()
    {
        try
        {
            var path = DiagnosticLog.PathName;
            if (!File.Exists(path)) return [];
            if (new FileInfo(path).Length > 2_097_152) return [];
            return File.ReadAllLines(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or System.Security.SecurityException or ArgumentException)
        {
            return [];
        }
    }
}
