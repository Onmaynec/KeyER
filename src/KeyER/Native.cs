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
    private static extern short GetAsyncKeyState(int virtualKey);

    public static bool IsPressed(Keys key) =>
        (GetAsyncKeyState((int)(key & Keys.KeyCode)) & 0x8000) != 0;

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
        // INPUT includes native union alignment; Marshal.SizeOf is architecture-aware.
        var size = Marshal.SizeOf<INPUT>();
        var sent = SendInput((uint)inputs.Length, inputs, size);
        if (sent == inputs.Length) return;

        var error = Marshal.GetLastWin32Error();

        // SendInput normally inserts the entire batch, but in a partial write a
        // key-down might have been delivered without key-up. Best-effort release.
        var release = inputs.Take((int)sent)
            .Where(input => input.type == INPUT_KEYBOARD &&
                            (input.data.ki.dwFlags & KEYEVENTF_KEYUP) == 0)
            .Select(input => MakeKey(input.data.ki.wVk, input.data.ki.wScan,
                input.data.ki.dwFlags | KEYEVENTF_KEYUP))
            .Reverse()
            .ToArray();
        if (release.Length > 0)
            _ = SendInput((uint)release.Length, release, size);

        throw new Win32Exception(error, "Windows не приняла нажатие клавиши. Возможно, окно запущено с правами администратора.");
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
        return RegisterHotKey(hwnd, id, HotkeyModifiers(keys), key);
    }

    // Keys.LWin is a key code, NOT a flag. Bitwise-AND with it incorrectly
    // interpreted e.g. F5 as Win+F5, making all default hotkeys unusable.
    internal static uint HotkeyModifiers(Keys keys)
    {
        uint flags = 0x4000; // MOD_NOREPEAT
        if ((keys & Keys.Control) == Keys.Control) flags |= 0x0002;
        if ((keys & Keys.Alt) == Keys.Alt) flags |= 0x0001;
        if ((keys & Keys.Shift) == Keys.Shift) flags |= 0x0004;
        return flags;
    }

    public static string HotkeyText(Keys keys)
    {
        var parts = new List<string>();
        if ((keys & Keys.Control) != 0) parts.Add("Ctrl");
        if ((keys & Keys.Alt) != 0) parts.Add("Alt");
        if ((keys & Keys.Shift) != 0) parts.Add("Shift");
        parts.Add((keys & Keys.KeyCode).ToString());
        return string.Join("+", parts);
    }
}
