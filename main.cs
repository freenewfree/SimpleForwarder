using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Diagnostics;

namespace RouteForwarderPro
{
    public class MainForm : Form
    {
        private TextBox txtRemoteAddr;
        private RichTextBox logBox;
        private Button btnAdd, btnDel, btnPrint;

        public MainForm()
        {
            this.Text = "RouteForwarder 增强版 (C# 原生)";
            this.Size = new Size(500, 450);
            this.StartPosition = FormStartPosition.CenterScreen;

            Label lbl = new Label() { Text = "目标域名/IP:", Left = 20, Top = 20, Width = 100 };
            txtRemoteAddr = new TextBox() { Left = 120, Top = 20, Width = 320, Text = "www.google.com" };
            
            btnAdd = new Button() { Text = "添加路由", Left = 120, Top = 60, Width = 100 };
            btnDel = new Button() { Text = "删除路由", Left = 230, Top = 60, Width = 100 };
            btnPrint = new Button() { Text = "查看路由表", Left = 340, Top = 60, Width = 100 };

            logBox = new RichTextBox() { Left = 20, Top = 100, Width = 440, Height = 280, ReadOnly = true };

            this.Controls.Add(lbl);
            this.Controls.Add(txtRemoteAddr);
            this.Controls.Add(btnAdd);
            this.Controls.Add(btnDel);
            this.Controls.Add(btnPrint);
            this.Controls.Add(logBox);

            btnAdd.Click += (s, e) => HandleRoute("add");
            btnDel.Click += (s, e) => HandleRoute("delete");
            btnPrint.Click += (s, e) => Process.Start("cmd", "/c route print & pause");
        }

        private void HandleRoute(string action)
        {
            string input = txtRemoteAddr.Text.Trim();
            logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] 正在执行 {action}: {input}\n");

            try
            {
                IPAddress[] ips = Dns.GetHostAddresses(input);
                foreach (var ip in ips)
                {
                    string cmd, args;
                    if (ip.AddressFamily == AddressFamily.InterNetwork) // IPv4
                    {
                        cmd = "route";
                        args = $"{action} {ip} mask 255.255.255.255";
                    }
                    else if (ip.AddressFamily == AddressFamily.InterNetworkV6) // IPv6
                    {
                        cmd = "netsh";
                        string netshAction = action == "add" ? "add" : "delete";
                        args = $"interface ipv6 {netshAction} route {ip}/128 interface=1";
                    }
                    else continue;

                    RunCmd(cmd, args);
                }
            }
            catch (Exception ex)
            {
                logBox.AppendText($"错误: {ex.Message}\n");
            }
        }

        private void RunCmd(string cmd, string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo(cmd, args)
            {
                Verb = "runas", // 强制管理员权限
                CreateNoWindow = true,
                UseShellExecute = true
            };
            try {
                Process.Start(psi);
                logBox.AppendText($"成功发送指令: {cmd} {args}\n");
            } catch {
                logBox.AppendText("用户取消了授权或执行失败。\n");
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }
    }
}
