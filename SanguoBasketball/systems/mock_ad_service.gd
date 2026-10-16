class_name MockAdService
extends AdService
## Mock 广告服务：开发期使用。用 UI 层注册的播放器模拟“观看广告”。
## 未注册播放器时直接视为完整观看（便于自测/截图）。

## UI 注册的模拟广告播放器：参数 = (点位ID, 完成回调)
static var player: Callable = Callable()


func is_rewarded_ready(_placement: String) -> bool:
	return true


func show_rewarded(placement: String, on_done: Callable) -> void:
	if player.is_valid():
		player.call(placement, on_done)
	else:
		on_done.call(true)


func show_interstitial(on_closed: Callable) -> void:
	on_closed.call()
