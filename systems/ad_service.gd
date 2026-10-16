class_name AdService
## 广告抽象层基类（01 文档 §7.2，MUST）。
## 业务代码只允许调用本抽象层；核心逻辑禁止直接调用平台 SDK。


## 激励视频是否就绪
func is_rewarded_ready(_placement: String) -> bool:
	return false


## 播放激励视频；on_done(true)=完整观看，可发奖
func show_rewarded(_placement: String, on_done: Callable) -> void:
	on_done.call(false)


## 插屏（可选，默认不用）
func show_interstitial(on_closed: Callable) -> void:
	on_closed.call()
