using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Linq;

namespace RouteForwarder
{
    public partial class MainForm : Form
    {
        private ComboBox cbInterface, cbGateway, cbRouteList, cbAction;
        private CheckBox chkForward, chkExtra;
        private TextBox txtExtraRoutes;
        private Button btnPrint, btnExecute;

        public MainForm()
        {
            this.Text = "RouteForwarder - 路由转发工具";
            this.Size = new Size(550, 400);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // 顶部复选框
            chkForward = new CheckBox() { Text = "路由转发", Left = 200, Top = 10, AutoSize = true };
            chkExtra = new CheckBox() { Text = "额外路由条目", Left = 310, Top = 10, AutoSize = true, Checked = true };

            // 左侧标签与下拉框
            Label lbl1 = new Label() { Text = "网卡接口:", Left = 20, Top = 45, Width = 80 };
            cbInterface = new ComboBox() { Left = 100, Top = 42, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cbInterface.Items.Add("WLAN"); cbInterface.SelectedIndex = 0;

            Label lbl2 = new Label() { Text = "默认网关:", Left = 20, Top = 85, Width = 80 };
            cbGateway = new ComboBox() { Left = 100, Top = 82, Width = 200 };
            cbGateway.Text = GetDefaultGateway();

            Label lbl3 = new Label() { Text = "路由条目:", Left = 20, Top = 125, Width = 80 };
            cbRouteList = new ComboBox() { Left = 100, Top = 122, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cbRouteList.Items.Add("china_ip_list.txt"); cbRouteList.SelectedIndex = 0;

            Label lbl4 = new Label() { Text = "路由动作:", Left = 20, Top = 165, Width = 80 };
            cbAction = new ComboBox() { Left = 100, Top = 162, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cbAction.Items.AddRange(new string[] { "添加路由", "删除路由" }); cbAction.SelectedIndex = 0;

            // 底部按钮
            btnPrint = new Button() { Text = "查看路由表", Left = 40, Top = 240, Width = 110, Height = 35 };
            btnExecute = new Button() { Text = "执行", Left = 180, Top = 240, Width = 110, Height = 35 };

            // 右侧文本框
            txtExtraRoutes = new TextBox() { 
                Left = 310, Top = 42, Width = 200, Height = 233, 
                Multiline = true, ScrollBars = ScrollBars.Both,
                Text = "114.114.114.114\r\n223.5.5.5\r\n8.8.8.8\r\nwww.msftconnecttest.com"
            };

            // 底部链接
            LinkLabel link = new LinkLabel() { Text = "https://youtube.com/@bulianglin", Left = 20, Top = 310, Width = 300 };

            this.Controls.AddRange(new Control[] { chkForward, chkExtra, lbl1, cbInterface, lbl2, cbGateway, lbl3, cbRouteList, lbl4, cbAction, btnPrint, btnExecute, txtExtraRoutes, link });

            btnExecute.Click += ExecuteAction;
            btnPrint.Click += (s, e) => Process.Start("cmd", "/c route print & pause");
        }

        private string GetDefaultGateway()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                .Select(g => g.Address.ToString())
                .FirstOrDefault() ?? "192.168.1.1";
        }

        private async void ExecuteAction(object? sender, EventArgs e)
        {
            string action = cbAction.Text == "添加路由" ? "add" : "delete";
            string gw = cbGateway.Text;
            string[] lines = txtExtraRoutes.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                try {
                    IPAddress[] ips = await Dns.GetHostAddressesAsync(line.Trim());
                    foreach (var ip in ips) {
                        string cmd = "", args = "";
                        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                            cmd = "route";
                            args = $"{action} {ip} mask 255.255.255.255 {gw} metric 1";
                        } else {
                            cmd = "netsh";
                            string ns = action == "add" ? "add" : "delete";
                            args = $"interface ipv6 {ns} route {ip}/128 interface=1";
                        }
                        RunAsAdmin(cmd, args);
                    }
                } catch { }
            }
            MessageBox.Show("操作执行完毕！");
        }

        private void RunAsAdmin(string cmd, string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo(cmd, args) { Verb = "runas", CreateNoWindow = true, UseShellExecute = true };
            try { Process.Start(psi); } catch { }
        }

        [STAThread] static void Main() { Application.EnableVisualStyles(); Application.Run(new MainForm()); }
    }
}
