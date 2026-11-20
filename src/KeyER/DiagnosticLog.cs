using System.Globalization;
using System.Text;

namespace KeyER;

/// <summary>Privacy-preserving diagnostics: never records source text, keystrokes or window titles.</summary>
internal static class DiagnosticLog
{
    private static readonly object Sync = new();
    public static string PathName => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KeyER", "diagnostics.log");

    public static void Error(string component, Exception exception)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
                var info = new FileInfo(PathName);
                if (info.Exists && info.Length >= 1_048_576)
                    File.Move(PathName, PathName + ".old", overwrite: true);

                // Log only exception type and numeric code, not Exception.Message/StackTrace
                // which could include a filename, editor content or private data.
                var entry = string.Format(CultureInfo.InvariantCulture,
                    "{0:O} ERROR {1} {2} HRESULT=0x{3:X8}\n",
                    DateTimeOffset.UtcNow, component, exception.GetType().Name, exception.HResult);
                File.AppendAllText(PathName, entry, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never crash or block typing.
        }
    }
}
