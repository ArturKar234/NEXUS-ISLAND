using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NexusIsland;
public partial class MainWindow : Window
{
 const int GWL_EXSTYLE=-20, WS_EX_TRANSPARENT=0x20, WS_EX_LAYERED=0x80000, HOTKEY_ID=0x4E58;
 const uint MOD_ALT=1, MOD_CONTROL=2, VK_I=0x49;
 readonly DispatcherTimer clock=new(){Interval=TimeSpan.FromSeconds(1)};
 readonly DispatcherTimer pointer=new(){Interval=TimeSpan.FromMilliseconds(120)};
 HwndSource? source; IntPtr hwnd; bool passThrough=true, expanded=false;
 public MainWindow(){InitializeComponent();clock.Tick+=(_,_)=>UpdateClock();pointer.Tick+=(_,_)=>CheckPointer();UpdateClock();}
 void Window_Loaded(object sender,RoutedEventArgs e){Left=(SystemParameters.PrimaryScreenWidth-Width)/2;Top=0;hwnd=new WindowInteropHelper(this).Handle;source=HwndSource.FromHwnd(hwnd);source?.AddHook(WndProc);RegisterHotKey(hwnd,HOTKEY_ID,MOD_CONTROL|MOD_ALT,VK_I);clock.Start();pointer.Start();ApplyMode();}
 void UpdateClock(){var n=DateTime.Now;TimeText.Text=n.ToString("HH:mm");DateText.Text=n.ToString("ddd, d MMM",CultureInfo.GetCultureInfo("ru-RU"));LanguageText.Text=GetLayout();}
 static string GetLayout(){var w=GetForegroundWindow();var tid=GetWindowThreadProcessId(w,out _);var id=(int)(GetKeyboardLayout(tid).ToInt64()&0xffff);return id switch{0x0419=>"RU",0x0409=>"EN",_=>id.ToString("X4")};}
 void CheckPointer(){if(hwnd==IntPtr.Zero)return;GetCursorPos(out var p);var r=new RECT();if(!GetWindowRect(hwnd,ref r))return;bool over=p.X>=r.Left&&p.X<r.Right&&p.Y>=r.Top&&p.Y<r.Bottom;Opacity=passThrough&&over?0.38:1.0;if(!passThrough&&over&&!expanded)Expand();if(!over&&expanded)CollapsePanel();}
 void ApplyMode(){long s=GetWindowLongPtr(hwnd,GWL_EXSTYLE).ToInt64();s|=WS_EX_LAYERED;if(passThrough)s|=WS_EX_TRANSPARENT;else s&=~(long)WS_EX_TRANSPARENT;SetWindowLongPtr(hwnd,GWL_EXSTYLE,new IntPtr(s));ModeText.Text=passThrough?"Сквозной режим включён — клики проходят сквозь остров":"Режим управления — остров принимает клики";}
 void ToggleMode(){passThrough=!passThrough;ApplyMode();Opacity=1;}
 void Toggle_Click(object sender,RoutedEventArgs e)=>ToggleMode();
 void Collapse_Click(object sender,RoutedEventArgs e)=>CollapsePanel();
 void Exit_Click(object sender,RoutedEventArgs e)=>Close();
 void Expand(){expanded=true;ExpandedPanel.Visibility=Visibility.Visible;AnimateHeight(158);}
 void CollapsePanel(){if(!expanded)return;expanded=false;AnimateHeight(54);ExpandedPanel.Visibility=Visibility.Collapsed;}
 void AnimateHeight(double h)=>BeginAnimation(HeightProperty,new DoubleAnimation(h,TimeSpan.FromMilliseconds(220)){EasingFunction=new QuadraticEase{EasingMode=EasingMode.EaseOut}});
 IntPtr WndProc(IntPtr h,int m,IntPtr wp,IntPtr lp,ref bool handled){if(m==0x312&&wp.ToInt32()==HOTKEY_ID){ToggleMode();handled=true;}return IntPtr.Zero;}
 void Window_Closing(object? s,System.ComponentModel.CancelEventArgs e){clock.Stop();pointer.Stop();if(hwnd!=IntPtr.Zero)UnregisterHotKey(hwnd,HOTKEY_ID);source?.RemoveHook(WndProc);}
 [StructLayout(LayoutKind.Sequential)] struct POINT{public int X,Y;}
 [StructLayout(LayoutKind.Sequential)] struct RECT{public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,ref RECT r);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint id);
 [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
 [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h,int i);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h,int i,IntPtr v);
}
