// SPDX-License-Identifier: MIT
// Windows screensaver launcher for the Night Railway canvas animation.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string command = args.Length == 0 ? "/s" : args[0].ToLowerInvariant();
        if (command.StartsWith("/c") || command.StartsWith("-c"))
        {
            MessageBox.Show(
                "Night Railway\n\n夜間の鉄道前面展望を表示します。\n矢印キー、マウス操作、または Esc で終了します。\n\n通常表示では index.html をブラウザーで開き、速度や一時停止を操作できます。",
                "Night Railway Screensaver",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (command.StartsWith("/p") || command.StartsWith("-p"))
        {
            IntPtr parent = ParsePreviewHandle(args, command);
            if (parent != IntPtr.Zero)
                Application.Run(new PreviewForm(parent));
            return;
        }

        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string pagePath = Path.Combine(baseDirectory, "index.html");
        if (!File.Exists(pagePath))
        {
            MessageBox.Show(
                "index.html が見つかりません。NightRailway.scr と同じフォルダーに置いてください。",
                "Night Railway Screensaver",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        Application.Run(new SaverHost(pagePath));
    }

    private static IntPtr ParsePreviewHandle(string[] args, string command)
    {
        string raw = null;
        int colon = command.IndexOf(':');
        if (colon >= 0 && colon + 1 < command.Length)
            raw = command.Substring(colon + 1);
        else if (args.Length > 1)
            raw = args[1];

        long value;
        return raw != null && long.TryParse(raw, out value) ? new IntPtr(value) : IntPtr.Zero;
    }
}

internal sealed class SaverHost : Form
{
    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int WS_SYSMENU = 0x00080000;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    private readonly string pagePath;
    private readonly Timer monitorTimer;
    private readonly DateTime startedAt = DateTime.UtcNow;
    private Point lastCursor;
    private bool armed;
    private Process edgeProcess;
    private string temporaryProfile;
    private bool edgePositioned;
    private HashSet<int> edgeProcessesBeforeLaunch;
    private DateTime edgeLaunchTime;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr window, int index, int value);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    internal SaverHost(string pagePath)
    {
        this.pagePath = pagePath;
        BackColor = Color.FromArgb(2, 3, 10);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        TopMost = true;
        KeyPreview = true;

        monitorTimer = new Timer { Interval = 50 };
        monitorTimer.Tick += MonitorTimerOnTick;
        Shown += SaverHostOnShown;
        FormClosed += SaverHostOnClosed;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (armed) Close();
        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (armed) Close();
        base.OnMouseDown(e);
    }

    private void SaverHostOnShown(object sender, EventArgs e)
    {
        Cursor.Hide();
        lastCursor = Cursor.Position;
        try
        {
            StartEdge();
            monitorTimer.Start();
        }
        catch (Exception ex)
        {
            Cursor.Show();
            MessageBox.Show(
                "Microsoft Edge を起動できませんでした。\n\n" + ex.Message,
                "Night Railway Screensaver",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Close();
        }
    }

    private void StartEdge()
    {
        string edgePath = FindEdge();
        if (edgePath == null)
            throw new FileNotFoundException("Microsoft Edge が見つかりません。");

        edgeProcessesBeforeLaunch = new HashSet<int>();
        foreach (Process process in Process.GetProcessesByName("msedge"))
        {
            try { edgeProcessesBeforeLaunch.Add(process.Id); }
            finally { process.Dispose(); }
        }

        Rectangle desktop = SystemInformation.VirtualScreen;
        temporaryProfile = Path.Combine(Path.GetTempPath(), "NightRailwayScreensaver", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryProfile);

        string url = new Uri(pagePath).AbsoluteUri + "?screensaver=1";
        string arguments =
            "--app=\"" + url + "\" " +
            "--user-data-dir=\"" + temporaryProfile + "\" " +
            "--no-first-run --disable-extensions --disable-sync " +
            "--disable-session-crashed-bubble --disable-background-networking " +
            "--window-position=" + desktop.Left + "," + desktop.Top + " " +
            "--window-size=" + desktop.Width + "," + desktop.Height;

        edgeProcess = Process.Start(new ProcessStartInfo
        {
            FileName = edgePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(pagePath)
        });

        if (edgeProcess == null)
            throw new InvalidOperationException("Microsoft Edge のプロセスを開始できませんでした。");
        edgeLaunchTime = DateTime.UtcNow;
    }

    private void MonitorTimerOnTick(object sender, EventArgs e)
    {
        TryPositionEdge();

        if (!armed)
        {
            if ((DateTime.UtcNow - startedAt).TotalMilliseconds < 1500)
                return;
            armed = true;
            lastCursor = Cursor.Position;
            return;
        }

        Point current = Cursor.Position;
        if (Math.Abs(current.X - lastCursor.X) > 6 || Math.Abs(current.Y - lastCursor.Y) > 6 || AnyInputPressed())
        {
            Close();
            return;
        }
        lastCursor = current;

        if (edgePositioned && edgeProcess != null && edgeProcess.HasExited)
            Close();
        else if (!edgePositioned && (DateTime.UtcNow - edgeLaunchTime).TotalSeconds > 12)
            Close();
    }

    private void TryPositionEdge()
    {
        if (edgePositioned)
            return;

        IntPtr window = IntPtr.Zero;
        if (edgeProcess != null)
        {
            try
            {
                if (!edgeProcess.HasExited)
                {
                    edgeProcess.Refresh();
                    window = edgeProcess.MainWindowHandle;
                }
            }
            catch { window = IntPtr.Zero; }
        }

        if (window == IntPtr.Zero)
        {
            foreach (Process candidate in Process.GetProcessesByName("msedge"))
            {
                bool selected = false;
                try
                {
                    if (edgeProcessesBeforeLaunch.Contains(candidate.Id)) continue;
                    candidate.Refresh();
                    if (candidate.MainWindowHandle == IntPtr.Zero) continue;
                    if (edgeProcess != null) edgeProcess.Dispose();
                    edgeProcess = candidate;
                    selected = true;
                    window = candidate.MainWindowHandle;
                    break;
                }
                catch { }
                finally { if (!selected) candidate.Dispose(); }
            }
        }

        if (window == IntPtr.Zero)
            return;

        Rectangle desktop = SystemInformation.VirtualScreen;
        int style = GetWindowLong(window, GWL_STYLE);
        style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
        SetWindowLong(window, GWL_STYLE, style);
        SetWindowPos(window, HWND_TOPMOST, desktop.Left, desktop.Top, desktop.Width, desktop.Height, SWP_SHOWWINDOW);
        SetForegroundWindow(window);
        edgePositioned = true;
        TopMost = false;
    }

    private static bool AnyInputPressed()
    {
        for (int key = 1; key < 255; key++)
        {
            if ((GetAsyncKeyState(key) & 0x8000) != 0)
                return true;
        }
        return false;
    }

    private static string FindEdge()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "Application", "msedge.exe")
        };
        foreach (string candidate in candidates)
            if (File.Exists(candidate)) return candidate;
        return null;
    }

    private void SaverHostOnClosed(object sender, FormClosedEventArgs e)
    {
        monitorTimer.Stop();
        try
        {
            if (edgeProcess != null && !edgeProcess.HasExited)
            {
                edgeProcess.CloseMainWindow();
                if (!edgeProcess.WaitForExit(800)) edgeProcess.Kill();
            }
        }
        catch { }

        try
        {
            if (!string.IsNullOrEmpty(temporaryProfile) && Directory.Exists(temporaryProfile))
                Directory.Delete(temporaryProfile, true);
        }
        catch { }
        Cursor.Show();
    }
}

internal sealed class PreviewForm : Form
{
    private readonly Timer timer = new Timer { Interval = 33 };
    private readonly Random random = new Random(17);
    private readonly float[] starX = new float[90];
    private readonly float[] starY = new float[90];
    private float travel;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr window, out NativeRect rect);

    internal PreviewForm(IntPtr parent)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(2, 3, 10);
        SetParent(Handle, parent);
        NativeRect rect;
        GetClientRect(parent, out rect);
        Bounds = new Rectangle(0, 0, rect.Right - rect.Left, rect.Bottom - rect.Top);

        for (int i = 0; i < starX.Length; i++)
        {
            starX[i] = (float)random.NextDouble();
            starY[i] = (float)random.NextDouble() * 0.62f;
        }
        timer.Tick += delegate { travel += 0.035f; Invalidate(); };
        timer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(2, 3, 10));
        int w = Math.Max(1, ClientSize.Width);
        int h = Math.Max(1, ClientSize.Height);
        float horizon = h * 0.55f;

        using (Brush stars = new SolidBrush(Color.FromArgb(165, 190, 205, 235)))
            for (int i = 0; i < starX.Length; i++)
                g.FillEllipse(stars, starX[i] * w, starY[i] * h, 1.3f, 1.3f);

        using (Brush glow = new SolidBrush(Color.FromArgb(35, 255, 130, 50)))
            g.FillRectangle(glow, 0, horizon - 6, w, 14);

        for (int i = 0; i < 38; i++)
        {
            float z = ((i / 38f + travel) % 1f);
            float depth = 0.08f + z * z;
            float y = horizon + depth * h * 0.46f;
            float spread = depth * w * 0.48f;
            float size = 0.8f + depth * 3.5f;
            DrawLight(g, w / 2f - spread, y, size, Color.FromArgb(235, 235, 235));
            DrawLight(g, w / 2f + spread, y, size, Color.FromArgb(255, 170, 75));
        }

        using (Pen lane = new Pen(Color.FromArgb(85, 190, 205, 245), 1f))
        {
            g.DrawLine(lane, w / 2f, horizon, w * 0.30f, h);
            g.DrawLine(lane, w / 2f, horizon, w * 0.70f, h);
        }
    }

    private static void DrawLight(Graphics g, float x, float y, float size, Color color)
    {
        using (Brush halo = new SolidBrush(Color.FromArgb(40, color)))
            g.FillEllipse(halo, x - size * 2.2f, y - size * 2.2f, size * 4.4f, size * 4.4f);
        using (Brush core = new SolidBrush(color))
            g.FillEllipse(core, x - size / 2, y - size / 2, size, size);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}
