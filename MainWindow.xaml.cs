using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NexusIsland;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _pointerTimer = new() { Interval = TimeSpan.FromMilliseconds(70) };
    private readonly DispatcherTimer _systemTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _expanded;
    private IntPtr _handle;
    private bool _passThrough;
    private ulong _lastIdle, _lastKernel, _lastUser;
    private double _cpuUsage;
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;

    public MainWindow()
    {
        InitializeComponent();
        _clockTimer.Tick += (_, _) => UpdateClock();
        _pointerTimer.Tick += (_, _) => UpdatePointerMode();
        _systemTimer.Tick += (_, _) => UpdateSystemInfo();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 0;
        int style = GetWindowLong(_handle, GwlExStyle);
        SetWindowLong(_handle, GwlExStyle, style | WsExToolWindow | WsExNoActivate);
        UpdateClock();
        UpdateSystemInfo();
        LoadNote();
        _clockTimer.Start();
        _pointerTimer.Start();
        _systemTimer.Start();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _clockTimer.Stop(); _pointerTimer.Stop(); _systemTimer.Stop();
    }

    private void UpdateClock()
    {
        DateTime now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm");
        DayText.Text = now.ToString("dddd", new CultureInfo("ru-RU")).ToUpper(new CultureInfo("ru-RU"));
        DateText.Text = now.ToString("d MMMM", new CultureInfo("ru-RU")).ToUpper(new CultureInfo("ru-RU"));
        FullDateText.Text = now.ToString("dddd, d MMMM yyyy", new CultureInfo("ru-RU"));
        string lang = GetActiveKeyboardLanguage();
        LanguageText.Text = lang;
        FullLanguageText.Text = lang == "RU" ? "RU — Русский" : lang == "EN" ? "EN — English" : lang;
    }

    private static string GetActiveKeyboardLanguage()
    {
        IntPtr foreground = GetForegroundWindow();
        uint thread = GetWindowThreadProcessId(foreground, out _);
        IntPtr layout = GetKeyboardLayout(thread);
        int id = unchecked((int)(long)layout) & 0xFFFF;
        return id switch { 0x0419 => "RU", 0x0409 or 0x0809 or 0x0C09 => "EN", _ => id.ToString("X4") };
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        if (_expanded) CollapseIsland(); else ExpandIsland();
    }

    private void ExpandIsland()
    {
        _expanded = true;
        _passThrough = false;
        ApplyTransparentStyle(false);
        ExpandButton.Content = "⌃";
        Dashboard.Visibility = Visibility.Visible;
        var height = new DoubleAnimation(54, 660, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        BeginAnimation(HeightProperty, height);
        Dashboard.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        IslandShell.Opacity = 1;
        UpdateSystemInfo();
    }

    private void CollapseIsland()
    {
        _expanded = false;
        ExpandButton.Content = "⌄";
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(130));
        fade.Completed += (_, _) => Dashboard.Visibility = Visibility.Collapsed;
        Dashboard.BeginAnimation(OpacityProperty, fade);
        var height = new DoubleAnimation(660, 54, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
        BeginAnimation(HeightProperty, height);
    }

    private void UpdatePointerMode()
    {
        if (_handle == IntPtr.Zero || _expanded) return;
        GetCursorPos(out POINT p);
        GetWindowRect(_handle, out RECT r);
        bool inside = p.X >= r.Left && p.X <= r.Right && p.Y >= r.Top && p.Y <= r.Bottom;
        bool onExpand = inside && p.X >= r.Right - 54 && p.Y <= r.Top + 54;
        bool shouldPass = inside && !onExpand;
        if (shouldPass != _passThrough)
        {
            _passThrough = shouldPass;
            ApplyTransparentStyle(shouldPass);
        }
        IslandShell.Opacity = shouldPass ? 0.62 : 1.0;
    }

    private void ApplyTransparentStyle(bool transparent)
    {
        int style = GetWindowLong(_handle, GwlExStyle);
        style = transparent ? style | WsExTransparent : style & ~WsExTransparent;
        SetWindowLong(_handle, GwlExStyle, style);
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        HomePage.Visibility = Visibility.Collapsed;
        SystemPage.Visibility = Visibility.Collapsed;
        NotesPage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;
        switch (button.Tag?.ToString())
        {
            case "home": HomePage.Visibility = Visibility.Visible; break;
            case "system": SystemPage.Visibility = Visibility.Visible; UpdateSystemInfo(); break;
            case "notes": NotesPage.Visibility = Visibility.Visible; break;
            case "settings": SettingsPage.Visibility = Visibility.Visible; break;
        }
    }

    private void SaveNote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NexusIsland");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "note.txt"), NotesBox.Text);
            NoteStatusText.Text = "Сохранено на этом компьютере.";
        }
        catch (Exception ex) { NoteStatusText.Text = "Не удалось сохранить: " + ex.Message; }
    }

    private void LoadNote()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NexusIsland", "note.txt");
        if (File.Exists(path)) NotesBox.Text = File.ReadAllText(path);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => UpdateSystemInfo();

    private void UpdateSystemInfo()
    {
        try
        {
            GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);
            ulong i = ToUInt64(idle), k = ToUInt64(kernel), u = ToUInt64(user);
            if (_lastKernel != 0)
            {
                ulong total = (k - _lastKernel) + (u - _lastUser);
                ulong busy = total > (i - _lastIdle) ? total - (i - _lastIdle) : 0;
                _cpuUsage = total == 0 ? 0 : busy * 100.0 / total;
            }
            _lastIdle = i; _lastKernel = k; _lastUser = u;
            MEMORYSTATUSEX mem = new();
            if (GlobalMemoryStatusEx(ref mem))
            {
                double used = (mem.TotalPhys - mem.AvailPhys) / 1024d / 1024 / 1024;
                double total = mem.TotalPhys / 1024d / 1024 / 1024;
                CpuText.Text = $"{_cpuUsage:0}%";
                RamText.Text = $"{used:0.0}";
                RamSubText.Text = $"из {total:0.0} ГБ";
                SystemDetailsText.Text = $"Процессор: {_cpuUsage:0.0}% загрузки\nОперативная память: {used:0.00} из {total:0.00} ГБ ({mem.MemoryLoad}%)\nОперационная система: Windows {Environment.OSVersion.Version}\nИмя компьютера: {Environment.MachineName}\nАрхитектура: { (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit") }";
            }
            SYSTEM_POWER_STATUS power = new();
            if (GetSystemPowerStatus(out power) && power.BatteryLifePercent <= 100)
            {
                BatteryText.Text = power.BatteryLifePercent + "%";
                BatterySubText.Text = power.ACLineStatus == 1 ? "подключено питание" : "от батареи";
            }
            else { BatteryText.Text = "—"; BatterySubText.Text = "нет батареи"; }
        }
        catch { CpuText.Text = "—"; RamText.Text = "—"; BatteryText.Text = "—"; }
    }

    private static ulong ToUInt64(FILETIME value) => ((ulong)value.dwHighDateTime << 32) | value.dwLowDateTime;

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct FILETIME { public uint dwLowDateTime, dwHighDateTime; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint MemoryLoad; public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }
    [StructLayout(LayoutKind.Sequential)] private struct SYSTEM_POWER_STATUS { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public uint BatteryLifeTime, BatteryFullLifeTime; }

    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint threadId);
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);
    [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr hWnd, int index, int value);
}
