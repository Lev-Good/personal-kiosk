using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace PersonalKiosk;

internal sealed class Guard : IDisposable
{
    private readonly Settings settings;
    private readonly Action home;
    private readonly Func<string, bool> ask;
    private readonly Action<string> fault;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer timer;
    private readonly Native.KeyboardProc callback;
    private IntPtr hook;
    private bool busy, disposed;
    private readonly HashSet<IntPtr> minimized = new();
    private readonly HashSet<IntPtr> approvedDialogs = new();
    private readonly Dictionary<uint, (string Image, DateTime Until)> images = new();
    private uint pendingPid;
    private DateTime pendingUntil;
    internal Action? ApprovedForeground { get; set; }

    internal Guard(Settings settings, Action home, Func<string, bool> ask, Action<string> fault, Dispatcher dispatcher)
    {
        this.settings = settings; this.home = home; this.ask = ask; this.fault = fault; this.dispatcher = dispatcher;
        callback = Hook;
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Input, (_, _) => Tick(), dispatcher);
        timer.Stop();
    }
    internal void Start()
    {
        hook = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) throw new InvalidOperationException("לא ניתן להפעיל את חסימת המקשים. מצב הקיוסק לא הופעל.");
        timer.Start();
    }
    private string Image(uint pid)
    {
        if (images.TryGetValue(pid, out var entry) && entry.Until > DateTime.UtcNow) return entry.Image;
        var image = Native.Image(pid); images[pid] = (image, DateTime.UtcNow.AddSeconds(1)); return image;
    }
    private bool Allowed(uint pid) => pid == Environment.ProcessId || settings.Apps.Any(app => Policy.AllowedImage(app, Image(pid)));
    private static bool Dialog(IntPtr hwnd)
    {
        var cls = Native.Class(hwnd);
        return cls == "#32770" || cls.StartsWith("bosa_sdm", StringComparison.OrdinalIgnoreCase);
    }
    private void Tick()
    {
        if (busy || disposed) return;
        try
        {
            approvedDialogs.RemoveWhere(h => !Native.IsWindow(h));
            // Snapshot first: a password prompt must never run inside an EnumWindows callback.
            var windows = new List<IntPtr>();
            Native.EnumWindows((h, _) => { if (Native.IsWindowVisible(h) && !Native.IsIconic(h)) windows.Add(h); return true; }, IntPtr.Zero);
            foreach (var hwnd in windows)
            {
                var pid = Native.Pid(hwnd);
                if (pid == Environment.ProcessId) continue;
                var cls = Native.Class(hwnd);
                if (cls is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW") continue;
                if (!Allowed(pid))
                {
                    if (!string.IsNullOrEmpty(Native.Title(hwnd))) { Native.ShowWindow(hwnd, 6); minimized.Add(hwnd); }
                    continue;
                }
                if (settings.GateDialogs && Dialog(hwnd) && !approvedDialogs.Contains(hwnd))
                {
                    if (pid == pendingPid && pendingUntil > DateTime.UtcNow)
                    {
                        pendingPid = 0; approvedDialogs.Add(hwnd); continue;
                    }
                    GateDialog(hwnd);
                    return;
                }
            }
            var foreground = Native.GetForegroundWindow();
            var foregroundPid = Native.Pid(foreground);
            if (foregroundPid != Environment.ProcessId)
            {
                if (!Allowed(foregroundPid)) home();
                else ApprovedForeground?.Invoke();
            }
        }
        catch (Exception ex) { timer.Stop(); fault("ניטור החלונות נעצר: " + ex.Message); }
    }
    private void GateDialog(IntPtr hwnd)
    {
        busy = true;
        Native.ShowWindow(hwnd, 0);
        try
        {
            if (ask("אישור פעולה בתוכנה")) { approvedDialogs.Add(hwnd); Native.ShowWindow(hwnd, 5); Native.SetForegroundWindow(hwnd); }
            else { Native.PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero); home(); }
        }
        finally { if (Native.IsWindow(hwnd) && approvedDialogs.Contains(hwnd)) Native.ShowWindow(hwnd, 5); busy = false; }
    }
    private IntPtr Hook(int code, IntPtr message, IntPtr data)
    {
        try
        {
            if (code < 0 || disposed) return Native.CallNextHookEx(hook, code, message, data);
            var key = Marshal.PtrToStructure<Native.KeyData>(data);
            if ((key.flags & 0x10) != 0) return Native.CallNextHookEx(hook, code, message, data);
            bool down = message == (IntPtr)0x100 || message == (IntPtr)0x104;
            bool ctrl = (Native.GetAsyncKeyState(0x11) & 0x8000) != 0;
            bool alt = (key.flags & 0x20) != 0;
            bool shift = (Native.GetAsyncKeyState(0x10) & 0x8000) != 0;
            bool win = (Native.GetAsyncKeyState(0x5B) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x5C) & 0x8000) != 0;
            if (key.vkCode is 0x5B or 0x5C || win || (alt && key.vkCode is 0x09 or 0x1B or 0x73 or 0x20) || (ctrl && key.vkCode == 0x1B)) return (IntPtr)1;
            if (down && ctrl && alt && key.vkCode == 0x24) { dispatcher.BeginInvoke(home); return (IntPtr)1; }
            if (down && settings.GateDialogs && ctrl && !alt && key.vkCode is 0x4F or 0x53 or 0x50 or 0x4B)
            {
                var target = Native.GetForegroundWindow(); var pid = Native.Pid(target);
                if (pid != Environment.ProcessId && Allowed(pid))
                {
                    if (!busy) { busy = true; dispatcher.BeginInvoke(() => GateShortcut(target, (int)key.vkCode, shift)); }
                    return (IntPtr)1;
                }
            }
        }
        catch { dispatcher.BeginInvoke(() => { timer.Stop(); fault("אירעה תקלה בחסימת המקשים. יש לצאת מהקיוסק ולנסות שוב."); }); }
        return Native.CallNextHookEx(hook, code, message, data);
    }
    private void GateShortcut(IntPtr target, int key, bool shift)
    {
        try
        {
            if (!ask(key switch { 0x4F => "אישור פתיחת מסמך", 0x53 => "אישור שמירת מסמך", 0x50 => "אישור הדפסה", _ => "אישור קישור" })) { home(); return; }
            if (!Native.IsWindow(target) || !Allowed(Native.Pid(target))) return;
            pendingPid = Native.Pid(target); pendingUntil = DateTime.UtcNow.AddSeconds(3);
            Native.SetForegroundWindow(target);
            if (Native.GetForegroundWindow() != target) throw new IOException("לא ניתן להחזיר את המיקוד לתוכנה. יש לנסות שוב.");
            Native.ReplayShortcut(key, shift);
        }
        catch (Exception ex) { fault(ex.Message); }
        finally { busy = false; }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; timer.Stop(); if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
        foreach (var hwnd in minimized) if (Native.IsWindow(hwnd)) Native.ShowWindow(hwnd, 4);
        minimized.Clear();
    }
}
