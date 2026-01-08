using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RouteForwarder
{
    public partial class MainForm : Form
    {
        // 加上 ? 表示这些字段可以暂时为 null，消除 CS8618 警告
        private ComboBox? cbInterface, cbGateway, cbRouteList, cbAction;
        private CheckBox? chkForward, chkExtra;
        private TextBox? txtExtraRoutes;
        private Button? btnPrint, btnExecute;
        
        private NotifyIcon? trayIcon;
        private ContextMenuStrip? trayMenu;

        public MainForm()
        {
            this.Font = new Font("Tahoma", 8.25F);
            this.Text = "RouteForwarder - 永久路由版";
            this.ClientSize = new Size(480, 320);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // 1. 先初始化托盘
            InitTrayIcon();

            // 2. 初始化控件
            chkForward = new CheckBox() { Text = "路由转发", Left = 180, Top = 12, AutoSize = true, Checked = true };
            chkExtra = new CheckBox() { Text = "额外路由条目", Left = 280, Top = 12, AutoSize = true, Checked = true };

            int labelLeft = 15, comboLeft = 85, spacing = 32;
            
            Label lbl1 = new Label() { Text = "网卡接口:", Left = labelLeft, Top = 48, Width = 65 };
            cbInterface = new ComboBox() { Left = comboLeft, Top = 45, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            cbInterface.Items.AddRange(new string[] { "WLAN", "以太网" });
            cbInterface.SelectedIndex = 0;

            Label lbl2 = new Label() { Text = "默认网关:", Left = labelLeft, Top = 48 + spacing, Width = 65 };
            cbGateway = new ComboBox() { Left = comboLeft, Top = 45 + spacing, Width = 180 };
            
            Label lbl3 = new Label() { Text = "路由条目:", Left = labelLeft, Top = 48 + spacing * 2, Width = 65 };
            cbRouteList = new ComboBox() { Left = comboLeft, Top = 45 + spacing * 2, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            cbRouteList.Items.Add("china_ip_list.txt"); cbRouteList.SelectedIndex = 0;

            Label lbl4 = new Label() { Text = "路由动作:", Left = labelLeft, Top = 48 + spacing * 3, Width = 65 };
            cbAction = new ComboBox() { Left = comboLeft, Top = 45 + spacing * 3, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            cbAction.Items.AddRange(new string[] { "添加路由", "删除路由" }); cbAction.SelectedIndex = 0;

            btnPrint = new Button() { Text = "查看路由表", Left = 35, Top = 210, Width = 100, Height = 30 };
            btnExecute = new Button() { Text = "执行", Left = 160, Top = 210, Width = 100, Height = 30 };

            txtExtraRoutes = new TextBox() { 
                Left = 285, Top = 45, Width = 175, Height = 195, 
                Multiline = true, ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 8F),
                Text = "114.114.114.114\r\n223.5.5.5\r\n8.8.8.8\r\nwww.msftconnecttest.com"
            };

            LinkLabel link = new LinkLabel() { Text = "https://youtube.com/@bulianglin", Left = 15, Top = 285, Width = 250 };

            this.Controls.AddRange(new Control[] { chkForward, chkExtra, lbl1, cbInterface, lbl2, cbGateway, lbl3, cbAction, btnPrint, btnExecute, txtExtraRoutes, link });

            FillGatewayOptions();

            // 事件处理
            btnExecute.Click += ExecuteAction;
            btnPrint.Click += (s, e) => {
                ProcessStartInfo psi = new ProcessStartInfo("cmd", "/c route print & pause") { UseShellExecute = true };
                Process.Start(psi);
            };

            this.FormClosing += MainForm_FormClosing;
        }

        private void InitTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("显示界面", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; });
            trayMenu.Items.Add("退出程序", null, (s, e) => { if (trayIcon != null) trayIcon.Visible = false; Environment.Exit(0); });

            trayIcon = new NotifyIcon()
            {
                Text = "RouteForwarder",
                Icon = SystemIcons.Application,
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            
            trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; };
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                var result = MessageBox.Show("是否最小化到系统托盘？\n(点击“否”将完全退出)", "退出提示", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    e.Cancel = true;
                    this.Hide();
                }
                else if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }

        private void FillGatewayOptions()
        {
            if (cbGateway == null) return;
            try {
                var gateways = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                    .Select(g => g.Address.ToString())
                    .Where(g => g != "0.0.0.0")
                    .ToList();

                cbGateway.Items.Clear();
                foreach (var gw in gateways) cbGateway.Items.Add(gw);

                string? v4Gateway = gateways.FirstOrDefault(g => g.Contains("."));
                if (v4Gateway != null) cbGateway.Text = v4Gateway;
                else if (gateways.Count > 0) cbGateway.SelectedIndex = 0;
            } catch { cbGateway.Text = "192.168.1.1"; }
        }

        private async void ExecuteAction(object? sender, EventArgs e)
        {
            if (cbAction == null || cbGateway == null || txtExtraRoutes == null || btnExecute == null) return;

            string action = cbAction.Text == "添加路由" ? "add" : "delete";
            string gw = cbGateway.Text;
            string[] lines = txtExtraRoutes.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            btnExecute.Enabled = false;
            btnExecute.Text = "处理中...";

            foreach (var line in lines)
            {
                try {
                    string target = line.Trim();
                    if (string.IsNullOrEmpty(target)) continue;

                    IPAddress[] ips = await Dns.GetHostAddressesAsync(target);
                    foreach (var ip in ips) {
                        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                            RunCmd("route", $"-p {action} {ip} mask 255.255.255.255 {gw} metric 1");
                        } else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) {
                            string ns = action == "add" ? "add" : "delete";
                            RunCmd("netsh", $"interface ipv6 {ns} route {ip}/128 interface=1 store=persistent");
                        }
                    }
                } catch { }
            }

            btnExecute.Enabled = true;
            btnExecute.Text = "执行";
            MessageBox.Show("执行完毕！路由已持久化。");
        }

        private void RunCmd(string fileName, string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo(fileName, args)
            {
                Verb = "runas",
                CreateNoWindow = true,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            try { Process.Start(psi); } catch { }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
