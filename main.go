package main

import (
	"fmt"
	"net"
	"os"
	"os/exec"
	"strings"
	"syscall"

	"github.com/lxn/walk"
	. "github.com/lxn/walk/declarative"
	"github.com/lxn/win"
)

// 使用底层 API 判断管理员权限，彻底解决 undefined 报错
func isUserAdmin() bool {
	var sid *syscall.SID
	err := syscall.AllocateAndInitializeSid(
		&syscall.SECURITY_NT_AUTHORITY,
		2,
		syscall.SECURITY_BUILTIN_DOMAIN_RID,
		syscall.DOMAIN_ALIAS_RID_ADMINS,
		0, 0, 0, 0, 0, 0,
		&sid)
	if err != nil {
		return false
	}
	defer syscall.FreeSid(sid)

	token := syscall.Token(0)
	member, err := token.IsMember(sid)
	if err != nil {
		return false
	}
	return member
}

func init() {
	if !strings.Contains(strings.Join(os.Args, ""), "-noderun") {
		if !isUserAdmin() {
			exe, _ := os.Executable()
			cwd, _ := os.Getwd()
			args := append([]string{"-noderun"}, os.Args[1:]...)
			argStr := strings.Join(args, " ")

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
