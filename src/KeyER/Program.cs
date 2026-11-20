namespace KeyER;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
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
