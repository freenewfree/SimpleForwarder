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
)

func main() {
	// 1. 自动请求管理员权限 (使用 Go 标准库 syscall，不再依赖 win 库，绝对不报错)
	if len(os.Args) == 1 || os.Args[1] != "-admin" {
		verbPtr, _ := syscall.UTF16PtrFromString("runas")
		exe, _ := os.Executable()
		exePtr, _ := syscall.UTF16PtrFromString(exe)
		cwd, _ := os.Getwd()
		cwdPtr, _ := syscall.UTF16PtrFromString(cwd)
		argPtr, _ := syscall.UTF16PtrFromString("-admin")

		var showCmd int32 = 1 // SW_NORMAL
		
		// 调用 Windows 底层接口
		shell32 := syscall.NewLazyDLL("shell32.dll")
		shellExecute := shell32.NewProc("ShellExecuteW")
		shellExecute.Call(0, uintptr(unsafePointer(verbPtr)), uintptr(unsafePointer(exePtr)), uintptr(unsafePointer(argPtr)), uintptr(unsafePointer(cwdPtr)), uintptr(showCmd))
		os.Exit(0)
	}

	var mw *walk.MainWindow
	var outTE *walk.TextEdit
	var remoteAddr *walk.LineEdit

	// 2. 构建图形界面 (不良林风格)
	if _, err := (MainWindow{
		AssignTo: &mw,
		Title:    "RouteForwarder 增强版",
		MinSize:  Size{Width: 500, Height: 400},
		Layout:   VBox{},
		Children: []Widget{
			Label{Text: "目标域名/IP (如: google.com):"},
			LineEdit{AssignTo: &remoteAddr, Text: "www.google.com"},
			Composite{
				Layout: HBox{MarginsZero: true},
				Children: []Widget{
					PushButton{
						Text: "添加路由 (Add)",
						OnClicked: func() {
							handleRoute(remoteAddr.Text(), "add", outTE)
						},
					},
					PushButton{
						Text: "删除路由 (Delete)",
						OnClicked: func() {
							handleRoute(remoteAddr.Text(), "delete", outTE)
						},
					},
				},
			},
			Label{Text: "运行日志:"},
			TextEdit{AssignTo: &outTE, ReadOnly: true, VScroll: true},
			PushButton{
				Text: "查看当前系统路由表",
				OnClicked: func() {
					exec.Command("cmd", "/c", "start route print").Run()
				},
			},
		},
	}.Run()); err != nil {
		os.Exit(1)
	}
}

// 辅助函数：转换指针
func unsafePointer(p *uint16) unsafe.Pointer {
	return unsafe.Pointer(p)
}

// 必须引用的底层包
import "unsafe"

func handleRoute(input, action string, log *walk.TextEdit) {
	log.AppendText(fmt.Sprintf("[%s] 正在处理: %s\r\n", action, input))
	ips, err := net.LookupIP(input)
	if err != nil {
		log.AppendText(fmt.Sprintf("解析错误: %v\r\n", err))
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
		log.AppendText(fmt.Sprintf("IP: %s -> %s\r\n", ip.String(), string(output)))
	}
	log.AppendText("--- 操作完毕 ---\r\n\r\n")
}
