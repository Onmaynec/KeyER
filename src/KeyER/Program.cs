namespace KeyER;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var singleInstance = SingleInstanceGuard.TryAcquire();
        if (singleInstance is null)
        {
            MessageBox.Show(
                "KeyER уже запущен. Переключитесь в существующее окно приложения.\n" +
                "Повторный экземпляр не открывается, чтобы избежать конфликта глобальных горячих клавиш.",
                "KeyER Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Application.ThreadException += (_, e) =>
        {
            DiagnosticLog.Error("ui-thread", e.Exception);
            MessageBox.Show(e.Exception.Message, "KeyER — ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
                DiagnosticLog.Error("unhandled", exception);
        };

        Application.Run(new MainForm());
    }
}
