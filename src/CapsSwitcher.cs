// CapsSwitcher — переключает уже набранный текст между русской и английской раскладкой по F1.
// Компилируется встроенным в Windows компилятором .NET Framework 4.x (C# 5), без внешних зависимостей.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("CapsSwitcher")]
[assembly: System.Reflection.AssemblyProduct("CapsSwitcher")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace CapsSwitcher
{
    public static class LayoutConverter
    {
        // Символы, которые даёт одна и та же клавиша в английской (US) и русской раскладке.
        const string En = "`qwertyuiop[]asdfghjkl;'zxcvbnm,./~QWERTYUIOP{}ASDFGHJKL:\"ZXCVBNM<>?@#$^&|";
        const string Ru = "ёйцукенгшщзхъфывапролджэячсмитьбю.ЁЙЦУКЕНГШЩЗХЪФЫВАПРОЛДЖЭЯЧСМИТЬБЮ,\"№;:?/";

        static readonly Dictionary<char, char> EnToRu = new Dictionary<char, char>();
        static readonly Dictionary<char, char> RuToEn = new Dictionary<char, char>();

        static LayoutConverter()
        {
            for (int i = 0; i < En.Length; i++)
            {
                EnToRu[En[i]] = Ru[i];
                RuToEn[Ru[i]] = En[i];
            }
        }

        static bool IsCyrillic(char c) { return (c >= 'Ѐ' && c <= 'ӿ'); }
        static bool IsLatin(char c) { return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'); }

        /// <summary>true — текст в основном русский (значит, переводим в английский).</summary>
        public static bool LooksRussian(string text)
        {
            int ru = 0, en = 0;
            foreach (char c in text)
            {
                if (IsCyrillic(c)) ru++;
                else if (IsLatin(c)) en++;
            }
            return ru > en;
        }

        public static string Convert(string text, out bool toEnglish)
        {
            toEnglish = LooksRussian(text);
            Dictionary<char, char> map = toEnglish ? RuToEn : EnToRu;
            char[] result = text.ToCharArray();
            for (int i = 0; i < result.Length; i++)
            {
                char mapped;
                if (map.TryGetValue(result[i], out mapped)) result[i] = mapped;
            }
            return new string(result);
        }
    }

    static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const int WM_INPUTLANGCHANGEREQUEST = 0x0050;
        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern IntPtr LoadKeyboardLayout(string id, uint flags);
        [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT { public uint type; public InputUnion u; }

        static INPUT Key(Keys vk, bool up)
        {
            INPUT inp = new INPUT();
            inp.type = INPUT_KEYBOARD;
            inp.u.ki.wVk = (ushort)vk;
            inp.u.ki.dwFlags = up ? KEYEVENTF_KEYUP : 0;
            return inp;
        }

        /// <summary>Нажать сочетание: модификаторы по порядку, затем клавишу, затем отпустить в обратном порядке.</summary>
        public static void Chord(params Keys[] keys)
        {
            List<INPUT> list = new List<INPUT>();
            foreach (Keys k in keys) list.Add(Key(k, false));
            for (int i = keys.Length - 1; i >= 0; i--) list.Add(Key(keys[i], true));
            INPUT[] arr = list.ToArray();
            SendInput((uint)arr.Length, arr, Marshal.SizeOf(typeof(INPUT)));
        }
    }

    class TrayApp : Form
    {
        const int HotkeyId = 0xCA95;
        const string AppName = "CapsSwitcher";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        readonly NotifyIcon tray = new NotifyIcon();
        readonly ToolStripMenuItem enabledItem;
        readonly ToolStripMenuItem autostartItem;
        bool enabled = true;
        bool busy;

        public TrayApp()
        {
            ShowInTaskbar = false;

            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem title = new ToolStripMenuItem("CapsSwitcher — F1 меняет раскладку текста");
            title.Enabled = false;
            menu.Items.Add(title);
            menu.Items.Add(new ToolStripSeparator());
            enabledItem = new ToolStripMenuItem("Включено", null, delegate { SetEnabled(!enabled); });
            enabledItem.Checked = true;
            menu.Items.Add(enabledItem);
            autostartItem = new ToolStripMenuItem("Запускать вместе с Windows", null, delegate { ToggleAutostart(); });
            autostartItem.Checked = IsAutostart();
            menu.Items.Add(autostartItem);
            menu.Items.Add(new ToolStripMenuItem("Как пользоваться", null, delegate { ShowHelp(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Выход", null, delegate { ExitApp(); }));

            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { SetEnabled(!enabled); };
            UpdateTray();
            tray.Visible = true;
        }

        // Окно никогда не показывается — программа живёт только в трее.
        protected override void SetVisibleCore(bool value)
        {
            if (!IsHandleCreated) CreateHandle();
            base.SetVisibleCore(false);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!Native.RegisterHotKey(Handle, HotkeyId, 0, (uint)Keys.F1))
            {
                tray.ShowBalloonTip(5000, AppName,
                    "Не удалось занять клавишу F1 — её уже использует другая программа.", ToolTipIcon.Warning);
            }
        }

        void ExitApp()
        {
            Native.UnregisterHotKey(Handle, HotkeyId);
            tray.Visible = false;
            tray.Dispose();
            Application.Exit();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                if (enabled && !busy)
                {
                    busy = true;
                    try { ConvertActiveText(); }
                    catch (Exception ex) { tray.ShowBalloonTip(3000, AppName, "Ошибка: " + ex.Message, ToolTipIcon.Error); }
                    finally { busy = false; }
                }
                return;
            }
            base.WndProc(ref m);
        }

        // ---------- основная логика ----------

        void ConvertActiveText()
        {
            IntPtr target = Native.GetForegroundWindow();
            WaitForModifiersReleased();

            IDataObject saved = BackupClipboard();

            // 1) Пробуем взять выделенный текст.
            string text = CopyFromApp(new[] { Keys.ControlKey, Keys.C });

            // 2) Ничего не выделено — выделяем всё от курсора до начала поля и берём это.
            if (string.IsNullOrEmpty(text))
            {
                Native.Chord(Keys.ControlKey, Keys.ShiftKey, Keys.Home);
                Thread.Sleep(30);
                text = CopyFromApp(new[] { Keys.ControlKey, Keys.C });
            }

            if (string.IsNullOrEmpty(text))
            {
                RestoreClipboard(saved);
                return;
            }

            bool toEnglish;
            string converted = LayoutConverter.Convert(text, out toEnglish);

            SetClipboardText(converted);
            Native.Chord(Keys.ControlKey, Keys.V);
            Thread.Sleep(150); // даём приложению вставить текст, прежде чем вернуть буфер обмена

            SwitchLayout(target, toEnglish);
            RestoreClipboard(saved);
        }

        static void WaitForModifiersReleased()
        {
            int[] vks = { 0x10, 0x11, 0x12, 0x5B, 0x5C, 0x70 }; // Shift, Ctrl, Alt, Win, Win, F1
            for (int i = 0; i < 100; i++)
            {
                bool pressed = false;
                foreach (int vk in vks) if ((Native.GetAsyncKeyState(vk) & 0x8000) != 0) pressed = true;
                if (!pressed) return;
                Thread.Sleep(10);
            }
        }

        static string CopyFromApp(Keys[] chord)
        {
            try { Clipboard.Clear(); } catch { }
            uint before = Native.GetClipboardSequenceNumber();
            Native.Chord(chord);
            for (int i = 0; i < 40; i++) // до ~400 мс
            {
                Thread.Sleep(10);
                if (Native.GetClipboardSequenceNumber() != before)
                {
                    Thread.Sleep(20);
                    try { if (Clipboard.ContainsText()) return Clipboard.GetText(); } catch { }
                }
            }
            return null;
        }

        static void SetClipboardText(string text)
        {
            for (int i = 0; i < 10; i++)
            {
                try { Clipboard.SetText(text); return; }
                catch (ExternalException) { Thread.Sleep(20); }
            }
        }

        static IDataObject BackupClipboard()
        {
            try
            {
                IDataObject src = Clipboard.GetDataObject();
                if (src == null) return null;
                DataObject copy = new DataObject();
                foreach (string fmt in src.GetFormats(false))
                {
                    try { object data = src.GetData(fmt, false); if (data != null) copy.SetData(fmt, data); }
                    catch { }
                }
                return copy;
            }
            catch { return null; }
        }

        static void RestoreClipboard(IDataObject saved)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    if (saved == null || saved.GetFormats().Length == 0) Clipboard.Clear();
                    else Clipboard.SetDataObject(saved, true);
                    return;
                }
                catch (ExternalException) { Thread.Sleep(20); }
            }
        }

        static void SwitchLayout(IntPtr window, bool toEnglish)
        {
            // После исправления текста переключаем раскладку окна, чтобы дальше печаталось правильно.
            IntPtr hkl = Native.LoadKeyboardLayout(toEnglish ? "00000409" : "00000419", 0);
            if (hkl != IntPtr.Zero && window != IntPtr.Zero)
                Native.PostMessage(window, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hkl);
        }

        // ---------- трей и настройки ----------

        void SetEnabled(bool value)
        {
            enabled = value;
            enabledItem.Checked = value;
            UpdateTray();
        }

        void UpdateTray()
        {
            Icon old = tray.Icon;
            tray.Icon = MakeIcon(enabled);
            tray.Text = enabled ? "CapsSwitcher: F1 — сменить раскладку текста" : "CapsSwitcher: выключено";
            if (old != null) { Native.DestroyIcon(old.Handle); old.Dispose(); }
        }

        static Icon MakeIcon(bool active)
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                Color bg = active ? Color.FromArgb(37, 99, 235) : Color.FromArgb(120, 120, 120);
                using (SolidBrush b = new SolidBrush(bg)) g.FillRectangle(b, 1, 1, 30, 30);
                using (Font f = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("EN", f, Brushes.White, new RectangleF(0, -1, 32, 17), sf);
                    g.DrawString("RU", f, Brushes.White, new RectangleF(0, 14, 32, 17), sf);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        static bool IsAutostart()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(AppName) != null;
        }

        void ToggleAutostart()
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (IsAutostart()) k.DeleteValue(AppName, false);
                else k.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
            }
            autostartItem.Checked = IsAutostart();
        }

        void ShowHelp()
        {
            MessageBox.Show(
                "Набрали текст не в той раскладке? Нажмите F1.\n\n" +
                "• Если текст выделен — переключится выделенный текст.\n" +
                "• Если ничего не выделено — весь текст от курсора до начала поля ввода.\n\n" +
                "Направление определяется автоматически (рус → англ или англ → рус),\n" +
                "после этого раскладка окна тоже переключается.\n" +
                "Повторное нажатие F1 возвращает как было (или Ctrl+Z).\n\n" +
                "Двойной клик по значку в трее — включить/выключить.",
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, @"Local\CapsSwitcher.SingleInstance", out created))
            {
                if (!created) return; // уже запущена
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }
    }
}
