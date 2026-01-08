package main

import (
	"fmt"
	"net"
	"os"
	"os/exec"
	"strings"
	"syscall" // 增加了这个包，用于处理兼容性

	"github.com/lxn/walk"
	. "github.com/lxn/walk/declarative"
	"github.com/lxn/win"
)

func init() {
	if !strings.Contains(strings.Join(os.Args, ""), "-noderun") {
		// 修复 win.IsUserAnAdmin 报错
		if !win.IsUserAnAdmin() {
			exe, _ := os.Executable()
			cwd, _ := os.Getwd()
			args := append([]string{"-noderun"}, os.Args[1:]...)
			argStr := strings.Join(args, " ")

			// 使用 syscall 替代 win.UTF16PtrFromString 解决兼容性报错
			verbPtr, _ := syscall.UTF16PtrFromString("runas")
			exePtr, _ := syscall.UTF16PtrFromString(exe)
			cwdPtr, _ := syscall.UTF16PtrFromString(cwd)
			argPtr, _ := syscall.UTF16PtrFromString(argStr)

			win.ShellExecute(0, verbPtr, exePtr, argPtr, cwdPtr, win.SW_NORMAL)
			os.Exit(0)
		}
	}
}

func main() {
	var mw *walk.MainWindow
	var outTE *walk.TextEdit
	var remoteAddr *walk.LineEdit

	if _, err := (MainWindow{
		AssignTo: &mw,
		Title:    "RouteForwarder 增强版 (IPv6 & Domain Support)",
		MinSize:  Size{Width: 500, Height: 400},
		Layout:   VBox{},
		Children: []Widget{
			Label{Text: "目标地址 (输入域名如 google.com 或 IP):"},
			LineEdit{AssignTo: &remoteAddr, Text: "www.google.com"},
			Composite{
				Layout: HBox{MarginsZero: true},
				Children: []Widget{
					PushButton{
						Text: "添加路由",
						OnClicked: func() {
							handleRoute(remoteAddr.Text(), "add", outTE)
						},
					},
					PushButton{
						Text: "删除路由",
						OnClicked: func() {
							handleRoute(remoteAddr.Text(), "delete", outTE)
						},
					},
				},
			},
			Label{Text: "操作日志:"},
			TextEdit{AssignTo: &outTE, ReadOnly: true, VScroll: true},
			PushButton{
				Text: "查看系统路由表 (Print)",
				OnClicked: func() {
					exec.Command("cmd", "/c", "start route print").Run()
				},
			},
		},
	}.Run()); err != nil {
		fmt.Println(err)
	}
}

func handleRoute(input, action string, log *walk.TextEdit) {
	log.AppendText(fmt.Sprintf("[%s] 正在处理: %s\r\n", action, input))
	
	ips, err := net.LookupIP(input)
	if err != nil {
		log.AppendText(fmt.Sprintf("错误: 无法解析地址 %v\r\n", err))
		return
	}

	for _, ip := range ips {
		var cmd *exec.Cmd
		if ip.To4() != nil {
			cmd = exec.Command("route", action, ip.String(), "mask", "255.255.255.255")
		} else {
			if action == "add" {
				cmd = exec.Command("netsh", "interface", "ipv6", "add", "route", ip.String()+"/128", "interface=1")
			} else {
				cmd = exec.Command("netsh", "interface", "ipv6", "delete", "route", ip.String()+"/128", "interface=1")
			}
		}

		output, _ := cmd.CombinedOutput()
		log.AppendText(fmt.Sprintf("结果 (%s): %s\r\n", ip.String(), string(output)))
	}
	log.AppendText("--- 操作完成 ---\r\n\r\n")
}
