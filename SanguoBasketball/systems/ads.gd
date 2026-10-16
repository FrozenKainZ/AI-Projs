class_name Ads
## 广告入口（业务侧唯一调用点）+ 频控。

static var service: AdService = MockAdService.new()


## 检测平台 SDK（TODO：发布到虎扑/微信时切换为 SdkAdService）。
static func detect() -> void:
	# 目前无广告 SDK：使用 Mock，保证玩法闭环可测。
	# if (Platform.HasSdk) service = SdkAdService.new()
	service = MockAdService.new()


## 复活广告（每局 1 次，频控由局状态承载）。
static func show_revive(on_done: Callable) -> void:
	if GameState.run.revive_used:
		on_done.call(false)
		return
	service.show_rewarded(AdPlacement.Revive, func(ok):
		if ok:
			GameState.run.revive_used = true
		on_done.call(ok))


## 七星坛广告（每天 7 次，次数在 AltarSystem 记账）。
static func show_altar(on_done: Callable) -> void:
	service.show_rewarded(AdPlacement.Altar, func(ok): on_done.call(ok))
