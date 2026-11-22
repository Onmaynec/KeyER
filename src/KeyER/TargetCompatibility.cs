using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KeyER;

/// <summary>
/// Inspects only process/window capabilities. Never reads window titles,
/// document text, or keyboard input from another application.
/// </summary>
internal enum TargetHealthCode
{
    Ready, NoWindow, OwnWindow, WindowUnavailable, NotForeground,
    Minimized, NotResponding, ElevatedTarget, ProcessUnavailable
}

internal sealed record TargetHealthReport(
    TargetHealthCode Code, string Summary, string Guidance, string ProcessLabel)
{
    public bool CanType => Code == TargetHealthCode.Ready;
}

internal static class TargetCompatibility
{
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsHungAppWindow(IntPtr hWnd);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr tokenHandle, int infoClass,
        out int information, int informationLength, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private const uint TokenQuery = 0x0008;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int TokenElevation = 20;

    /// <summary>Pure decision core, testable without foreground windows.</summary>
    public static TargetHealthReport Evaluate(bool hasWindow, bool ownWindow,
        bool available, bool foreground, bool minimized, bool hung,
        bool? targetElevated, bool? currentElevated, string? processLabel,
        bool processAvailable = true)
    {
        var safeName = SafeProcessName(processLabel);
        TargetHealthReport Result(TargetHealthCode code, string summary, string guidance) =>
            new(code, summary, guidance, safeName);

        if (!hasWindow)
            return Result(TargetHealthCode.NoWindow, "Целевое окно не обнаружено.",
                "Откройте Блокнот или редактор, поставьте курсор в поле ввода и повторите проверку.");
        if (ownWindow)
            return Result(TargetHealthCode.OwnWindow, "Активно окно KeyER.",
                "Перейдите в другое приложение и выберите его текстовое поле.");
        if (!available)
            return Result(TargetHealthCode.WindowUnavailable, "Окно больше недоступно.",
                "Вернитесь в целевое приложение и повторите проверку.");
        if (!processAvailable)
            return Result(TargetHealthCode.ProcessUnavailable, "Не удалось определить процесс окна.",
                "Закройте окно, откройте редактор заново и повторите проверку. " +
                "Без идентификатора процесса безопасный набор не запускается.");
        if (!foreground)
            return Result(TargetHealthCode.NotForeground, "Целевое окно потеряло фокус.",
                "Вернитесь в то же окно и повторите запуск. Набор в другое окно не продолжается.");
        if (minimized)
            return Result(TargetHealthCode.Minimized, "Целевое окно свёрнуто.",
                "Разверните окно и установите курсор в редактор.");
        if (hung)
            return Result(TargetHealthCode.NotResponding, "Целевое приложение не отвечает.",
                "Восстановите работу приложения перед новым запуском. KeyER не отправляет клавиши в зависшее окно.");
        if (targetElevated == true && currentElevated == false)
            return Result(TargetHealthCode.ElevatedTarget,
                "Целевое приложение запущено с повышенными правами.",
                "Windows может блокировать SendInput из обычного процесса (UIPI). " +
                "Откройте редактор без повышения прав. Не отключайте защиту Windows.");
        return Result(TargetHealthCode.Ready, "Окно готово к проверяемому старту.",
            "Курсор должен стоять в текстовом поле. Это предварительная проверка окна, " +
            "а не подтверждение, что редактор примет каждую клавишу.");
    }

    public static TargetHealthReport Inspect(IntPtr target)
    {
        if (target == IntPtr.Zero)
            return Evaluate(false, false, false, false, false, false,
                null, null, null);

        var own = Native.IsOwnWindow(target);
        var available = Native.IsWindow(target);
        var foreground = Native.GetForegroundWindow() == target;
        var process = Native.GetWindowProcessName(target);
        bool minimized = false, hung = false;
        if (available && !own)
        {
            minimized = IsIconic(target);
            hung = IsHungAppWindow(target);
        }

        var targetId = Native.GetWindowProcessId(target);
        var elevatedTarget = targetId == 0 ? null : ReadElevation(targetId);
        var elevatedCurrent = ReadElevation((uint)Environment.ProcessId);
        return Evaluate(true, own, available, foreground, minimized, hung,
            elevatedTarget, elevatedCurrent, process, targetId != 0);
    }

    private static bool? ReadElevation(uint processId)
    {
        // Open only the minimum access required to query the token. Using
        // Process.Handle here often fails on elevated targets unnecessarily.
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero) return null;
        try
        {
            if (!OpenProcessToken(handle, TokenQuery, out var token))
                return null;
            try
            {
                return GetTokenInformation(token, TokenElevation, out var elevation,
                    sizeof(int), out _) ? elevation != 0 : null;
            }
            finally { CloseHandle(token); }
        }
        finally { CloseHandle(handle); }
    }

    private static string SafeProcessName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Неизвестное приложение";
        // A process name is safe to display; never include window titles or paths.
        var characters = name.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.')
            .Take(48).ToArray();
        return characters.Length == 0 ? "Неизвестное приложение" : new string(characters);
    }
}
