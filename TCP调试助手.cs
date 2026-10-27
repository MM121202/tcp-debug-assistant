// ============================================================
//  TCP 调试助手 (C# WinForms, 单文件, 无第三方依赖)
//  界面风格：Industrial Minimal v2 — 顶栏、三套配色、可拖动分区、
//            参数输入格、分段模式切换、状态指示灯、等宽数据显示
//  功能：TCP 客户端 / TCP 服务器双模式、HEX/ASCII 收发、
//        多客户端管理(定向/广播/踢出)、时间戳、暂停显示、
//        快捷指令、循环发送、RX/TX 字节计数、日志保存
//  编译：csc /target:winexe /codepage:65001 /out:TCP调试助手.exe
//        /r:System.Windows.Forms.dll /r:System.Drawing.dll TCP调试助手.cs
// ============================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TcpDebugger
{
    // ============ 设计系统：工业简洁 (Industrial Minimal) ============
    static class Theme
    {
        public static Color Canvas;      // 窗体底
        public static Color Surface;     // 卡片面
        public static Color Line;        // 描边
        public static Color LineSoft;    // 分隔线
        public static Color Ink;         // 主文字
        public static Color Sub;         // 次要文字
        public static Color Accent;      // 主色
        public static Color AccentHover;
        public static Color AccentPress;
        public static Color Hover;
        public static Color Press;
        public static Color Ok;          // 在线
        public static Color Warn;        // 进行中
        public static Color Err;         // 故障
        public static Color Idle;        // 待机
        public static Color Bar;         // 顶栏底
        public static Color BarInk;      // 顶栏字
        public static Color Rx;          // 接收计数
        public static Color Tx;          // 发送计数

        public static readonly Font Ui = new Font("Microsoft YaHei UI", 9F);
        public static readonly Font UiBold = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        public static readonly Font Mono = new Font("Consolas", 10F);

        public static string SchemeName = "工业蓝";

        public static void Apply(string name)
        {
            if (name == "石墨深") Dark();
            else if (name == "军绿") Olive();
            else Blue();
            SchemeName = name;
        }

        static void Blue()
        {
            Canvas = Color.FromArgb(237, 239, 243);
            Surface = Color.White;
            Line = Color.FromArgb(205, 211, 219);
            LineSoft = Color.FromArgb(230, 234, 240);
            Ink = Color.FromArgb(26, 30, 36);
            Sub = Color.FromArgb(106, 115, 126);
            Accent = Color.FromArgb(22, 86, 158);
            AccentHover = Color.FromArgb(17, 70, 131);
            AccentPress = Color.FromArgb(12, 55, 105);
            Hover = Color.FromArgb(243, 245, 249);
            Press = Color.FromArgb(228, 232, 239);
            Ok = Color.FromArgb(26, 127, 62);
            Warn = Color.FromArgb(193, 96, 12);
            Err = Color.FromArgb(196, 44, 44);
            Idle = Color.FromArgb(152, 160, 171);
            Bar = Color.FromArgb(18, 26, 38);
            BarInk = Color.FromArgb(236, 240, 245);
            Rx = Color.FromArgb(26, 127, 62);
            Tx = Color.FromArgb(22, 86, 158);
        }

        static void Dark()
        {
            Canvas = Color.FromArgb(30, 33, 38);
            Surface = Color.FromArgb(38, 42, 48);
            Line = Color.FromArgb(58, 64, 72);
            LineSoft = Color.FromArgb(48, 53, 61);
            Ink = Color.FromArgb(230, 234, 240);
            Sub = Color.FromArgb(150, 159, 172);
            Accent = Color.FromArgb(74, 158, 255);
            AccentHover = Color.FromArgb(56, 140, 235);
            AccentPress = Color.FromArgb(40, 118, 210);
            Hover = Color.FromArgb(48, 53, 61);
            Press = Color.FromArgb(58, 64, 72);
            Ok = Color.FromArgb(76, 200, 122);
            Warn = Color.FromArgb(232, 160, 60);
            Err = Color.FromArgb(232, 88, 88);
            Idle = Color.FromArgb(130, 140, 152);
            Bar = Color.FromArgb(20, 22, 26);
            BarInk = Color.FromArgb(236, 240, 245);
            Rx = Color.FromArgb(76, 200, 122);
            Tx = Color.FromArgb(90, 170, 255);
        }

        static void Olive()
        {
            Canvas = Color.FromArgb(240, 241, 236);
            Surface = Color.White;
            Line = Color.FromArgb(206, 211, 196);
            LineSoft = Color.FromArgb(231, 234, 224);
            Ink = Color.FromArgb(28, 32, 26);
            Sub = Color.FromArgb(108, 116, 102);
            Accent = Color.FromArgb(85, 107, 47);
            AccentHover = Color.FromArgb(70, 90, 38);
            AccentPress = Color.FromArgb(56, 73, 30);
            Hover = Color.FromArgb(245, 247, 240);
            Press = Color.FromArgb(230, 234, 224);
            Ok = Color.FromArgb(74, 124, 52);
            Warn = Color.FromArgb(178, 112, 24);
            Err = Color.FromArgb(178, 52, 40);
            Idle = Color.FromArgb(152, 160, 148);
            Bar = Color.FromArgb(32, 38, 26);
            BarInk = Color.FromArgb(238, 241, 232);
            Rx = Color.FromArgb(74, 124, 52);
            Tx = Color.FromArgb(85, 107, 47);
        }
    }

    // 扁平描边卡片
    class Card : Panel
    {
        public Card()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Theme.Surface;
            Padding = new Padding(1);
            Margin = new Padding(0);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Theme.Line))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // 扁平按钮（支持换肤：记住自己的角色）
    class FlatButton : Button
    {
        public string Kind = "neutral";
        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 1;
            UseVisualStyleBackColor = false;
            Height = 28;
            Font = Theme.Ui;
            ApplyKind();
        }
        public void SetPrimary() { Kind = "primary"; ApplyKind(); }
        public void SetDanger() { Kind = "danger"; ApplyKind(); }
        public void SetWarn() { Kind = "warn"; ApplyKind(); }
        public void SetNeutral() { Kind = "neutral"; ApplyKind(); }
        public void SetActive(bool on) { Kind = on ? "primary" : "neutral"; ApplyKind(); }
        public void ApplyKind()
        {
            Font = Kind == "neutral" ? Theme.Ui : Theme.UiBold;
            switch (Kind)
            {
                case "primary":
                    BackColor = Theme.Accent;
                    ForeColor = Color.White;
                    FlatAppearance.BorderColor = Theme.Accent;
                    FlatAppearance.MouseOverBackColor = Theme.AccentHover;
                    FlatAppearance.MouseDownBackColor = Theme.AccentPress;
                    break;
                case "danger":
                    BackColor = Theme.Err;
                    ForeColor = Color.White;
                    FlatAppearance.BorderColor = Theme.Err;
                    FlatAppearance.MouseOverBackColor = Color.FromArgb(Math.Max(0, Theme.Err.R - 28), Math.Max(0, Theme.Err.G - 8), Math.Max(0, Theme.Err.B - 8));
                    FlatAppearance.MouseDownBackColor = Color.FromArgb(Math.Max(0, Theme.Err.R - 56), Math.Max(0, Theme.Err.G - 16), Math.Max(0, Theme.Err.B - 16));
                    break;
                case "warn":
                    BackColor = Theme.Warn;
                    ForeColor = Color.White;
                    FlatAppearance.BorderColor = Theme.Warn;
                    FlatAppearance.MouseOverBackColor = Color.FromArgb(Math.Max(0, Theme.Warn.R - 25), Math.Max(0, Theme.Warn.G - 20), Math.Max(0, Theme.Warn.B - 4));
                    FlatAppearance.MouseDownBackColor = Color.FromArgb(Math.Max(0, Theme.Warn.R - 50), Math.Max(0, Theme.Warn.G - 40), Math.Max(0, Theme.Warn.B - 8));
                    break;
                default:
                    BackColor = Theme.Surface;
                    ForeColor = Theme.Ink;
                    FlatAppearance.BorderColor = Theme.Line;
                    FlatAppearance.MouseOverBackColor = Theme.Hover;
                    FlatAppearance.MouseDownBackColor = Theme.Press;
                    break;
            }
            Invalidate();
        }
    }

    // 状态指示灯
    class Led : Control
    {
        Color col = Theme.Idle;
        public Led()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(12, 12);
            BackColor = Color.Transparent;
            Margin = new Padding(0, 8, 0, 0);
        }
        public void Set(Color c) { col = c; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush halo = new SolidBrush(Color.FromArgb(45, col)))
                e.Graphics.FillEllipse(halo, 0, 0, Width - 1, Height - 1);
            using (SolidBrush b = new SolidBrush(col))
                e.Graphics.FillEllipse(b, 2, 2, Width - 5, Height - 5);
        }
    }

    // 顶栏（自绘：深底 + 品牌标识 + 版本号）
    class TopBar : Panel
    {
        public TopBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Top;
            Height = 40;
            BackColor = Theme.Bar;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush b = new SolidBrush(Theme.Bar))
                g.FillRectangle(b, ClientRectangle);

            using (SolidBrush ab = new SolidBrush(Theme.Accent))
                g.FillRectangle(ab, 14, 11, 18, 18);
            using (Pen lp = new Pen(Theme.BarInk, 2))
            {
                g.DrawLine(lp, 19, 16, 27, 16);
                g.DrawLine(lp, 19, 20, 27, 20);
                g.DrawLine(lp, 19, 24, 27, 24);
            }

            TextRenderer.DrawText(g, "TCP 调试助手",
                new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
                new Point(40, 10), Theme.BarInk);
            TextRenderer.DrawText(g, "v1.2.0",
                Theme.Ui, new Point(178, 13), Color.FromArgb(150, Theme.BarInk));
        }
    }

    // 配色色块
    class Swatch : Button
    {
        public string Scheme;
        public Color SwatchColor;
        public Swatch(string scheme, Color c)
        {
            Scheme = scheme;
            SwatchColor = c;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 1;
            FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
            UseVisualStyleBackColor = false;
            BackColor = c;
            Size = new Size(18, 18);
            Margin = new Padding(0, 11, 6, 0);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Theme.SchemeName == Scheme)
            {
                using (Pen p = new Pen(Theme.BarInk, 2))
                    e.Graphics.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
            }
        }
    }

    // 分段切换（客户端 / 服务器）
    class Segmented : Control
    {
        string[] items;
        int index;
        public event Action<int> Changed;
        public Segmented(string[] items)
        {
            this.items = items;
            Width = items.Length * 78;
            Height = 28;
            Margin = new Padding(0, 12, 14, 0);
            BackColor = Theme.Surface;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }
        public int Index
        {
            get { return index; }
            set { index = value; Invalidate(); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Surface);
            int w = Width / items.Length;
            TextFormatFlags tf = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
            for (int i = 0; i < items.Length; i++)
            {
                Rectangle r = new Rectangle(i * w, 0, w, Height);
                if (i == index)
                    using (SolidBrush b = new SolidBrush(Theme.Accent))
                        g.FillRectangle(b, r);
                TextRenderer.DrawText(g, items[i], i == index ? Theme.UiBold : Theme.Ui, r,
                    i == index ? Color.White : Theme.Ink, tf);
                if (i > 0)
                    using (Pen p = new Pen(Theme.Line))
                        g.DrawLine(p, i * w, 0, i * w, Height - 1);
            }
            using (Pen p = new Pen(Theme.Line))
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            int w = Width / items.Length;
            int i = e.X / w;
            if (i < 0) i = 0;
            if (i >= items.Length) i = items.Length - 1;
            if (i != index)
            {
                index = i;
                Invalidate();
                if (Changed != null) Changed(i);
            }
        }
    }

    // 参数输入格：左标签 + 右输入框
    class Field : Panel
    {
        public TextBox Box;
        public Field(string cap, int width)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            Width = width;
            Height = 28;
            Margin = new Padding(0, 12, 10, 0);
            BackColor = Theme.Surface;

            Label capLbl = new Label();
            capLbl.Text = cap;
            capLbl.Tag = "sub";
            capLbl.ForeColor = Theme.Sub;
            capLbl.Font = Theme.Ui;
            capLbl.Bounds = new Rectangle(1, 1, 38, 26);
            capLbl.TextAlign = ContentAlignment.MiddleRight;

            Box = new TextBox();
            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = Theme.Surface;
            Box.ForeColor = Theme.Ink;
            Box.Font = Theme.Ui;
            Box.Bounds = new Rectangle(44, 5, width - 48, 18);
            Box.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

            Controls.Add(Box);
            Controls.Add(capLbl);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Theme.Line))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            using (Pen p = new Pen(Theme.LineSoft))
                e.Graphics.DrawLine(p, 41, 5, 41, Height - 6);
        }
    }

    // 底部状态条（自绘：RX / TX 色块 + 在线数 + 提示）
    class StatusBar : Panel
    {
        public string RxText = "RX 0 B";
        public string TxText = "TX 0 B";
        public string Extra = "";
        public StatusBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Bottom;
            Height = 30;
            BackColor = Theme.Surface;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Surface);
            using (Pen p = new Pen(Theme.LineSoft))
                g.DrawLine(p, 0, 0, Width, 0);

            int x = 10;
            x = DrawChip(g, x, Theme.Rx, RxText);
            x = DrawChip(g, x + 18, Theme.Tx, TxText);
            if (Extra.Length > 0)
                TextRenderer.DrawText(g, Extra, Theme.Ui, new Point(x + 6, 8), Theme.Sub);

            string right = "Ctrl+Enter 发送   ·   配色：" + Theme.SchemeName;
            Size sz = TextRenderer.MeasureText(right, Theme.Ui);
            TextRenderer.DrawText(g, right, Theme.Ui, new Point(Width - sz.Width - 10, 8), Theme.Sub);
        }
        int DrawChip(Graphics g, int x, Color c, string text)
        {
            using (SolidBrush b = new SolidBrush(c))
                g.FillRectangle(b, x, 11, 8, 8);
            TextRenderer.DrawText(g, text, Theme.Ui, new Point(x + 13, 8), Theme.Ink);
            return x + 13 + TextRenderer.MeasureText(text, Theme.Ui).Width;
        }
    }

    // 简易输入对话框
    class PromptForm : Form
    {
        TextBox txt;
        public string Value { get { return txt.Text; } }
        public PromptForm(string title, string init)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Size = new Size(380, 140);
            MinimizeBox = false;
            MaximizeBox = false;
            BackColor = Color.FromArgb(245, 246, 248);
            Font = Theme.Ui;

            Label tip = new Label();
            tip.Text = "指令内容（\\r\\n 表示回车换行，\\r 表示回车）";
            tip.Bounds = new Rectangle(14, 14, 340, 20);
            tip.ForeColor = Color.FromArgb(106, 115, 126);
            Controls.Add(tip);

            txt = new TextBox();
            txt.Text = init;
            txt.Bounds = new Rectangle(14, 38, 336, 24);
            txt.Font = new Font("Consolas", 10F);
            Controls.Add(txt);

            Button ok = new Button();
            ok.Text = "确定";
            ok.Bounds = new Rectangle(196, 72, 74, 26);
            ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.Bounds = new Rectangle(276, 72, 74, 26);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    // 一个已连接的客户端（服务器模式）
    class ClientConn
    {
        public Socket Sock;
        public string Label;
        public int Index;
        public override string ToString() { return Label; }
    }

    class MainForm : Form
    {
        // ---------- 顶栏 / 控制条 ----------
        TopBar topBar;
        Segmented segMode;
        Field fldHost, fldPort;
        FlatButton btnConnect;
        Label lblStatus;
        Led ledStatus;

        // ---------- 接收区 ----------
        RichTextBox txtRecv;
        CheckBox chkHexView, chkTimestamp;
        FlatButton btnPause, btnClearRecv, btnSaveLog;
        Label lblClientsTitle;
        ListBox lstClients;
        FlatButton btnKick;

        // ---------- 发送区 ----------
        RichTextBox txtSend;
        CheckBox chkHexSend, chkCrlf, chkLoop;
        TextBox txtInterval;
        ComboBox cboEncoding;
        FlatButton btnSend, btnClearSend;
        FlowLayoutPanel quickRow;
        List<string> quickCmds = new List<string>();
        string quickFile;

        // ---------- 状态 ----------
        StatusBar statusBar;
        SplitContainer splitMain, splitMid;
        volatile bool running = false;      // 已连接 / 监听中
        volatile bool paused = false;       // 暂停显示
        bool isServerMode = false;
        Socket serverSocket;
        ClientConn clientConn;
        List<ClientConn> conns = new List<ClientConn>();
        readonly object connsLock = new object();
        int connSeq = 0;
        long rxBytes = 0, txBytes = 0;
        System.Windows.Forms.Timer loopTimer;
        string statusText = "待机 · 未连接";
        Color statusColor = Theme.Idle;

        public MainForm()
        {
            // 先初始化配色（记住上次选择的配色）
            string saved = "工业蓝";
            try
            {
                string tf = Path.Combine(Application.StartupPath, "theme.txt");
                if (File.Exists(tf)) saved = File.ReadAllText(tf, Encoding.UTF8).Trim();
            }
            catch { }
            Theme.Apply(saved);

            BuildUi();
            loopTimer = new System.Windows.Forms.Timer();
            loopTimer.Interval = 1000;
            loopTimer.Tick += delegate { if (running) SendData(true); };
            UpdateCounters();
            SetStatus("待机 · 未连接", Theme.Idle);
            LoadQuick();
            FormClosing += OnFormClosing;
        }

        // ================= 界面 =================
        void BuildUi()
        {
            Text = "TCP 调试助手 v1.2.0";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1100, 770);
            MinimumSize = new Size(940, 640);
            Font = Theme.Ui;
            BackColor = Theme.Canvas;

            // ---------- 顶栏 ----------
            topBar = new TopBar();
            FlowLayoutPanel swFlow = new FlowLayoutPanel();
            swFlow.Dock = DockStyle.Right;
            swFlow.Width = 152;
            swFlow.BackColor = Theme.Bar;
            swFlow.Tag = "bar";
            swFlow.WrapContents = false;
            swFlow.Padding = new Padding(0, 0, 12, 0);
            Label swCap = new Label();
            swCap.Text = "配色";
            swCap.AutoSize = true;
            swCap.ForeColor = Color.FromArgb(160, Theme.BarInk);
            swCap.Margin = new Padding(0, 13, 8, 0);
            swFlow.Controls.Add(swCap);
            Swatch s1 = new Swatch("工业蓝", Color.FromArgb(22, 86, 158));
            Swatch s2 = new Swatch("石墨深", Color.FromArgb(38, 42, 48));
            Swatch s3 = new Swatch("军绿", Color.FromArgb(85, 107, 47));
            ToolTip tt = new ToolTip();
            tt.SetToolTip(s1, "工业蓝");
            tt.SetToolTip(s2, "石墨深（深色）");
            tt.SetToolTip(s3, "军绿");
            s1.Click += delegate { SwitchTheme("工业蓝"); };
            s2.Click += delegate { SwitchTheme("石墨深"); };
            s3.Click += delegate { SwitchTheme("军绿"); };
            swFlow.Controls.Add(s1);
            swFlow.Controls.Add(s2);
            swFlow.Controls.Add(s3);
            topBar.Controls.Add(swFlow);

            // ---------- 主体 ----------
            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Theme.Canvas;
            body.Tag = "canvas";
            body.Padding = new Padding(10);

            // ---- 控制条 ----
            Card barCard = new Card();
            barCard.Dock = DockStyle.Top;
            barCard.Height = 52;
            barCard.Margin = new Padding(0, 0, 0, 8);

            FlowLayoutPanel barLeft = new FlowLayoutPanel();
            barLeft.Dock = DockStyle.Fill;
            barLeft.BackColor = Theme.Surface;
            barLeft.Tag = "surface";
            barLeft.WrapContents = false;
            barLeft.Padding = new Padding(12, 0, 0, 0);

            Label capMode = SmallCap("模式");
            capMode.Margin = new Padding(0, 18, 8, 0);
            barLeft.Controls.Add(capMode);

            segMode = new Segmented(new string[] { "客户端", "服务器" });
            segMode.Changed += delegate(int i) { SetMode(i == 1); };
            barLeft.Controls.Add(segMode);

            fldHost = new Field("地址", 190);
            fldHost.Box.Text = "127.0.0.1";
            barLeft.Controls.Add(fldHost);

            fldPort = new Field("端口", 110);
            fldPort.Box.Text = "8899";
            barLeft.Controls.Add(fldPort);

            btnConnect = new FlatButton();
            btnConnect.Text = "连 接";
            btnConnect.Width = 100;
            btnConnect.Margin = new Padding(0, 12, 0, 0);
            btnConnect.Click += delegate { OnConnectClick(); };
            btnConnect.SetPrimary();
            barLeft.Controls.Add(btnConnect);

            FlowLayoutPanel barRight = new FlowLayoutPanel();
            barRight.Dock = DockStyle.Right;
            barRight.Width = 210;
            barRight.BackColor = Theme.Surface;
            barRight.Tag = "surface";
            barRight.WrapContents = false;
            barRight.Padding = new Padding(0, 0, 14, 0);
            ledStatus = new Led();
            barRight.Controls.Add(ledStatus);
            lblStatus = new Label();
            lblStatus.Text = "待机 · 未连接";
            lblStatus.AutoSize = true;
            lblStatus.ForeColor = Theme.Sub;
            lblStatus.Margin = new Padding(6, 14, 0, 0);
            barRight.Controls.Add(lblStatus);

            barCard.Controls.Add(barLeft);
            barCard.Controls.Add(barRight);

            // ---- 中间：接收区 + 客户端列表 ----
            splitMain = new SplitContainer();
            splitMain.Dock = DockStyle.Fill;
            splitMain.Orientation = Orientation.Horizontal;
            splitMain.FixedPanel = FixedPanel.Panel2;
            splitMain.SplitterWidth = 8;
            splitMain.SplitterIncrement = 8;
            splitMain.BackColor = Theme.Canvas;
            splitMain.Tag = "canvas";
            splitMain.Panel1.Tag = "canvas";
            splitMain.Panel1.Padding = new Padding(0, 0, 0, 4);
            splitMain.Panel2.Tag = "canvas";
            splitMain.Panel2.Padding = new Padding(0, 4, 0, 0);

            splitMid = new SplitContainer();
            splitMid.Dock = DockStyle.Fill;
            splitMid.Orientation = Orientation.Vertical;
            splitMid.FixedPanel = FixedPanel.Panel2;
            splitMid.SplitterWidth = 8;
            splitMid.SplitterIncrement = 8;
            splitMid.BackColor = Theme.Canvas;
            splitMid.Tag = "canvas";
            splitMid.Panel1.Tag = "canvas";
            splitMid.Panel1.Padding = new Padding(0, 0, 4, 0);
            splitMid.Panel2.Tag = "canvas";
            splitMid.Panel2.Padding = new Padding(4, 0, 0, 0);
            splitMain.Panel1.Controls.Add(splitMid);

            // 接收卡片
            Card recvCard = new Card();
            recvCard.Dock = DockStyle.Fill;
            txtRecv = new RichTextBox();
            txtRecv.Dock = DockStyle.Fill;
            txtRecv.ReadOnly = true;
            txtRecv.BorderStyle = BorderStyle.None;
            txtRecv.BackColor = Theme.Surface;
            txtRecv.ForeColor = Theme.Ink;
            txtRecv.Font = Theme.Mono;
            txtRecv.WordWrap = false;
            txtRecv.ScrollBars = RichTextBoxScrollBars.Both;
            txtRecv.HideSelection = false;
            recvCard.Controls.Add(txtRecv);
            recvCard.Controls.Add(MakeRecvHead());
            splitMid.Panel1.Controls.Add(recvCard);

            // 客户端卡片
            Card cliCard = new Card();
            cliCard.Dock = DockStyle.Fill;
            lblClientsTitle = new Label();
            lblClientsTitle.Text = "客户端 · CLIENTS";
            lblClientsTitle.Dock = DockStyle.Fill;
            lblClientsTitle.TextAlign = ContentAlignment.MiddleLeft;
            lblClientsTitle.ForeColor = Theme.Sub;
            lblClientsTitle.Font = Theme.UiBold;
            lblClientsTitle.Tag = "title";
            lblClientsTitle.Padding = new Padding(10, 0, 0, 0);
            btnKick = new FlatButton();
            btnKick.Text = "断开";
            btnKick.Width = 62;
            btnKick.Click += delegate { KickSelected(); };
            lstClients = new ListBox();
            lstClients.Dock = DockStyle.Fill;
            lstClients.BorderStyle = BorderStyle.None;
            lstClients.BackColor = Theme.Surface;
            lstClients.ForeColor = Theme.Ink;
            lstClients.Font = new Font("Consolas", 9.5F);
            lstClients.IntegralHeight = false;
            lstClients.SelectionMode = SelectionMode.MultiExtended;
            cliCard.Controls.Add(lstClients);
            cliCard.Controls.Add(MakeHead(lblClientsTitle, "cli", btnKick));
            splitMid.Panel2.Controls.Add(cliCard);

            // ---- 发送卡片 ----
            Card sendCard = new Card();
            sendCard.Dock = DockStyle.Fill;

            btnSend = new FlatButton();
            btnSend.Text = "发 送";
            btnSend.Width = 92;
            btnSend.Margin = new Padding(0, 0, 6, 0);
            btnSend.Click += delegate { SendData(false); };
            btnClearSend = new FlatButton();
            btnClearSend.Text = "清空";
            btnClearSend.Width = 66;
            btnClearSend.Margin = new Padding(0, 0, 10, 0);
            btnClearSend.Click += delegate { txtSend.Clear(); };

            FlowLayoutPanel optRow = new FlowLayoutPanel();
            optRow.Dock = DockStyle.Top;
            optRow.Height = 36;
            optRow.WrapContents = false;
            optRow.BackColor = Theme.Surface;
            optRow.Tag = "surface";
            optRow.Padding = new Padding(10, 6, 0, 0);

            chkHexSend = new CheckBox();
            chkHexSend.Text = "HEX 发送";
            chkHexSend.AutoSize = true;
            chkHexSend.ForeColor = Theme.Ink;
            chkHexSend.BackColor = Theme.Surface;
            chkHexSend.Margin = new Padding(0, 4, 14, 0);
            optRow.Controls.Add(chkHexSend);

            chkCrlf = new CheckBox();
            chkCrlf.Text = "末尾加 \\r\\n";
            chkCrlf.AutoSize = true;
            chkCrlf.Checked = true;
            chkCrlf.ForeColor = Theme.Ink;
            chkCrlf.BackColor = Theme.Surface;
            chkCrlf.Margin = new Padding(0, 4, 14, 0);
            optRow.Controls.Add(chkCrlf);

            optRow.Controls.Add(SmallCap("编码"));
            cboEncoding = new ComboBox();
            cboEncoding.DropDownStyle = ComboBoxStyle.DropDownList;
            cboEncoding.FlatStyle = FlatStyle.Flat;
            cboEncoding.BackColor = Theme.Surface;
            cboEncoding.ForeColor = Theme.Ink;
            cboEncoding.Font = Theme.Ui;
            cboEncoding.Items.Add("UTF-8");
            cboEncoding.Items.Add("GBK");
            cboEncoding.Items.Add("ASCII");
            cboEncoding.SelectedIndex = 0;
            cboEncoding.Width = 78;
            cboEncoding.Margin = new Padding(0, 4, 14, 0);
            optRow.Controls.Add(cboEncoding);

            chkLoop = new CheckBox();
            chkLoop.Text = "循环发送";
            chkLoop.AutoSize = true;
            chkLoop.ForeColor = Theme.Ink;
            chkLoop.BackColor = Theme.Surface;
            chkLoop.Margin = new Padding(0, 4, 6, 0);
            chkLoop.CheckedChanged += delegate { ApplyLoopTimer(); };
            optRow.Controls.Add(chkLoop);

            txtInterval = new TextBox();
            txtInterval.Text = "1000";
            txtInterval.Width = 52;
            txtInterval.BorderStyle = BorderStyle.FixedSingle;
            txtInterval.BackColor = Theme.Surface;
            txtInterval.ForeColor = Theme.Ink;
            txtInterval.Font = Theme.Ui;
            txtInterval.Margin = new Padding(0, 5, 2, 0);
            txtInterval.TextChanged += delegate { ApplyLoopTimer(); };
            optRow.Controls.Add(txtInterval);

            Label lms = new Label();
            lms.Text = "ms";
            lms.AutoSize = true;
            lms.ForeColor = Theme.Sub;
            lms.Tag = "sub";
            lms.Margin = new Padding(0, 8, 0, 0);
            optRow.Controls.Add(lms);

            // 快捷指令行
            quickRow = new FlowLayoutPanel();
            quickRow.Dock = DockStyle.Bottom;
            quickRow.Height = 38;
            quickRow.WrapContents = false;
            quickRow.BackColor = Theme.Surface;
            quickRow.Tag = "surface";
            quickRow.Padding = new Padding(10, 6, 0, 0);
            quickRow.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.LineSoft))
                    e.Graphics.DrawLine(p, 0, 0, quickRow.Width, 0);
            };

            txtSend = new RichTextBox();
            txtSend.Dock = DockStyle.Fill;
            txtSend.BorderStyle = BorderStyle.None;
            txtSend.BackColor = Theme.Surface;
            txtSend.ForeColor = Theme.Ink;
            txtSend.Font = Theme.Mono;
            txtSend.WordWrap = false;
            txtSend.ScrollBars = RichTextBoxScrollBars.Both;
            txtSend.Text = "Hello TCP!";
            txtSend.KeyDown += OnSendKeyDown;

            sendCard.Controls.Add(txtSend);
            sendCard.Controls.Add(quickRow);
            sendCard.Controls.Add(optRow);
            sendCard.Controls.Add(MakeHead("发送 · TX", "tx", btnSend, btnClearSend));
            splitMain.Panel2.Controls.Add(sendCard);

            // ---- 底部状态条 ----
            statusBar = new StatusBar();
            statusBar.Margin = new Padding(0, 8, 0, 0);

            body.Controls.Add(splitMain);
            body.Controls.Add(statusBar);
            body.Controls.Add(barCard);

            Controls.Add(body);
            Controls.Add(topBar);

            Load += delegate
            {
                // 容器有真实尺寸后才能设置分区大小，否则会抛异常
                try
                {
                    splitMain.Panel1MinSize = 180;
                    splitMain.Panel2MinSize = 150;
                    splitMid.Panel1MinSize = 320;
                    splitMid.Panel2MinSize = 190;
                    int dh = splitMain.Height - 240;
                    splitMain.SplitterDistance = Math.Min(splitMain.Height - 158, Math.Max(180, dh));
                    int dw = splitMid.Width - 250;
                    splitMid.SplitterDistance = Math.Min(splitMid.Width - 198, Math.Max(320, dw));
                }
                catch { }
            };

            SetMode(false);
        }

        // ---- 界面小工具 ----
        static Label SmallCap(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = Theme.Sub;
            l.Tag = "sub";
            l.Margin = new Padding(0, 8, 6, 0);
            return l;
        }

        // 带底部细分隔线的标题栏，右侧可放控件
        Panel MakeHead(string title, string kind, params Control[] rightItems)
        {
            Label t = new Label();
            t.Text = title;
            t.Dock = DockStyle.Fill;
            t.TextAlign = ContentAlignment.MiddleLeft;
            t.ForeColor = Theme.Sub;
            t.Font = Theme.UiBold;
            t.Tag = "title";
            t.Padding = new Padding(10, 0, 0, 0);
            return MakeHead(t, kind, rightItems);
        }

        Panel MakeHead(Label title, string kind, params Control[] rightItems)
        {
            Panel head = new Panel();
            head.Dock = DockStyle.Top;
            head.Height = 34;
            head.BackColor = Theme.Surface;
            head.Tag = "surface";
            head.Padding = new Padding(kind == "tx" || kind == "rx" ? 0 : 0);

            TableLayoutPanel lay = new TableLayoutPanel();
            lay.Dock = DockStyle.Fill;
            lay.BackColor = Theme.Surface;
            lay.Tag = "surface";
            lay.ColumnCount = 2;
            lay.RowCount = 1;
            lay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            lay.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            lay.Controls.Add(title, 0, 0);

            if (rightItems != null && rightItems.Length > 0)
            {
                FlowLayoutPanel flow = new FlowLayoutPanel();
                flow.AutoSize = true;
                flow.WrapContents = false;
                flow.BackColor = Theme.Surface;
                flow.Tag = "surface";
                flow.Padding = new Padding(0, 0, 8, 0);
                foreach (Control c in rightItems)
                {
                    c.Margin = new Padding(c.Margin.Left, 3, c.Margin.Right, 0);
                    flow.Controls.Add(c);
                }
                lay.Controls.Add(flow, 1, 0);
            }
            head.Controls.Add(lay);

            head.Paint += delegate(object s, PaintEventArgs e)
            {
                // 左侧主色竖条
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                    e.Graphics.FillRectangle(b, 0, 0, 3, head.Height);
                using (Pen p = new Pen(Theme.LineSoft))
                    e.Graphics.DrawLine(p, 0, head.Height - 1, head.Width, head.Height - 1);
            };
            return head;
        }

        Panel MakeRecvHead()
        {
            chkHexView = new CheckBox();
            chkHexView.Text = "HEX 显示";
            chkHexView.AutoSize = true;
            chkHexView.ForeColor = Theme.Ink;
            chkHexView.BackColor = Theme.Surface;
            chkHexView.Margin = new Padding(0, 5, 14, 0);

            chkTimestamp = new CheckBox();
            chkTimestamp.Text = "时间戳";
            chkTimestamp.AutoSize = true;
            chkTimestamp.Checked = true;
            chkTimestamp.ForeColor = Theme.Ink;
            chkTimestamp.BackColor = Theme.Surface;
            chkTimestamp.Margin = new Padding(0, 5, 14, 0);

            btnPause = new FlatButton();
            btnPause.Text = "暂停";
            btnPause.Width = 62;
            btnPause.Margin = new Padding(0, 0, 6, 0);
            btnPause.Click += delegate
            {
                paused = !paused;
                btnPause.Text = paused ? "继续" : "暂停";
                if (paused) btnPause.SetWarn(); else btnPause.SetNeutral();
            };

            btnClearRecv = new FlatButton();
            btnClearRecv.Text = "清空";
            btnClearRecv.Width = 62;
            btnClearRecv.Margin = new Padding(0, 0, 6, 0);
            btnClearRecv.Click += delegate { txtRecv.Clear(); };

            btnSaveLog = new FlatButton();
            btnSaveLog.Text = "保存日志";
            btnSaveLog.Width = 80;
            btnSaveLog.Click += delegate { SaveLog(); };

            return MakeHead("接收 · RX", "rx", chkHexView, chkTimestamp, btnPause, btnClearRecv, btnSaveLog);
        }

        // ================= 换肤 =================
        void SwitchTheme(string name)
        {
            Theme.Apply(name);
            try { File.WriteAllText(Path.Combine(Application.StartupPath, "theme.txt"), name, Encoding.UTF8); }
            catch { }
            ApplyThemeTo(this);
            ledStatus.Set(statusColor);
            SetStatus(statusText, statusColor);
            UpdateCounters();
            Invalidate(true);
        }

        static void ApplyThemeTo(Control c)
        {
            string tag = c.Tag as string;
            if (tag == "surface") c.BackColor = Theme.Surface;
            else if (tag == "canvas") c.BackColor = Theme.Canvas;
            else if (tag == "bar") c.BackColor = Theme.Bar;

            if (c is Card) c.BackColor = Theme.Surface;
            else if (c is FlatButton) ((FlatButton)c).ApplyKind();
            else if (c is Segmented) { c.BackColor = Theme.Surface; c.Invalidate(); }
            else if (c is StatusBar) { c.BackColor = Theme.Surface; c.Invalidate(); }
            else if (c is TopBar) { c.BackColor = Theme.Bar; c.Invalidate(); }
            else if (c is Swatch) { c.BackColor = ((Swatch)c).SwatchColor; c.Invalidate(); }
            else if (c is TextBoxBase || c is ListBox || c is ComboBox) { c.BackColor = Theme.Surface; c.ForeColor = Theme.Ink; }
            else if (c is CheckBox) { c.BackColor = Theme.Surface; c.ForeColor = Theme.Ink; }
            else if (c is Label)
            {
                if (tag == "sub" || tag == "title") c.ForeColor = Theme.Sub;
                else c.ForeColor = Theme.Ink;
            }
            else if (c is Form) c.BackColor = Theme.Canvas;

            foreach (Control k in c.Controls) ApplyThemeTo(k);
        }

        // ================= 模式与状态 =================
        void SetMode(bool server)
        {
            isServerMode = server;
            segMode.Index = server ? 1 : 0;
            fldHost.Box.Enabled = !server && !running;
            lblClientsTitle.Text = server ? "客户端 · CLIENTS（不选=广播）" : "客户端 · CLIENTS（服务器模式）";
            btnKick.Enabled = server;
            btnConnect.Text = running ? (server ? "停止监听" : "断 开") : (server ? "开始监听" : "连 接");
            UpdateCounters();
        }

        void SetStatus(string text, Color color)
        {
            statusText = text;
            statusColor = color;
            if (InvokeRequired) { BeginInvoke(new Action<string, Color>(SetStatus), text, color); return; }
            lblStatus.Text = text;
            lblStatus.ForeColor = color == Theme.Idle ? Theme.Sub : color;
            ledStatus.Set(color);
        }

        void SetConnectUiRunning(bool run)
        {
            if (InvokeRequired) { BeginInvoke(new Action<bool>(SetConnectUiRunning), run); return; }
            btnConnect.Text = run ? (isServerMode ? "停止监听" : "断 开") : (isServerMode ? "开始监听" : "连 接");
            if (run) btnConnect.SetDanger(); else btnConnect.SetPrimary();
            segMode.Enabled = !run;
            fldPort.Box.Enabled = !run;
            fldHost.Box.Enabled = !run && !isServerMode;
        }

        // ================= 快捷指令 =================
        void LoadQuick()
        {
            try
            {
                quickFile = Path.Combine(Application.StartupPath, "quick.txt");
                if (File.Exists(quickFile))
                {
                    string[] lines = File.ReadAllLines(quickFile, Encoding.UTF8);
                    foreach (string s in lines)
                        if (s.Trim().Length > 0) quickCmds.Add(s);
                }
                if (quickCmds.Count == 0)
                {
                    quickCmds.Add("Hello TCP!");
                    quickCmds.Add("AT\\r\\n");
                    quickCmds.Add("01 03 00 00 00 01");
                    quickCmds.Add("PING\\r\\n");
                    SaveQuick();
                }
                if (quickCmds.Count > 6) quickCmds = quickCmds.GetRange(0, 6);
            }
            catch { }
            BuildQuickRow();
        }

        void SaveQuick()
        {
            try { File.WriteAllLines(quickFile, quickCmds.ToArray(), Encoding.UTF8); }
            catch { }
        }

        void BuildQuickRow()
        {
            quickRow.Controls.Clear();
            Label cap = SmallCap("快捷");
            cap.Margin = new Padding(0, 8, 8, 0);
            quickRow.Controls.Add(cap);

            for (int i = 0; i < quickCmds.Count; i++)
            {
                int idx = i;
                FlatButton b = new FlatButton();
                b.Text = quickCmds[i];
                b.AutoSize = false;
                b.Height = 26;
                b.Width = Math.Min(150, Math.Max(64, TextRenderer.MeasureText(quickCmds[i], Theme.Ui).Width + 22));
                b.Margin = new Padding(0, 0, 6, 0);
                b.Click += delegate
                {
                    txtSend.Text = quickCmds[idx];
                    SendData(false);
                };
                ContextMenuStrip menu = new ContextMenuStrip();
                ToolStripItem editItem = menu.Items.Add("编辑此指令…");
                editItem.Click += delegate
                {
                    PromptForm p = new PromptForm("编辑快捷指令", quickCmds[idx]);
                    if (p.ShowDialog(this) == DialogResult.OK)
                    {
                        quickCmds[idx] = p.Value;
                        SaveQuick();
                        BuildQuickRow();
                    }
                };
                b.ContextMenuStrip = menu;
                quickRow.Controls.Add(b);
            }

            FlatButton add = new FlatButton();
            add.Text = "＋";
            add.Width = 34;
            add.Height = 26;
            add.Margin = new Padding(0, 0, 0, 0);
            add.Click += delegate
            {
                if (quickCmds.Count >= 6) { MessageBox.Show("最多 6 条快捷指令", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                PromptForm p = new PromptForm("新增快捷指令", "");
                if (p.ShowDialog(this) == DialogResult.OK && p.Value.Trim().Length > 0)
                {
                    quickCmds.Add(p.Value);
                    SaveQuick();
                    BuildQuickRow();
                }
            };
            quickRow.Controls.Add(add);
        }

        // ================= 连接 / 监听 =================
        void OnConnectClick()
        {
            if (running) { StopAll(); return; }
            int port;
            if (!int.TryParse(fldPort.Box.Text.Trim(), out port) || port < 1 || port > 65535)
            {
                MessageBox.Show("端口必须是 1~65535 的数字", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (isServerMode) StartServer(port);
            else StartClient(fldHost.Box.Text.Trim(), port);
        }

        void StartClient(string host, int port)
        {
            if (host.Length == 0) { MessageBox.Show("请输入服务器地址", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            running = true;
            SetConnectUiRunning(true);
            SetStatus("连接中…", Theme.Warn);
            Thread t = new Thread(delegate()
            {
                Socket s = null;
                try
                {
                    s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    s.Connect(host, port);
                    ClientConn c = new ClientConn();
                    c.Sock = s;
                    c.Label = host + ":" + port;
                    c.Index = 0;
                    clientConn = c;
                    SetStatus("已连接 · " + c.Label, Theme.Ok);
                    AppendSys("[系统] 已连接到 " + c.Label);
                    RecvLoop(s, c, true);
                }
                catch (Exception ex)
                {
                    try { if (s != null) s.Close(); } catch { }
                    SetStatus("连接失败", Theme.Err);
                    AppendErr("[错误] 无法连接 " + host + ":" + port + " — " + ex.Message);
                    running = false;
                    SetConnectUiRunning(false);
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        void StartServer(int port)
        {
            try
            {
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
                serverSocket.Listen(10);
            }
            catch (Exception ex)
            {
                SetStatus("监听失败", Theme.Err);
                MessageBox.Show("无法监听端口 " + port + "：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                try { if (serverSocket != null) serverSocket.Close(); } catch { }
                serverSocket = null;
                return;
            }
            running = true;
            SetConnectUiRunning(true);
            SetStatus("监听中 · 0.0.0.0:" + port, Theme.Ok);
            AppendSys("[系统] 开始监听 0.0.0.0:" + port);
            UpdateCounters();

            Thread t = new Thread(AcceptLoop);
            t.IsBackground = true;
            t.Start();
        }

        void AcceptLoop()
        {
            while (running && serverSocket != null)
            {
                Socket s;
                try { s = serverSocket.Accept(); }
                catch { break; }
                if (s == null) break;
                ClientConn c = new ClientConn();
                c.Sock = s;
                string ep;
                try { ep = s.RemoteEndPoint.ToString(); }
                catch { ep = "客户端"; }
                lock (connsLock) { connSeq++; c.Index = connSeq; }
                c.Label = "[" + c.Index.ToString("D2") + "] " + ep;
                lock (connsLock) { conns.Add(c); }
                AppendSys("[系统] 客户端接入 " + c.Label);
                UpdateClientList();
                Thread rt = new Thread(delegate() { RecvLoop(s, c, false); });
                rt.IsBackground = true;
                rt.Start();
            }
        }

        // ================= 接收 =================
        void RecvLoop(Socket s, ClientConn c, bool clientMode)
        {
            byte[] buf = new byte[8192];
            try
            {
                while (true)
                {
                    int n = s.Receive(buf);
                    if (n <= 0) break;
                    byte[] data = new byte[n];
                    Buffer.BlockCopy(buf, 0, data, 0, n);
                    Interlocked.Add(ref rxBytes, n);
                    ShowRx(data, c);
                    UpdateCounters();
                }
            }
            catch { }
            finally
            {
                try { s.Close(); } catch { }
                if (clientMode)
                {
                    running = false;
                    SetConnectUiRunning(false);
                    SetStatus("连接已断开", Theme.Err);
                    AppendSys("[系统] 连接已断开");
                }
                else
                {
                    lock (connsLock) { conns.Remove(c); }
                    AppendSys("[系统] 客户端断开 " + c.Label);
                    UpdateClientList();
                }
            }
        }

        void ShowRx(byte[] data, ClientConn c)
        {
            if (!txtRecv.IsHandleCreated) return;
            try { txtRecv.BeginInvoke(new Action<byte[], string>(DisplayRx), data, c.Label); }
            catch { }
        }

        void DisplayRx(byte[] data, string from)
        {
            string pre = "";
            if (chkTimestamp.Checked) pre = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] ";
            if (isServerMode) pre += "<" + from + "> ";
            string body = chkHexView.Checked ? HexStr(data) : DecodeBytes(data);
            AppendRaw(pre + body + "\r\n", Theme.Ink);
        }

        // ================= 发送 =================
        void SendData(bool quiet)
        {
            if (!running) { if (!quiet) SetStatus("未连接", Theme.Err); return; }
            string text = txtSend.Text;
            byte[] data;
            if (chkHexSend.Checked)
            {
                data = ParseHex(text);
                if (data == null)
                {
                    if (!quiet) MessageBox.Show("HEX 格式不正确：请填偶数个十六进制字符，如 01 03 00 00 00 01", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (data.Length == 0) { if (!quiet) MessageBox.Show("请输入要发送的内容", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            }
            else
            {
                string s = text;
                if (chkCrlf.Checked && !s.EndsWith("\r\n")) s += "\r\n";
                if (s.Length == 0) { if (!quiet) MessageBox.Show("请输入要发送的内容", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                data = GetEncoding().GetBytes(s);
            }

            List<Socket> targets = GetTargetSockets();
            if (targets.Count == 0) { if (!quiet) SetStatus("没有可发送的连接", Theme.Err); return; }

            int ok = 0;
            foreach (Socket s in targets)
            {
                try { s.Send(data); ok++; }
                catch { }
            }
            if (ok == 0) { if (!quiet) SetStatus("发送失败（连接已断开）", Theme.Err); return; }
            Interlocked.Add(ref txBytes, data.Length * ok);
            UpdateCounters();
            if (!quiet)
            {
                string pre = "";
                if (chkTimestamp.Checked) pre = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] ";
                if (isServerMode && lstClients.SelectedItems.Count == 0) pre += "<广播> ";
                AppendRaw(pre + (chkHexSend.Checked ? HexStr(data) : DecodeBytes(data)) + "\r\n", Theme.Accent);
            }
        }

        List<Socket> GetTargetSockets()
        {
            List<Socket> list = new List<Socket>();
            if (!isServerMode)
            {
                if (clientConn != null && clientConn.Sock != null) list.Add(clientConn.Sock);
                return list;
            }
            if (lstClients.SelectedItems.Count > 0)
            {
                foreach (object o in lstClients.SelectedItems)
                {
                    ClientConn c = o as ClientConn;
                    if (c != null && c.Sock != null) list.Add(c.Sock);
                }
                return list;
            }
            lock (connsLock) { foreach (ClientConn c in conns) if (c.Sock != null) list.Add(c.Sock); }
            return list;
        }

        void KickSelected()
        {
            if (!isServerMode) return;
            List<ClientConn> picked = new List<ClientConn>();
            foreach (object o in lstClients.SelectedItems)
            {
                ClientConn c = o as ClientConn;
                if (c != null) picked.Add(c);
            }
            if (picked.Count == 0) { SetStatus("请先选中要断开的客户端", Theme.Warn); return; }
            foreach (ClientConn c in picked) { try { c.Sock.Close(); } catch { } }
        }

        void ApplyLoopTimer()
        {
            if (chkLoop.Checked)
            {
                int ms;
                if (!int.TryParse(txtInterval.Text.Trim(), out ms) || ms < 50) ms = 1000;
                loopTimer.Interval = ms;
                loopTimer.Start();
            }
            else loopTimer.Stop();
        }

        void OnSendKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendData(false);
            }
        }

        // ================= 断开 / 关闭 =================
        void StopAll()
        {
            running = false;
            try { if (serverSocket != null) serverSocket.Close(); } catch { }
            serverSocket = null;
            List<ClientConn> snap;
            lock (connsLock) { snap = new List<ClientConn>(conns); }
            foreach (ClientConn c in snap) { try { c.Sock.Close(); } catch { } }
            lock (connsLock) { conns.Clear(); }
            if (clientConn != null) { try { clientConn.Sock.Close(); } catch { } clientConn = null; }
            UpdateClientList();
            SetConnectUiRunning(false);
            SetStatus("待机 · 已断开", Theme.Idle);
            AppendSys("[系统] 已断开");
        }

        void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            loopTimer.Stop();
            StopAll();
        }

        // ================= 辅助 =================
        void UpdateClientList()
        {
            if (InvokeRequired) { BeginInvoke(new Action(UpdateClientList)); return; }
            lstClients.BeginUpdate();
            lstClients.Items.Clear();
            lock (connsLock) { foreach (ClientConn c in conns) lstClients.Items.Add(c); }
            lstClients.EndUpdate();
            if (running && isServerMode)
                SetStatus("监听中 · 在线 " + conns.Count + " 个客户端", Theme.Ok);
            UpdateCounters();
        }

        void AppendSys(string s) { AppendRaw(s + "\r\n", Theme.Sub); }
        void AppendErr(string s) { AppendRaw(s + "\r\n", Theme.Err); }

        void AppendRaw(string s, Color color)
        {
            if (paused) return;
            if (txtRecv.InvokeRequired) { try { txtRecv.BeginInvoke(new Action<string, Color>(AppendRaw), s, color); } catch { } return; }
            if (txtRecv.TextLength > 600000)
            {
                txtRecv.Select(0, 150000);
                txtRecv.SelectedText = "";
            }
            txtRecv.SelectionStart = txtRecv.TextLength;
            txtRecv.SelectionLength = 0;
            txtRecv.SelectionColor = color;
            txtRecv.AppendText(s);
            txtRecv.SelectionColor = txtRecv.ForeColor;
            txtRecv.ScrollToCaret();
        }

        void UpdateCounters()
        {
            if (statusBar == null || !statusBar.IsHandleCreated) return;
            if (statusBar.InvokeRequired) { try { statusBar.BeginInvoke(new Action(UpdateCounters)); } catch { } return; }
            long rx = Interlocked.Read(ref rxBytes);
            long tx = Interlocked.Read(ref txBytes);
            int online;
            lock (connsLock) { online = conns.Count; }
            statusBar.RxText = "RX " + FmtBytes(rx);
            statusBar.TxText = "TX " + FmtBytes(tx);
            statusBar.Extra = isServerMode ? ("在线 " + online + " 个客户端") : (running ? "已连接" : "");
            statusBar.Invalidate();
        }

        static string FmtBytes(long n)
        {
            if (n < 1024) return n + " B";
            if (n < 1024 * 1024) return (n / 1024.0).ToString("0.0") + " KB";
            return (n / 1024.0 / 1024.0).ToString("0.00") + " MB";
        }

        Encoding GetEncoding()
        {
            string name = cboEncoding.SelectedItem as string;
            if (name == "GBK")
            {
                try { return Encoding.GetEncoding(936); }
                catch { return Encoding.UTF8; }
            }
            if (name == "ASCII") return Encoding.ASCII;
            return Encoding.UTF8;
        }

        string DecodeBytes(byte[] data)
        {
            try { return GetEncoding().GetString(data); }
            catch { return HexStr(data); }
        }

        static string HexStr(byte[] data)
        {
            StringBuilder sb = new StringBuilder(data.Length * 3);
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }
            return sb.ToString();
        }

        static byte[] ParseHex(string s)
        {
            if (s == null) return new byte[0];
            s = s.Replace("0x", "").Replace("0X", "");
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char ch in s)
                if (Uri.IsHexDigit(ch)) sb.Append(ch);
            if (sb.Length == 0) return new byte[0];
            if (sb.Length % 2 != 0) return null;
            byte[] result = new byte[sb.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = Convert.ToByte(sb.ToString(i * 2, 2), 16);
            return result;
        }

        void SaveLog()
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "文本文件|*.txt|所有文件|*.*";
            dlg.FileName = "tcp_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(dlg.FileName, txtRecv.Text, Encoding.UTF8);
                    SetStatus("日志已保存", Theme.Ok);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("保存失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ================= 自动化测试接口 =================
        internal void TestSetMode(bool server) { SetMode(server); }
        internal void TestSetAddress(string host, string port)
        {
            fldHost.Box.Text = host;
            fldPort.Box.Text = port;
        }
        internal void TestConnect() { btnConnect.PerformClick(); }
        internal void TestSend(string text) { txtSend.Text = text; btnSend.PerformClick(); }
        internal string RecvText { get { return txtRecv.Text; } }
        internal bool IsRunning { get { return running; } }
        internal void TestSetHexSend(bool hex) { chkHexSend.Checked = hex; }
        internal void TestSetHexView(bool hex) { chkHexView.Checked = hex; }
    }
}
