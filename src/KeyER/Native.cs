using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KeyER;

internal static class Native
{
    public const int WM_HOTKEY = 0x0312;
    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public INPUTUNION data;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    public static bool IsOwnWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        GetWindowThreadProcessId(hWnd, out var pid);
        return pid == (uint)Environment.ProcessId;
    }

    private static INPUT MakeKey(ushort vk, ushort scan, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        data = new INPUTUNION
        {
            ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags }
        }
    };

    private static void Send(params INPUT[] inputs)
    {
        // Marshal.SizeOf<INPUT>() is 40 bytes on x64, including the Win32 union alignment.
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows отклонила ввод. Возможно, целевое окно запущено с правами администратора.");
    }

    public static void TypeUnicode(char codeUnit) => Send(
        MakeKey(0, codeUnit, KEYEVENTF_UNICODE),
        MakeKey(0, codeUnit, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));

    public static void PressKey(Keys key)
    {
        var vk = (ushort)(key & Keys.KeyCode);
        if (vk == 0) throw new ArgumentOutOfRangeException(nameof(key));
        Send(MakeKey(vk, 0, 0), MakeKey(vk, 0, KEYEVENTF_KEYUP));
    }

    public static void PressChord(Keys modifier, Keys key)
    {
        var vkMod = (ushort)(modifier & Keys.KeyCode);
        var vkKey = (ushort)(key & Keys.KeyCode);
        Send(MakeKey(vkMod, 0, 0), MakeKey(vkKey, 0, 0),
             MakeKey(vkKey, 0, KEYEVENTF_KEYUP), MakeKey(vkMod, 0, KEYEVENTF_KEYUP));
    }

    public static bool TryRegisterHotkey(IntPtr hwnd, int id, Keys keys)
    {
        var key = (uint)(keys & Keys.KeyCode);
        if (key == 0) return false;
        uint modifiers = 0x4000; // MOD_NOREPEAT
        if ((keys & Keys.Control) != 0) modifiers |= 0x0002;
        if ((keys & Keys.Alt) != 0) modifiers |= 0x0001;
        if ((keys & Keys.Shift) != 0) modifiers |= 0x0004;
        if ((keys & Keys.LWin) != 0) modifiers |= 0x0008;
        return RegisterHotKey(hwnd, id, modifiers, key);
    }

    public static string HotkeyText(Keys keys)
    {
        var parts = new List<string>();
        if ((keys & Keys.Control) != 0) parts.Add("Ctrl");
        if ((keys & Keys.Alt) != 0) parts.Add("Alt");
        if ((keys & Keys.Shift) != 0) parts.Add("Shift");
        if ((keys & Keys.LWin) != 0) parts.Add("Win");
        parts.Add((keys & Keys.KeyCode).ToString());
        return string.Join("+", parts);
    }
}
