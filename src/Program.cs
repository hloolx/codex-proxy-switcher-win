using System.Diagnostics;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace CodexProxySwitcher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            var configStore = LauncherConfigStore.LoadOrCreate();
            if (configStore is null)
            {
                return;
            }

            Application.Run(new LauncherForm(configStore));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Codex Proxy Switcher 启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

internal sealed class LauncherForm : Form
{
    private const string NoProxy = "localhost,127.0.0.1,::1";
    private const string ProjectUrl = "https://github.com/hloolx/codex-proxy-switcher-win";
    private static readonly string[] ProxyEnvironmentKeys =
    {
        "HTTP_PROXY",
        "HTTPS_PROXY",
        "ALL_PROXY",
        "NO_PROXY"
    };

    private readonly LauncherConfigStore configStore;
    private string proxyText = "";
    private string codexPathText = "";
    private string codexStateText = "";
    private Color codexStateColor = Color.FromArgb(126, 231, 190);

    public LauncherForm(LauncherConfigStore configStore)
    {
        this.configStore = configStore;

        Text = "Codex Proxy Switcher";
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(680, 390);
        BackColor = Color.FromArgb(11, 18, 32);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        DoubleBuffered = true;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var nativeButton = new ModeButton
        {
            Title = "原生启动",
            Caption = "不注入代理环境变量",
            Text = "原生启动",
            Location = new Point(36, 228),
            Size = new Size(292, 76),
            AccentColor = Color.FromArgb(91, 141, 239),
            AccentColor2 = Color.FromArgb(59, 196, 221)
        };
        nativeButton.Click += (_, _) => Launch("Native");

        var vpnButton = new ModeButton
        {
            Title = "VPN 启动",
            Caption = "为本次 Codex 注入本地代理",
            Text = "VPN 启动",
            Location = new Point(352, 228),
            Size = new Size(292, 76),
            AccentColor = Color.FromArgb(64, 216, 172),
            AccentColor2 = Color.FromArgb(88, 145, 242)
        };
        vpnButton.Click += (_, _) => Launch("Vpn");

        var changePortButton = new GlassButton
        {
            Text = "修改端口",
            Location = new Point(36, 326),
            Size = new Size(124, 34),
            SurfaceColor = Color.FromArgb(30, 42, 61),
            AccentColor = Color.FromArgb(82, 211, 190)
        };
        changePortButton.Text = "Settings";
        changePortButton.Click += (_, _) => ChangeSettings();

        var openConfigButton = new GlassButton
        {
            Text = "打开配置目录",
            Location = new Point(174, 326),
            Size = new Size(142, 34),
            SurfaceColor = Color.FromArgb(30, 42, 61),
            AccentColor = Color.FromArgb(132, 170, 248)
        };
        openConfigButton.Click += (_, _) => Process.Start(new ProcessStartInfo
        {
            FileName = configStore.ConfigDirectory,
            UseShellExecute = true
        });

        var authorLink = new LinkLabel
        {
            Text = "project repo",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(492, 330),
            Size = new Size(152, 24),
            LinkColor = Color.FromArgb(153, 216, 255),
            ActiveLinkColor = Color.White,
            VisitedLinkColor = Color.FromArgb(153, 216, 255),
            ForeColor = Color.FromArgb(153, 216, 255),
            BackColor = Color.Transparent
        };
        authorLink.Links.Add(0, authorLink.Text.Length, ProjectUrl);
        authorLink.LinkClicked += (_, e) =>
        {
            var url = e.Link?.LinkData?.ToString() ?? ProjectUrl;
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        };

        Controls.Add(nativeButton);
        Controls.Add(vpnButton);
        Controls.Add(changePortButton);
        Controls.Add(openConfigButton);
        Controls.Add(authorLink);

        RefreshStatus();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        BringToFront();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var background = new LinearGradientBrush(
            ClientRectangle,
            Color.FromArgb(9, 15, 28),
            Color.FromArgb(18, 28, 48),
            32F);
        e.Graphics.FillRectangle(background, ClientRectangle);

        using var gridPen = new Pen(Color.FromArgb(16, 154, 191, 255), 1F);
        for (var y = 34; y < ClientSize.Height; y += 44)
        {
            e.Graphics.DrawLine(gridPen, 0, y, ClientSize.Width, y);
        }

        var shell = new Rectangle(24, 24, 632, 342);
        using var shellPath = DrawingHelpers.RoundRect(shell, 22);
        using var shellBrush = new LinearGradientBrush(shell, Color.FromArgb(26, 37, 56), Color.FromArgb(18, 26, 42), 90F);
        using var shellBorder = new Pen(Color.FromArgb(78, 120, 151, 190), 1F);
        e.Graphics.FillPath(shellBrush, shellPath);
        e.Graphics.DrawPath(shellBorder, shellPath);

        DrawingHelpers.DrawCloudLogo(e.Graphics, new Rectangle(46, 50, 58, 58), includeBadge: true);

        using var eyebrowFont = new Font(Font.FontFamily, 9.25F, FontStyle.Regular);
        using var titleFont = new Font(Font.FontFamily, 20F, FontStyle.Bold);
        using var proxyFont = new Font(Font.FontFamily, 10.75F, FontStyle.Bold);

        TextRenderer.DrawText(e.Graphics, "Windows Codex network launcher", eyebrowFont, new Rectangle(124, 48, 430, 22), Color.FromArgb(153, 197, 235), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, "Codex Proxy Switcher", titleFont, new Rectangle(122, 72, 470, 38), Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, "选择原生或 VPN 模式启动 Codex，避免旧进程复用错误网络环境。", Font, new Rectangle(124, 110, 500, 22), Color.FromArgb(194, 210, 232), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        var status = new Rectangle(36, 146, 608, 60);
        using var statusPath = DrawingHelpers.RoundRect(status, 16);
        using var statusBrush = new LinearGradientBrush(status, Color.FromArgb(34, 47, 69), Color.FromArgb(27, 38, 58), 90F);
        using var statusBorder = new Pen(Color.FromArgb(74, 113, 139, 170), 1F);
        e.Graphics.FillPath(statusBrush, statusPath);
        e.Graphics.DrawPath(statusBorder, statusPath);

        TextRenderer.DrawText(e.Graphics, "当前代理", Font, new Rectangle(58, 156, 84, 20), Color.FromArgb(151, 174, 205), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics, proxyText, proxyFont, new Rectangle(148, 154, 438, 24), Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, codexStateText, Font, new Rectangle(58, 180, 84, 20), codexStateColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics, codexPathText, Font, new Rectangle(148, 180, 470, 20), Color.FromArgb(212, 224, 241), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void RefreshStatus()
    {
        proxyText = configStore.Settings.ProxyUrl;

        var codexPath = CodexFinder.TryFindCodexExePath(configStore.Settings.CodexExePath);
        if (string.IsNullOrWhiteSpace(codexPath))
        {
            codexStateText = "Codex";
            codexStateColor = Color.FromArgb(255, 196, 128);
            codexPathText = "未找到 Windows 商店版 Codex";
            Invalidate();
            return;
        }

        codexStateText = "已找到";
        codexStateColor = Color.FromArgb(126, 231, 190);
        codexPathText = CompactPath(codexPath, 62);
        Invalidate();
    }

    private void ChangeSettings()
    {
        using var setupForm = new ProxySetupForm(configStore.Settings.ProxyPort, configStore.Settings.CodexExePath);
        if (setupForm.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        configStore.Settings.ProxyPort = setupForm.ProxyPort;
        configStore.Settings.CodexExePath = setupForm.CodexExePath;
        configStore.Save();
        RefreshStatus();
    }

    private void Launch(string mode)
    {
        try
        {
            var codexInstall = CodexFinder.FindCodexInstall(configStore.Settings.CodexExePath);
            StopExistingCodex();
            StartCodex(codexInstall, mode, configStore.Settings.ProxyUrl);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            RefreshStatus();
        }
    }

    private static void StopExistingCodex()
    {
        foreach (var process in Process.GetProcessesByName("Codex"))
        {
            try
            {
                process.Kill();
                process.WaitForExit(3000);
            }
            catch
            {
                // Best effort. If a process cannot be closed, launching may still work.
            }
        }

        Thread.Sleep(500);
    }

    private static void StartCodex(CodexInstallInfo codexInstall, string mode, string proxyUrl)
    {
        if (!string.IsNullOrWhiteSpace(codexInstall.AppUserModelId))
        {
            StartCodexViaPackagedActivation(codexInstall.AppUserModelId, mode, proxyUrl);
            return;
        }

        StartCodexDirect(codexInstall.ExePath, mode, proxyUrl);
    }

    private static void StartCodexDirect(string codexExePath, string mode, string proxyUrl)
    {
        var processInfo = new ProcessStartInfo(codexExePath)
        {
            WorkingDirectory = GetLaunchWorkingDirectory(codexExePath),
            UseShellExecute = false
        };

        ApplyLaunchEnvironment(processInfo.Environment, BuildLaunchEnvironment(mode, proxyUrl));

        Process.Start(processInfo);
    }

    private static void StartCodexViaPackagedActivation(string appUserModelId, string mode, string proxyUrl)
    {
        using var _ = new TemporaryLaunchEnvironment(BuildLaunchEnvironment(mode, proxyUrl));
        PackagedAppLauncher.Activate(appUserModelId);
    }

    private static Dictionary<string, string?> BuildLaunchEnvironment(string mode, string proxyUrl)
    {
        var environment = ProxyEnvironmentKeys.ToDictionary(key => key, _ => (string?)null, StringComparer.OrdinalIgnoreCase);

        if (string.Equals(mode, "Vpn", StringComparison.OrdinalIgnoreCase))
        {
            environment["HTTP_PROXY"] = proxyUrl;
            environment["HTTPS_PROXY"] = proxyUrl;
            environment["ALL_PROXY"] = proxyUrl;
            environment["NO_PROXY"] = NoProxy;
        }

        return environment;
    }

    private static void ApplyLaunchEnvironment(IDictionary<string, string?> target, IReadOnlyDictionary<string, string?> values)
    {
        foreach (var (key, value) in values)
        {
            if (value is null)
            {
                target.Remove(key);
                continue;
            }

            target[key] = value;
        }
    }

    private static string GetLaunchWorkingDirectory(string codexExePath)
    {
        var exeDirectory = Path.GetDirectoryName(codexExePath);
        if (string.IsNullOrWhiteSpace(exeDirectory))
        {
            return AppContext.BaseDirectory;
        }

        var windowsAppsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "WindowsApps");

        if (codexExePath.StartsWith(windowsAppsPath, StringComparison.OrdinalIgnoreCase))
        {
            return AppContext.BaseDirectory;
        }

        return Directory.Exists(exeDirectory) ? exeDirectory : AppContext.BaseDirectory;
    }

    private static string CompactPath(string path, int maxLength)
    {
        if (path.Length <= maxLength)
        {
            return path;
        }

        var fileName = Path.GetFileName(path);
        var root = Path.GetPathRoot(path) ?? string.Empty;
        var available = Math.Max(12, maxLength - root.Length - fileName.Length - 6);
        var middle = path[root.Length..^fileName.Length].Trim('\\');
        if (middle.Length > available)
        {
            middle = middle[^available..];
        }

        return root + "...\\" + middle + "\\" + fileName;
    }
}

internal sealed class GlassPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 26;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color GlassColor { get; set; } = Color.FromArgb(48, 255, 255, 255);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = Color.FromArgb(96, 255, 255, 255);

    public GlassPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

        using var path = DrawingHelpers.RoundRect(bounds, Radius);
        using var shadowPath = DrawingHelpers.RoundRect(new Rectangle(4, 8, Width - 9, Height - 13), Radius);
        using var shadowBrush = new SolidBrush(Color.FromArgb(42, 0, 0, 0));
        e.Graphics.FillPath(shadowBrush, shadowPath);

        using var glassBrush = new LinearGradientBrush(
            bounds,
            Color.FromArgb(Math.Min(GlassColor.A + 20, 255), GlassColor),
            GlassColor,
            90F);
        e.Graphics.FillPath(glassBrush, path);

        using var highlightPen = new Pen(BorderColor, 1F);
        e.Graphics.DrawPath(highlightPen, path);

        using var shinePen = new Pen(Color.FromArgb(72, 255, 255, 255), 1F);
        e.Graphics.DrawLine(shinePen, Radius, 1, Width - Radius, 1);
    }
}

internal sealed class LogoMark : Control
{
    public LogoMark()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        DrawingHelpers.DrawCloudLogo(e.Graphics, ClientRectangle, includeBadge: true);
    }
}

internal class GlassButton : Button
{
    private bool hovering;
    private bool pressing;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; set; } = Color.FromArgb(132, 196, 255);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SurfaceColor { get; set; } = Color.FromArgb(30, 42, 61);

    public GlassButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.FromArgb(11, 18, 32);
        ForeColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var path = DrawingHelpers.RoundRect(new Rectangle(0, 0, Width, Height), 12);
        Region = new Region(path);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovering = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovering = false;
        pressing = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        pressing = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        pressing = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        var top = pressing ? DrawingHelpers.Mix(SurfaceColor, Color.Black, 0.10F) : hovering ? DrawingHelpers.Mix(SurfaceColor, AccentColor, 0.18F) : SurfaceColor;
        var bottom = DrawingHelpers.Mix(top, Color.Black, 0.22F);

        using var path = DrawingHelpers.RoundRect(rect, 13);
        using var brush = new LinearGradientBrush(
            rect,
            top,
            bottom,
            90F);
        using var pen = new Pen(Color.FromArgb(hovering ? 170 : 112, AccentColor), 1F);

        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);

        using var shine = new Pen(Color.FromArgb(70, 255, 255, 255), 1F);
        e.Graphics.DrawLine(shine, 14, 1, Width - 14, 1);

        TextRenderer.DrawText(e.Graphics, Text, Font, rect, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class ModeButton : Control
{
    private bool hovering;
    private bool pressing;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title { get; set; } = "";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Caption { get; set; } = "";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; set; } = Color.FromArgb(91, 141, 239);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor2 { get; set; } = Color.FromArgb(125, 255, 220);

    public ModeButton()
    {
        Cursor = Cursors.Hand;
        ForeColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var path = DrawingHelpers.RoundRect(new Rectangle(0, 0, Width, Height), 18);
        Region = new Region(path);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovering = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovering = false;
        pressing = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        pressing = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        pressing = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        using var path = DrawingHelpers.RoundRect(rect, 20);
        var surfaceTop = pressing ? Color.FromArgb(26, 36, 54) : hovering ? Color.FromArgb(42, 58, 84) : Color.FromArgb(33, 46, 68);
        var surfaceBottom = pressing ? Color.FromArgb(21, 30, 46) : hovering ? Color.FromArgb(31, 45, 69) : Color.FromArgb(24, 35, 54);
        using var surface = new LinearGradientBrush(rect, surfaceTop, surfaceBottom, 90F);
        e.Graphics.FillPath(surface, path);

        using var accent = new LinearGradientBrush(
            rect,
            Color.FromArgb(255, AccentColor),
            Color.FromArgb(255, AccentColor2),
            30F);
        using var accentPath = DrawingHelpers.RoundRect(new Rectangle(0, 0, 7, Height - 1), 8);
        e.Graphics.FillPath(accent, accentPath);

        var iconBounds = new Rectangle(22, 18, 40, 40);
        using var iconFill = new LinearGradientBrush(iconBounds, AccentColor, AccentColor2, LinearGradientMode.ForwardDiagonal);
        e.Graphics.FillEllipse(iconFill, iconBounds);
        using var iconPen = new Pen(Color.White, 2.4F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawLine(iconPen, iconBounds.Left + 14, iconBounds.Top + 14, iconBounds.Left + 22, iconBounds.Top + 20);
        e.Graphics.DrawLine(iconPen, iconBounds.Left + 22, iconBounds.Top + 20, iconBounds.Left + 14, iconBounds.Top + 26);
        e.Graphics.DrawLine(iconPen, iconBounds.Left + 24, iconBounds.Top + 25, iconBounds.Left + 31, iconBounds.Top + 25);

        using var pen = new Pen(Color.FromArgb(hovering ? 156 : 92, 148, 177, 213), 1F);
        e.Graphics.DrawPath(pen, path);

        using var shine = new Pen(Color.FromArgb(44, 255, 255, 255), 1F);
        e.Graphics.DrawLine(shine, 22, 1, Width - 22, 1);

        var titleRect = new Rectangle(78, 17, Width - 104, 25);
        var captionRect = new Rectangle(78, 43, Width - 104, 20);
        using var titleFont = new Font(Font.FontFamily, 12F, FontStyle.Bold);

        TextRenderer.DrawText(e.Graphics, Title, titleFont, titleRect, Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, Caption, Font, captionRect, Color.FromArgb(190, 209, 232), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal static class DrawingHelpers
{
    public static void DrawCloudLogo(Graphics graphics, Rectangle bounds, bool includeBadge)
    {
        var rect = RectangleF.Inflate(bounds, -4, -4);
        using var cloud = CloudPath(rect);
        using var shadow = CloudPath(new RectangleF(rect.X + 1.5F, rect.Y + 3F, rect.Width, rect.Height));
        using var shadowBrush = new SolidBrush(Color.FromArgb(38, 0, 0, 0));
        graphics.FillPath(shadowBrush, shadow);

        using var fill = new LinearGradientBrush(rect, Color.FromArgb(176, 157, 255), Color.FromArgb(50, 65, 246), 92F);
        var blend = new ColorBlend
        {
            Positions = new[] { 0F, 0.52F, 1F },
            Colors = new[]
            {
                Color.FromArgb(178, 160, 255),
                Color.FromArgb(102, 134, 255),
                Color.FromArgb(52, 63, 245)
            }
        };
        fill.InterpolationColors = blend;
        graphics.FillPath(fill, cloud);

        using var shine = new LinearGradientBrush(
            new RectangleF(rect.X, rect.Y, rect.Width, rect.Height * 0.5F),
            Color.FromArgb(72, 255, 255, 255),
            Color.FromArgb(0, 255, 255, 255),
            90F);
        graphics.SetClip(cloud);
        graphics.FillEllipse(shine, rect.X + rect.Width * 0.20F, rect.Y + rect.Height * 0.08F, rect.Width * 0.56F, rect.Height * 0.32F);
        graphics.ResetClip();

        using var symbol = new Pen(Color.White, Math.Max(3.2F, rect.Width * 0.07F))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        graphics.DrawLine(symbol, rect.X + rect.Width * 0.26F, rect.Y + rect.Height * 0.39F, rect.X + rect.Width * 0.34F, rect.Y + rect.Height * 0.52F);
        graphics.DrawLine(symbol, rect.X + rect.Width * 0.34F, rect.Y + rect.Height * 0.52F, rect.X + rect.Width * 0.26F, rect.Y + rect.Height * 0.65F);
        graphics.DrawLine(symbol, rect.X + rect.Width * 0.48F, rect.Y + rect.Height * 0.57F, rect.X + rect.Width * 0.66F, rect.Y + rect.Height * 0.57F);

        if (!includeBadge)
        {
            return;
        }

        var badgeSize = rect.Width * 0.25F;
        var badge = new RectangleF(rect.Right - badgeSize * 1.02F, rect.Bottom - badgeSize * 0.98F, badgeSize, badgeSize);
        using var badgePath = RoundRect(Rectangle.Round(badge), (int)(badgeSize * 0.36F));
        using var badgeBrush = new LinearGradientBrush(badge, Color.FromArgb(40, 238, 214), Color.FromArgb(72, 142, 255), 35F);
        using var badgeBorder = new Pen(Color.FromArgb(230, 255, 255, 255), Math.Max(1.2F, rect.Width * 0.018F));
        graphics.FillPath(badgeBrush, badgePath);
        graphics.DrawPath(badgeBorder, badgePath);

        using var mini = new Pen(Color.White, Math.Max(1.4F, rect.Width * 0.028F))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(mini, badge.X + badge.Width * 0.26F, badge.Y + badge.Height * 0.38F, badge.X + badge.Width * 0.72F, badge.Y + badge.Height * 0.38F);
        graphics.DrawLine(mini, badge.X + badge.Width * 0.72F, badge.Y + badge.Height * 0.38F, badge.X + badge.Width * 0.62F, badge.Y + badge.Height * 0.28F);
        graphics.DrawLine(mini, badge.X + badge.Width * 0.72F, badge.Y + badge.Height * 0.38F, badge.X + badge.Width * 0.62F, badge.Y + badge.Height * 0.48F);
        graphics.DrawLine(mini, badge.X + badge.Width * 0.74F, badge.Y + badge.Height * 0.66F, badge.X + badge.Width * 0.28F, badge.Y + badge.Height * 0.66F);
        graphics.DrawLine(mini, badge.X + badge.Width * 0.28F, badge.Y + badge.Height * 0.66F, badge.X + badge.Width * 0.38F, badge.Y + badge.Height * 0.56F);
        graphics.DrawLine(mini, badge.X + badge.Width * 0.28F, badge.Y + badge.Height * 0.66F, badge.X + badge.Width * 0.38F, badge.Y + badge.Height * 0.76F);
    }

    private static GraphicsPath CloudPath(RectangleF rect)
    {
        var path = new GraphicsPath { FillMode = FillMode.Winding };
        path.AddEllipse(rect.X + rect.Width * 0.02F, rect.Y + rect.Height * 0.28F, rect.Width * 0.38F, rect.Height * 0.42F);
        path.AddEllipse(rect.X + rect.Width * 0.16F, rect.Y + rect.Height * 0.06F, rect.Width * 0.42F, rect.Height * 0.48F);
        path.AddEllipse(rect.X + rect.Width * 0.42F, rect.Y + rect.Height * 0.15F, rect.Width * 0.42F, rect.Height * 0.44F);
        path.AddEllipse(rect.X + rect.Width * 0.56F, rect.Y + rect.Height * 0.36F, rect.Width * 0.36F, rect.Height * 0.38F);
        path.AddEllipse(rect.X + rect.Width * 0.14F, rect.Y + rect.Height * 0.52F, rect.Width * 0.34F, rect.Height * 0.34F);
        path.AddEllipse(rect.X + rect.Width * 0.38F, rect.Y + rect.Height * 0.48F, rect.Width * 0.40F, rect.Height * 0.38F);
        path.AddRectangle(new RectangleF(rect.X + rect.Width * 0.20F, rect.Y + rect.Height * 0.34F, rect.Width * 0.54F, rect.Height * 0.42F));
        return path;
    }

    public static GraphicsPath RoundRect(Rectangle bounds, int radius)
    {
        radius = Math.Max(1, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
        var diameter = radius * 2;
        var path = new GraphicsPath();
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }

    public static Color Mix(Color first, Color second, float amount)
    {
        amount = Math.Clamp(amount, 0F, 1F);
        var inverse = 1F - amount;
        return Color.FromArgb(
            (int)(first.A * inverse + second.A * amount),
            (int)(first.R * inverse + second.R * amount),
            (int)(first.G * inverse + second.G * amount),
            (int)(first.B * inverse + second.B * amount));
    }
}

internal sealed class ProxySetupForm : Form
{
    private readonly TextBox portBox;
    private readonly TextBox codexPathBox;

    public int ProxyPort { get; private set; }
    public string CodexExePath { get; private set; } = "";

    public ProxySetupForm(int suggestedPort, string? suggestedCodexExePath)
    {
        Text = "配置代理端口";
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(520, 245);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = Color.FromArgb(20, 24, 40);
        DoubleBuffered = true;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var title = new Label
        {
            Text = "第一次使用需要填写本地代理端口",
            AutoSize = false,
            Font = new Font(Font.FontFamily, 10F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(24, 18),
            Size = new Size(472, 28)
        };

        var hint = new Label
        {
            Text = "例如 7897、7890 或 9000；启动器会使用 127.0.0.1 加这个端口。",
            AutoSize = false,
            ForeColor = Color.FromArgb(205, 218, 238),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(24, 52),
            Size = new Size(472, 38)
        };

        Text = "Proxy and Codex settings";
        title.Text = "Configure proxy port and Codex.exe path";
        hint.Text = "Leave Codex.exe empty to auto-detect the Windows Store app.";

        var portLabel = new Label
        {
            Text = "端口号",
            AutoSize = false,
            Location = new Point(70, 105),
            Size = new Size(70, 24),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(205, 218, 238),
            BackColor = Color.Transparent
        };

        portBox = new TextBox
        {
            Text = suggestedPort.ToString(),
            Location = new Point(146, 103),
            Size = new Size(160, 24),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(244, 249, 255),
            ForeColor = Color.FromArgb(20, 24, 40)
        };
        portBox.SelectAll();

        var codexPathLabel = new Label
        {
            Text = "Codex.exe",
            AutoSize = false,
            Location = new Point(70, 139),
            Size = new Size(70, 24),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(205, 218, 238),
            BackColor = Color.Transparent
        };

        codexPathBox = new TextBox
        {
            Text = suggestedCodexExePath ?? "",
            Location = new Point(146, 137),
            Size = new Size(260, 24),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(244, 249, 255),
            ForeColor = Color.FromArgb(20, 24, 40)
        };

        var browseButton = new GlassButton
        {
            Text = "...",
            DialogResult = DialogResult.None,
            Location = new Point(416, 136),
            Size = new Size(42, 28),
            AccentColor = Color.FromArgb(160, 188, 255)
        };
        browseButton.Click += (_, _) => BrowseCodexPath();

        var okButton = new GlassButton
        {
            Text = "保存",
            DialogResult = DialogResult.None,
            Location = new Point(156, 195),
            Size = new Size(90, 30),
            AccentColor = Color.FromArgb(115, 238, 189)
        };
        okButton.Click += (_, _) => SavePort();

        var cancelButton = new GlassButton
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Location = new Point(260, 195),
            Size = new Size(90, 30),
            AccentColor = Color.FromArgb(160, 188, 255)
        };

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(title);
        Controls.Add(hint);
        Controls.Add(portLabel);
        Controls.Add(portBox);
        Controls.Add(codexPathLabel);
        Controls.Add(codexPathBox);
        Controls.Add(browseButton);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(22, 28, 48), Color.FromArgb(42, 57, 92), 35F);
        e.Graphics.FillRectangle(brush, ClientRectangle);

        using var border = new Pen(Color.FromArgb(48, 255, 255, 255), 1F);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    private void BrowseCodexPath()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select Codex.exe",
            Filter = "Codex executable (Codex.exe)|Codex.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            FileName = "Codex.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            codexPathBox.Text = dialog.FileName;
        }
    }

    private void SavePort()
    {
        if (!int.TryParse(portBox.Text.Trim(), out var port) || port < 1 || port > 65535)
        {
            MessageBox.Show(this, "请输入 1 到 65535 之间的端口号。", "端口无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            portBox.Focus();
            portBox.SelectAll();
            return;
        }

        var codexExePath = codexPathBox.Text.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(codexExePath))
        {
            if (!File.Exists(codexExePath))
            {
                MessageBox.Show(this, "Codex.exe path does not exist.", "Invalid Codex path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                codexPathBox.Focus();
                codexPathBox.SelectAll();
                return;
            }

            if (!string.Equals(Path.GetFileName(codexExePath), "Codex.exe", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Please select Codex.exe.", "Invalid Codex path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                codexPathBox.Focus();
                codexPathBox.SelectAll();
                return;
            }
        }

        ProxyPort = port;
        CodexExePath = codexExePath;
        DialogResult = DialogResult.OK;
        Close();
    }
}

internal sealed class LauncherConfigStore
{
    private const int DefaultProxyPort = 7897;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private LauncherConfigStore(string configDirectory, string settingsPath, LauncherSettings settings)
    {
        ConfigDirectory = configDirectory;
        SettingsPath = settingsPath;
        Settings = settings;
    }

    public string ConfigDirectory { get; }

    public string SettingsPath { get; }

    public LauncherSettings Settings { get; }

    public static LauncherConfigStore? LoadOrCreate()
    {
        var configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodexProxySwitcher");
        var settingsPath = Path.Combine(configDirectory, "settings.json");

        Directory.CreateDirectory(configDirectory);
        MigrateLegacySettings(settingsPath);

        var settings = Load(settingsPath);
        if (settings is null)
        {
            using var setupForm = new ProxySetupForm(ReadSuggestedPort(), "");
            if (setupForm.ShowDialog() != DialogResult.OK)
            {
                return null;
            }

            settings = new LauncherSettings
            {
                ProxyPort = setupForm.ProxyPort,
                CodexExePath = setupForm.CodexExePath
            };
            Save(settingsPath, settings);
            PromptForDesktopShortcut(settingsPath, settings);
        }

        return new LauncherConfigStore(configDirectory, settingsPath, settings);
    }

    public void Save()
    {
        Save(SettingsPath, Settings);
    }

    private static LauncherSettings? Load(string settingsPath)
    {
        try
        {
            if (!File.Exists(settingsPath))
            {
                return null;
            }

            var settings = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(settingsPath), JsonOptions);
            if (settings is null || settings.ProxyPort < 1 || settings.ProxyPort > 65535)
            {
                return null;
            }

            return settings;
        }
        catch
        {
            return null;
        }
    }

    private static void Save(string settingsPath, LauncherSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static void PromptForDesktopShortcut(string settingsPath, LauncherSettings settings)
    {
        if (settings.DesktopShortcutPrompted)
        {
            return;
        }

        settings.DesktopShortcutPrompted = true;
        Save(settingsPath, settings);

        var result = MessageBox.Show(
            "是否添加桌面快捷方式？以后可以直接从桌面打开 Codex Proxy Switcher。",
            "添加桌面快捷方式",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            DesktopShortcutManager.CreateOrUpdate();
            MessageBox.Show(
                "已添加桌面快捷方式。",
                "Codex Proxy Switcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "创建桌面快捷方式失败：\n" + ex.Message,
                "Codex Proxy Switcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static void MigrateLegacySettings(string settingsPath)
    {
        if (File.Exists(settingsPath))
        {
            return;
        }

        var legacyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            // Migrate settings from versions released before the project was renamed.
            "CodexLauncher",
            "settings.json");

        if (!File.Exists(legacyPath))
        {
            return;
        }

        try
        {
            File.Copy(legacyPath, settingsPath, overwrite: false);
        }
        catch
        {
            // If migration fails, the first-run setup dialog will ask for the port again.
        }
    }

    private static int ReadSuggestedPort()
    {
        var proxyFile = Path.Combine(AppContext.BaseDirectory, "proxy-url.txt");
        if (!File.Exists(proxyFile))
        {
            return DefaultProxyPort;
        }

        var configured = File.ReadLines(proxyFile)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultProxyPort;
        }

        if (int.TryParse(configured, out var port) && port is >= 1 and <= 65535)
        {
            return port;
        }

        if (Uri.TryCreate(configured, UriKind.Absolute, out var uri) && uri.Port is >= 1 and <= 65535)
        {
            return uri.Port;
        }

        return DefaultProxyPort;
    }
}

internal sealed class LauncherSettings
{
    public string ProxyScheme { get; set; } = "http";

    public string ProxyHost { get; set; } = "127.0.0.1";

    public int ProxyPort { get; set; } = 7897;

    public string CodexExePath { get; set; } = "";

    public bool DesktopShortcutPrompted { get; set; }

    public string ProxyUrl => $"{ProxyScheme}://{ProxyHost}:{ProxyPort}";
}

internal sealed class CodexInstallInfo
{
    public CodexInstallInfo(string exePath, string? appUserModelId)
    {
        ExePath = exePath;
        AppUserModelId = appUserModelId;
    }

    public string ExePath { get; }

    public string? AppUserModelId { get; }
}

internal sealed class TemporaryLaunchEnvironment : IDisposable
{
    private readonly Dictionary<string, string?> previousProcessValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> previousUserValues = new(StringComparer.OrdinalIgnoreCase);

    public TemporaryLaunchEnvironment(IReadOnlyDictionary<string, string?> values)
    {
        foreach (var (key, value) in values)
        {
            previousProcessValues[key] = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process);
            previousUserValues[key] = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.User);
        }

        EnvironmentChangeBroadcaster.Broadcast();
    }

    public void Dispose()
    {
        foreach (var (key, value) in previousProcessValues)
        {
            Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
        }

        foreach (var (key, value) in previousUserValues)
        {
            Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.User);
        }

        EnvironmentChangeBroadcaster.Broadcast();
    }
}

internal static class EnvironmentChangeBroadcaster
{
    private static readonly IntPtr HwndBroadcast = new(0xffff);
    private const int WmSettingChange = 0x001A;
    private const int SmtoAbortIfHung = 0x0002;

    public static void Broadcast()
    {
        SendMessageTimeout(
            HwndBroadcast,
            WmSettingChange,
            UIntPtr.Zero,
            "Environment",
            SmtoAbortIfHung,
            5000,
            out _);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        int msg,
        UIntPtr wParam,
        string lParam,
        int fuFlags,
        int uTimeout,
        out UIntPtr lpdwResult);
}

internal static class PackagedAppLauncher
{
    public static void Activate(string appUserModelId)
    {
        object managerObject = new ApplicationActivationManager();
        var manager = (IApplicationActivationManager)managerObject;
        try
        {
            var hr = manager.ActivateApplication(appUserModelId, null, ActivateOptions.None, out _);
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(managerObject);
        }
    }

    [Flags]
    private enum ActivateOptions
    {
        None = 0
    }

    [ComImport]
    [Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private sealed class ApplicationActivationManager
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
    private interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments,
            ActivateOptions options,
            out uint processId);

        [PreserveSig]
        int ActivateForFile(IntPtr appUserModelId, IntPtr itemArray, string verb, out uint processId);

        [PreserveSig]
        int ActivateForProtocol(IntPtr appUserModelId, IntPtr itemArray, out uint processId);
    }
}

internal static class DesktopShortcutManager
{
    public static void CreateOrUpdate()
    {
        var desktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var shortcutPath = Path.Combine(desktopDirectory, "Codex Proxy Switcher.lnk");
        var executablePath = Application.ExecutablePath;
        var workingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;

        object shellLinkObject = new ShellLink();
        var shellLink = (IShellLinkW)shellLinkObject;
        try
        {
            shellLink.SetPath(executablePath);
            shellLink.SetWorkingDirectory(workingDirectory);
            shellLink.SetDescription("Start Codex Desktop with optional process-local proxy.");
            shellLink.SetIconLocation(executablePath, 0);

            var persistFile = (IPersistFile)shellLink;
            persistFile.Save(shortcutPath, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellLinkObject);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);

        void GetIDList(out IntPtr ppidl);

        void SetIDList(IntPtr pidl);

        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);

        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);

        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);

        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);

        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);

        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

        void GetHotkey(out short pwHotkey);

        void SetHotkey(short wHotkey);

        void GetShowCmd(out int piShowCmd);

        void SetShowCmd(int iShowCmd);

        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);

        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);

        void Resolve(IntPtr hwnd, uint fFlags);

        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);

        void IsDirty();

        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);

        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);

        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);

        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}

internal static class CodexFinder
{
    public static CodexInstallInfo FindCodexInstall(string? configuredCodexExePath)
    {
        var codexInstall = TryFindCodexInstall(configuredCodexExePath);
        if (codexInstall is not null)
        {
            return codexInstall;
        }

        throw new InvalidOperationException("没有找到 Codex。请在 Settings 中选择 Codex.exe，或确认这台电脑已经安装 Windows 商店版 Codex。");
    }

    public static string FindCodexExePath(string? configuredCodexExePath)
    {
        return FindCodexInstall(configuredCodexExePath).ExePath;
    }

    public static string? TryFindCodexExePath(string? configuredCodexExePath)
    {
        try
        {
            return TryFindCodexInstall(configuredCodexExePath)?.ExePath;
        }
        catch
        {
            return null;
        }
    }

    public static CodexInstallInfo? TryFindCodexInstall(string? configuredCodexExePath)
    {
        var configuredInstall = TryFindCodexViaConfiguredPath(configuredCodexExePath);
        if (configuredInstall is not null)
        {
            return configuredInstall;
        }

        return FindCodexViaAppxPackage() ?? FindCodexViaWindowsApps();
    }

    private static CodexInstallInfo? TryFindCodexViaConfiguredPath(string? configuredCodexExePath)
    {
        if (string.IsNullOrWhiteSpace(configuredCodexExePath))
        {
            return null;
        }

        var exePath = configuredCodexExePath.Trim().Trim('"');
        if (!File.Exists(exePath))
        {
            throw new InvalidOperationException("Configured Codex.exe path does not exist: " + exePath);
        }

        if (!string.Equals(Path.GetFileName(exePath), "Codex.exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Configured Codex path must point to Codex.exe: " + exePath);
        }

        return new CodexInstallInfo(exePath, null);
    }

    private static CodexInstallInfo? FindCodexViaAppxPackage()
    {
        try
        {
            var processInfo = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            processInfo.ArgumentList.Add("-NoProfile");
            processInfo.ArgumentList.Add("-Command");
            processInfo.ArgumentList.Add("$pkg = Get-AppxPackage -Name OpenAI.Codex | Sort-Object Version -Descending | Select-Object -First 1; if ($pkg) { [pscustomobject]@{ InstallLocation = $pkg.InstallLocation; PackageFamilyName = $pkg.PackageFamilyName } | ConvertTo-Json -Compress }");

            using var process = Process.Start(processInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);

            if (string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            using var document = JsonDocument.Parse(output);
            var root = document.RootElement;
            var installLocation = root.TryGetProperty("InstallLocation", out var installLocationElement)
                ? installLocationElement.GetString()
                : null;
            var packageFamilyName = root.TryGetProperty("PackageFamilyName", out var packageFamilyNameElement)
                ? packageFamilyNameElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(installLocation))
            {
                return null;
            }

            var exePath = Path.Combine(installLocation, "app", "Codex.exe");
            var appId = TryReadApplicationId(installLocation) ?? "App";
            var appUserModelId = string.IsNullOrWhiteSpace(packageFamilyName)
                ? null
                : $"{packageFamilyName}!{appId}";

            return new CodexInstallInfo(exePath, appUserModelId);
        }
        catch
        {
            return null;
        }
    }

    private static CodexInstallInfo? FindCodexViaWindowsApps()
    {
        try
        {
            var basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "WindowsApps");

            if (!Directory.Exists(basePath))
            {
                return null;
            }

            var packagePath = Directory
                .GetDirectories(basePath, "OpenAI.Codex_*_x64__2p2nqsd0c76g0")
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(packagePath))
            {
                return null;
            }

            var exePath = Path.Combine(packagePath, "app", "Codex.exe");
            if (!File.Exists(exePath))
            {
                return null;
            }

            return new CodexInstallInfo(exePath, TryBuildAppUserModelId(packagePath));
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadApplicationId(string installLocation)
    {
        try
        {
            var manifestPath = Path.Combine(installLocation, "AppxManifest.xml");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            var manifest = XDocument.Load(manifestPath);
            return manifest
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Application")
                ?.Attribute("Id")
                ?.Value;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryBuildAppUserModelId(string packagePath)
    {
        var packageName = Path.GetFileName(packagePath);
        var publisherSeparator = packageName.LastIndexOf("__", StringComparison.Ordinal);
        if (publisherSeparator < 0 || publisherSeparator + 2 >= packageName.Length)
        {
            return null;
        }

        var publisherId = packageName[(publisherSeparator + 2)..];
        return $"OpenAI.Codex_{publisherId}!{TryReadApplicationId(packagePath) ?? "App"}";
    }
}
