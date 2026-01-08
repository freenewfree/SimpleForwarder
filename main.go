package main

import (
	"fmt"
	"net"
	"os/exec"
	"strings"
	"sync"

	"github.com/lxn/walk"
	. "github.com/lxn/walk/declarative"
)

func main() {
	var mw *walk.MainWindow
	var outTE *walk.TextEdit
	var remoteAddr *walk.LineEdit
	var logLabel *walk.Label

	if _, err := (MainWindow{
		AssignTo: &mw,
		Title:    "RouteForwarder 增强版 (支持IPv6/域名)",
		MinSize:  Size{Width: 450, Height: 350},
		Layout:   VBox{},
		Children: []Widget{
			Composite{
				Layout: Grid{Columns: 2},
				Children: []Widget{
					Label{Text: "目标地址 (域名或IP):"},
					LineEdit{AssignTo: &remoteAddr, Text: "www.example.com"},
					
					Label{Text: "操作说明:"},
					Label{AssignTo: &logLabel, Text: "输入地址后点击下方按钮执行"},
				},
			},
			HSplitter{
				Children: []Widget{
					PushButton{
						Text: "添加路由 (IPv4+IPv6)",
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
			Label{Text: "运行日志:"},
			TextEdit{AssignTo: &outTE, ReadOnly: true, VScroll: true},
			PushButton{
				Text: "查看系统路由表 (Print)",
				OnClicked: func() {
					cmd := exec.Command("cmd", "/c", "start cmd /k route print")
					cmd.Run()
				},
			},
		},
	}.Run()); err != nil {
		fmt.Fprintln(mw, err)
	}
}

func handleRoute(input, action string, log *walk.TextEdit) {
	log.AppendText(fmt.Sprintf("--- 正在%s路由: %s ---\r\n", action, input))
	
	// 1. 解析地址 (支持 IPv4 和 IPv6)
	ips, err := net.LookupIP(input)
	if err != nil {
		log.AppendText(fmt.Sprintf("解析失败: %v\r\n", err))
		return
	}

	for _, ip := range ips {
		var cmd *exec.Cmd
		if ip.To4() != nil {
			// IPv4 路由处理
			// route add <IP> mask 255.255.255.255
			cmd = exec.Command("route", action, ip.String(), "mask", "255.255.255.255")
		} else {
			// IPv6 路由处理
			// netsh interface ipv6 add route <IP>/128 "网卡名/索引"
			if action == "add" {
				cmd = exec.Command("netsh", "interface", "ipv6", "add", "route", ip.String()+"/128", "interface=1") // 默认尝试 interface 1
			} else {
				cmd = exec.Command("netsh", "interface", "ipv6", "delete", "route", ip.String()+"/128", "interface=1")
			}
		}

		output, _ := cmd.CombinedOutput()
		log.AppendText(fmt.Sprintf("IP: %s -> %s\r\n", ip.String(), string(output)))
	}
	log.AppendText("执行完毕。\r\n\r\n")
}
