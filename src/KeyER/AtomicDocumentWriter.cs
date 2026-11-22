using System.Text;

namespace KeyER;

/// <summary>
/// Explicit Save As with a temp file in the destination directory. A failed
/// write never truncates the existing document. Never autosaves source code.
/// </summary>
internal static class AtomicDocumentWriter
{
    internal static void WriteText(string destination, string text)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Не указан путь файла.", nameof(destination));

        var full = Path.GetFullPath(destination);
        var directory = Path.GetDirectoryName(full)
            ?? throw new IOException("Недопустимая папка.");
        var temporary = Path.Combine(directory,
            "." + Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(text);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(full))
                File.Replace(temporary, full, destinationBackupFileName: null);
            else
                File.Move(temporary, full);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { /* Original failure must not be masked. */ }
            catch (UnauthorizedAccessException) { /* Best effort cleanup. */ }
        }
    }
}
