// ============================================================
//  TCP 调试助手 (C# WinForms, 单文件, 无第三方依赖)
//  功能：TCP 客户端 / TCP 服务器双模式、HEX/ASCII 收发、
//        多客户端管理(定向/广播)、时间戳、循环发送、
//        RX/TX 字节计数、日志保存
//  编译：csc /target:winexe /codepage:65001 /out:TCP调试助手.exe
//        /r:System.Windows.Forms.dll /r:System.Drawing.dll TCP调试助手.cs
// ============================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TcpDebugger
{
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
        // ---------- 连接区 ----------
        ComboBox cboMode;
        TextBox txtHost, txtPort;
        Button btnConnect;
        Label lblStatus;

        // ---------- 接收区 ----------
        RichTextBox txtRecv;
        CheckBox chkHexView, chkTimestamp;
        Button btnClearRecv, btnSaveLog;
        Label lblClientsTitle;
        ListBox lstClients;

        // ---------- 发送区 ----------
        RichTextBox txtSend;
        CheckBox chkHexSend, chkCrlf, chkLoop;
        TextBox txtInterval;
        ComboBox cboEncoding;
        Button btnSend, btnClearSend;
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
            SetStatus("未连接", Color.Gray);
            FormClosing += OnFormClosing;
        }

        // ================= 界面 =================
        void BuildUi()
        {
            Text = "TCP 调试助手";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1060, 720);
            MinimumSize = new Size(880, 600);
            Font = new Font("Microsoft YaHei UI", 9F);

            TableLayoutPanel main = new TableLayoutPanel();
            main.Dock = DockStyle.Fill;
            main.Padding = new Padding(8);
            main.ColumnCount = 1;
            main.RowCount = 6;
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            Controls.Add(main);

            // ----- 第0行：连接参数 -----
            FlowLayoutPanel top = new FlowLayoutPanel();
            top.Dock = DockStyle.Fill;
            top.WrapContents = false;
            top.Padding = new Padding(0, 6, 0, 0);

            Label l1 = new Label();
            l1.Text = "模式:";
            l1.AutoSize = true;
            l1.Margin = new Padding(0, 8, 2, 0);
            top.Controls.Add(l1);

            cboMode = new ComboBox();
            cboMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cboMode.Items.Add("TCP 客户端");
            cboMode.Items.Add("TCP 服务器");
            cboMode.SelectedIndex = 0;
            cboMode.Width = 108;
            cboMode.Margin = new Padding(0, 4, 10, 0);
            cboMode.SelectedIndexChanged += delegate { OnModeChanged(); };
            top.Controls.Add(cboMode);

            Label l2 = new Label();
            l2.Text = "地址:";
            l2.AutoSize = true;
            l2.Margin = new Padding(0, 8, 2, 0);
            top.Controls.Add(l2);

            txtHost = new TextBox();
            txtHost.Text = "127.0.0.1";
            txtHost.Width = 150;
            txtHost.Margin = new Padding(0, 4, 10, 0);
            top.Controls.Add(txtHost);

            Label l3 = new Label();
            l3.Text = "端口:";
            l3.AutoSize = true;
            l3.Margin = new Padding(0, 8, 2, 0);
            top.Controls.Add(l3);

            txtPort = new TextBox();
            txtPort.Text = "8899";
            txtPort.Width = 64;
            txtPort.Margin = new Padding(0, 4, 10, 0);
            top.Controls.Add(txtPort);

            btnConnect = new Button();
            btnConnect.Text = "连接";
            btnConnect.Width = 92;
            btnConnect.Height = 26;
            btnConnect.Margin = new Padding(0, 4, 12, 0);
            btnConnect.Click += delegate { OnConnectClick(); };
            top.Controls.Add(btnConnect);

            lblStatus = new Label();
            lblStatus.Text = "未连接";
            lblStatus.AutoSize = true;
            lblStatus.Margin = new Padding(0, 8, 0, 0);
            top.Controls.Add(lblStatus);

            main.Controls.Add(top, 0, 0);

            // ----- 第1行：接收工具条 -----
            FlowLayoutPanel rtb = new FlowLayoutPanel();
            rtb.Dock = DockStyle.Fill;
            rtb.WrapContents = false;
            rtb.Padding = new Padding(0, 4, 0, 0);

            chkHexView = new CheckBox();
            chkHexView.Text = "HEX 显示";
            chkHexView.AutoSize = true;
            chkHexView.Margin = new Padding(0, 4, 14, 0);
            rtb.Controls.Add(chkHexView);

            chkTimestamp = new CheckBox();
            chkTimestamp.Text = "时间戳";
            chkTimestamp.AutoSize = true;
            chkTimestamp.Checked = true;
            chkTimestamp.Margin = new Padding(0, 4, 14, 0);
            rtb.Controls.Add(chkTimestamp);

            btnClearRecv = new Button();
            btnClearRecv.Text = "清空接收";
            btnClearRecv.Width = 84;
            btnClearRecv.Height = 24;
            btnClearRecv.Margin = new Padding(0, 0, 8, 0);
            btnClearRecv.Click += delegate { txtRecv.Clear(); };
            rtb.Controls.Add(btnClearRecv);

            btnSaveLog = new Button();
            btnSaveLog.Text = "保存日志";
            btnSaveLog.Width = 84;
            btnSaveLog.Height = 24;
            btnSaveLog.Click += delegate { SaveLog(); };
            rtb.Controls.Add(btnSaveLog);

            main.Controls.Add(rtb, 0, 1);

            // ----- 第2行：接收区 + 客户端列表 -----
            TableLayoutPanel recvWrap = new TableLayoutPanel();
            recvWrap.Dock = DockStyle.Fill;
            recvWrap.ColumnCount = 2;
            recvWrap.RowCount = 1;
            recvWrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            recvWrap.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 215));

            txtRecv = new RichTextBox();
            txtRecv.Dock = DockStyle.Fill;
            txtRecv.ReadOnly = true;
            txtRecv.BackColor = Color.White;
            txtRecv.Font = new Font("Consolas", 10F);
            txtRecv.HideSelection = false;
            recvWrap.Controls.Add(txtRecv, 0, 0);

            TableLayoutPanel cliWrap = new TableLayoutPanel();
            cliWrap.Dock = DockStyle.Fill;
            cliWrap.ColumnCount = 1;
            cliWrap.RowCount = 2;
            cliWrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            cliWrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            lblClientsTitle = new Label();
            lblClientsTitle.Text = "在线客户端（未选=广播）";
            lblClientsTitle.Dock = DockStyle.Fill;
            lblClientsTitle.TextAlign = ContentAlignment.MiddleLeft;
            lblClientsTitle.ForeColor = Color.DimGray;
            cliWrap.Controls.Add(lblClientsTitle, 0, 0);

            lstClients = new ListBox();
            lstClients.Dock = DockStyle.Fill;
            lstClients.SelectionMode = SelectionMode.MultiExtended;
            cliWrap.Controls.Add(lstClients, 0, 1);

            recvWrap.Controls.Add(cliWrap, 1, 0);
            main.Controls.Add(recvWrap, 0, 2);

            // ----- 第3行：发送工具条 -----
            FlowLayoutPanel stb = new FlowLayoutPanel();
            stb.Dock = DockStyle.Fill;
            stb.WrapContents = false;
            stb.Padding = new Padding(0, 4, 0, 0);

            chkHexSend = new CheckBox();
            chkHexSend.Text = "HEX 发送";
            chkHexSend.AutoSize = true;
            chkHexSend.Margin = new Padding(0, 4, 14, 0);
            stb.Controls.Add(chkHexSend);

            chkCrlf = new CheckBox();
            chkCrlf.Text = "末尾加 \\r\\n";
            chkCrlf.AutoSize = true;
            chkCrlf.Checked = true;
            chkCrlf.Margin = new Padding(0, 4, 14, 0);
            stb.Controls.Add(chkCrlf);

            Label l4 = new Label();
            l4.Text = "编码:";
            l4.AutoSize = true;
            l4.Margin = new Padding(0, 8, 2, 0);
            stb.Controls.Add(l4);

            cboEncoding = new ComboBox();
            cboEncoding.DropDownStyle = ComboBoxStyle.DropDownList;
            cboEncoding.Items.Add("UTF-8");
            cboEncoding.Items.Add("GBK");
            cboEncoding.Items.Add("ASCII");
            cboEncoding.SelectedIndex = 0;
            cboEncoding.Width = 78;
            cboEncoding.Margin = new Padding(0, 4, 14, 0);
            stb.Controls.Add(cboEncoding);

            chkLoop = new CheckBox();
            chkLoop.Text = "循环发送";
            chkLoop.AutoSize = true;
            chkLoop.Margin = new Padding(0, 4, 6, 0);
            chkLoop.CheckedChanged += delegate { ApplyLoopTimer(); };
            stb.Controls.Add(chkLoop);

            txtInterval = new TextBox();
            txtInterval.Text = "1000";
            txtInterval.Width = 52;
            txtInterval.Margin = new Padding(0, 4, 2, 0);
            txtInterval.TextChanged += delegate { ApplyLoopTimer(); };
            stb.Controls.Add(txtInterval);

            Label l5 = new Label();
            l5.Text = "ms";
            l5.AutoSize = true;
            l5.Margin = new Padding(0, 8, 14, 0);
            stb.Controls.Add(l5);

            btnSend = new Button();
            btnSend.Text = "发 送";
            btnSend.Width = 92;
            btnSend.Height = 24;
            btnSend.Margin = new Padding(0, 0, 8, 0);
            btnSend.Click += delegate { SendData(false); };
            stb.Controls.Add(btnSend);

            btnClearSend = new Button();
            btnClearSend.Text = "清空发送";
            btnClearSend.Width = 84;
            btnClearSend.Height = 24;
            btnClearSend.Click += delegate { txtSend.Clear(); };
            stb.Controls.Add(btnClearSend);

            main.Controls.Add(stb, 0, 3);

            // ----- 第4行：发送框 -----
            txtSend = new RichTextBox();
            txtSend.Dock = DockStyle.Fill;
            txtSend.Font = new Font("Consolas", 10F);
            txtSend.Text = "Hello TCP!";
            txtSend.KeyDown += OnSendKeyDown;
            main.Controls.Add(txtSend, 0, 4);

            // ----- 第5行：计数与提示 -----
            lblCounters = new Label();
            lblCounters.Dock = DockStyle.Fill;
            lblCounters.TextAlign = ContentAlignment.MiddleLeft;
            lblCounters.ForeColor = Color.DimGray;
            main.Controls.Add(lblCounters, 0, 5);

            OnModeChanged();
        }

        // ================= 模式与状态 =================
        void OnModeChanged()
        {
            isServerMode = cboMode.SelectedIndex == 1;
            btnConnect.Text = running ? (isServerMode ? "停止监听" : "断开") : (isServerMode ? "开始监听" : "连接");
            txtHost.Enabled = !isServerMode && !running;
            lblClientsTitle.Text = isServerMode ? "在线客户端（未选=广播）" : "在线客户端（服务器模式可用）";
            if (!isServerMode) lstClients.Enabled = false;
            else lstClients.Enabled = true;
        }

        void SetStatus(string text, Color color)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string, Color>(SetStatus), text, color); return; }
            lblStatus.Text = text;
            lblStatus.ForeColor = color;
        }

        void SetConnectUiRunning(bool run)
        {
            if (InvokeRequired) { BeginInvoke(new Action<bool>(SetConnectUiRunning), run); return; }
            btnConnect.Text = run ? (isServerMode ? "停止监听" : "断开") : (isServerMode ? "开始监听" : "连接");
            cboMode.Enabled = !run;
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
            SetStatus("连接中...", Color.DarkOrange);
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
                    SetStatus("已连接 " + c.Label, Color.Green);
                    AppendSys("[系统] 已连接到 " + c.Label);
                    RecvLoop(s, c, true);
                }
                catch (Exception ex)
                {
                    try { if (s != null) s.Close(); } catch { }
                    SetStatus("连接失败", Color.Red);
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
                SetStatus("监听失败", Color.Red);
                MessageBox.Show("无法监听端口 " + port + "：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                try { if (serverSocket != null) serverSocket.Close(); } catch { }
                serverSocket = null;
                return;
            }
            running = true;
            SetConnectUiRunning(true);
            SetStatus("监听中 0.0.0.0:" + port, Color.DarkOrange);
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
                    SetStatus("连接已断开", Color.Red);
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
            AppendRaw(pre + body + "\r\n", Color.Black);
        }

        // ================= 发送 =================
        void SendData(bool quiet)
        {
            if (!running) { if (!quiet) SetStatus("未连接", Color.Red); return; }
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
            if (targets.Count == 0) { if (!quiet) SetStatus("没有可发送的连接", Color.Red); return; }

            int ok = 0;
            foreach (Socket s in targets)
            {
                try { s.Send(data); ok++; }
                catch { }
            }
            if (ok == 0) { if (!quiet) SetStatus("发送失败（连接已断开）", Color.Red); return; }
            Interlocked.Add(ref txBytes, data.Length * ok);
            UpdateCounters();
            if (!quiet)
            {
                string pre = "";
                if (chkTimestamp.Checked) pre = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] ";
                if (isServerMode && lstClients.SelectedItems.Count == 0) pre += "<广播> ";
                AppendRaw(pre + (chkHexSend.Checked ? HexStr(data) : DecodeBytes(data)) + "\r\n", Color.Blue);
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
            SetStatus("已断开", Color.Gray);
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
                SetStatus("监听中，在线客户端 " + conns.Count + " 个", Color.DarkOrange);
        }

        void AppendSys(string s) { AppendRaw(s + "\r\n", Color.DimGray); }
        void AppendErr(string s) { AppendRaw(s + "\r\n", Color.Red); }

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
            lblCounters.Text = "RX " + FmtBytes(rx) + "    TX " + FmtBytes(tx) +
                "    |    Ctrl+Enter 发送；服务器模式选中客户端=定向，不选=广播";
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
                    SetStatus("日志已保存", Color.Green);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("保存失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ================= 自动化测试接口 =================
        internal void TestSetMode(bool server)
        {
            cboMode.SelectedIndex = server ? 1 : 0;
        }
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
