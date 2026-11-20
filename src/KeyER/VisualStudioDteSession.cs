using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace KeyER;

/// <summary>
/// Experimental Visual Studio DTE automation adapter. Runs on the WinForms STA.
/// No VS SDK dependency is needed: discovers the matching DTE instance by PID.
/// Only blank documents are eligible for automatic verification/repair.
/// </summary>
internal sealed class VisualStudioDteSession
{
    private readonly dynamic _dte;
    private readonly IntPtr _targetHwnd;
    private readonly uint _targetPid;
    private readonly string _documentName;

    private VisualStudioDteSession(object dte, IntPtr targetHwnd, uint targetPid,
        string documentName)
    {
        _dte = dte;
        _targetHwnd = targetHwnd;
        _targetPid = targetPid;
        _documentName = documentName;
    }

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(uint reserved, out IRunningObjectTable rot);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(uint reserved, out IBindCtx context);

    internal static bool TryPrepare(IntPtr target, out VisualStudioDteSession? session,
        out string diagnostic)
    {
        session = null;
        diagnostic = "";
        var pid = Native.GetWindowProcessId(target);
        if (pid == 0 || !string.Equals(Native.GetWindowProcessName(target),
            "devenv", StringComparison.OrdinalIgnoreCase))
        {
            diagnostic = "Для интеграции Visual Studio выберите окно devenv.exe.";
            return false;
        }

        try
        {
            if (GetRunningObjectTable(0, out var rot) != 0 ||
                CreateBindCtx(0, out var context) != 0)
            {
                diagnostic = "Невозможно получить Running Object Table Visual Studio.";
                return false;
            }

            rot.EnumRunning(out var iterator);
            var monikers = new IMoniker[1];
            while (iterator.Next(1, monikers, IntPtr.Zero) == 0)
            {
                var moniker = monikers[0];
                moniker.GetDisplayName(context, null, out var display);
                if (!display.StartsWith("!VisualStudio.DTE.", StringComparison.OrdinalIgnoreCase))
                    continue;

                var colon = display.LastIndexOf(':');
                if (colon < 0 || !uint.TryParse(display[(colon + 1)..], out var objectPid) ||
                    objectPid != pid)
                    continue;

                rot.GetObject(moniker, out var value);
                if (value is null) continue;
                dynamic dte = value;
                dynamic document = dte.ActiveDocument;
                if (document is null)
                {
                    diagnostic = "В Visual Studio нет активного текстового документа.";
                    return false;
                }

                dynamic textDocument = document.Object("TextDocument");
                string text = textDocument.StartPoint.CreateEditPoint().GetText(textDocument.EndPoint);
                if (text.Length != 0)
                {
                    diagnostic = "Проверяемый набор Visual Studio поддерживает только пустой документ.";
                    return false;
                }

                string name = document.FullName;
                session = new VisualStudioDteSession(value, target, pid, name);
                diagnostic = "Visual Studio DTE: пустой редактор подтверждён.";
                return true;
            }

            diagnostic = "Экземпляр DTE для активного окна Visual Studio не найден. " +
                         "Проверьте, что Visual Studio полностью запущена и права приложений совпадают.";
            return false;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("visualstudio-prepare", ex);
            diagnostic = "Не удалось прочитать редактор Visual Studio через DTE: " +
                         ex.GetType().Name;
            return false;
        }
    }

    internal bool IsStillActive()
    {
        try
        {
            if (Native.GetForegroundWindow() != _targetHwnd ||
                Native.GetWindowProcessId(_targetHwnd) != _targetPid)
                return false;
            dynamic doc = _dte.ActiveDocument;
            return doc is not null && (string)doc.FullName == _documentName;
        }
        catch { return false; }
    }

    internal (bool matched, bool repaired, string message) Finish(string source,
        CodeIntegrityMode mode)
    {
        if (Native.GetForegroundWindow() != _targetHwnd ||
            Native.GetWindowProcessId(_targetHwnd) != _targetPid)
            return (false, false, "Окно Visual Studio изменилось. Сверка отменена.");

        try
        {
            dynamic document = _dte.ActiveDocument;
            if (document is null || (string)document.FullName != _documentName)
                return (false, false, "Активный документ Visual Studio изменился. Сверка отменена.");

            dynamic textDocument = document.Object("TextDocument");
            string original = textDocument.StartPoint.CreateEditPoint().GetText(textDocument.EndPoint);
            var expected = Normalize(source);
            if (Normalize(original) == expected)
                return (true, false, "Visual Studio: итоговый код совпадает с исходником.");

            if (mode != CodeIntegrityMode.VerifyAndRepair)
                return (false, false, "Visual Studio: обнаружено расхождение, исходник не изменён.");

            // TextDocument.EditPoint.ReplaceText uses the IDE editor API, not
            // synthetic keyboard events. Only opt-in and only after blank preflight.
            dynamic point = textDocument.StartPoint.CreateEditPoint();
            point.ReplaceText(textDocument.EndPoint, expected, 0);
            string after = textDocument.StartPoint.CreateEditPoint().GetText(textDocument.EndPoint);
            var matched = Normalize(after) == expected;
            return matched
                ? (true, true, "Visual Studio: расхождения исправлены через DTE.")
                : (false, false, "Visual Studio не подтвердила точное совпадение после исправления.");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("visualstudio-complete", ex);
            return (false, false, "Ошибка DTE при проверке Visual Studio: " + ex.GetType().Name);
        }
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
}
