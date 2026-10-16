class_name Shell
extends Control
## 应用根节点（Shell）。负责：
## - 启动引导（读取配置/存档）
## - 屏幕栈管理（页面转场 FX-02）
## - 弹窗层与 Toast 层
## - 全局命令行开关（--selftest / --shots）

static var instance: Shell


func _ready() -> void:
	instance = self
	name = "Shell"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	Boot.run()


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST or what == NOTIFICATION_PREDELETE:
		SaveSystem.save_on_exit()
