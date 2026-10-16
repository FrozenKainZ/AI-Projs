class_name Fx
## 动效助手（04 文档 FX-01/02/05/04/06/12/14/21）。数值统一从 motion.json 读取。


static func Dur(key: String, fb: float) -> float:
	return ConfigDb.I.motion.f(key, fb)


static func Nested(path: String, fb: float) -> float:
	return ConfigDb.I.motion.nested(path, fb)


## FX-01 按钮按压：缩放 1.0→0.96→1.0。
static func Press(c: BaseButton) -> void:
	c.resized.connect(func(): c.pivot_offset = c.size / 2)
	c.button_down.connect(func():
		var t := c.create_tween()
		t.tween_property(c, "scale", Vector2(0.96, 0.96), Dur("dur_instant", 0.12) * 0.5) \
			.set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT))
	c.button_up.connect(func():
		var t := c.create_tween()
		t.tween_property(c, "scale", Vector2.ONE, Dur("dur_instant", 0.12)) \
			.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT))


## FX-02 页面转场（新页右滑入）。
static func PageIn(node: Control) -> void:
	var dur := Dur("dur_standard", 0.24)
	var offset := Nested("transition.new_shift", 24.0)
	node.modulate = Color(1, 1, 1, 0)
	node.position = Vector2(offset, 0)
	var t := node.create_tween()
	t.set_parallel(true)
	t.tween_property(node, "modulate:a", 1.0, dur).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	t.tween_property(node, "position:x", 0.0, dur).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)


## 返回时旧页轻微退场。
static func PageOut(node: Control, on_done: Callable) -> void:
	var dur := Dur("dur_standard", 0.24)
	var t := node.create_tween()
	t.set_parallel(true)
	t.tween_property(node, "modulate:a", 0.0, dur * 0.8)
	t.tween_property(node, "position:x", 24.0, dur * 0.8)
	t.chain().tween_callback(on_done)


## FX-05 弹窗出现：0.85→1.05→1.0。
static func PopIn(node: Control) -> void:
	var dur := Dur("dur_standard", 0.24)
	node.pivot_offset = node.size / 2
	node.scale = Vector2(0.85, 0.85)
	node.modulate = Color(1, 1, 1, 0)
	var t := node.create_tween()
	t.set_parallel(true)
	t.tween_property(node, "scale", Vector2(1.05, 1.05), dur * 0.7) \
		.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	t.tween_property(node, "modulate:a", 1.0, dur * 0.5)
	t.chain().tween_property(node, "scale", Vector2.ONE, dur * 0.4) \
		.set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)


static func PopOut(node: Control, on_done: Callable) -> void:
	var t := node.create_tween()
	t.set_parallel(true)
	t.tween_property(node, "scale", Vector2(0.9, 0.9), 0.16).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_IN)
	t.tween_property(node, "modulate:a", 0.0, 0.16)
	t.chain().tween_callback(on_done)


## FX-06 数值滚动（数字翻牌感）。
static func RollNumber(label: Label, from: float, to: float, format: Callable, dur := -1.0) -> void:
	if dur <= 0:
		dur = Dur("number_roll.dur", 0.5)
	var t := label.create_tween()
	t.tween_method(format, from, to, dur).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)


## FX-11 卡牌入场（延迟到入树后播放；容器子节点只做缩放+淡入，避免与容器布局冲突）。
static func CardEnter(node: Control, delay := 0.0) -> void:
	(func(): _card_enter_now(node, delay)).call_deferred()


static func _card_enter_now(node: Control, delay: float) -> void:
	if not is_instance_valid(node) or not node.is_inside_tree():
		return
	var dur := Nested("card_enter.dur", 0.36)
	var y := Nested("card_enter.y", 40.0)
	node.modulate = Color(1, 1, 1, 0)
	node.pivot_offset = node.size / 2
	node.scale = Vector2(0.85, 0.85)
	var t := node.create_tween()
	t.set_parallel(true)
	t.tween_property(node, "modulate:a", 1.0, dur * 0.6).set_delay(delay)
	t.tween_property(node, "scale", Vector2.ONE, dur) \
		.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT).set_delay(delay)
	if not (node.get_parent() is Container):
		t.tween_property(node, "position:y", node.position.y, dur) \
			.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT).set_delay(delay) \
			.from(node.position.y + y)


## FX-12 呼吸浮动。
static func Breathe(node: Control, amp := -1.0, period := -1.0) -> void:
	if amp <= 0:
		amp = Nested("breathing.amp", 3.0)
	if period <= 0:
		period = Nested("breathing.period", 3.0)
	var base_y := node.position.y
	var t := node.create_tween()
	t.set_loops()
	t.tween_property(node, "position:y", base_y - amp, period * 0.5) \
		.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	t.tween_property(node, "position:y", base_y, period * 0.5) \
		.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)


## FX-19 常驻微浮动。
static func FloatLoop(node: Control, scale: float, period: float) -> void:
	var t := node.create_tween()
	t.set_loops()
	t.tween_property(node, "scale", Vector2(scale, scale), period * 0.5) \
		.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	t.tween_property(node, "scale", Vector2.ONE, period * 0.5) \
		.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)


## FX-20 胜利：上浮 + 淡入。
static func RiseIn(node: Control, dist := 24.0) -> void:
	node.modulate = Color(1, 1, 1, 0)
	var t := node.create_tween()
	t.set_parallel(true)
	t.tween_property(node, "modulate:a", 1.0, Dur("dur_emphasis", 0.36))
	t.tween_property(node, "position:y", node.position.y, Dur("dur_emphasis", 0.36)) \
		.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT) \
		.from(node.position.y + dist)


static func Shake(node: Control, strength := 4.0, times := 3) -> void:
	var t := node.create_tween()
	var x := node.position.x
	for i in times:
		t.tween_property(node, "position:x", x + strength, 0.04)
		t.tween_property(node, "position:x", x - strength, 0.04)
	t.tween_property(node, "position:x", x, 0.04)
