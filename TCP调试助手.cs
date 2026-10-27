// ============================================================
//  TCP 调试助手 (C# WinForms, 单文件, 无第三方依赖)
//  界面风格：Industrial Minimal — 工业灰蓝配色、扁平卡片、
//            1px 描边、方角控件、状态指示灯、等宽数据显示
//  功能：TCP 客户端 / TCP 服务器双模式、HEX/ASCII 收发、
//        多客户端管理(定向/广播)、时间戳、循环发送、
//        RX/TX 字节计数、日志保存
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
        public static readonly Color Canvas = Color.FromArgb(237, 239, 243);   // 窗体底
        public static readonly Color Surface = Color.White;                     // 卡片面
        public static readonly Color Line = Color.FromArgb(205, 211, 219);      // 描边
        public static readonly Color LineSoft = Color.FromArgb(230, 234, 240);  // 分隔线
        public static readonly Color Ink = Color.FromArgb(26, 30, 36);          // 主文字
        public static readonly Color Sub = Color.FromArgb(106, 115, 126);       // 次要文字
        public static readonly Color Accent = Color.FromArgb(22, 86, 158);      // 工业蓝
        public static readonly Color AccentHover = Color.FromArgb(17, 70, 131);
        public static readonly Color AccentPress = Color.FromArgb(12, 55, 105);
        public static readonly Color Hover = Color.FromArgb(243, 245, 249);
        public static readonly Color Press = Color.FromArgb(228, 232, 239);
        public static readonly Color Ok = Color.FromArgb(26, 127, 62);          // 在线
        public static readonly Color Warn = Color.FromArgb(193, 96, 12);        // 进行中
        public static readonly Color Err = Color.FromArgb(196, 44, 44);         // 故障
        public static readonly Color Idle = Color.FromArgb(152, 160, 171);      // 待机

        public static readonly Font Ui = new Font("Microsoft YaHei UI", 9F);
        public static readonly Font UiBold = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        public static readonly Font Mono = new Font("Consolas", 10F);
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
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Theme.Line))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // 扁平按钮
    class FlatButton : Button
    {
        public bool Active;
        public FlatButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 1;
            FlatAppearance.BorderColor = Theme.Line;
            FlatAppearance.MouseOverBackColor = Theme.Hover;
            FlatAppearance.MouseDownBackColor = Theme.Press;
            BackColor = Theme.Surface;
            ForeColor = Theme.Ink;
            UseVisualStyleBackColor = false;
            Height = 26;
            Font = Theme.Ui;
        }
        public void SetPrimary()
        {
            BackColor = Theme.Accent;
            ForeColor = Color.White;
            FlatAppearance.BorderColor = Theme.Accent;
            FlatAppearance.MouseOverBackColor = Theme.AccentHover;
            FlatAppearance.MouseDownBackColor = Theme.AccentPress;
            Font = Theme.UiBold;
        }
        public void SetDanger()
        {
            BackColor = Theme.Err;
            ForeColor = Color.White;
            FlatAppearance.BorderColor = Theme.Err;
            FlatAppearance.MouseOverBackColor = Color.FromArgb(168, 36, 36);
            FlatAppearance.MouseDownBackColor = Color.FromArgb(140, 28, 28);
            Font = Theme.UiBold;
        }
        public void SetNeutral()
        {
            BackColor = Theme.Surface;
            ForeColor = Theme.Ink;
            FlatAppearance.BorderColor = Theme.Line;
            FlatAppearance.MouseOverBackColor = Theme.Hover;
            FlatAppearance.MouseDownBackColor = Theme.Press;
            Font = Theme.Ui;
        }
        public void SetActive(bool on)
        {
            Active = on;
            if (on)
            {
                BackColor = Theme.Accent;
                ForeColor = Color.White;
                FlatAppearance.BorderColor = Theme.Accent;
                FlatAppearance.MouseOverBackColor = Theme.AccentHover;
                FlatAppearance.MouseDownBackColor = Theme.AccentPress;
                Font = Theme.UiBold;
            }
            else SetNeutral();
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
            Margin = new Padding(0, 7, 0, 0);
        }
        public void Set(Color c) { col = c; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush b = new SolidBrush(col))
                e.Graphics.FillEllipse(b, 1, 1, Width - 3, Height - 3);
            using (Pen p = new Pen(Color.FromArgb(70, 0, 0, 0)))
                e.Graphics.DrawEllipse(p, 1, 1, Width - 3, Height - 3);
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
        public override string ToString() { return Label; }
    }

    class MainForm : Form
    {
        // ---------- 控制条 ----------
        FlatButton btnModeClient, btnModeServer;
        TextBox txtHost, txtPort;
        FlatButton btnConnect;
        Label lblStatus;
        Led ledStatus;

        // ---------- 接收区 ----------
        RichTextBox txtRecv;
        CheckBox chkHexView, chkTimestamp;
        FlatButton btnClearRecv, btnSaveLog;
        Label lblClientsTitle;
        ListBox lstClients;

        // ---------- 发送区 ----------
        RichTextBox txtSend;
        CheckBox chkHexSend, chkCrlf, chkLoop;
        TextBox txtInterval;
        ComboBox cboEncoding;
        FlatButton btnSend, btnClearSend;
        Label lblCounters;

        // ---------- 状态 ----------
        volatile bool running = false;      // 已连接 / 监听中
        bool isServerMode = false;
        Socket serverSocket;
        ClientConn clientConn;
        List<ClientConn> conns = new List<ClientConn>();
        readonly object connsLock = new object();
        long rxBytes = 0, txBytes = 0;
        System.Windows.Forms.Timer loopTimer;

        public MainForm()
        {
            BuildUi();
            loopTimer = new System.Windows.Forms.Timer();
            loopTimer.Interval = 1000;
            loopTimer.Tick += delegate { if (running) SendData(true); };
            UpdateCounters();
            SetStatus("待机 · 未连接", Theme.Idle);
            FormClosing += OnFormClosing;
        }

        // ================= 界面 =================
        void BuildUi()
        {
            Text = "TCP 调试助手";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1080, 740);
            MinimumSize = new Size(920, 620);
            Font = Theme.Ui;
            BackColor = Theme.Canvas;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(10);
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 196));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            Controls.Add(root);

            // ---------- 第0行：控制条 ----------
            Card barCard = new Card();
            barCard.Dock = DockStyle.Fill;
            barCard.Margin = new Padding(0, 0, 0, 8);
            barCard.Padding = new Padding(1);

            TableLayoutPanel bar = new TableLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.ColumnCount = 2;
            bar.RowCount = 1;
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            barCard.Controls.Add(bar);

            FlowLayoutPanel barLeft = new FlowLayoutPanel();
            barLeft.Dock = DockStyle.Fill;
            barLeft.WrapContents = false;
            barLeft.Padding = new Padding(10, 8, 0, 0);

            Label cap1 = SmallCap("模式");
            barLeft.Controls.Add(cap1);

            btnModeClient = new FlatButton();
            btnModeClient.Text = "客户端";
            btnModeClient.Width = 76;
            btnModeClient.Margin = new Padding(0, 0, 0, 0);
            btnModeClient.Click += delegate { SetMode(false); };
            barLeft.Controls.Add(btnModeClient);

            btnModeServer = new FlatButton();
            btnModeServer.Text = "服务器";
            btnModeServer.Width = 76;
            btnModeServer.Margin = new Padding(4, 0, 14, 0);
            btnModeServer.Click += delegate { SetMode(true); };
            barLeft.Controls.Add(btnModeServer);

            barLeft.Controls.Add(SmallCap("地址"));
            txtHost = new TextBox();
            txtHost.Text = "127.0.0.1";
            txtHost.Width = 148;
            txtHost.BorderStyle = BorderStyle.FixedSingle;
            txtHost.BackColor = Theme.Surface;
            txtHost.ForeColor = Theme.Ink;
            txtHost.Margin = new Padding(0, 3, 14, 0);
            barLeft.Controls.Add(txtHost);

            barLeft.Controls.Add(SmallCap("端口"));
            txtPort = new TextBox();
            txtPort.Text = "8899";
            txtPort.Width = 62;
            txtPort.BorderStyle = BorderStyle.FixedSingle;
            txtPort.BackColor = Theme.Surface;
            txtPort.ForeColor = Theme.Ink;
            txtPort.Margin = new Padding(0, 3, 14, 0);
            barLeft.Controls.Add(txtPort);

            btnConnect = new FlatButton();
            btnConnect.Text = "连 接";
            btnConnect.Width = 96;
            btnConnect.Margin = new Padding(0, 0, 0, 0);
            btnConnect.Click += delegate { OnConnectClick(); };
            barLeft.Controls.Add(btnConnect);

            bar.Controls.Add(barLeft, 0, 0);

            FlowLayoutPanel barRight = new FlowLayoutPanel();
            barRight.AutoSize = true;
            barRight.WrapContents = false;
            barRight.Padding = new Padding(0, 0, 12, 0);
            ledStatus = new Led();
            barRight.Controls.Add(ledStatus);
            lblStatus = new Label();
            lblStatus.Text = "待机 · 未连接";
            lblStatus.AutoSize = true;
            lblStatus.ForeColor = Theme.Sub;
            lblStatus.Margin = new Padding(4, 5, 0, 0);
            barRight.Controls.Add(lblStatus);
            bar.Controls.Add(barRight, 1, 0);

            root.Controls.Add(barCard, 0, 0);

            // ---------- 第1行：接收区 + 客户端列表 ----------
            TableLayoutPanel mid = new TableLayoutPanel();
            mid.Dock = DockStyle.Fill;
            mid.ColumnCount = 2;
            mid.RowCount = 1;
            mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 232));

            Card recvCard = new Card();
            recvCard.Dock = DockStyle.Fill;
            recvCard.Margin = new Padding(0, 0, 8, 8);

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

            // 标题栏最后加入 => 最先停靠，内容区才不会被压住
            Panel recvHead = MakeRecvHead();
            recvCard.Controls.Add(recvHead);
            mid.Controls.Add(recvCard, 0, 0);

            Card cliCard = new Card();
            cliCard.Dock = DockStyle.Fill;
            cliCard.Margin = new Padding(0, 0, 0, 8);
            lblClientsTitle = new Label();
            lblClientsTitle.Text = "客户端 · CLIENTS";
            lblClientsTitle.Dock = DockStyle.Fill;
            lblClientsTitle.TextAlign = ContentAlignment.MiddleLeft;
            lblClientsTitle.ForeColor = Theme.Sub;
            lblClientsTitle.Font = Theme.UiBold;
            lblClientsTitle.Padding = new Padding(10, 0, 0, 0);
            Panel cliHead = MakeHead(lblClientsTitle);

            lstClients = new ListBox();
            lstClients.Dock = DockStyle.Fill;
            lstClients.BorderStyle = BorderStyle.None;
            lstClients.BackColor = Theme.Surface;
            lstClients.ForeColor = Theme.Ink;
            lstClients.Font = new Font("Consolas", 9.5F);
            lstClients.IntegralHeight = false;
            lstClients.SelectionMode = SelectionMode.MultiExtended;
            cliCard.Controls.Add(lstClients);
            cliCard.Controls.Add(cliHead);
            mid.Controls.Add(cliCard, 1, 0);

            root.Controls.Add(mid, 0, 1);

            // ---------- 第2行：发送区 ----------
            Card sendCard = new Card();
            sendCard.Dock = DockStyle.Fill;
            sendCard.Margin = new Padding(0, 0, 0, 8);

            btnSend = new FlatButton();
            btnSend.Text = "发 送";
            btnSend.Width = 92;
            btnSend.Margin = new Padding(0, 3, 6, 0);
            btnSend.Click += delegate { SendData(false); };
            btnClearSend = new FlatButton();
            btnClearSend.Text = "清空";
            btnClearSend.Width = 66;
            btnClearSend.Margin = new Padding(0, 3, 10, 0);
            btnClearSend.Click += delegate { txtSend.Clear(); };

            FlowLayoutPanel optRow = new FlowLayoutPanel();
            optRow.Dock = DockStyle.Top;
            optRow.Height = 32;
            optRow.WrapContents = false;
            optRow.BackColor = Theme.Surface;
            optRow.Padding = new Padding(10, 4, 0, 0);

            chkHexSend = new CheckBox();
            chkHexSend.Text = "HEX 发送";
            chkHexSend.AutoSize = true;
            chkHexSend.ForeColor = Theme.Ink;
            chkHexSend.Margin = new Padding(0, 4, 14, 0);
            optRow.Controls.Add(chkHexSend);

            chkCrlf = new CheckBox();
            chkCrlf.Text = "末尾加 \\r\\n";
            chkCrlf.AutoSize = true;
            chkCrlf.Checked = true;
            chkCrlf.ForeColor = Theme.Ink;
            chkCrlf.Margin = new Padding(0, 4, 14, 0);
            optRow.Controls.Add(chkCrlf);

            optRow.Controls.Add(SmallCap("编码"));
            cboEncoding = new ComboBox();
            cboEncoding.DropDownStyle = ComboBoxStyle.DropDownList;
            cboEncoding.FlatStyle = FlatStyle.Flat;
            cboEncoding.BackColor = Theme.Surface;
            cboEncoding.ForeColor = Theme.Ink;
            cboEncoding.Items.Add("UTF-8");
            cboEncoding.Items.Add("GBK");
            cboEncoding.Items.Add("ASCII");
            cboEncoding.SelectedIndex = 0;
            cboEncoding.Width = 78;
            cboEncoding.Margin = new Padding(0, 3, 14, 0);
            optRow.Controls.Add(cboEncoding);

            chkLoop = new CheckBox();
            chkLoop.Text = "循环发送";
            chkLoop.AutoSize = true;
            chkLoop.ForeColor = Theme.Ink;
            chkLoop.Margin = new Padding(0, 4, 6, 0);
            chkLoop.CheckedChanged += delegate { ApplyLoopTimer(); };
            optRow.Controls.Add(chkLoop);

            txtInterval = new TextBox();
            txtInterval.Text = "1000";
            txtInterval.Width = 52;
            txtInterval.BorderStyle = BorderStyle.FixedSingle;
            txtInterval.BackColor = Theme.Surface;
            txtInterval.ForeColor = Theme.Ink;
            txtInterval.Margin = new Padding(0, 3, 2, 0);
            txtInterval.TextChanged += delegate { ApplyLoopTimer(); };
            optRow.Controls.Add(txtInterval);

            Label lms = new Label();
            lms.Text = "ms";
            lms.AutoSize = true;
            lms.ForeColor = Theme.Sub;
            lms.Margin = new Padding(0, 6, 0, 0);
            optRow.Controls.Add(lms);

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
            sendCard.Controls.Add(optRow);
            sendCard.Controls.Add(MakeHead("发送 · TX", btnSend, btnClearSend));

            root.Controls.Add(sendCard, 0, 2);

            // ---------- 第3行：状态栏 ----------
            TableLayoutPanel sb = new TableLayoutPanel();
            sb.Dock = DockStyle.Fill;
            sb.ColumnCount = 2;
            sb.RowCount = 1;
            sb.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sb.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            lblCounters = new Label();
            lblCounters.Dock = DockStyle.Fill;
            lblCounters.TextAlign = ContentAlignment.MiddleLeft;
            lblCounters.ForeColor = Theme.Sub;
            lblCounters.Padding = new Padding(2, 0, 0, 0);
            sb.Controls.Add(lblCounters, 0, 0);

            Label hint = new Label();
            hint.Text = "Ctrl+Enter 发送";
            hint.AutoSize = true;
            hint.ForeColor = Theme.Sub;
            hint.TextAlign = ContentAlignment.MiddleRight;
            hint.Padding = new Padding(0, 0, 2, 0);
            sb.Controls.Add(hint, 1, 0);

            root.Controls.Add(sb, 0, 3);

            SetMode(false);
        }

        // ---- 界面小工具 ----
        static Label SmallCap(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = Theme.Sub;
            l.Margin = new Padding(0, 6, 6, 0);
            return l;
        }

        // 带底部细分隔线的标题栏，右侧可放控件
        Panel MakeHead(string title, params Control[] rightItems)
        {
            Label t = new Label();
            t.Text = title;
            t.Dock = DockStyle.Fill;
            t.TextAlign = ContentAlignment.MiddleLeft;
            t.ForeColor = Theme.Sub;
            t.Font = Theme.UiBold;
            t.Padding = new Padding(10, 0, 0, 0);
            return MakeHead(t, rightItems);
        }

        Panel MakeHead(Label title, params Control[] rightItems)
        {
            TableLayoutPanel head = new TableLayoutPanel();
            head.Dock = DockStyle.Top;
            head.Height = 32;
            head.BackColor = Theme.Surface;
            head.ColumnCount = 2;
            head.RowCount = 1;
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            head.Controls.Add(title, 0, 0);

            if (rightItems != null && rightItems.Length > 0)
            {
                FlowLayoutPanel flow = new FlowLayoutPanel();
                flow.AutoSize = true;
                flow.WrapContents = false;
                flow.Padding = new Padding(0, 0, 8, 0);
                foreach (Control c in rightItems)
                {
                    c.Margin = new Padding(c.Margin.Left, 3, c.Margin.Right, 0);
                    flow.Controls.Add(c);
                }
                head.Controls.Add(flow, 1, 0);
            }
            head.Paint += delegate(object s, PaintEventArgs e)
            {
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
            chkHexView.Margin = new Padding(0, 4, 14, 0);

            chkTimestamp = new CheckBox();
            chkTimestamp.Text = "时间戳";
            chkTimestamp.AutoSize = true;
            chkTimestamp.Checked = true;
            chkTimestamp.ForeColor = Theme.Ink;
            chkTimestamp.Margin = new Padding(0, 4, 14, 0);

            btnClearRecv = new FlatButton();
            btnClearRecv.Text = "清空";
            btnClearRecv.Width = 62;
            btnClearRecv.Margin = new Padding(0, 0, 6, 0);
            btnClearRecv.Click += delegate { txtRecv.Clear(); };

            btnSaveLog = new FlatButton();
            btnSaveLog.Text = "保存日志";
            btnSaveLog.Width = 80;
            btnSaveLog.Click += delegate { SaveLog(); };

            return MakeHead("接收 · RX", chkHexView, chkTimestamp, btnClearRecv, btnSaveLog);
        }

        // ================= 模式与状态 =================
        void SetMode(bool server)
        {
            isServerMode = server;
            btnModeClient.SetActive(!server);
            btnModeServer.SetActive(server);
            txtHost.Enabled = !server && !running;
            lblClientsTitle.Text = server ? "客户端 · CLIENTS（不选=广播）" : "客户端 · CLIENTS（服务器模式）";
            lstClients.Enabled = server;
            btnConnect.Text = running ? (server ? "停止监听" : "断 开") : (server ? "开始监听" : "连 接");
        }

        void SetStatus(string text, Color color)
        {
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
            btnModeClient.Enabled = !run;
            btnModeServer.Enabled = !run;
            txtPort.Enabled = !run;
            txtHost.Enabled = !run && !isServerMode;
        }

        // ================= 连接 / 监听 =================
        void OnConnectClick()
        {
            if (running) { StopAll(); return; }
            int port;
            if (!int.TryParse(txtPort.Text.Trim(), out port) || port < 1 || port > 65535)
            {
                MessageBox.Show("端口必须是 1~65535 的数字", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (isServerMode) StartServer(port);
            else StartClient(txtHost.Text.Trim(), port);
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
                try { c.Label = s.RemoteEndPoint.ToString(); }
                catch { c.Label = "客户端"; }
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
        }

        void AppendSys(string s) { AppendRaw(s + "\r\n", Theme.Sub); }
        void AppendErr(string s) { AppendRaw(s + "\r\n", Theme.Err); }

        void AppendRaw(string s, Color color)
        {
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
            if (!lblCounters.IsHandleCreated) return;
            if (lblCounters.InvokeRequired) { try { lblCounters.BeginInvoke(new Action(UpdateCounters)); } catch { } return; }
            long rx = Interlocked.Read(ref rxBytes);
            long tx = Interlocked.Read(ref txBytes);
            lblCounters.Text = "RX " + FmtBytes(rx) + "    TX " + FmtBytes(tx);
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
            txtHost.Text = host;
            txtPort.Text = port;
        }
        internal void TestConnect() { btnConnect.PerformClick(); }
        internal void TestSend(string text) { txtSend.Text = text; btnSend.PerformClick(); }
        internal string RecvText { get { return txtRecv.Text; } }
        internal bool IsRunning { get { return running; } }
        internal void TestSetHexSend(bool hex) { chkHexSend.Checked = hex; }
        internal void TestSetHexView(bool hex) { chkHexView.Checked = hex; }
    }
}
