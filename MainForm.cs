// ACE Tool Hub - 2026-04-08 Upgraded
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Management;
using System.Windows.Forms;
using Microsoft.Win32;
using System.ServiceProcess;
using System.Security.AccessControl;
using System.Linq;
using System.Threading.Tasks;
using System.IO.Compression;

namespace SystemMonitorApp
{
    // =========================================================================
    //  GAUGE BAR — thick rounded progress bar
    // =========================================================================
    internal sealed class GaugeBar : Control
    {
        private int   _val;
        private Color _fill = Color.FromArgb(0x25, 0x63, 0xeb);

        public GaugeBar()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint  |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
        }

        public int   Value { get => _val;  set { _val  = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public Color Fill  { get => _fill; set { _fill = value; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int r = Math.Max(1, Height / 2);

            var trackBounds = new Rectangle(0, 0, Width - 1, Height - 1);
            if (trackBounds.Width > 0 && trackBounds.Height > 0)
            {
                using (var path = CardPanel.RRPath(0, 0, Width - 1, Height - 1, r))
                using (var br = new SolidBrush(Color.FromArgb(0xf3, 0xf4, 0xf6)))
                {
                    g.FillPath(br, path);
                }
            }

            if (_val > 0)
            {
                int fw = (int)Math.Round((Width - 1) * _val / 100.0);
                fw = Math.Max(fw, Height); fw = Math.Min(fw, Width - 1);
                var fillBounds = new Rectangle(0, 0, fw, Height - 1);
                if (fillBounds.Width > 0 && fillBounds.Height > 0)
                {
                    using (var path = CardPanel.RRPath(0, 0, fw, Height - 1, r))
                    {
                        using (var br = new SolidBrush(_fill))
                            g.FillPath(br, path);
                    }
                }
            }
        }
    }

    // =========================================================================
    //  CARD PANEL — light surface with rounded border
    // =========================================================================
    internal sealed class CardPanel : Panel
    {
        private static readonly Color Bg     = Color.White;
        private static readonly Color Border = Color.FromArgb(0xe5, 0xe7, 0xeb);

        public CardPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint  |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Bg;
            Padding   = new Padding(16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            
            using (var path = RRPath(1, 1, Width - 3, Height - 3, 12))
            {
                using (var br = new SolidBrush(Bg))
                    g.FillPath(br, path);

                using (var pen = new Pen(Border, 1.5f))
                    g.DrawPath(pen, path);
            }
        }

        internal static GraphicsPath RRPath(int x, int y, int w, int h, int r)
        {
            r = Math.Min(r, Math.Min(w / 2, h / 2));
            var p = new GraphicsPath();
            p.AddArc(x,       y,       r*2, r*2, 180, 90);
            p.AddArc(x+w-r*2, y,       r*2, r*2, 270, 90);
            p.AddArc(x+w-r*2, y+h-r*2, r*2, r*2,   0, 90);
            p.AddArc(x,       y+h-r*2, r*2, r*2,  90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // =========================================================================
    //  MAIN FORM
    // =========================================================================
    public partial class MainForm : Form
    {
        // ── Design tokens ─────────────────────────────────────────────────────
        // ── Design tokens ─────────────────────────────────────────────────────
        static readonly Color PrimaryBg   = Color.FromArgb(0xf3, 0xf4, 0xf6); // Gray 100
        static readonly Color SidebarBg   = Color.FromArgb(0xff, 0xff, 0xff); // White
        static readonly Color CardBg      = Color.FromArgb(0xff, 0xff, 0xff); // White
        static readonly Color BorderClr   = Color.FromArgb(0xe5, 0xe7, 0xeb); // Gray 200
        static readonly Color AccentBlue  = Color.FromArgb(0x25, 0x63, 0xeb); // Blue 600 
        static readonly Color AmberClr    = Color.FromArgb(0xd9, 0x77, 0x06); // Amber 600
        static readonly Color CoralRed    = Color.FromArgb(0xdc, 0x26, 0x26); // Red 600
        static readonly Color SuccessGrn  = Color.FromArgb(0x05, 0x96, 0x69); // Emerald 600
        static readonly Color TextPrimary = Color.FromArgb(0x11, 0x18, 0x27); // Gray 900
        static readonly Color TextSecond  = Color.FromArgb(0x4b, 0x55, 0x63); // Gray 600
        static readonly Color TextMuted   = Color.FromArgb(0x6b, 0x72, 0x80); // Gray 500
        static readonly Color NavActiveBg = Color.FromArgb(0xef, 0xf6, 0xff); // Blue 50
        static readonly Color NavHoverBg  = Color.FromArgb(0xf9, 0xfa, 0xfb); // Gray 50
        static readonly Color BtnBlue     = Color.FromArgb(0x25, 0x63, 0xeb); // Blue 600
        static readonly Color BtnRed      = Color.FromArgb(0xdc, 0x26, 0x26); // Red 600
        static readonly Color BtnOrange   = Color.FromArgb(0xd9, 0x77, 0x06); // Amber 600

        static readonly string HF; // header font

        static MainForm()
        {
            HF = "Segoe UI";
            using (var fc = new System.Drawing.Text.InstalledFontCollection())
                foreach (var ff in fc.Families)
                {
                    if (ff.Name.Equals("Inter", StringComparison.OrdinalIgnoreCase)) { HF = "Inter"; break; }
                    if (ff.Name.Equals("Roboto", StringComparison.OrdinalIgnoreCase)) { HF = "Roboto"; break; }
                }
        }

        // ── State fields ───────────────────────────────────────────────────────
        private readonly Panel[]   _pages    = new Panel[NAV_COUNT];
        private readonly Panel[]   _navItems = new Panel[NAV_COUNT];
        private int _activePage = -1;

        // Monitor
        private Label    _lblCpuPct, _lblCpuStatus;
        private Label    _lblRamVal, _lblRamStatus;
        private GaugeBar _gauCpu, _gauRam;
        private Label    _lblUptime, _lblNetIo, _lblTopProc;
        private long     _lastBytesRecv, _lastBytesSent;
        private DateTime _lastNetTime = DateTime.MinValue;
        private FlowLayoutPanel _diskFlow;
        private List<DiskControls> _diskCtrls = new List<DiskControls>();
        private Label _jarvisStatus;

        private struct DiskControls {
            public string Drive;
            public Label  Val;
            public Label  Status;
            public GaugeBar Gau;
            public Panel Card;
        }
        private Label    _lblRefresh;
        private System.Windows.Forms.Timer _monTimer;
        private PerformanceCounter _cpuCounter;

        // Dump / Temp
        private CheckedListBox _dumpList; private Label _dumpStatus;
        private CheckedListBox _tempList; private Label _tempStatus;

        // Event Log
        private ComboBox _logCombo; private DataGridView _eventGrid; private Label _eventStatus;

        // Deep Sweeper
        private DataGridView _deepGrid; private Label _deepStatus, _deepWasteLabel;

        // Status Strip & Top Bar
        private ToolStripStatusLabel _stripAction, _stripDate;
        private Label _lblLive, _lblTimeHeader;

        // =====================================================================
        // State fields for CPU calculations
        private Dictionary<int, TimeSpan> _prevCpuTimes = new Dictionary<int, TimeSpan>();
        
        public MainForm() { Build(); }

        // =====================================================================
        //  SHELL
        // =====================================================================
        private void Build()
        {
            Text            = "ACE tool hub";
            try { 
                string lp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
                if (File.Exists(lp)) {
                    using (var bmp = new Bitmap(lp)) this.Icon = Icon.FromHandle(bmp.GetHicon());
                }
            } catch { }
            DoubleBuffered = true;
            this.MinimumSize = new Size(1100, 750);
            this.Size = new Size(1280, 800);
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = PrimaryBg;
            ForeColor       = TextPrimary;
            Font            = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.Sizable;

            Controls.Add(BuildTopBar());

            var body    = new Panel { Dock = DockStyle.Fill, BackColor = PrimaryBg };
            var sidebar = BuildSidebar();
            body.Controls.Add(sidebar);

            var host = new Panel { Dock = DockStyle.Fill, BackColor = PrimaryBg };
            _pages[0] = BuildMonitorPage();
            _pages[1] = BuildDumpPage();
            _pages[2] = BuildTempPage();
            _pages[3] = BuildDeepPage();
            _pages[4] = BuildQBPage();
            _pages[5] = BuildJarvisPage();
            _pages[6] = BuildReclaimPage();
            _pages[7] = BuildQBNXTPage();
            foreach (var pg in _pages) { if (pg == null) continue; pg.Dock = DockStyle.Fill; pg.Visible = false; host.Controls.Add(pg); }
            body.Controls.Add(host);
            Controls.Add(body);

            var strip = new StatusStrip { BackColor = SidebarBg, Padding = new Padding(4, 0, 4, 0) };
            _stripAction = new ToolStripStatusLabel("Ready") { ForeColor = AccentBlue, Font = new Font("Segoe UI", 8.5f) };
            _stripDate   = new ToolStripStatusLabel(Now()) { ForeColor = TextMuted, Alignment = ToolStripItemAlignment.Right, Font = new Font("Segoe UI", 8.5f) };
            strip.Items.AddRange(new ToolStripItem[] { _stripAction, new ToolStripSeparator(), _stripDate });
            Controls.Add(strip);

            try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); _cpuCounter.NextValue(); } catch { }

            _monTimer        = new System.Windows.Forms.Timer { Interval = 2000 };
            _monTimer.Tick  += (s, e) => RefreshMonitor();
            _monTimer.Start();

            // Fix WinForms docking Z-order so Fill panels yield to Left/Top panels correctly
            host.BringToFront();
            body.BringToFront();

            SwitchPage(0);
            RefreshMonitor();
            FormClosed += (s, e) => { _monTimer?.Dispose(); _cpuCounter?.Dispose(); };
        }

        // ── TOP BAR ──────────────────────────────────────────────────────────
        private Panel BuildTopBar()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = SidebarBg };
            bar.Paint += (s, e) => { using (var p = new Pen(Color.FromArgb(229, 231, 235))) e.Graphics.DrawLine(p, 0, bar.Height-1, bar.Width, bar.Height-1); };

            var lblTitle = FL("ACE TOOL HUB", new Point(24, 18), 13f, FontStyle.Bold, TextPrimary);
            bar.Controls.Add(lblTitle);

            _lblTimeHeader = FL(DateTime.Now.ToString("HH:mm  ·  ddd MMM d"), new Point(0, 22), 8.5f, FontStyle.Regular, TextSecond);
            _lblTimeHeader.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bar.Controls.Add(_lblTimeHeader);

            bar.Resize += (s, e) => {
                _lblTimeHeader.Location = new Point(bar.Width - _lblTimeHeader.Width - 24, 17);
            };

            var pulseT = new System.Windows.Forms.Timer { Interval = 1000 };
            pulseT.Tick += (s, e) => { 
                _lblTimeHeader.Text = DateTime.Now.ToString("HH:mm  ·  ddd MMM d");
            };
            pulseT.Start();
            return bar;
        }

        // ── SIDEBAR ──────────────────────────────────────────────────────────
        private const int NAV_COUNT = 8;
        private static readonly string[] _navIcons  = { "\uE9D9", "\uE74D", "\uEA99", "\uE8B7", "\uE115", "\uE99A", "\uE105", "\uE192" };
        private static readonly string[] _navLabels = { "Node Monitor", "Dump Maint.", "Disk Maint.", "Deep Sweeper", "QB Clean", "Jarvis 2.0", "Reclaim Server", "QB License Gen" };

        private Panel BuildSidebar()
        {
            var sb = new Panel { Dock = DockStyle.Left, Width = 178, BackColor = SidebarBg };
            sb.Paint += (s, e) => { using (var p = new Pen(BorderClr)) e.Graphics.DrawLine(p, sb.Width-1, 0, sb.Width-1, sb.Height); };

            sb.Controls.Add(FL("NAVIGATION", new Point(24, 20), 7f, FontStyle.Bold, TextMuted));

            for (int i = 0; i < NAV_COUNT; i++)
            {
                int idx = i;
                var item = BuildNavItem(idx, 46 + idx * 50);
                _navItems[i] = item;
                sb.Controls.Add(item);
            }

            var ver = FL("v1.0  ·  .NET 4.8", new Point(16, 0), 7.5f, FontStyle.Regular, TextMuted);
            ver.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            sb.Controls.Add(ver);
            sb.Resize += (s, e) => ver.Location = new Point(16, sb.Height - 28);
            return sb;
        }

        private Panel BuildNavItem(int idx, int y)
        {
            var p = new Panel { Location = new Point(0, y), Size = new Size(178, 48), BackColor = Color.Transparent, Cursor = Cursors.Hand };
            p.MouseEnter += (s, e) => { if (_activePage != idx) p.BackColor = NavHoverBg; };
            p.MouseLeave += (s, e) => { if (_activePage != idx) p.BackColor = Color.Transparent; };

            var accent = new Panel { Location = new Point(0, 8), Size = new Size(3, 32), BackColor = AccentBlue, Visible = false };
            var icon = new Label {
                Text = _navIcons[idx],
                Location = new Point(14, 10),
                Size = new Size(32, 28), // Fixed bounds prevents overlapping the text
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = TextSecond,
                BackColor = Color.Transparent,
                Font = new Font("Segoe MDL2 Assets", 14f, FontStyle.Regular),
                UseCompatibleTextRendering = true
            };
            var lbl    = FL(_navLabels[idx], new Point(50, 15), 9.5f, FontStyle.Regular, TextSecond);

            p.Controls.AddRange(new Control[] { accent, icon, lbl });
            p.Tag = new object[] { accent, icon, lbl };

            EventHandler go = (s, e) => SwitchPage(idx);
            p.Click += go; icon.Click += go; lbl.Click += go;

            EventHandler enter = (s, e) => { if (_activePage != idx) p.BackColor = NavHoverBg; };
            EventHandler leave = (s, e) => { if (_activePage != idx) p.BackColor = Color.Transparent; };
            foreach (Control c in new Control[] { p, icon, lbl }) { c.MouseEnter += enter; c.MouseLeave += leave; }
            return p;
        }

        private void SwitchPage(int idx)
        {
            _activePage = idx;
            for (int i = 0; i < NAV_COUNT; i++)
            {
                _pages[i].Visible = (i == idx);
                var refs = _navItems[i].Tag as object[];
                if (refs == null) continue;
                var accent = refs[0] as Panel;
                var icon   = refs[1] as Label;
                var lbl    = refs[2] as Label;
                bool active = (i == idx);
                if (accent != null) accent.Visible = active;
                if (icon   != null) icon.ForeColor  = active ? AccentBlue  : TextSecond;
                if (lbl    != null) lbl.ForeColor   = active ? TextPrimary : TextSecond;
                _navItems[i].BackColor = active ? NavActiveBg : Color.Transparent;
            }
        }

        // =====================================================================
        //  PAGE 0 — SYSTEM MONITOR
        // =====================================================================
        private Panel BuildMonitorPage()
        {
            var page = new Panel { BackColor = PrimaryBg };

            // ── CPU HERO CARD ──────────────────────────────────────────────────
            var cpuCard = new CardPanel { Location = new Point(20, 16), Size = new Size(100, 150), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            var cpuTitle = FL("CPU USAGE", new Point(16, 14), 8f, FontStyle.Bold, TextMuted);
            _lblCpuStatus = FL("● NORMAL", new Point(0, 14), 8f, FontStyle.Bold, AccentBlue);
            _lblCpuStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            _lblCpuPct = new Label { Text = "—%", Location = new Point(16, 32), AutoSize = true,
                Font = new Font(HF, 38f, FontStyle.Bold), ForeColor = AccentBlue, BackColor = Color.Transparent, UseCompatibleTextRendering = true };

            _lblTopProc = FL("Top Process: Scanning...", new Point(16, 116), 7.5f, FontStyle.Regular, TextMuted);
            var cpuSub = FL("% Processor Time  ·  _Total  ·  2s refresh", new Point(16, 130), 7.5f, FontStyle.Regular, TextMuted);

            _gauCpu = new GaugeBar { Location = new Point(200, 50), Size = new Size(100, 20), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            var cpuGauLbl = FL("UTILIZATION", new Point(200, 34), 7.5f, FontStyle.Bold, TextMuted);
            cpuGauLbl.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            cpuCard.Controls.AddRange(new Control[] { cpuTitle, _lblCpuStatus, _lblCpuPct, _lblTopProc, cpuSub, cpuGauLbl, _gauCpu });
            cpuCard.Resize += (s, e) => {
                int cw = cpuCard.Width;
                _lblCpuStatus.Location = new Point(cw - _lblCpuStatus.Width - 16, 14);
                _gauCpu.Location = new Point(200, 50);
                _gauCpu.Size     = new Size(cw - 220, 20);
                cpuGauLbl.Location = new Point(200, 34);
            };

            // ── NODE INFO CARD ─────────────────────────────────────────────────
            var nodeCard = new CardPanel { Location = new Point(0,0), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            var nodeTitle = FL("SYSTEM NODE INFO", new Point(16, 14), 8f, FontStyle.Bold, TextMuted);
            var lblHost = FL("Host  ·  " + Environment.MachineName, new Point(16, 38), 9f, FontStyle.Regular, TextPrimary);
            string ip = "";
            try {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                        foreach (var u in ni.GetIPProperties().UnicastAddresses)
                            if (u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) { ip = u.Address.ToString(); break; }
            } catch { }
            var lblIp = FL("IPv4  ·  " + (string.IsNullOrEmpty(ip) ? "Unknown" : ip), new Point(16, 62), 9f, FontStyle.Regular, TextPrimary);
            var lblOS = FL("OSV   ·  " + Environment.OSVersion.VersionString, new Point(16, 86), 9f, FontStyle.Regular, TextPrimary);
            _lblNetIo = FL("Net   ·  Initializing...", new Point(16, 110), 9f, FontStyle.Regular, TextPrimary);
            _lblUptime = FL("Up    ·  Calculating...", new Point(16, 134), 9f, FontStyle.Bold, AccentBlue);
            nodeCard.Controls.AddRange(new Control[] { nodeTitle, lblHost, lblIp, lblOS, _lblNetIo, _lblUptime });

            // ── RAM + DISK ROW ─────────────────────────────────────────────────
            var ramCard  = new CardPanel { Location = new Point(20, 180), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            // RAM card internals
            var ramTitle = FL("RAM USAGE", new Point(16, 14), 8f, FontStyle.Bold, TextMuted);
            _lblRamStatus = FL("● NORMAL", new Point(0, 14), 8f, FontStyle.Bold, SuccessGrn);
            _lblRamStatus.AutoSize = false;
            _lblRamStatus.Size = new Size(150, 20);
            _lblRamStatus.TextAlign = ContentAlignment.TopRight;
            _lblRamStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _lblRamVal = FL("— GB / — GB", new Point(16, 36), 20f, FontStyle.Bold, TextPrimary);
            _gauRam = new GaugeBar { Location = new Point(16, 90), Size = new Size(100, 16), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var ramGauLbl = FL("MEMORY UTILIZATION", new Point(16, 75), 7.5f, FontStyle.Bold, TextMuted);
            ramCard.Controls.AddRange(new Control[] { ramTitle, _lblRamStatus, _lblRamVal, ramGauLbl, _gauRam });
            ramCard.Resize += (s, e) => {
                _lblRamStatus.Location = new Point(ramCard.Width - _lblRamStatus.Width - 16, 14);
                _gauRam.Size = new Point(200, 34).X > 0 ? new Size(ramCard.Width - 220, 20) : new Size(ramCard.Width - 32, 16); // Dynamic sizing or fixed
                // Let's just use a clear full width for RAM
                _gauRam.Location = new Point(200, 50);
                _gauRam.Size = new Size(ramCard.Width - 220, 20);
            };

            // Disk Flow Panel
            _diskFlow = new FlowLayoutPanel { 
                Location = new Point(20, 330), 
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = false,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(0)
            };

            // ── LEGEND CARD ────────────────────────────────────────────────────
            var legCard = new CardPanel { Location = new Point(20, 480), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            legCard.Controls.Add(FL("AI INSIGHTS  ·  STATUS LEGEND", new Point(16, 12), 8f, FontStyle.Bold, TextMuted));
            legCard.Controls.Add(FL("●  Normal  (< 50%)", new Point(16, 34), 9f, FontStyle.Regular, AccentBlue));
            legCard.Controls.Add(FL("●  Moderate  (51 – 80%)", new Point(200, 34), 9f, FontStyle.Regular, AmberClr));
            legCard.Controls.Add(FL("●  Critical  (> 80%)", new Point(420, 34), 9f, FontStyle.Regular, CoralRed));
            _lblRefresh = FL("Last refreshed: —", new Point(0, 34), 8f, FontStyle.Regular, TextMuted);
            _lblRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            legCard.Controls.Add(_lblRefresh);
            legCard.Resize += (s, e) => _lblRefresh.Location = new Point(legCard.Width - _lblRefresh.Width - 16, 34);

            page.Controls.AddRange(new Control[] { cpuCard, ramCard, _diskFlow, legCard, nodeCard });

            // Resize handler to layout cards
            page.Resize += (s, e) => LayoutMonitorPage(page, cpuCard, ramCard, legCard, nodeCard);
            return page;
        }

        private void LayoutMonitorPage(Panel page, CardPanel cpu, CardPanel ram, CardPanel leg, CardPanel node)
        {
            int w = page.Width - 40, x = 20;
            if (w < 1) return;

            // Split RAM and Disk side by side
            // int half = (w - 12) / 2; // This line is commented out as it's no longer used for splitting.

            int topH = 166;
            int nodeWidth = 260;
            int cpuWidth  = w - nodeWidth - 12;
            cpu.SetBounds(x, 16, cpuWidth, topH);
            node.SetBounds(x + cpuWidth + 12, 16, nodeWidth, topH);

            ram.SetBounds(x, 16 + topH + 16, w, 110);
            
            _diskFlow.SetBounds(x, 16 + topH + 16 + 110 + 12, w, page.Height - (16 + topH + 16 + 110 + 12) - leg.Height - 16);
            leg.SetBounds(x, page.Height - leg.Height - 12, w, 68);
        }

        // ── Refresh live metrics ──────────────────────────────────────────────
        private void RefreshMonitor()
        {
            // CPU
            try
            {
                int pct = _cpuCounter != null ? Math.Min(100, (int)Math.Round(_cpuCounter.NextValue())) : 0;
                _lblCpuPct.Text    = pct + "%";
                _gauCpu.Value      = pct;
                _gauCpu.Fill       = GaugeColor(pct);
                _lblCpuPct.ForeColor = GaugeColor(pct);
                _lblCpuStatus.Text  = StatusLabel(pct);
                _lblCpuStatus.ForeColor = GaugeColor(pct);
            }
            catch { }

            // Network I/O
            long cRecv = 0, cSent = 0;
            try {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()) {
                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback) {
                        var st = ni.GetIPv4Statistics(); cRecv += st.BytesReceived; cSent += st.BytesSent;
                    }
                }
                if (_lastNetTime != DateTime.MinValue && _lblNetIo != null) {
                    double sec = (DateTime.Now - _lastNetTime).TotalSeconds;
                    if (sec > 0) {
                        double kbR = ((cRecv - _lastBytesRecv) / 1024.0) / sec;
                        double kbS = ((cSent - _lastBytesSent) / 1024.0) / sec;
                        _lblNetIo.Text = $"Net   ·  ↓ {kbR:F1} KB/s   ↑ {kbS:F1} KB/s";
                    }
                }
                _lastBytesRecv = cRecv; _lastBytesSent = cSent; _lastNetTime = DateTime.Now;
            } catch { }

            // Top CPU Process (Zero Allocation Deltas)
            System.Threading.Tasks.Task.Run(() => {
                try {
                    var procs = Process.GetProcesses();
                    string topObj = "Idle"; double topCpuMs = 0;
                    
                    foreach (var p in procs) {
                        try {
                            if (p.Id == 0 || p.ProcessName == "Idle") continue;
                            var cur = p.TotalProcessorTime;
                            if (_prevCpuTimes.TryGetValue(p.Id, out var prev)) {
                                double delta = (cur - prev).TotalMilliseconds;
                                if (delta > topCpuMs) { topCpuMs = delta; topObj = p.ProcessName; }
                            }
                            _prevCpuTimes[p.Id] = cur;
                        } catch { } // Access denied
                    }
                    
                    if (_prevCpuTimes.Count > procs.Length + 50) {
                        var keep = new HashSet<int>();
                        foreach (var p in procs) keep.Add(p.Id);
                        var nextCpuTimes = new Dictionary<int, TimeSpan>();
                        foreach(var kv in _prevCpuTimes) if (keep.Contains(kv.Key)) nextCpuTimes[kv.Key] = kv.Value;
                        _prevCpuTimes = nextCpuTimes;
                    }
                    
                    int pct = (int)((topCpuMs / (Environment.ProcessorCount * 2000.0)) * 100);
                    if (IsHandleCreated) Invoke(new Action(() => { if (_lblTopProc != null) _lblTopProc.Text = $"Top Process: {topObj} ({pct}%)"; }));
                } catch { }
            });

            // RAM (P/Invoke)
            try
            {
                NativeMethods.MEMORYSTATUSEX mem = new NativeMethods.MEMORYSTATUSEX();
                mem.Init();
                if (NativeMethods.GlobalMemoryStatusEx(ref mem))
                {
                    long tot = (long)mem.ullTotalPhys;
                    long free = (long)mem.ullAvailPhys;
                    long used = tot - free;
                    int pct = tot > 0 ? (int)Math.Round(100.0 * used / tot) : 0;
                    _lblRamVal.Text = $"{used / 1073741824.0:F1} GB / {tot / 1073741824.0:F1} GB  ({pct}%)";
                    _gauRam.Value = pct; _gauRam.Fill = GaugeColor(pct);
                    _lblRamStatus.Text = StatusLabel(pct); _lblRamStatus.ForeColor = GaugeColor(pct);
                    
                    try {
                        long tickMs = (long)NativeMethods.GetTickCount64();
                        var up = TimeSpan.FromMilliseconds(tickMs);
                        if (_lblUptime != null) _lblUptime.Text = $"Up    ·  {up.Days}d {up.Hours}h {up.Minutes}m";
                    } catch { }
                }
            }
            catch { }

            // Dynamic Disks
            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var di in drives)
                {
                    if (di.DriveType != DriveType.Fixed || !di.IsReady) continue;

                    var ctrl = _diskCtrls.Find(c => c.Drive == di.Name);
                    if (ctrl.Drive == null)
                    {
                        // Create new card for this disk
                        var card = new CardPanel { Size = new Size(224, 100), Margin = new Padding(0, 0, 10, 10) };
                        var title = FL($"DISK {di.Name} USAGE", new Point(12, 10), 7f, FontStyle.Bold, TextMuted);
                        var status = FL("● —", new Point(154, 10), 7f, FontStyle.Bold, AccentBlue);
                        var val = FL("— GB / — GB", new Point(12, 28), 12f, FontStyle.Bold, TextPrimary);
                        var gau = new GaugeBar { Location = new Point(12, 65), Size = new Size(216, 12) };
                        var sub = FL("UTILIZATION", new Point(12, 52), 6.5f, FontStyle.Bold, TextMuted);
                        
                        card.Controls.AddRange(new Control[] { title, status, val, gau, sub });
                        ctrl = new DiskControls { Drive = di.Name, Val = val, Status = status, Gau = gau, Card = card };
                        _diskCtrls.Add(ctrl);
                        _diskFlow.Controls.Add(card);
                    }

                    long tot = di.TotalSize, free = di.TotalFreeSpace, used = tot - free;
                    int pct  = tot > 0 ? (int)Math.Round(100.0 * used / tot) : 0;
                    ctrl.Val.Text = $"{used/1073741824.0:F1} GB / {tot/1073741824.0:F1} GB";
                    ctrl.Gau.Value = pct; ctrl.Gau.Fill = GaugeColor(pct);
                    ctrl.Status.Text = StatusLabel(pct); ctrl.Status.ForeColor = GaugeColor(pct);
                }
            }
            catch { }

            if (_lblRefresh != null) _lblRefresh.Text = "Last refreshed: " + DateTime.Now.ToString("HH:mm:ss");
            SetStatus("Metrics refreshed");
        }

        private Color GaugeColor(int pct) => pct < 50 ? AccentBlue : pct <= 80 ? AmberClr : CoralRed;
        private string StatusLabel(int pct) => pct < 50 ? "● NORMAL" : pct <= 80 ? "● MODERATE" : "● CRITICAL";

        private async void ScanDumpFiles()
        {
            _dumpList.Items.Clear(); _dumpStatus.Text = "Scanning…"; _dumpStatus.ForeColor = TextMuted;
            var found = new List<(string path, long size)>();

            await Task.Run(() => {
                // 1. Kernel memory dump (single file)
                try {
                    string memDmp = @"C:\Windows\MEMORY.DMP";
                    if (File.Exists(memDmp)) found.Add((memDmp, new FileInfo(memDmp).Length));
                } catch { }

                // 2. Minidump folder
                try {
                    string miniDir = @"C:\Windows\Minidump";
                    if (Directory.Exists(miniDir))
                        foreach (string f in SafeEnumerateFiles(miniDir, "*.*", true))
                            try { found.Add((f, new FileInfo(f).Length)); } catch { }
                } catch { }

                // 3. LiveKernelReports
                try {
                    string lkr = @"C:\Windows\LiveKernelReports";
                    if (Directory.Exists(lkr))
                        foreach (string f in SafeEnumerateFiles(lkr, "*.*", true))
                            try { found.Add((f, new FileInfo(f).Length)); } catch { }
                } catch { }

                // 4. WER
                string[] werPaths = {
                    @"C:\ProgramData\Microsoft\Windows\WER\ReportArchive",
                    @"C:\ProgramData\Microsoft\Windows\WER\ReportQueue",
                    @"C:\ProgramData\Microsoft\Windows\WER\Temp"
                };
                foreach (string wp in werPaths) {
                    try {
                        if (Directory.Exists(wp))
                            foreach (string f in SafeEnumerateFiles(wp, "*.*", true))
                                try { found.Add((f, new FileInfo(f).Length)); } catch { }
                    } catch { }
                }

                // 5. User Profile CrashDumps
                try {
                    if (Directory.Exists(@"C:\Users")) {
                        foreach (var dir in Directory.EnumerateDirectories(@"C:\Users")) {
                            string crashPath = Path.Combine(dir, @"AppData\Local\CrashDumps");
                            if (Directory.Exists(crashPath))
                                try { foreach (string f in SafeEnumerateFiles(crashPath, "*.*", true)) try { found.Add((f, new FileInfo(f).Length)); } catch { } } catch { }
                        }
                    }
                } catch { }

                // 6. Windows Temp
                try {
                    string winTemp = @"C:\Windows\Temp";
                    if (Directory.Exists(winTemp)) {
                        foreach (string f in SafeEnumerateFiles(winTemp, "*.*", false)) {
                            try {
                                string ext = Path.GetExtension(f).ToLowerInvariant();
                                if (ext == ".dmp" || ext == ".mdmp" || ext == ".hdmp" || ext == ".tmp" || ext == ".etl" || ext == ".cab")
                                    found.Add((f, new FileInfo(f).Length));
                            } catch { }
                        }
                    }
                } catch { }

                // 7. System temp folder
                try {
                    string sysTemp = Environment.GetEnvironmentVariable("TEMP");
                    if (!string.IsNullOrEmpty(sysTemp) && Directory.Exists(sysTemp)) {
                        foreach (string f in SafeEnumerateFiles(sysTemp, "*.dmp", true))
                            try { found.Add((f, new FileInfo(f).Length)); } catch { }
                        foreach (string f in SafeEnumerateFiles(sysTemp, "*.mdmp", true))
                            try { found.Add((f, new FileInfo(f).Length)); } catch { }
                        foreach (string f in SafeEnumerateFiles(sysTemp, "*.hdmp", true))
                            try { found.Add((f, new FileInfo(f).Length)); } catch { }
                    }
                } catch { }
            });

            if (found.Count == 0) { _dumpStatus.Text = "✔  No dump files found."; _dumpStatus.ForeColor = AccentBlue; SetStatus("Dump scan complete — nothing found"); return; }
            
            long tot = 0;
            foreach (var item in found) { tot += item.size; _dumpList.Items.Add($"[ {HumanSize(item.size),-9} ]  {item.path}"); }
            _dumpStatus.ForeColor = AmberClr;
            _dumpStatus.Text = $"Found {found.Count} file(s)  ·  Total: {HumanSize(tot)}";
            SetStatus($"Dump scan complete — {found.Count} file(s)");
        }

        private Panel BuildDumpPage()
        {
            var page = new Panel { BackColor = PrimaryBg };
            var top  = FL("🗑  DUMP FILE CLEANUP", new Point(20, 20), 13f, FontStyle.Bold, TextPrimary);
            var sub  = FL("Scan for Windows crash dump files (.dmp) and remove them to free disk space.", new Point(20, 50), 9f, FontStyle.Regular, TextMuted);

            var btnScan   = MkBtn("🔍  Scan for Dump Files", new Point(20, 78),  BtnBlue);
            var btnDel    = MkBtn("🗑  Delete Selected",      new Point(228, 78), BtnRed);
            var btnDelAll = MkBtn("⚠  Delete All",           new Point(436, 78), BtnOrange);
            _dumpStatus = FL("Click 'Scan' to begin.", new Point(20, 120), 9f, FontStyle.Regular, TextMuted);
            _dumpList   = MkList(new Point(20, 145), new Size(200, 400));

            btnScan.Click   += (s, e) => ScanDumpFiles();
            btnDel.Click    += (s, e) => ConfirmAndDelete(_dumpList, _dumpStatus, true,  (ss, ee) => ScanDumpFiles());
            btnDelAll.Click += (s, e) => ConfirmAndDelete(_dumpList, _dumpStatus, false, (ss, ee) => ScanDumpFiles());

            page.Controls.AddRange(new Control[] { top, sub, btnScan, btnDel, btnDelAll, _dumpStatus, _dumpList });
            page.Resize += (s, e) => { _dumpList.Size = new Size(page.Width - 40, page.Height - 170); };
            return page;
        }

        // =====================================================================
        //  PAGE 2 — TEMP FILE CLEANUP
        // =====================================================================
        private Label _tempWasteLabel;

        private Panel BuildTempPage()
        {
            var page = new Panel { BackColor = PrimaryBg };
            var top  = FL("🧹  DISK CLEANUP", new Point(20, 20), 13f, FontStyle.Bold, TextPrimary);
            var sub  = FL("Scan C: drive for temporary folders, Windows Update cache, and Recycle Bin.", new Point(20, 50), 9f, FontStyle.Regular, TextMuted);

            _tempWasteLabel = FL("WASTED SPACE: 0.00 MB", new Point(600, 20), 16f, FontStyle.Bold, AccentBlue);
            _tempWasteLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            var btnScan   = MkBtn("🔍  Scan for Files", new Point(20, 78),  BtnBlue);
            var btnDel    = MkBtn("🗑  Delete Selected",      new Point(228, 78), BtnRed);
            var btnDelAll = MkBtn("⚠  Delete All",           new Point(436, 78), BtnOrange);
            var btnCleanMgr = MkBtn("💽  OS Disk Cleanup",   new Point(644, 78), BtnBlue);
            _tempStatus = FL("Click 'Scan' to begin.", new Point(20, 120), 9f, FontStyle.Regular, TextMuted);
            _tempList   = MkList(new Point(20, 145), new Size(200, 400));

            btnScan.Click   += (s, e) => ScanTemps();
            btnDel.Click    += (s, e) => ConfirmAndDelete(_tempList, _tempStatus, true,  (ss, ee) => ScanTemps());
            btnDelAll.Click += (s, e) => ConfirmAndDelete(_tempList, _tempStatus, false, (ss, ee) => ScanTemps());
            btnCleanMgr.Click += (s, e) => { try { Process.Start("cleanmgr.exe", "/d C"); } catch { } };

            page.Controls.AddRange(new Control[] { top, sub, _tempWasteLabel, btnScan, btnDel, btnDelAll, btnCleanMgr, _tempStatus, _tempList });
            page.Resize += (s, e) => { 
                _tempList.Size = new Size(page.Width - 40, page.Height - 170); 
                _tempWasteLabel.Location = new Point(page.Width - _tempWasteLabel.Width - 20, 20);
            };
            return page;
        }

        private void ScanTemps()
        {
            var paths = new List<string> { 
                Environment.GetEnvironmentVariable("TEMP"),
                @"C:\Windows\Temp", 
                @"C:\Windows\Prefetch", 
                @"C:\Windows\SoftwareDistribution\Download", 
                @"C:\$Recycle.Bin",
                @"C:\inetpub\logs\LogFiles",
                @"C:\Windows\Logs\CBS",
                @"C:\ProgramData\Adobe\ARM"
            };
            paths.RemoveAll(x => string.IsNullOrEmpty(x));
            ScanFiles(_tempList, _tempStatus, paths.ToArray(), "*.*", false, _tempWasteLabel); // Set recursive to FALSE for true 'top-level' design
        }

        // =====================================================================
        //  PAGE 3 — EVENT LOG VIEWER
        // =====================================================================
        private Panel BuildLogPage()
        {
            var page = new Panel { BackColor = PrimaryBg };
            var top  = FL("📋  EVENT LOG VIEWER", new Point(20, 20), 13f, FontStyle.Bold, TextPrimary);

            var lblSel = FL("Select Log:", new Point(20, 60), 9f, FontStyle.Regular, TextSecond);
            _logCombo  = new ComboBox
            {
                Location = new Point(102, 56), Size = new Size(160, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White, ForeColor = TextPrimary,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f)
            };
            _logCombo.Items.AddRange(new object[] { "Application", "System", "Security" });
            _logCombo.SelectedIndex = 0;

            var btnLoad = MkBtn("🔄  Load Events", new Point(278, 52), BtnBlue);
            btnLoad.Width = 155;

            _eventStatus = FL("Select a log source and click Load.", new Point(20, 94), 9f, FontStyle.Regular, TextMuted);

            _eventGrid = new DataGridView
            {
                Location = new Point(20, 118), Size = new Size(100, 400),
                Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                BackgroundColor = Color.White,
                GridColor       = Color.FromArgb(0xe5, 0xe7, 0xeb),
                BorderStyle     = BorderStyle.None, RowHeadersVisible = false,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true,
                SelectionMode   = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 32,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White, ForeColor = TextPrimary,
                    SelectionBackColor = AccentBlue, SelectionForeColor = Color.White,
                    Font = new Font("Segoe UI", 9f), WrapMode = DataGridViewTriState.False
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(0xf3, 0xf4, 0xf6), ForeColor = AccentBlue,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    SelectionBackColor = Color.FromArgb(0xf3, 0xf4, 0xf6), SelectionForeColor = AccentBlue
                },
                EnableHeadersVisualStyles = false
            };

            AddCol(_eventGrid, "Timestamp", 145);
            AddCol(_eventGrid, "Level",      80);
            AddCol(_eventGrid, "Source",    175);
            AddCol(_eventGrid, "Event ID",   75);
            AddCol(_eventGrid, "Message",   415);
            _eventGrid.RowPrePaint += OnRowPrePaint;

            btnLoad.Click += (s, e) => LoadEventLog();

            page.Controls.AddRange(new Control[] { top, lblSel, _logCombo, btnLoad, _eventStatus, _eventGrid });
            page.Resize += (s, e) => { _eventGrid.Size = new Size(page.Width - 40, page.Height - 130); };
            return page;
        }

        // =====================================================================
        //  PAGE 4 — DEEP SWEEPER
        // =====================================================================
        private Panel BuildDeepPage()
        {
            var page = new Panel { BackColor = PrimaryBg };
            var top  = FL("🛡️  DEEP SYSTEM SWEEPER", new Point(20, 20), 13f, FontStyle.Bold, TextPrimary);
            var sub  = FL("Scan for .old / _old directories and associated orphaned Registry keys across C: drive.", new Point(20, 50), 9f, FontStyle.Regular, TextMuted);

            _deepWasteLabel = FL("POTENTIAL RECLAIM: 0.00 MB", new Point(600, 20), 16f, FontStyle.Bold, AmberClr);
            _deepWasteLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            var btnScan   = MkBtn("🔍  Deep Scan System", new Point(20, 82), BtnBlue);
            var btnDel    = MkBtn("🗑️  Delete Selected",  new Point(228, 82), BtnRed);
            var btnDelAll = MkBtn("🗑️  Delete All",       new Point(436, 82), BtnRed);

            _deepStatus = FL("Review items carefully before deletion.", new Point(20, 126), 9f, FontStyle.Regular, TextMuted);

            _deepGrid = new DataGridView
            {
                Location = new Point(20, 150), Size = new Size(1000, 400),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                BackgroundColor = Color.White, GridColor = Color.FromArgb(0xe5, 0xe7, 0xeb),
                BorderStyle = BorderStyle.None, RowHeadersVisible = false,
                AllowUserToAddRows = false, ReadOnly = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EnableHeadersVisualStyles = false
            };

            _deepGrid.DefaultCellStyle = new DataGridViewCellStyle {
                BackColor = Color.White, ForeColor = TextPrimary,
                SelectionBackColor = AccentBlue, SelectionForeColor = Color.White,
                Font = new Font("Segoe UI", 9f), WrapMode = DataGridViewTriState.False
            };
            _deepGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle {
                BackColor = Color.FromArgb(0xf3, 0xf4, 0xf6), ForeColor = AccentBlue,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                SelectionBackColor = Color.FromArgb(0xf3, 0xf4, 0xf6), SelectionForeColor = AccentBlue
            };

            // Checkbox column first
            var chkCol = new DataGridViewCheckBoxColumn {
                Name = "Select", HeaderText = "✓", Width = 36,
                FalseValue = false, TrueValue = true, IndeterminateValue = false,
                ReadOnly = false, SortMode = DataGridViewColumnSortMode.NotSortable
            };
            _deepGrid.Columns.Add(chkCol);
            AddCol(_deepGrid, "Type", 100);
            AddCol(_deepGrid, "Category", 120);
            AddCol(_deepGrid, "Description / Path", 530);
            AddCol(_deepGrid, "Details", 200);
            // Make only the checkbox column editable
            _deepGrid.CellValueChanged += (s, e) => { };
            _deepGrid.CurrentCellDirtyStateChanged += (s, e) => {
                if (_deepGrid.IsCurrentCellDirty) _deepGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            btnScan.Click += (s, e) => ScanDeep();
            btnDel.Click += (s, e) => DeleteDeep();
            btnDelAll.Click += (s, e) => {
                if (_deepGrid.Rows.Count > 0) {
                    foreach (DataGridViewRow r in _deepGrid.Rows) r.Cells["Select"].Value = true;
                    DeleteDeep();
                } else {
                    MessageBox.Show("No items to delete.", "Empty", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            page.Controls.AddRange(new Control[] { top, sub, _deepWasteLabel, btnScan, btnDel, btnDelAll, _deepStatus, _deepGrid });
            page.Resize += (s, e) => {
                _deepGrid.Size = new Size(page.Width - 40, page.Height - 170);
                _deepWasteLabel.Location = new Point(page.Width - _deepWasteLabel.Width - 20, 20);
            };

            return page;
        }

        private void ScanDeep()
        {
            if (_deepGrid == null) return;
            _deepGrid.Rows.Clear();
            _deepStatus.Text = "Deep scanning System (Registry + Files)...";
            _deepStatus.ForeColor = TextMuted;
            long totalWaste = 0;
            SetStatus("Deep scan initiated...");

            System.Threading.Tasks.Task.Run(() => {
                var results = new List<object[]>();

                // 1. Scan Folders & Files
                var pathsToScan = new List<(string path, int depth)> {
                    (@"C:\", 1),
                    (@"C:\Windows", 1),
                    (@"C:\Users", 2),
                    (@"C:\Program Files", 2),
                    (@"C:\Program Files (x86)", 2),
                    (@"C:\ProgramData", 2)
                };

                foreach (var (root, limit) in pathsToScan)
                {
                    if (!Directory.Exists(root)) continue;
                    ScanDirRecursive(root, 0, limit, results, ref totalWaste);
                }

                // 2. Scan Registry for orphaned paths
                string[] regPaths = {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (var rp in regPaths)
                {
                    try {
                        using (var key = Registry.LocalMachine.OpenSubKey(rp))
                        {
                            if (key == null) continue;
                            foreach (var subKeyName in key.GetSubKeyNames())
                            {
                                try {
                                    using (var subKey = key.OpenSubKey(subKeyName))
                                    {
                                        if (subKey == null) continue;
                                        object loc = subKey.GetValue("InstallLocation");
                                        if (loc != null)
                                        {
                                            string path = loc.ToString();
                                            if (path.IndexOf(".old", StringComparison.OrdinalIgnoreCase) >= 0 || 
                                                path.IndexOf("_old", StringComparison.OrdinalIgnoreCase) >= 0)
                                            {
                                                results.Add(new object[] { false, "Registry", "Uninstall", $@"HKLM\{rp}\{subKeyName}", path });
                                            }
                                        }
                                    }
                                } catch {}
                            }
                        }
                    } catch {}
                }

                SafeInvoke(() => {
                    _deepGrid.Rows.Clear();
                    foreach (var r in results) _deepGrid.Rows.Add(r);
                    
                    if (results.Count == 0) {
                        _deepStatus.Text = "✔  System is clean. No .old / _old items found.";
                        _deepStatus.ForeColor = AccentBlue;
                    } else {
                        _deepStatus.Text = $"Scan complete. Found {results.Count} items to review.";
                        _deepStatus.ForeColor = AmberClr;
                    }
                    _deepWasteLabel.Text = $"POTENTIAL RECLAIM: {HumanSize(totalWaste)}";
                    SetStatus(results.Count == 0 ? "Deep scan: system clean" : $"Deep scan found {results.Count} items");
                });
            });
        }

        private void ScanDirRecursive(string path, int currentDepth, int maxDepth, List<object[]> results, ref long totalWaste)
        {
            if (currentDepth > maxDepth) return;
            try
            {
                foreach (var f in Directory.EnumerateFiles(path)) {
                    string name = Path.GetFileName(f);
                    if (name.EndsWith(".old", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_old", StringComparison.OrdinalIgnoreCase)) {
                        try {
                            long size = new FileInfo(f).Length;
                            lock (results) { results.Add(new object[] { false, "📄 File", "System/App Backup", f, HumanSize(size) }); totalWaste += size; }
                        } catch {}
                    }
                }

                var dirs = Directory.EnumerateDirectories(path);
                foreach (var d in dirs)
                {
                    try {
                        string name = Path.GetFileName(d);
                        bool isMatch = name.EndsWith(".old", StringComparison.OrdinalIgnoreCase) || 
                                       name.EndsWith("_old", StringComparison.OrdinalIgnoreCase) ||
                                       name.Equals("Windows.old", StringComparison.OrdinalIgnoreCase);

                        if (isMatch)
                        {
                            long size = 0;
                            foreach(var f in SafeEnumerateFiles(d, "*.*", true)) {
                                try { size += new FileInfo(f).Length; } catch {}
                            }
                            lock(results) {
                                results.Add(new object[] { false, "📁 Folder", (name.Contains("Windows.old") ? "⚠ OS ROLLBACK" : "System/App"), d, HumanSize(size) });
                                totalWaste += size;
                            }
                        }
                        else if (currentDepth < maxDepth)
                        {
                            ScanDirRecursive(d, currentDepth + 1, maxDepth, results, ref totalWaste);
                        }
                    } catch {}
                }
            } catch {}
        }

        private void SafeInvoke(Action act)
        {
            if (IsHandleCreated) 
            {
                try { BeginInvoke(act); } catch {}
            }
        }

        private void DeleteDeep()
        {
            var checkedRows = new List<DataGridViewRow>();
            foreach (DataGridViewRow row in _deepGrid.Rows) {
                if (row.Cells["Select"].Value is bool b && b) checkedRows.Add(row);
            }

            if (checkedRows.Count == 0) {
                MessageBox.Show("Please tick the checkboxes next to the items you want to delete.", "Nothing Checked", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show($"Are you sure you want to delete the {checkedRows.Count} checked items?\n\nThis will remove folders, files, and Registry keys permanently.",
                "Confirm Deep Clean", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            int deleted = 0, failed = 0;
            foreach (DataGridViewRow row in checkedRows)
            {
                string type = row.Cells["Type"].Value?.ToString() ?? "";
                string path = row.Cells["Description / Path"].Value?.ToString() ?? "";

                try {
                    if (type.Contains("Folder")) {
                        if (Directory.Exists(path)) {
                            Directory.Delete(path, true);
                            deleted++;
                        }
                    } else if (type.Contains("Registry")) {
                        if (path.StartsWith(@"HKLM\")) {
                            string subPath = path.Substring(5);
                            Registry.LocalMachine.DeleteSubKeyTree(subPath, false);
                            deleted++;
                        }
                    } else if (type.Contains("File")) {
                        if (File.Exists(path)) {
                            File.Delete(path);
                            deleted++;
                        }
                    }
                } catch { failed++; }
            }

            MessageBox.Show($"Clean completed.\nSuccess: {deleted}\nFailed/Locked: {failed}", "Execution Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ScanDeep();
        }

        private void OnRowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _eventGrid.Rows.Count) return;
            var row   = _eventGrid.Rows[e.RowIndex];
            string lv = row.Cells["Level"].Value?.ToString() ?? "";
            if (lv.IndexOf("Error",   StringComparison.OrdinalIgnoreCase) >= 0) row.DefaultCellStyle.BackColor = Color.FromArgb(60, 20, 20);
            else if (lv.IndexOf("Warning", StringComparison.OrdinalIgnoreCase) >= 0) row.DefaultCellStyle.BackColor = Color.FromArgb(58, 46, 14);
            else row.DefaultCellStyle.BackColor = row.Index % 2 == 0 ? Color.White : Color.FromArgb(0xf9, 0xfa, 0xfb);
        }

        private async void LoadEventLog()
        {
            string logName = _logCombo.SelectedItem?.ToString() ?? "Application";
            _eventStatus.Text = "Loading async…"; _eventStatus.ForeColor = TextMuted;
            _eventGrid.Rows.Clear(); Application.DoEvents();

            try
            {
                var entries = new List<object[]>();
                await System.Threading.Tasks.Task.Run(() => {
                    var log = new EventLog(logName);
                    int total = log.Entries.Count, start = Math.Max(0, total - 300);
                    for (int i = total - 1; i >= start; i--)
                    {
                        try {
                            var en = log.Entries[i];
                            string msg = (en.Message ?? "").Replace('\n', ' ').Replace('\r', ' ');
                            if (msg.Length > 300) msg = msg.Substring(0, 300) + "…";
                            entries.Add(new object[] { en.TimeGenerated.ToString("yyyy-MM-dd HH:mm:ss"), en.EntryType.ToString(), en.Source, en.InstanceId.ToString(), msg });
                        } catch { }
                    }
                });

                foreach (var row in entries) _eventGrid.Rows.Add(row);
                _eventStatus.ForeColor = SuccessGrn;
                _eventStatus.Text = $"Loaded {_eventGrid.Rows.Count} entries from '{logName}'";
                SetStatus($"Event log '{logName}' loaded");
            }
            catch (Exception ex) { _eventStatus.ForeColor = CoralRed; _eventStatus.Text = "Error: " + ex.Message; }
        }

        // =====================================================================
        //  SHARED SCAN LOGIC
        // =====================================================================
        private async void ScanFiles(CheckedListBox list, Label status, string[] paths, string pattern, bool recursive, Label analyticsLabel = null)
        {
            list.Items.Clear(); status.Text = "Scanning…"; status.ForeColor = TextMuted;
            var found = new List<(string path, long size)>();

            await Task.Run(() => {
                foreach (string p in paths)
                {
                    if (!Directory.Exists(p)) { if (File.Exists(p)) try { found.Add((p, new FileInfo(p).Length)); } catch { } continue; }
                    try { foreach (string f in SafeEnumerateFiles(p, pattern, recursive)) try { found.Add((f, new FileInfo(f).Length)); } catch { } }
                    catch { }
                }
            });

            if (found.Count == 0) { status.Text = "✔  No files found."; status.ForeColor = AccentBlue; SetStatus("Scan complete — nothing found"); return; }

            long tot = 0;
            foreach (var (path, size) in found) { tot += size; list.Items.Add($"[ {HumanSize(size),-9} ]  {path}"); }
            status.ForeColor = AmberClr;
            status.Text = $"Found {found.Count} file(s)  ·  Total: {HumanSize(tot)}";
            if (analyticsLabel != null) analyticsLabel.Text = $"WASTED SPACE: {HumanSize(tot)}";
            SetStatus($"Scan complete — {found.Count} file(s)");
        }

        private IEnumerable<string> SafeEnumerateFiles(string path, string pattern = "*.*", bool recursive = false)
        {
            IEnumerable<string> topFiles = null;
            try { topFiles = Directory.EnumerateFiles(path, pattern, SearchOption.TopDirectoryOnly); } catch { }
            if (topFiles != null) { foreach (var f in topFiles) yield return f; }

            if (recursive) {
                IEnumerable<string> topDirs = null;
                try { topDirs = Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly); } catch { }
                if (topDirs != null) {
                    foreach (var d in topDirs) {
                        foreach (var f in SafeEnumerateFiles(d, pattern, true)) yield return f;
                    }
                }
            }
        }

        // =====================================================================
        //  SHARED DELETE UTILITY
        // =====================================================================
        private async void ConfirmAndDelete(CheckedListBox list, Label status, bool selectedOnly, EventHandler rescan)
        {
            var items = new List<string>();
            if (selectedOnly) {
                if (list.CheckedItems.Count == 0) { MessageBox.Show("No files checked. Please tick the checkboxes next to the files you want to delete.", "Nothing Checked", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                foreach (object o in list.CheckedItems) items.Add(ExtractPath(o.ToString()));
            } else {
                foreach (object o in list.Items) items.Add(ExtractPath(o.ToString()));
            }

            if (items.Count == 0) { MessageBox.Show("No files to delete.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            string lbl = selectedOnly ? $"{items.Count} selected file(s)" : $"all {items.Count} file(s)";
            if (MessageBox.Show($"Delete {lbl}?\n\nThis cannot be undone.", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            status.Text = "Deleting…"; status.ForeColor = TextMuted;
            int del = 0, skip = 0;
            await Task.Run(() => {
                foreach (string path in items) try { if (File.Exists(path)) { File.Delete(path); del++; } } catch { skip++; }
            });

            rescan?.Invoke(this, EventArgs.Empty);
            MessageBox.Show($"Deleted: {del}\nSkipped (locked / missing): {skip}", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetStatus($"Deleted {del}, skipped {skip}");
        }

        private static string ExtractPath(string item)
        {
            int idx = item.IndexOf("]");
            return idx >= 0 && idx + 2 < item.Length ? item.Substring(idx + 1).TrimStart() : item.Trim();
        }

        // =====================================================================
        //  UI FACTORY HELPERS
        // =====================================================================
        /// <summary>Make a flat label (FL = Factory Label).</summary>
        private static Label FL(string text, Point loc, float size, FontStyle style, Color color)
        {
            string fontName = (style == FontStyle.Bold) ? HF : "Segoe UI";
            return new Label 
            { 
                Text = text, 
                Location = loc, 
                AutoSize = true, 
                ForeColor = color, 
                BackColor = Color.Transparent, 
                Font = new Font(fontName, size, style),
                UseCompatibleTextRendering = true
            };
        }

        private static Button MkBtn(string text, Point loc, Color bg)
            => new Button { Text = text, Location = loc, Size = new Size(200, 34), BackColor = bg, ForeColor = Color.White,
                            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand,
                            FlatAppearance = { BorderSize = 0 } };

        private static CheckedListBox MkList(Point loc, Size sz)
            => new CheckedListBox { Location = loc, Size = sz, BackColor = Color.White, ForeColor = Color.FromArgb(17, 24, 39),
                             Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false,
                             CheckOnClick = true, HorizontalScrollbar = true };

        private static void AddCol(DataGridView g, string name, int width)
            => g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, Name = name, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable });

        // =====================================================================
        //  HUMAN-READABLE SIZE
        // =====================================================================
        private static string HumanSize(long b)
        {
            if (b >= 1_073_741_824L) return $"{b / 1_073_741_824.0:F2} GB";
            if (b >= 1_048_576L)     return $"{b / 1_048_576.0:F1} MB";
            if (b >= 1_024L)         return $"{b / 1024L} KB";
            return $"{b} B";
        }

        private static string Now() => DateTime.Now.ToString("ddd, MMM d yyyy");

        // =====================================================================
        //  STATUS BAR
        // =====================================================================
        private void SetStatus(string msg)
        {
            if (_stripAction != null) _stripAction.Text = msg;
            if (_stripDate   != null) _stripDate.Text   = Now();
        }

        #region PAGE 5 - VM METRICS (PROMETHEUS)

        private ComboBox _timeRangeCombo;
        private const string GRAFANA_BASE = "https://107.191.176.44";
        private const string DASH_UID = "vmware-windows-slowness";

        private Panel BuildMetricsPage()
        {
            var p = new Panel { BackColor = PrimaryBg, Padding = new Padding(20) };

            var header = FL("📈 Prometheus — VM Slowness Diagnostics", new Point(20, 20), 14f, FontStyle.Bold, TextPrimary);
            var sub = FL("Regional infrastructure monitoring for US East/West/Central. Click panels to view high-res graphs in browser.", new Point(20, 50), 9f, FontStyle.Italic, TextMuted);

            var controls = new FlowLayoutPanel { Location = new Point(20, 85), Size = new Size(800, 40), FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent };
            controls.Controls.Add(FL("Time Range:", new Point(0, 0), 9f, FontStyle.Regular, TextPrimary));
            
            _timeRangeCombo = new ComboBox { Width = 150, BackColor = Color.White, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f) };
            _timeRangeCombo.Items.AddRange(new string[] { "Last 30 minutes", "Last 1 hour", "Last 3 hours", "Last 6 hours", "Last 24 hours" });
            _timeRangeCombo.SelectedIndex = 2;
            controls.Controls.Add(_timeRangeCombo);

            var btnFull = MkBtn("🌐 Open Full Dashboard", new Point(0, 0), AccentBlue);
            btnFull.Width = 180; btnFull.Height = 28;
            btnFull.Click += (s, e) => OpenGrafana("");
            controls.Controls.Add(btnFull);

            var grid = new FlowLayoutPanel { Location = new Point(20, 130), Size = new Size(p.Width - 40, p.Height - 160), AutoScroll = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom, BackColor = Color.Transparent };

            AddSection(grid, "📊 SUMMARY INDICATORS", AccentBlue);
            AddMetricCard(grid, 2, "VMs with CPU Ready > 5%", "VMware: CPU contention", "summary", AmberClr);
            AddMetricCard(grid, 3, "VMs Ballooning Memory", "VMware: Active ballooning", "summary", AmberClr);
            AddMetricCard(grid, 4, "VMs with Disk Latency > 20ms", "VMware + OS: I/O slowness", "summary", CoralRed);
            AddMetricCard(grid, 6, "Exporters DOWN", "Unreachable monitoring targets", "summary", CoralRed);
            AddMetricCard(grid, 7, "Active Firing Alerts", "Total alerts currently firing", "summary", CoralRed);

            AddSection(grid, "🔵 VMWARE VSPHERE LAYER", AmberClr);
            AddMetricCard(grid, 11, "VM CPU Ready % — Top VMs", "Who is waiting for CPU", "vmware", AmberClr);
            AddMetricCard(grid, 12, "ESXi Host CPU Utilisation", "Over-committed hosts", "vmware", AmberClr);
            AddMetricCard(grid, 21, "VM Memory Balloon (MB)", "Active ballooning per VM", "vmware", Color.MediumPurple);
            AddMetricCard(grid, 22, "VM Memory Swap-in Rate", "Critical swap activity", "vmware", CoralRed);
            AddMetricCard(grid, 31, "VMware Disk Read/Write", "Datastore latency in ms", "vmware", CoralRed);

            AddSection(grid, "🟢 WINDOWS OS LAYER", SuccessGrn);
            AddMetricCard(grid, 32, "Windows Disk Queue", "OS: Disk saturation", "windows", CoralRed);
            AddMetricCard(grid, 33, "Windows Disk Free Space", "OS: Free space per volume", "windows", SuccessGrn);
            AddMetricCard(grid, 43, "Windows Network Out", "OS: NIC bytes/s", "windows", AccentBlue);
            AddMetricCard(grid, 44, "Windows Packet Drops", "OS: NIC discard rate", "windows", CoralRed);

            p.Controls.AddRange(new Control[] { header, sub, controls, grid });
            return p;
        }

        private void AddSection(FlowLayoutPanel grid, string title, Color c)
        {
            var lbl = FL(title, new Point(0, 0), 9f, FontStyle.Bold, c);
            lbl.Width = grid.Width - 60; lbl.Margin = new Padding(0, 25, 0, 10);
            grid.Controls.Add(lbl);
        }

        private void AddMetricCard(FlowLayoutPanel grid, int panelId, string title, string desc, string layer, Color badgeColor)
        {
            var card = new CardPanel { Size = new Size(230, 145), Margin = new Padding(0, 0, 15, 15) };
            
            var badge = FL(layer.ToUpper(), new Point(16, 14), 7.5f, FontStyle.Bold, badgeColor);
            var lblTitle = FL(title, new Point(16, 36), 9.5f, FontStyle.Bold, Color.White);
            lblTitle.AutoSize = false; lblTitle.Size = new Size(200, 38);
            
            var lblDesc = FL(desc, new Point(16, 75), 8f, FontStyle.Italic, TextMuted);
            lblDesc.AutoSize = false; lblDesc.Size = new Size(200, 35);
            
            var btn = MkBtn("Open Panel", new Point(16, 108), Color.White);
            btn.Size = new Size(198, 26); btn.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            btn.Click += (s, e) => OpenGrafana(panelId.ToString());

            card.Controls.AddRange(new Control[] { badge, lblTitle, lblDesc, btn });
            grid.Controls.Add(card);
        }

        private void OpenGrafana(string panelId)
        {
            string time = "now-3h";
            if (_timeRangeCombo != null) {
                switch (_timeRangeCombo.SelectedIndex) {
                    case 0: time = "now-30m"; break;
                    case 1: time = "now-1h"; break;
                    case 2: time = "now-3h"; break;
                    case 3: time = "now-6h"; break;
                    case 4: time = "now-24h"; break;
                }
            }

            string url = $"{GRAFANA_BASE}/?orgId=1&search=open";
            if (!string.IsNullOrEmpty(panelId)) url = $"{GRAFANA_BASE}/d/{DASH_UID}/title?orgId=1&from={time}&to=now&panelId={panelId}";
            try { Process.Start(url); } catch { }
        }

        #endregion

        #region PAGE 6 - QUICKBOOKS MAINTENANCE

        private ComboBox _qbTypeCombo, _qbYearCombo;
        private Label _qbStatus;
        private ListBox _qbLog;

        private Panel BuildQBPage()
        {
            var p = new Panel { BackColor = PrimaryBg, Padding = new Padding(20) };

            var header = FL("📦 QuickBooks Maintenance & Clean Install", new Point(20, 20), 14f, FontStyle.Bold, TextPrimary);
            var sub = FL("Automated diagnostics for company file access (.ND/.TLG) and clean install preparation.", new Point(20, 50), 9f, FontStyle.Italic, TextMuted);

            // Group 1: Diagnostics
            var groupDiag = new Panel { Location = new Point(20, 90), Size = new Size(380, 200), BackColor = SidebarBg };
            groupDiag.Controls.Add(FL("🔍 SERVICE & FILE DIAGNOSTICS", new Point(15, 15), 9f, FontStyle.Bold, AccentBlue));
            
            var btnCheck = MkBtn("Scan QB Services", new Point(15, 45), AccentBlue);
            btnCheck.Width = 160;
            btnCheck.Click += (s, e) => {
                _qbLog.Items.Clear();
                QBLog("▶ Starting QB Service Scan...");
                _qbStatus.Text = "● SCANNING SERVICES..."; _qbStatus.ForeColor = AmberClr;
                string[] svcs = { "QBCFMonitorService", "QuickBooksDB25", "QuickBooksDB26", "QuickBooksDB27", "QuickBooksDB28", "QBUpdateService" };
                foreach (var svc in svcs) {
                    try {
                        using (var sc = new ServiceController(svc)) {
                            string status = sc.Status.ToString();
                            string icon = status == "Running" ? "✅" : status == "Stopped" ? "🔴" : "⚠";
                            QBLog($"{icon} {svc,-35} → {status}");
                        }
                    } catch { QBLog($"⬛ {svc,-35} → Not Installed"); }
                }
                QBLog("─────────────────────────────────────────────");
                QBLog("✔ Service scan complete.");
                _qbStatus.Text = "● SCAN COMPLETE"; _qbStatus.ForeColor = SuccessGrn;
            };

            var btnRepair = MkBtn("Fix Company File Access", new Point(185, 45), SuccessGrn);
            btnRepair.Width = 180;
            btnRepair.Click += (s, e) => {
                using (var fbd = new FolderBrowserDialog { Description = "Select folder containing QuickBooks Company Files (.QBW)" }) {
                    if (fbd.ShowDialog() == DialogResult.OK) {
                        _qbLog.Items.Clear();
                        QBLog($"▶ Fixing company file access in: {fbd.SelectedPath}");
                        _qbStatus.Text = "● REPAIRING..."; _qbStatus.ForeColor = AmberClr;
                        try {
                            int count = 0;
                            foreach (var f in Directory.GetFiles(fbd.SelectedPath, "*.nd", SearchOption.TopDirectoryOnly)) {
                                QBLog($"🗑 Deleting: {Path.GetFileName(f)}");
                                File.Delete(f); count++;
                            }
                            foreach (var f in Directory.GetFiles(fbd.SelectedPath, "*.tlg", SearchOption.TopDirectoryOnly)) {
                                QBLog($"🗑 Deleting: {Path.GetFileName(f)}");
                                File.Delete(f); count++;
                            }
                            QBLog($"✔ Removed {count} .ND/.TLG file(s). QB will recreate them on next open.");
                            _qbStatus.Text = "● REPAIR COMPLETE"; _qbStatus.ForeColor = SuccessGrn;
                        } catch (Exception ex) {
                            QBLog($"❌ Error: {ex.Message}");
                            _qbStatus.Text = "● ERROR"; _qbStatus.ForeColor = CoralRed;
                        }
                    }
                }
            };

            var diagInfo = FL("Commonly fixes: Multi-user access 6xxx series errors, 'File in use' locks, and service startup failures.", new Point(15, 85), 8.5f, FontStyle.Regular, TextMuted);
            diagInfo.AutoSize = false; diagInfo.Size = new Size(350, 60);

            groupDiag.Controls.AddRange(new Control[] { btnCheck, btnRepair, diagInfo });

            // Group 2: Clean Install Prep
            var groupClean = new Panel { Location = new Point(420, 90), Size = new Size(380, 280), BackColor = SidebarBg };
            groupClean.Controls.Add(FL("⚡ CLEAN INSTALL PREPARATION", new Point(15, 15), 9f, FontStyle.Bold, CoralRed));
            
            groupClean.Controls.Add(FL("QuickBooks Edition:", new Point(15, 45), 8.5f, FontStyle.Regular, TextPrimary));
            _qbTypeCombo = new ComboBox { Location = new Point(15, 65), Width = 230, DropDownWidth = 350, BackColor = Color.White, ForeColor = Color.FromArgb(17, 24, 39), FlatStyle = FlatStyle.Flat };
            _qbTypeCombo.Items.AddRange(new string[] { "QuickBooks Desktop Pro", "QuickBooks Desktop Premier", "QuickBooks Desktop Enterprise" });
            _qbTypeCombo.SelectedIndex = 0;

            groupClean.Controls.Add(FL("Version Year:", new Point(260, 45), 8.5f, FontStyle.Regular, TextPrimary));
            _qbYearCombo = new ComboBox { Location = new Point(260, 65), Width = 100, BackColor = Color.White, ForeColor = Color.FromArgb(17, 24, 39), FlatStyle = FlatStyle.Flat };
            _qbYearCombo.Items.AddRange(new string[] { "2018", "2019", "2020", "2021", "2022", "2023", "2024", "Unknown/All" });
            _qbYearCombo.SelectedIndex = 6; // Default to 2024

            var btnPrep = MkBtn("Run Clean Prep (Rename Folders)", new Point(15, 110), CoralRed);
            btnPrep.Width = 350;
            btnPrep.Click += (s, e) => ExecuteQBPrep();

            var cleanNote = FL("Critical: Uninstall QuickBooks via Control Panel FIRST. This tool automates the renaming of residual folders to .OLD to prevent corruption in fresh installs.", new Point(15, 150), 8.5f, FontStyle.Italic, AmberClr);
            cleanNote.AutoSize = false; cleanNote.Size = new Size(350, 100);

            groupClean.Controls.AddRange(new Control[] { _qbTypeCombo, _qbYearCombo, btnPrep, cleanNote });

            _qbStatus = FL("● READY", new Point(20, 390), 9f, FontStyle.Bold, TextMuted);

            // Live Activity Log
            var logHeader = FL("📋 ACTIVITY LOG", new Point(20, 415), 9f, FontStyle.Bold, AccentBlue);
            _qbLog = new ListBox {
                Location = new Point(20, 440),
                Size = new Size(800, 180),
                BackColor = Color.FromArgb(17, 24, 39),
                ForeColor = Color.FromArgb(110, 231, 183),
                Font = new Font("Cascadia Code", 8.5f),
                BorderStyle = BorderStyle.FixedSingle,
                HorizontalScrollbar = true,
                IntegralHeight = false
            };
            p.Resize += (s, e) => {
                _qbLog.Size = new Size(p.Width - 40, 180);
                logHeader.Location = new Point(20, groupClean.Bottom + 20);
                _qbLog.Location = new Point(20, groupClean.Bottom + 45);
                _qbStatus.Location = new Point(20, _qbLog.Bottom + 6);
            };

            p.Controls.AddRange(new Control[] { header, sub, groupDiag, groupClean, logHeader, _qbLog, _qbStatus });
            return p;
        }

        private void QBLog(string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}]  {message}";
            if (_qbLog.InvokeRequired)
                _qbLog.Invoke((Action)(() => { _qbLog.Items.Add(entry); _qbLog.TopIndex = _qbLog.Items.Count - 1; }));
            else { _qbLog.Items.Add(entry); _qbLog.TopIndex = _qbLog.Items.Count - 1; }
        }

        private void ExecuteQBPrep()
        {
            string year = _qbYearCombo.SelectedItem?.ToString() ?? "2024";
            if (MessageBox.Show($"Are you sure you want to run a full Clean Install Prep for QuickBooks {year}?\n\nThis will stop all Intuit processes, execute the uninstaller, and then rename all leftover installation and Common Files folders to .old.",
                "Confirm Clean Prep", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            _qbLog.Items.Clear();
            QBLog($"▶ Starting Clean Install Prep for QuickBooks {year}...");
            _qbStatus.Text = "● PREPARING SYSTEM..."; _qbStatus.ForeColor = AmberClr;
            
            QBLog("⚙ Stopping QuickBooks Processes...");
            string[] procs = { "QBW32", "QBDBMgrN", "QBDBMgr", "QBCFMonitorService", "QBUpdate", "IntuitSyncManager", "QuickBooksMessenger", "IBUEngine", "DbNotify" };
            foreach (var pName in procs) {
                try {
                    var running = Process.GetProcessesByName(pName);
                    if (running.Length > 0) {
                        foreach (var p in running) { 
                            QBLog($"    🛑 Killing process: {pName} (PID: {p.Id})");
                            p.Kill(); 
                            p.WaitForExit(3000); 
                        }
                    } else {
                        QBLog($"    ⚪ {pName} is not running");
                    }
                } catch (Exception ex) { 
                    QBLog($"    ⚠ Error stopping {pName}: {ex.Message}");
                }
            }

            // Auto Uninstall QuickBooks
            QBLog("🗑 Uninstalling QuickBooks...");
            
            // Version strings for matching (e.g. 2024 -> 34.0, 24.0)
            var vStrs = new System.Collections.Generic.List<string>();
            if (int.TryParse(year, out int yI)) {
                vStrs.Add((yI - 1990).ToString() + ".0"); // Pro/Prem
                vStrs.Add((yI - 2000).ToString() + ".0"); // Enterprise
            }

            var keys = new[] {
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall")
            };

            int uninstalledCount = 0;
            foreach (var baseKey in keys)
            {
                if (baseKey == null) continue;
                foreach (var subKeyName in baseKey.GetSubKeyNames())
                {
                    using (var subKey = baseKey.OpenSubKey(subKeyName))
                    {
                        if (subKey == null) continue;
                        var displayName = subKey.GetValue("DisplayName")?.ToString();
                        if (string.IsNullOrEmpty(displayName)) continue;

                        bool match = false;
                        if (year == "Unknown/All") {
                            if (displayName.IndexOf("QuickBooks", StringComparison.OrdinalIgnoreCase) >= 0) match = true;
                        } else {
                            bool hasQB = displayName.IndexOf("QuickBooks", StringComparison.OrdinalIgnoreCase) >= 0;
                            bool hasYear = displayName.IndexOf(year, StringComparison.OrdinalIgnoreCase) >= 0;
                            bool hasVer = vStrs.Exists(v => displayName.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0);
                            if (hasQB && (hasYear || hasVer)) match = true;
                        }

                        if (match && displayName.IndexOf("Tool Hub", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            var uninstallString = subKey.GetValue("UninstallString")?.ToString();
                            if (!string.IsNullOrEmpty(uninstallString))
                            {
                                QBLog($"    ⚙ Found: {displayName}");
                                try {
                                    if (uninstallString.IndexOf("msiexec", StringComparison.OrdinalIgnoreCase) >= 0) {
                                        uninstallString = System.Text.RegularExpressions.Regex.Replace(uninstallString, "/[Ii]", "/X", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                        if (uninstallString.IndexOf("/passive", StringComparison.OrdinalIgnoreCase) < 0)
                                            uninstallString += " /passive /norestart";
                                    }
                                    QBLog($"    ▶ Executing sequence...");
                                    var pInfo = new ProcessStartInfo("cmd.exe", $"/c {uninstallString}") {
                                        CreateNoWindow = true,
                                        UseShellExecute = false
                                    };
                                    using (var p = Process.Start(pInfo)) {
                                        p.WaitForExit();
                                        QBLog($"    ✔ Exit Code: {p.ExitCode}");
                                        if (p.ExitCode == 0 || p.ExitCode == 1641 || p.ExitCode == 3010) uninstalledCount++;
                                    }
                                } catch (Exception ex) {
                                    QBLog($"    ⚠ Failed to uninstall {displayName}: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            int renamed = 0;
            QBLog("📂 Discovering installation folders...");
            
            var pathsToRename = new System.Collections.Generic.List<string>();

            // Map Year to Version strings (Pro/Premier: Year-1990, Enterprise: Year-2000)
            var verStrings = new System.Collections.Generic.List<string>();
            if (int.TryParse(year, out int yInt)) {
                verStrings.Add((yInt - 1990).ToString() + ".0");
                verStrings.Add((yInt - 2000).ToString() + ".0");
            }

            // Detect multiple versions to avoid breaking shared folders
            var installBasePaths = new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            var distinctVersionsFound = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var bp in installBasePaths) {
                string iPath = Path.Combine(bp, "Intuit");
                if (Directory.Exists(iPath)) {
                    foreach (var d in Directory.GetDirectories(iPath)) {
                        string dName = Path.GetFileName(d);
                        if (dName.IndexOf("QuickBooks", StringComparison.OrdinalIgnoreCase) >= 0) {
                            // Try to extract a year-like string (2018-2026)
                            var match = System.Text.RegularExpressions.Regex.Match(dName, @"(20\d{2})");
                            if (match.Success) distinctVersionsFound.Add(match.Value);
                            else distinctVersionsFound.Add(dName); // Fallback to full name if no year found
                        }
                    }
                }
            }
            bool isMultipleVersions = (year == "Unknown/All") ? false : (distinctVersionsFound.Count > 1);
            if (isMultipleVersions) QBLog($"    ℹ Multiple versions detected ({string.Join(", ", distinctVersionsFound)}). Selective cleanup enabled.");

            // Universal Base Paths
            var basePathsList = new System.Collections.Generic.List<string>();
            try {
                string progData = Environment.GetEnvironmentVariable("ProgramData");
                if (!string.IsNullOrEmpty(progData)) basePathsList.Add(Path.Combine(progData, "Intuit"));
                basePathsList.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Intuit"));
                basePathsList.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Intuit"));
                basePathsList.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Intuit"));
                basePathsList.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Intuit"));
                
                // Common Files folders - These are the ones we treat conditionally
                string commonFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
                if (!string.IsNullOrEmpty(commonFiles)) {
                    string intuitCommon = Path.Combine(commonFiles, "Intuit");
                    if (Directory.Exists(intuitCommon)) {
                        if (!isMultipleVersions) pathsToRename.Add(intuitCommon); // Rename whole Intuit folder
                        else basePathsList.Add(intuitCommon); // Scan inside Intuit folder
                    }
                }
                string commonFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86);
                if (!string.IsNullOrEmpty(commonFilesX86)) {
                    string intuitCommonX86 = Path.Combine(commonFilesX86, "Intuit");
                    if (Directory.Exists(intuitCommonX86)) {
                        if (!isMultipleVersions) pathsToRename.Add(intuitCommonX86); // Rename whole Intuit folder
                        else basePathsList.Add(intuitCommonX86); // Scan inside Intuit folder
                    }
                }

                // ProgramData\Common Files
                if (!string.IsNullOrEmpty(progData)) {
                    string pdCommon = Path.Combine(progData, @"Common Files\Intuit");
                    if (Directory.Exists(pdCommon)) {
                        if (!isMultipleVersions) pathsToRename.Add(pdCommon);
                        else basePathsList.Add(pdCommon);
                    }
                }
            } catch (Exception ex) { QBLog($"    ⚠ Error mapping base paths: {ex.Message}"); }

            foreach (var bp in basePathsList) {
                if (!Directory.Exists(bp)) continue;
                QBLog($"    🔍 Scanning: {bp}");
                try {
                    var dirs = Directory.GetDirectories(bp);
                    foreach (var dir in dirs) {
                        string name = Path.GetFileName(dir);
                        bool match = false;
                        if (year == "Unknown/All") {
                            if (name.IndexOf("QuickBooks", StringComparison.OrdinalIgnoreCase) >= 0) match = true;
                        } else {
                            bool hasQB = name.IndexOf("QuickBooks", StringComparison.OrdinalIgnoreCase) >= 0;
                            bool hasYear = name.IndexOf(year, StringComparison.OrdinalIgnoreCase) >= 0;
                            bool hasVer = verStrings.Exists(v => name.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0);
                            
                            if (hasQB && (hasYear || hasVer)) match = true;
                        }

                        if (match && !name.EndsWith(".old", StringComparison.OrdinalIgnoreCase) && name.IndexOf("Tool Hub", StringComparison.OrdinalIgnoreCase) < 0) {
                            QBLog($"    ⭐ Found: {name}");
                            pathsToRename.Add(dir);
                        }
                    }
                } catch (Exception ex) { QBLog($"    ⚠ Error scanning {bp}: {ex.Message}"); }
            }

            if (pathsToRename.Count == 0) {
                QBLog("    ⚪ No matching folders discovered to rename.");
            }

            foreach (var target in pathsToRename) {
                try {
                    if (Directory.Exists(target)) {
                        string oldPath = target + ".old";
                        if (Directory.Exists(oldPath)) {
                            QBLog($"    🗑 Removing previous backup: {Path.GetFileName(oldPath)}");
                            try { Directory.Delete(oldPath, true); } catch { }
                        }

                        QBLog($"    🔄 Renaming: {Path.GetFileName(target)} -> {Path.GetFileName(oldPath)}");
                        
                        bool success = false;
                        for (int i = 0; i < 3; i++) {
                            try {
                                Directory.Move(target, oldPath);
                                success = true; break;
                            } catch { System.Threading.Thread.Sleep(1000); }
                        }

                        if (success) renamed++;
                        else QBLog($"    ❌ Failed to rename {Path.GetFileName(target)} (Access Denied)");
                    }
                } catch (Exception ex) { 
                    QBLog($"    ⚠ Error renaming {target}: {ex.Message}");
                }
            }

            QBLog("─────────────────────────────────────────────");
            QBLog($"✔ Preparation Complete. Renamed {renamed} folders to .old");
            _qbStatus.Text = $"● DONE (Renamed {renamed})";
            _qbStatus.ForeColor = SuccessGrn;
            
            var result = MessageBox.Show(
                $"Step 1: Folder renaming complete ({renamed} folders renamed).\n\nWould you like to automatically download and reinstall QuickBooks {year} now?",
                "Clean Prep Done", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            // Collect license info
            string licenseNum = InputDialog.Show("LICENSE NUMBER", "Enter your QuickBooks License Number (e.g. 1234-5678-9012-3456):");
            if (string.IsNullOrWhiteSpace(licenseNum)) return;
            string productCode = InputDialog.Show("PRODUCT CODE", "Enter your QuickBooks Product Code (e.g. 123-456):");
            if (string.IsNullOrWhiteSpace(productCode)) return;

            LaunchQBDownloader(licenseNum, productCode);
        }

        private void LaunchQBDownloader(string licenseNum, string productCode)
        {
            string edition = _qbTypeCombo.SelectedItem?.ToString() ?? "Pro";
            string year    = _qbYearCombo.SelectedItem?.ToString() ?? "2024";

            // Map edition + year to QB download page product label
            string productLabel = "";
            if (edition.Contains("Enterprise")) {
                if (edition == "QuickBooks Desktop Enterprise" || edition == "QuickBooks Desktop Enterprise General Business") productLabel = $"QuickBooks Desktop Enterprise {year}";
                else productLabel = $"{edition} {year}";
            }
            else if (edition == "Mac") productLabel = $"QuickBooks Desktop Mac Plus {year}";
            else if (edition == "QuickBooks Desktop Pro") productLabel = $"QuickBooks Desktop Pro Plus {year}";
            else if (edition == "QuickBooks Desktop Premier") productLabel = $"QuickBooks Desktop Premier Plus {year}";
            else if (edition == "QuickBooks Desktop Accountant") productLabel = $"QuickBooks Desktop Premier Accountant Plus {year}";
            else productLabel = $"{edition} Plus {year}";

            var dlg = new Form {
                Text = "QuickBooks Downloader",
                Size = new Size(1200, 800),
                StartPosition = FormStartPosition.CenterScreen,
                BackColor = Color.White
            };

            var statusBar = new Label {
                Dock = DockStyle.Bottom, Height = 32, BackColor = Color.FromArgb(243, 244, 246),
                ForeColor = Color.FromArgb(17, 24, 39), Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0),
                Text = "⏳ Loading download page..."
            };

            var btnRunInstaller = new Button {
                Text = "▶ Run Installer", Dock = DockStyle.Bottom, Height = 40,
                BackColor = Color.FromArgb(22, 163, 74), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Enabled = false
            };
            btnRunInstaller.FlatAppearance.BorderSize = 0;

            var licensePanel = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Color.FromArgb(254, 243, 199) };
            licensePanel.Controls.Add(new Label {
                Text = $"📋  License: {licenseNum}     Product Code: {productCode}     — Keep this visible while installing",
                Font = new Font("Segoe UI Semibold", 9f), ForeColor = Color.FromArgb(120, 53, 15),
                AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter
            });

            string downloadedFile = "";

            var wv = new Microsoft.Web.WebView2.WinForms.WebView2 { Dock = DockStyle.Fill };
            dlg.Controls.Add(wv);
            dlg.Controls.Add(btnRunInstaller);
            dlg.Controls.Add(statusBar);
            dlg.Controls.Add(licensePanel);

            btnRunInstaller.Click += (s, e) => {
                if (!string.IsNullOrEmpty(downloadedFile) && File.Exists(downloadedFile)) {
                    try { Process.Start(downloadedFile); }
                    catch (Exception ex) { MessageBox.Show("Could not launch installer: " + ex.Message); }
                } else {
                    // Let user browse for it
                    using (var ofd = new OpenFileDialog { Title = "Select the QuickBooks Installer", Filter = "Executables|*.exe" }) {
                        if (ofd.ShowDialog() == DialogResult.OK)
                            try { Process.Start(ofd.FileName); } catch { }
                    }
                }
            };

            dlg.Load += async (s, e) => {
                await wv.EnsureCoreWebView2Async();

                // Monitor downloads
                wv.CoreWebView2.DownloadStarting += (ds, de) => {
                    downloadedFile = de.DownloadOperation.ResultFilePath;
                    de.DownloadOperation.StateChanged += (op, oe) => {
                        if (de.DownloadOperation.State == Microsoft.Web.WebView2.Core.CoreWebView2DownloadState.Completed) {
                            dlg.Invoke((Action)(() => {
                                statusBar.Text = $"✅ Download complete: {Path.GetFileName(downloadedFile)}";
                                statusBar.ForeColor = Color.FromArgb(22, 163, 74);
                                btnRunInstaller.Enabled = true;
                                btnRunInstaller.PerformClick();
                            }));
                        }
                    };
                    dlg.Invoke((Action)(() => statusBar.Text = $"⬇ Downloading: {Path.GetFileName(de.DownloadOperation.ResultFilePath)}..."));
                };

                wv.NavigationCompleted += async (ns, ne) => {
                    dlg.Invoke((Action)(() => statusBar.Text = "🔧 Selecting country and product..."));
                    await System.Threading.Tasks.Task.Delay(2500); // Wait for React to render

                    // Build a safe product search string (year + type)
                    string searchLabel = "QuickBooks Desktop Pro";
                    if (edition.Contains("Enterprise")) {
                        searchLabel = "QuickBooks Desktop Enterprise";
                    }
                    else if (edition == "Mac") searchLabel = "QuickBooks Mac Desktop";
                    else if (edition == "QuickBooks Desktop Premier") searchLabel = "QuickBooks Desktop Premier";
                    else if (edition == "QuickBooks Desktop Pro") searchLabel = "QuickBooks Desktop Pro";
                    else if (edition.Contains("Accountant")) searchLabel = "QuickBooks Desktop Accountant";
                    else if (edition.Contains("Contractor")) searchLabel = "QuickBooks Desktop Premier"; // Contractor is usually a sub-edition of Premier
                    else if (edition.Contains("Manufacturing")) searchLabel = "QuickBooks Desktop Premier";
                    else if (edition.Contains("Retail")) searchLabel = "QuickBooks Desktop Premier";
                    else if (edition.Contains("Nonprofit")) searchLabel = "QuickBooks Desktop Premier";
                    else if (edition.Contains("Professional")) searchLabel = "QuickBooks Desktop Premier";

                    string js = $@"
(async () => {{
    try {{
        const wait = (ms) => new Promise(r => setTimeout(r, ms));
        
        // Final Page Check: If we are on the 'Your download should start' page
        const finalLinks = document.querySelectorAll('a, button');
        for (const l of finalLinks) {{
            const text = l.innerText.toLowerCase();
            if (text.includes('try again') || text.includes('download now')) {{
                l.click();
                return 'FINAL_MANUAL_DOWNLOAD_TRIGGERED';
            }}
        }}

        // 1. Select Country (United States)
        const countryInput = document.getElementById('idsDropdownTextField3');
        if (countryInput && !countryInput.value.includes('United States')) {{
            countryInput.value = 'United States (US)';
            countryInput.dispatchEvent(new Event('input', {{bubbles:true}}));
            countryInput.dispatchEvent(new Event('change', {{bubbles:true}}));
            countryInput.dispatchEvent(new Event('blur', {{bubbles:true}}));
            await wait(1000);
        }}

        // 2. Select Product
        const productInput = document.getElementById('idsDropdownTextField6');
        if (productInput && !productInput.value.includes('{searchLabel}')) {{
            productInput.value = '{searchLabel}';
            productInput.dispatchEvent(new Event('input', {{bubbles:true}}));
            productInput.dispatchEvent(new Event('change', {{bubbles:true}}));
            productInput.dispatchEvent(new Event('blur', {{bubbles:true}}));
            await wait(1000);
        }}

        // 3. Select Version (Year)
        const versionInput = document.getElementById('idsDropdownTextField9');
        if (versionInput && !versionInput.value.includes('{year}')) {{
            versionInput.value = '{year}';
            versionInput.dispatchEvent(new Event('input', {{bubbles:true}}));
            versionInput.dispatchEvent(new Event('change', {{bubbles:true}}));
            versionInput.dispatchEvent(new Event('blur', {{bubbles:true}}));
            await wait(1000);
        }}

        // 4. Click Search if Download doesn't exist yet
        const searchBtn = document.getElementById('Search');
        const downloadBtn = document.getElementById('defaultdownlod2021plus') || 
                            Array.from(document.querySelectorAll('a,button')).find(el => el.innerText.toLowerCase().includes('download'));

        if (downloadBtn) {{
            downloadBtn.click();
            return 'DOWNLOAD_CLICKED';
        }} else if (searchBtn) {{
            searchBtn.click();
            return 'SEARCH_CLICKED';
        }}

        return 'IDLE_OR_LOADING';
    }} catch(ex) {{ return 'ERR: ' + ex.message; }}
}})();";

                    var jsResult = await wv.CoreWebView2.ExecuteScriptAsync(js);
                    dlg.Invoke((Action)(() => statusBar.Text = $"✅ Auto-selection applied ({jsResult}). Waiting for download..."));
                };

                wv.CoreWebView2.Navigate("https://downloads.quickbooks.com/app/qbdt/products");
            };

            dlg.Show();
        }


        #endregion
    }



    // ─────────────────────────────────────────────────────────────────────────
    //  P/INVOKE — progress-bar color (retained for compatibility)
    // ─────────────────────────────────────────────────────────────────────────
    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public const int GWL_STYLE = -16;
        public const int WS_CAPTION = 0x00C00000;
        public const int WS_THICKFRAME = 0x00040000;
        public const int WS_CHILD = 0x40000000;
        public const int WS_VISIBLE = 0x10000000;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public void Init() { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern ulong GetTickCount64();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool EnumThreadWindows(uint dwThreadId, EnumWindowsProc lpfn, IntPtr lParam);

        public const int SW_SHOW = 5;
        public const int SW_HIDE = 0;
        public const int SW_MAXIMIZE = 3;
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const int WS_CLIPCHILDREN = 0x02000000;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }

    // =====================================================================
    //  PAGE 5 — JARVIS 2.0 INTEGRATION
    // =====================================================================
    public partial class MainForm
    {
        private static string JarvisRuntimePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SystemMonitor", "Jarvis_v2");
        private static string JarvisPath = System.IO.Path.Combine(JarvisRuntimePath, "Jarvis 2.0.exe");
        private Panel _jarvisHost;
        private IntPtr _jarvisHwnd = IntPtr.Zero;

        protected override CreateParams CreateParams {
            get {
                CreateParams cp = base.CreateParams;
                cp.Style |= NativeMethods.WS_CLIPCHILDREN; // Avoid flickering and rendering gaps
                return cp;
            }
        }

        private Panel BuildJarvisPage()
        {
            var page = new Panel { Dock = DockStyle.Fill, BackColor = PrimaryBg };

            var lbl = new Label {
                Text = "⬢ JARVIS 2.0",
                Font = new Font("Segoe UI Semibold", 18),
                ForeColor = Color.FromArgb(17, 24, 39),
                AutoSize = true,
                Left = 20, Top = 20
            };

            var sub = new Label {
                Text = "Launch the Jarvis 2.0 console from C:\\Script-Do Not Delete\\Jarvis2.0",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = TextMuted,
                AutoSize = true,
                Left = 20, Top = 55
            };

            var btnLaunch = new Button {
                Text = "⚡  Launch Jarvis 2.0",
                Size = new Size(260, 50),
                Location = new Point(20, 100),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(43, 87, 248),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            btnLaunch.FlatAppearance.BorderSize = 0;
            btnLaunch.Click += (s, e) => {
                try {
                    string jarvisExe = @"C:\Script-Do Not Delete\Jarvis2.0\Jarvis 2.0.exe";
                    if (!File.Exists(jarvisExe)) {
                        MessageBox.Show("Jarvis 2.0 executable not found at:\n" + jarvisExe, "Jarvis Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    Process.Start(new ProcessStartInfo {
                        FileName = jarvisExe,
                        WorkingDirectory = Path.GetDirectoryName(jarvisExe),
                        UseShellExecute = false
                    });
                } catch (Exception ex) {
                    MessageBox.Show("Failed to launch Jarvis: " + ex.Message, "Jarvis Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            page.Controls.AddRange(new Control[] { lbl, sub, btnLaunch });
            return page;
        }
 
        // =====================================================================
        //  PAGE 6 — RECLAIM SERVER (BACKUP & EXFILTRATION)
        // =====================================================================
        private ListBox _reclaimLog;
        private ProgressBar _reclaimProgress;
        private Label _reclaimStatus;
        private string _reclaimBackupPath = "";

        private Panel BuildReclaimPage()
        {
            var page = new Panel { Dock = DockStyle.Fill, BackColor = PrimaryBg, Padding = new Padding(24) };
            var top  = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = Color.White };
            top.Paint += (s, e) => { using (var p = new Pen(Color.FromArgb(229, 231, 235))) e.Graphics.DrawLine(p, 0, top.Height - 1, top.Width, top.Height - 1); };

            var lblH = FL("RECLAIM SERVER", new Point(10, 20), 16f, FontStyle.Bold, TextPrimary);
            _reclaimStatus = FL("● READY FOR SCAN", new Point(12, 53), 9f, FontStyle.Bold, SuccessGrn);

            var rightGroup = new Panel { Dock = DockStyle.Right, Width = 480 };
            var btnDedicated = new Button {
                Text = "🖥️  Dedicated Server",
                Width = 190, Left = 60, Top = 22, Height = 48,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White,
                Cursor = Cursors.Hand, Font = new Font("Segoe UI Semibold", 10f)
            };
            btnDedicated.FlatAppearance.BorderSize = 0;
            btnDedicated.Click += async (s, e) => {
                top.Enabled = false;
                await RunReclaimBackup(true); 
                top.Enabled = true;
            };

            var btnShared = new Button {
                Text = "👥  Shared Server",
                Width = 190, Left = 265, Top = 22, Height = 48,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(59, 130, 246), ForeColor = Color.White,
                Cursor = Cursors.Hand, Font = new Font("Segoe UI Semibold", 10f)
            };
            btnShared.FlatAppearance.BorderSize = 0;
            btnShared.Click += async (s, e) => {
                top.Enabled = false;
                await RunSharedServerReclaim(); 
                top.Enabled = true;
            };

            rightGroup.Controls.AddRange(new Control[] { btnDedicated, btnShared });
            top.Controls.Add(rightGroup);
            top.Controls.Add(lblH);
            top.Controls.Add(_reclaimStatus);

            _reclaimLog = new ListBox {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(17, 24, 39),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                ItemHeight = 20
            };

            _reclaimProgress = new ProgressBar {
                Dock = DockStyle.Bottom,
                Height = 8,
                Style = ProgressBarStyle.Continuous,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(43, 87, 248)
            };

            page.Controls.Add(_reclaimLog);
            page.Controls.Add(_reclaimProgress);
            page.Controls.Add(top);

            return page;
        }

        private async Task RunReclaimBackup(bool directUpload)
        {
            _reclaimLog.Items.Clear();
            _reclaimProgress.Value = 0;
            _reclaimStatus.Text = "● SCANNING FILES...";
            _reclaimStatus.ForeColor = Color.Gold;

            var targets = new List<string>();
            string[] usersToExclude = { "QBdataservices", "rtdsprabhu", "rtcsprabhu", "rtcsadprabhu", "support", "supportrtcs", "supportrtcs05", "Public", "Default", "Default User", ".NET profile" };

            // 1. Scan C:\Users
            try {
                var userDirs = Directory.GetDirectories(@"C:\Users");
                foreach (var userDir in userDirs) {
                    string name = Path.GetFileName(userDir);
                    bool shouldExclude = false;
                    foreach (var exc in usersToExclude) {
                        if (name.StartsWith(exc, StringComparison.OrdinalIgnoreCase)) {
                            shouldExclude = true; break;
                        }
                    }
                    if (shouldExclude) continue;
                    if (name.EndsWith(".old", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_old", StringComparison.OrdinalIgnoreCase)) continue;

                    string[] profiles = { "Desktop", "Documents", "Downloads" };
                    foreach (var p in profiles) {
                        string path = Path.Combine(userDir, p);
                        if (Directory.Exists(path)) targets.Add(path);
                    }
                }
            } catch { }

            // 2. Scan D:\
            if (Directory.Exists(@"D:\")) targets.Add(@"D:\");

            LogReclaim($"Discovered {targets.Count} root targets. Starting consolidation...");
            
            // 3. Perform Backup
            _reclaimStatus.Text = "● PACKAGING DATA...";
            string tempFolder = Directory.Exists(@"D:\") ? @"D:\Reclaim_Buffer" : Path.Combine(Path.GetTempPath(), "Reclaim_Buffer");
            if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);
            
            string zipName = $"Reclaim_Backup_{DateTime.Now:yyyyMMdd_HHmm}.zip";
            string zipPath = Path.Combine(tempFolder, zipName);
            _reclaimBackupPath = zipPath;

            try {
                // Pre-scan: collect all files and total size for accurate progress
                _reclaimStatus.Text = "● SCANNING FILES (calculating size)...";
                Application.DoEvents();
                var allFiles = new List<(string filePath, string entryPath)>();
                long totalBytes = 0;

                foreach (var target in targets) {
                    try {
                        string drive = Path.GetPathRoot(target);
                        string baseEntry = drive.StartsWith("D:", StringComparison.OrdinalIgnoreCase) 
                            ? "D_Drive" 
                            : target.Replace(drive, "").TrimStart(Path.DirectorySeparatorChar);
                        CollectFiles(target, baseEntry, allFiles, ref totalBytes);
                    } catch { }
                }

                LogReclaim($"Pre-scan complete: {allFiles.Count} files, {HumanSize(totalBytes)} total.");

                long processedBytes = 0;
                int processedFiles = 0;

                await Task.Run(() => {
                    using (var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create)) {
                        foreach (var (filePath, entryPath) in allFiles) {
                            try {
                                long fSize = 0;
                                try { fSize = new FileInfo(filePath).Length; } catch { continue; }

                                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                                var level = IsPreCompressed(ext)
                                    ? System.IO.Compression.CompressionLevel.NoCompression
                                    : System.IO.Compression.CompressionLevel.Fastest;
                                
                                archive.CreateEntryFromFile(filePath, entryPath, level);

                                processedBytes += fSize;
                                processedFiles++;

                                // Update UI every 200 files (non-blocking)
                                if (processedFiles % 200 == 0) {
                                    int pct = totalBytes > 0 ? (int)((double)processedBytes / totalBytes * 100) : 0;
                                    int pf = processedFiles; long pb = processedBytes;
                                    this.BeginInvoke((Action)(() => {
                                        _reclaimProgress.Value = Math.Min(pct, 100);
                                        _reclaimStatus.Text = $"● PACKAGING: {pf}/{allFiles.Count} files ({HumanSize(pb)}/{HumanSize(totalBytes)})";
                                    }));
                                }
                            } catch { }
                        }
                    }
                });

                _reclaimBackupPath = zipPath;

                _reclaimStatus.Text = "● BACKUP COMPLETE";
                _reclaimStatus.ForeColor = Color.Lime;
                LogReclaim("--------------------------------------------------");
                LogReclaim($"SUCCESS: Backup stored at {zipPath}");
                LogReclaim("ACTION: Opening WeTransfer and Local Folder...");

                // 4. Unified Email — Auto-fill
                string email = "wetransfer@rtdsportal.zohodesk.in";
                LogReclaim($"AUTO-EMAIL: Using {email} for WeTransfer.");

                // 5. Exfiltration
                if (directUpload) {
                    _reclaimStatus.Text = "● PREPARING PORTAL...";
                    _reclaimStatus.ForeColor = Color.Gold;
                    LogReclaim("ACTION: Launching Automated WeTransfer Portal...");
                    
                    var automator = new WeTransferAutomator(zipPath, email, email);
                    automator.AutomationFinished += (ss, ee) => {
                        this.Invoke((Action)(() => {
                            _reclaimStatus.Text = "● READY: Close WeTransfer window when done to clean up.";
                            _reclaimStatus.ForeColor = Color.FromArgb(16, 185, 129);
                            LogReclaim("SUCCESS: File and email auto-attached. Click TRANSFER in the portal to complete.");
                            LogReclaim("IMPORTANT: Close the automated window manually when transfer finishes to trigger cleanup.");
                        }));
                    };

                    bool promptShown = false;
                    automator.TransferCompleted += (ss, ee) => {
                        this.Invoke((Action)(() => {
                            if (promptShown) return; promptShown = true;
                            _reclaimStatus.Text = "● TRANSFER COMPLETE";
                            LogReclaim("SUCCESS: WeTransfer portal indicates transfer is complete.");
                            var dr = MessageBox.Show("Transfer complete! Do you want to clean up (delete) the temporary Reclaim ZIP buffer?", "Cleanup", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (dr == DialogResult.Yes) {
                                try { Directory.Delete(tempFolder, true); } catch { }
                                LogReclaim("CLEANUP: Secure buffer removed.");
                                _reclaimStatus.Text = "● RECLAIM COMPLETE";
                            } else {
                                LogReclaim("CLEANUP: Buffer kept by user.");
                                _reclaimStatus.Text = "● RECLAIM COMPLETE (KEPT)";
                            }
                        }));
                    };

                    automator.FormClosing += (ss, ee) => {
                        this.Invoke((Action)(() => {
                            if (promptShown) return; promptShown = true;
                            LogReclaim("PORTAL: User closed the automated window.");
                            var dr = MessageBox.Show("WeTransfer portal closed. Do you want to clean up (delete) the temporary Reclaim ZIP buffer?", "Cleanup", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (dr == DialogResult.Yes) {
                                try { Directory.Delete(tempFolder, true); } catch { }
                                LogReclaim("CLEANUP: Secure buffer removed.");
                                _reclaimStatus.Text = "● RECLAIM COMPLETE";
                            } else {
                                LogReclaim("CLEANUP: Buffer kept by user.");
                                _reclaimStatus.Text = "● RECLAIM COMPLETE (KEPT)";
                            }
                        }));
                    };

                    automator.Show(); 
                    return; 
                } else {
                    // Managed Hand-off
                    Process.Start($"https://acecloudhosting.wetransfer.com/?to={Uri.EscapeDataString(email)}");
                    Process.Start("explorer.exe", $"/select,\"{zipPath}\"");
                }

            } catch (Exception ex) {
                _reclaimStatus.Text = "● FAILED";
                _reclaimStatus.ForeColor = Color.Red;
                LogReclaim($"ERROR: {ex.Message}");
            }
        }

        private async Task RunSharedServerReclaim()
        {
            _reclaimLog.Items.Clear();
            _reclaimStatus.Text = "● SCANNING SHARED...";
            _reclaimStatus.ForeColor = Color.Gold;

            var targets = new List<string>();

            // 1. D:\Profiles Interactive Search Loop
            try {
                if (Directory.Exists(@"D:\Profiles")) {
                    bool addAnother = true;
                    while (addAnother) {
                        string query = InputDialog.Show("USERNAME SEARCH", "Enter the exact or partial username to search in D:\\Profiles:");
                        if (string.IsNullOrWhiteSpace(query)) break;

                        var found = Directory.GetDirectories(@"D:\Profiles")
                            .Where(d => !d.EndsWith(".old", StringComparison.OrdinalIgnoreCase) && !d.EndsWith("_old", StringComparison.OrdinalIgnoreCase))
                            .Where(d => Path.GetFileName(d).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                            .ToList();

                        if (found.Count == 0) {
                            MessageBox.Show($"No profiles found matching '{query}' (excluding .old/_old).", "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        } else {
                            foreach (var f in found) {
                                var res = MessageBox.Show($"Found profile: {Path.GetFileName(f)}\n\nAttach to backup?", "Confirm Match", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                if (res == DialogResult.Yes) {
                                    if (!targets.Contains(f)) {
                                        targets.Add(f);
                                        LogReclaim($"ADDED: Profile {Path.GetFileName(f)}");
                                    }
                                }
                            }
                        }

                        var loopRes = MessageBox.Show("Do you want to search and add another user?", "Continue Search", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        addAnother = (loopRes == DialogResult.Yes);
                    }
                }
            } catch (Exception ex) { LogReclaim($"SEARCH ERROR: {ex.Message}"); }

            // 2. D:\Client Data Business Search
            string bizName = InputDialog.Show("BUSINESS NAME", "Enter the business name to search in D:\\Client Data:");
            if (!string.IsNullOrWhiteSpace(bizName)) {
                try {
                    if (Directory.Exists(@"D:\Client Data")) {
                        var clientDirs = Directory.GetDirectories(@"D:\Client Data");
                        var matches = clientDirs.Where(d => Path.GetFileName(d).IndexOf(bizName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                        
                        if (matches.Count == 0) {
                             LogReclaim($"WARNING: No folders matching '{bizName}' found in D:\\Client Data.");
                        } else {
                            foreach(var m in matches) targets.Add(m);
                            LogReclaim($"SUCCESS: Found {matches.Count} client folders for '{bizName}'.");
                        }
                    }
                } catch { }
            }

            if (targets.Count == 0) {
                LogReclaim("ABORTED: No profiles or client data selected.");
                _reclaimStatus.Text = "● IDLE";
                return;
            }

            // 3. Email — Auto-fill
            string email = "wetransfer@rtdsportal.zohodesk.in";
            LogReclaim($"AUTO-EMAIL: Using {email} for WeTransfer.");

            // 4. Packaging (chunked for large data)
            _reclaimStatus.Text = "● SCANNING SHARED FILES...";
            Application.DoEvents();
            string tempFolder = Directory.Exists(@"D:\") ? @"D:\Reclaim_Buffer" : Path.Combine(Path.GetTempPath(), "Reclaim_Buffer");
            if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);

            var allFiles = new List<(string filePath, string entryPath)>();
            long totalBytes = 0;
            foreach (var t in targets) {
                try { CollectFiles(t, Path.GetFileName(t), allFiles, ref totalBytes); } catch { }
            }
            LogReclaim($"Pre-scan complete: {allFiles.Count} files, {HumanSize(totalBytes)} total.");

            string zipPath = Path.Combine(tempFolder, $"Shared_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

            try {
                await Task.Run(() => {
                    long processedBytes = 0; int processedFiles = 0;
                    using (var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create)) {
                        foreach (var (filePath, entryPath) in allFiles) {
                            try {
                                long fSize = 0;
                                try { fSize = new FileInfo(filePath).Length; } catch { continue; }

                                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                                var level = IsPreCompressed(ext)
                                    ? System.IO.Compression.CompressionLevel.NoCompression
                                    : System.IO.Compression.CompressionLevel.Fastest;
                                
                                archive.CreateEntryFromFile(filePath, entryPath, level);

                                processedBytes += fSize;
                                processedFiles++;

                                if (processedFiles % 200 == 0) {
                                    int pct = totalBytes > 0 ? (int)((double)processedBytes / totalBytes * 100) : 0;
                                    int pf = processedFiles; long pb = processedBytes;
                                    this.BeginInvoke((Action)(() => {
                                        _reclaimProgress.Value = Math.Min(pct, 100);
                                        _reclaimStatus.Text = $"● PACKAGING: {pf}/{allFiles.Count} files ({HumanSize(pb)}/{HumanSize(totalBytes)})";
                                    }));
                                }
                            } catch { }
                        }
                    }
                });

                // 5. Automation Bridge
                var automator = new WeTransferAutomator(zipPath, email, email);
                automator.AutomationFinished += (ss, ee) => {
                    this.Invoke((Action)(() => {
                        _reclaimStatus.Text = "● PORTAL READY";
                        _reclaimStatus.ForeColor = Color.Lime;
                        MessageBox.Show("Shared Server Portal Ready!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        try { Directory.Delete(tempFolder, true); } catch { }
                        _reclaimStatus.Text = "● RECLAIM COMPLETE";
                    }));
                };
                automator.Show();
            } catch (Exception ex) {
                LogReclaim($"ERROR: {ex.Message}");
            }
        }


        private void AddFolderToArchive(System.IO.Compression.ZipArchive archive, string sourceDir, string entryPrefix, Action<string> onFileAdded)
        {
            // File extensions that are already compressed — store without re-compressing
            var storeExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                ".zip", ".rar", ".7z", ".gz", ".tar", ".iso", ".msi", ".cab",
                ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".avi", ".mkv", ".mp3",
                ".bak", ".qbb", ".qbw"
            };

            try {
                // 1. Add files in current directory (streaming enumeration)
                foreach (var file in Directory.EnumerateFiles(sourceDir)) {
                    try {
                        string name = Path.GetFileName(file);
                        string ext = Path.GetExtension(file);
                        var level = storeExts.Contains(ext) 
                            ? System.IO.Compression.CompressionLevel.NoCompression 
                            : System.IO.Compression.CompressionLevel.Fastest;
                        archive.CreateEntryFromFile(file, Path.Combine(entryPrefix, name), level);
                        onFileAdded?.Invoke(file);
                    } catch { }
                }

                // 2. Recurse into subdirectories (streaming enumeration)
                foreach (var dir in Directory.EnumerateDirectories(sourceDir)) {
                    try {
                        string name = Path.GetFileName(dir);
                        
                        // Skip protected system folders and legacy suffixes
                        if (name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)) continue;
                        if (name.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase)) continue;
                        if (name.Equals("Config.Msi", StringComparison.OrdinalIgnoreCase)) continue;
                        if (name.EndsWith(".old", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_old", StringComparison.OrdinalIgnoreCase)) continue;
                        
                        // Strict specific exemptions for telemetry/admin user accounts globally
                        if (name.StartsWith("rtcsprabhu", StringComparison.OrdinalIgnoreCase) || 
                            name.StartsWith("rtdsprabhu", StringComparison.OrdinalIgnoreCase) || 
                            name.StartsWith("rtcsadprabhu", StringComparison.OrdinalIgnoreCase) || 
                            name.StartsWith("supportrtcs", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith("support", StringComparison.OrdinalIgnoreCase)) continue;

                        string nextPrefix = Path.Combine(entryPrefix, name);
                        AddFolderToArchive(archive, dir, nextPrefix, onFileAdded);
                    } catch { }
                }
            } catch { }
        }

        // Recursive pre-scan to collect all files and sizes (respects same exclusions as AddFolderToArchive)
        private void CollectFiles(string sourceDir, string entryPrefix, List<(string filePath, string entryPath)> results, ref long totalBytes)
        {
            try {
                foreach (var file in Directory.EnumerateFiles(sourceDir)) {
                    try {
                        string name = Path.GetFileName(file);
                        results.Add((file, Path.Combine(entryPrefix, name)));
                        try { totalBytes += new FileInfo(file).Length; } catch { }
                    } catch { }
                }
                foreach (var dir in Directory.EnumerateDirectories(sourceDir)) {
                    try {
                        string name = Path.GetFileName(dir);
                        if (name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)) continue;
                        if (name.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase)) continue;
                        if (name.Equals("Config.Msi", StringComparison.OrdinalIgnoreCase)) continue;
                        if (name.EndsWith(".old", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_old", StringComparison.OrdinalIgnoreCase)) continue;
                        if (name.StartsWith("rtcsprabhu", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith("rtdsprabhu", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith("rtcsadprabhu", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith("supportrtcs", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith("support", StringComparison.OrdinalIgnoreCase)) continue;
                        CollectFiles(dir, Path.Combine(entryPrefix, name), results, ref totalBytes);
                    } catch { }
                }
            } catch { }
        }

        private static bool IsPreCompressed(string ext)
        {
            switch (ext) {
                case ".zip": case ".rar": case ".7z": case ".gz": case ".tar": case ".iso":
                case ".msi": case ".cab": case ".jpg": case ".jpeg": case ".png": case ".gif":
                case ".mp4": case ".avi": case ".mkv": case ".mp3": case ".bak": case ".qbb":
                case ".qbw": return true;
                default: return false;
            }
        }

        private void LogReclaim(string msg)
        {
            if (_reclaimLog.InvokeRequired) {
                _reclaimLog.BeginInvoke((Action)(() => LogReclaim(msg)));
                return;
            }
            _reclaimLog.Items.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
            _reclaimLog.SelectedIndex = _reclaimLog.Items.Count - 1;
        }

        private async Task<string> UploadToWeTransfer(string filePath) { return ""; } 

        private string CreateNumberedBackup(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string ext = Path.GetExtension(filePath);
            
            string backupPath = Path.Combine(dir, fileName + ".old");
            int counter = 1;
            while (File.Exists(backupPath)) {
                backupPath = Path.Combine(dir, fileName + ".old" + counter);
                counter++;
            }
            
            File.Copy(filePath, backupPath, true);
            return backupPath;
        }

        private Panel BuildQBNXTPage()
        {
            var p = new Panel { AutoScroll = true, BackColor = PrimaryBg, Padding = new Padding(20) };
            
            var header = FL("QBNXT LICENSE CONFIGURATOR", new Point(20, 20), 16f, FontStyle.Bold, TextPrimary);
            var sub = FL("Generate qbregistration.dat files and fetch ECML for specific clients.", new Point(20, 50), 9.5f, FontStyle.Regular, TextSecond);

            var cardClient = new CardPanel { Location = new Point(20, 90), Size = new Size(800, 140) };
            cardClient.Controls.Add(FL("CLIENT INFORMATION", new Point(16, 16), 8f, FontStyle.Bold, TextMuted));
            
            var lblName = FL("Client Company Name:", new Point(16, 45), 9f, FontStyle.Regular, TextPrimary);
            var txtName = new TextBox { Location = new Point(180, 42), Width = 300, Font = new Font(HF, 9f), BackColor = Color.White, ForeColor = TextPrimary };
            
            var lblType = FL("Client Type:", new Point(16, 85), 9f, FontStyle.Regular, TextPrimary);
            var rbNew = new RadioButton { Text = "New Client", Location = new Point(180, 83), AutoSize = true, Checked = true, Font = new Font(HF, 9f) };
            var rbExist = new RadioButton { Text = "Existing Client", Location = new Point(310, 83), AutoSize = true, Font = new Font(HF, 9f) };

            var lblPath = FL(@"Base Path: D:\Client Data", new Point(16, 115), 8.5f, FontStyle.Regular, TextMuted);

            cardClient.Controls.AddRange(new Control[] { lblName, txtName, lblType, rbNew, rbExist, lblPath });

            var cardEntries = new CardPanel { Location = new Point(20, 250), Size = new Size(800, 280) };
            cardEntries.Controls.Add(FL("QUICKBOOKS ENTRIES", new Point(16, 16), 8f, FontStyle.Bold, TextMuted));

            var lblYear = FL("Version Year:", new Point(16, 45), 9f, FontStyle.Regular, TextPrimary);
            var yearsData = new[] { 
                new { Year = "2024", Version = "34.0" },
                new { Year = "2023", Version = "33.0" },
                new { Year = "2022", Version = "32.0" },
                new { Year = "2021", Version = "31.0" },
                new { Year = "2020", Version = "30.0" }
            };
            var cbYear = new ComboBox { Location = new Point(150, 42), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font(HF, 9f), BackColor = Color.White };
            cbYear.Items.AddRange(yearsData.Select(y => y.Year).ToArray());
            cbYear.SelectedIndex = 0;

            var lblFlavor = FL("Flavor:", new Point(340, 45), 9f, FontStyle.Regular, TextPrimary);
            var flavorsData = new[] {
                new { Display = "QuickBooks Pro", Flavor = "pro" },
                new { Display = "QuickBooks Premier", Flavor = "superpro" },
                new { Display = "QuickBooks Premier Accountant", Flavor = "accountant" },
                new { Display = "QuickBooks Enterprise", Flavor = "bel" },
                new { Display = "QuickBooks Enterprise Accountant", Flavor = "belacct" },
                new { Display = "QuickBooks Enterprise Contractor", Flavor = "belcontractor" },
                new { Display = "QuickBooks Enterprise Manufacturing & Wholesale", Flavor = "belwholesale" },
                new { Display = "QuickBooks Enterprise NonProfit", Flavor = "belnonprofit" },
                new { Display = "QuickBooks Enterprise Professional Services", Flavor = "belprofessional" },
                new { Display = "QuickBooks Enterprise Retail", Flavor = "belretail" }
            };
            var cbFlavor = new ComboBox { Location = new Point(450, 42), Width = 300, DropDownWidth = 420, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font(HF, 9f), BackColor = Color.White };
            cbFlavor.Items.AddRange(flavorsData.Select(f => f.Display).ToArray());
            cbFlavor.SelectedIndex = 0;

            var lblPid = FL("Product ID:", new Point(16, 85), 9f, FontStyle.Regular, TextPrimary);
            var txtPid = new TextBox { Location = new Point(150, 82), Width = 150, Font = new Font(HF, 9f), ForeColor = TextMuted, Text = "Format: XXX-XXX", BackColor = Color.White };
            txtPid.GotFocus += (s, e) => { if (txtPid.Text == "Format: XXX-XXX") { txtPid.Text = ""; txtPid.ForeColor = TextPrimary; } };
            txtPid.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtPid.Text)) { txtPid.Text = "Format: XXX-XXX"; txtPid.ForeColor = TextMuted; } };

            var lblLic = FL("License Number:", new Point(340, 85), 9f, FontStyle.Regular, TextPrimary);
            var txtLic = new TextBox { Location = new Point(450, 82), Width = 300, Font = new Font(HF, 9f), ForeColor = TextMuted, Text = "Format: XXXX-XXXX-XXXX-XXX", BackColor = Color.White };
            txtLic.GotFocus += (s, e) => { if (txtLic.Text == "Format: XXXX-XXXX-XXXX-XXX") { txtLic.Text = ""; txtLic.ForeColor = TextPrimary; } };
            txtLic.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtLic.Text)) { txtLic.Text = "Format: XXXX-XXXX-XXXX-XXX"; txtLic.ForeColor = TextMuted; } };

            var btnAdd = MkBtn("Add Entry", new Point(150, 115), AccentBlue);
            btnAdd.Width = 120;
            
            var lvEntries = new ListView {
                Location = new Point(16, 155), Size = new Size(768, 115), View = View.Details, FullRowSelect = true, GridLines = true, BorderStyle = BorderStyle.FixedSingle, Font = new Font(HF, 9f)
            };
            lvEntries.Columns.Add("Year", 70);
            lvEntries.Columns.Add("Edition", 180);
            lvEntries.Columns.Add("Install ID", 120);
            lvEntries.Columns.Add("License Number", 260);
            cardEntries.Controls.AddRange(new Control[] { lblYear, cbYear, lblFlavor, cbFlavor, lblPid, txtPid, lblLic, txtLic, btnAdd, lvEntries });

            var btnRemove = MkBtn("Remove Entry", new Point(20, 540), AmberClr);
            btnRemove.Width = 140; btnRemove.Enabled = false;
            
            var btnGen = MkBtn("Generate File", new Point(570, 540), SuccessGrn);
            btnGen.Width = 250; btnGen.Enabled = false;

            lvEntries.SelectedIndexChanged += (s, e) => { btnRemove.Enabled = lvEntries.SelectedItems.Count > 0; };
            btnRemove.Click += (s, e) => {
                foreach (ListViewItem item in lvEntries.SelectedItems) lvEntries.Items.Remove(item);
                btnGen.Enabled = lvEntries.Items.Count > 0;
            };

            btnAdd.Click += (s, e) => {
                string pid = txtPid.Text.Trim();
                string lic = txtLic.Text.Trim();

                if (pid == "Format: XXX-XXX" || string.IsNullOrWhiteSpace(pid) || pid.Split('-').Length != 2) { 
                    MessageBox.Show("Invalid Product ID format. Please use XXX-XXX (e.g., 123-ABC).", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; 
                }
                if (lic == "Format: XXXX-XXXX-XXXX-XXX" || string.IsNullOrWhiteSpace(lic) || lic.Split('-').Length != 4) { 
                    MessageBox.Show("Invalid License Number format. Please use XXXX-XXXX-XXXX-XXX (e.g., 1234-ABCD-5678-EFG).", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; 
                }

                var lvi = new ListViewItem(cbYear.Text);
                lvi.SubItems.Add(cbFlavor.Text);
                lvi.SubItems.Add(pid);
                lvi.SubItems.Add(lic);
                lvi.Tag = new Tuple<string, string>(yearsData[cbYear.SelectedIndex].Version, flavorsData[cbFlavor.SelectedIndex].Flavor);
                lvEntries.Items.Add(lvi);

                txtPid.Text = "Format: XXX-XXX"; txtPid.ForeColor = TextMuted;
                txtLic.Text = "Format: XXXX-XXXX-XXXX-XXX"; txtLic.ForeColor = TextMuted;
                btnGen.Enabled = true;
            };

            btnGen.Click += async (s, e) => {
                string clientName = txtName.Text.Trim();
                if (string.IsNullOrWhiteSpace(clientName)) { MessageBox.Show("Please enter a client name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                
                string basePath = @"D:\Client Data";
                string clientPath = Path.Combine(basePath, clientName);
                string licPath = Path.Combine(clientPath, "QBLicense");

                if (rbNew.Checked && Directory.Exists(clientPath)) {
                    if (MessageBox.Show($"Client '{clientName}' already exists. Proceed anyway?", "Client Exists", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                } else if (rbExist.Checked && !Directory.Exists(clientPath)) {
                    if (MessageBox.Show($"Client '{clientName}' does not exist. Create as new client?", "Not Found", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                }

                try {
                    if (!Directory.Exists(clientPath)) Directory.CreateDirectory(clientPath);
                    if (!Directory.Exists(licPath)) Directory.CreateDirectory(licPath);

                    // Fetch ECML
                    string ecmlUrl = "https://acerepo.myrealdata.net/api/public/dl/nJfP0EEm";
                    string ecmlDest = Path.Combine(licPath, "EntitlementDataStore.ecml");
                    try {
                        using (var wc = new System.Net.WebClient()) { await wc.DownloadFileTaskAsync(ecmlUrl, ecmlDest); }
                    } catch {
                        MessageBox.Show("Failed to download EntitlementDataStore file. COPY THIS FILE manually to " + licPath, "Download Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    // XML Generation
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("<?xml version=\"1.0\"?>");
                    sb.AppendLine("<QBREG><QUICKBOOKSREGISTRATION>");
                    
                    var groups = lvEntries.Items.Cast<ListViewItem>().GroupBy(i => ((Tuple<string, string>)i.Tag).Item1);
                    foreach (var group in groups) {
                        sb.AppendLine($"\t\t<VERSION number=\"{group.Key}\">");
                        foreach (ListViewItem item in group) {
                            var t = (Tuple<string, string>)item.Tag;
                            string flav = t.Item2;
                            string entFields = ""; string actProd = "";
                            if (flav == "bel" || flav == "belacct" || flav.StartsWith("bel")) {
                                entFields = "<PPRA></PPRA><NFVN></NFVN><NFEV></NFEV><NFLN></NFLN><NFID></NFID>";
                                actProd = $"<ActivatedProduct>{flav}</ActivatedProduct>";
                            }
                            sb.AppendLine($"<FLAVOR name=\"{flav}\"><InstallNumber></InstallNumber><SerialNumber></SerialNumber><RegistrationNumber></RegistrationNumber><LA>YES</LA><InstallID>{item.SubItems[2].Text}</InstallID><LicenseNumber>{item.SubItems[3].Text}</LicenseNumber><QBMode1></QBMode1><QBMode2></QBMode2><QBMode></QBMode><VersionNumber></VersionNumber>{actProd}{entFields}</FLAVOR>");
                        }
                        sb.AppendLine("\t\t</VERSION>");
                    }
                    sb.AppendLine("</QUICKBOOKSREGISTRATION>\r\n</QBREG>");

                    string regFile = Path.Combine(licPath, "qbregistration.dat");
                    if (File.Exists(regFile)) {
                        string backup = CreateNumberedBackup(regFile);
                        MessageBox.Show($"Created backup of existing file at {backup}", "Backup Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    File.WriteAllText(regFile, sb.ToString());

                    MessageBox.Show($"Successfully created QuickBooks registration file at:\n{regFile}", "File Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                } catch (Exception ex) {
                    MessageBox.Show($"Error creating file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            p.Controls.AddRange(new Control[] { header, sub, cardClient, cardEntries, btnRemove, btnGen });
            return p;
        }


    }

    // =========================================================================
    //  WETRANSFER AUTOMATOR — Visual browser automation (WebView2 + CDP)
    // =========================================================================
    internal class WeTransferAutomator : Form
    {
        private Microsoft.Web.WebView2.WinForms.WebView2 _wv;
        private string _filePath, _to, _from;
        public event EventHandler AutomationFinished;
        public event EventHandler TransferCompleted;

        public WeTransferAutomator(string filePath, string to, string from)
        {
            _filePath = filePath; _to = to; _from = from;
            this.Text = "WETRANSFER AUTOMATED PORTAL - ATTACHING...";
            this.Size = new Size(1100, 850);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.TopMost = true;
            
            _wv = new Microsoft.Web.WebView2.WinForms.WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(_wv);
            this.Load += async (s, e) => await InitAutomation();
        }

        private async Task InitAutomation()
        {
            try {
                await _wv.EnsureCoreWebView2Async();
                _wv.CoreWebView2.Navigate("https://acecloudhosting.wetransfer.com/");
                _wv.CoreWebView2.NavigationCompleted += async (s, e) => {
                    if (e.IsSuccess) await RunPortalAutomation();
                };
            } catch { MessageBox.Show("WebView2 runtime missing."); }
        }

        private async Task RunPortalAutomation()
        {
            try {
                // 1. Clear cookie banners/popups if any
                await _wv.CoreWebView2.ExecuteScriptAsync(@"
                    document.querySelector('button[aria-label=""Accept all""]')?.click();
                    document.querySelector('#onetrust-accept-btn-handler')?.click();
                ");
                await Task.Delay(2000);

                // 2. Poll for File Input
                string nodeId = "0";
                for (int i = 0; i < 30; i++) { // 15s timeout
                    try {
                        string getDoc = await _wv.CoreWebView2.CallDevToolsProtocolMethodAsync("DOM.getDocument", "{\"depth\": -1, \"pierce\": true}");
                        if (getDoc.Contains("input")) {
                            string searchCommand = "{\"query\": \"input[type='file']\", \"includeUserAgentShadowDOM\": true}";
                            string searchRes = await _wv.CoreWebView2.CallDevToolsProtocolMethodAsync("DOM.performSearch", searchCommand);
                            string searchId = System.Text.RegularExpressions.Regex.Match(searchRes, "\"searchId\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
                            if (!string.IsNullOrEmpty(searchId)) {
                                string getRes = await _wv.CoreWebView2.CallDevToolsProtocolMethodAsync("DOM.getSearchResults", $"{{\"searchId\": \"{searchId}\", \"fromIndex\": 0, \"toIndex\": 1}}");
                                nodeId = System.Text.RegularExpressions.Regex.Match(getRes, "\\[\\s*(\\d+)").Groups[1].Value;
                                if (nodeId != "0" && !string.IsNullOrEmpty(nodeId)) break;
                            }
                        }
                    } catch { }
                    await Task.Delay(500);
                }

                if (nodeId == "0") {
                    this.Text = "WETRANSFER PORTAL - READY";
                    return; 
                }

                // 3. Attach using CDP
                await _wv.CoreWebView2.CallDevToolsProtocolMethodAsync("DOM.setFileInputFiles", 
                    $"{{\"nodeId\": {nodeId}, \"files\": [\"{_filePath.Replace("\\", "\\\\")}\"]}}");

                await Task.Delay(1500);

                // 4. Fill Emails via JS (Using multiple possible selectors)
                string js = $@"
                    (function() {{
                        const toSelectors = ['input#autosuggest', 'input[name=""autosuggestField""]', 'input[name=""email-to""]'];
                        const fromSelectors = ['input#email', 'input[name=""email""]', 'input[name=""email-from""]'];
                        
                        function fill(selectors, val) {{
                            for(let s of selectors) {{
                                let el = document.querySelector(s);
                                if(el) {{ 
                                    el.value = val; 
                                    el.dispatchEvent(new Event('input', {{ bubbles: true }}));
                                    el.dispatchEvent(new Event('change', {{ bubbles: true }}));
                                    return true;
                                }}
                            }}
                            return false;
                        }}
                        
                        fill(toSelectors, '{_to}');
                        fill(fromSelectors, '{_from}');
                    }})();
                ";
                await _wv.CoreWebView2.ExecuteScriptAsync(js);
                AutomationFinished?.Invoke(this, EventArgs.Empty);

                this.Text = "WETRANSFER AUTOMATED PORTAL - READY TO TRANSFER";
                _ = PollForCompletion();
            }
            catch { }
        }

        private async Task PollForCompletion()
        {
            while (!this.IsDisposed) {
                try {
                    string checkJs = @"
                        (function() {
                            let text = (document.body.innerText || '').toLowerCase();
                            if (text.includes('you\'re done') || text.includes('transfer complete') || text.includes('successfully sent') || text.includes('transfer details')) {
                                return 'done';
                            }
                            let headers = Array.from(document.querySelectorAll('h1, h2, h3')).map(h => h.innerText.toLowerCase());
                            for (let h of headers) {
                                if (h.includes('done') || h.includes('transfer complete')) return 'done';
                            }
                            return 'pending';
                        })();
                    ";
                    
                    var res = await _wv.CoreWebView2.ExecuteScriptAsync(checkJs);
                    if (res != null && res.Contains("done")) {
                        TransferCompleted?.Invoke(this, EventArgs.Empty);
                        break;
                    }
                } catch { }
                await Task.Delay(2000);
            }
        }
    }

    internal class InputDialog : Form
    {
        private TextBox _txt;
        private string _result = "";
        public static string Show(string title, string prompt)
        {
            using (var f = new InputDialog(title, prompt)) return f.ShowDialog() == DialogResult.OK ? f._result : "";
        }
        private InputDialog(string title, string prompt)
        {
            this.Text = title;
            this.Size = new Size(400, 180);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White; 
            this.ForeColor = Color.FromArgb(17, 24, 39);
            var lbl = new Label { Text = prompt, Left = 20, Top = 20, Width = 350, AutoSize = true };
            _txt = new TextBox { Left = 20, Top = 50, Width = 345, BackColor = Color.White, ForeColor = Color.FromArgb(17, 24, 39), BorderStyle = BorderStyle.FixedSingle };
            var btnOk = new Button { Text = "CONTINUE", Left = 265, Top = 90, Width = 100, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0x10, 0xb9, 0x81), ForeColor = Color.White };
            btnOk.Click += (s, e) => { _result = _txt.Text; this.DialogResult = DialogResult.OK; this.Close(); };
            this.Controls.AddRange(new Control[] { lbl, _txt, btnOk });
            this.AcceptButton = btnOk;
        }
    }

    internal class ProfileSelectionDialog : Form
    {
        private CheckedListBox _clb;
        private List<string> _result = new List<string>();
        public static string[] Show(string[] items)
        {
            using (var f = new ProfileSelectionDialog(items)) return f.ShowDialog() == DialogResult.OK ? f._result.ToArray() : new string[0];
        }
        private ProfileSelectionDialog(string[] items)
        {
            this.Text = "SELECT PROFILES TO BACKUP";
            this.Size = new Size(400, 500);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White; 
            this.ForeColor = Color.FromArgb(17, 24, 39);
            var lbl = new Label { Text = "Choose profiles from D:\\Profiles:", Left = 20, Top = 20, Width = 350, AutoSize = true };
            _clb = new CheckedListBox { 
                Left = 20, Top = 50, Width = 345, Height = 320, 
                BackColor = Color.White, ForeColor = Color.FromArgb(17, 24, 39), CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle
            };
            foreach (var item in items) _clb.Items.Add(item);
            var btnOk = new Button { Text = "CONFIRM SELECTION", Left = 215, Top = 390, Width = 150, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0x10, 0xb9, 0x81), ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnOk.Click += (s, e) => {
                foreach (var item in _clb.CheckedItems) _result.Add(item.ToString());
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.AddRange(new Control[] { lbl, _clb, btnOk });
        }
    }
}

