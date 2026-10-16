class_name AltarScreen
extends Control
## SCR_ALTAR · 七星坛：每日 7 次广告领奖（02 文档 §11）。

var _count_label: Label
var _ad_holder: Control
var _dots_holder: Control


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var bg := ColorRect.new()
	bg.color = Color("#141B2A")
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	var tex := Art.bg("bg_altar")
	if tex != null:
		var tr := TextureRect.new()
		tr.texture = tex
		tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
		tr.modulate = Color(1, 1, 1, 0.85)
		tr.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		add_child(tr)

	var margin := UiKit.Pad(16, 48, 16, 16)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(margin)
	var root := UiKit.VBox(10)
	margin.add_child(root)

	var head := UiKit.HBox(8)
	head.add_child(UiKit.BtnSecondary("← 返回", func(): UiKit.PopScreen(), 36, 13))
	var sp := Control.new()
	sp.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	head.add_child(sp)
	head.add_child(UiKit.L("七 星 坛", 17, Color.WHITE, true))
	root.add_child(head)

	# 次数灯
	_dots_holder = Control.new()
	_dots_holder.custom_minimum_size = Vector2(0, 34)
	_dots_holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(_dots_holder)
	_count_label = UiKit.L("", 13, Palette.Gamboge, true)
	_count_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	root.add_child(_count_label)

	var fill := Control.new()
	fill.size_flags_vertical = Control.SIZE_EXPAND_FILL
	fill.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(fill)

	var tip := UiKit.L("坛体光柱汇聚 · 双奖池：金币 / 虎符", 12, Color(1, 1, 1, 0.55))
	tip.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	root.add_child(tip)

	_ad_holder = Control.new()
	_ad_holder.custom_minimum_size = Vector2(0, 52)
	_ad_holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(_ad_holder)

	var rule := UiKit.L("规则：每日 7 次 · 04:00 重置 · 广告失败将返还次数", 11, Color(1, 1, 1, 0.45))
	rule.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	root.add_child(rule)

	Refresh()


func Refresh() -> void:
	GameState.ensure_altar_day()
	var used := GameState.D.altar.used
	var limit := ConfigDb.I.C.altar_daily_limit
	var remain := AltarSystem.remaining()
	_count_label.text = "今日次数 %d/%d" % [used, limit]

	UiKit.ClearChildren(_dots_holder)
	var dots := DotsRow.new(limit, used)
	dots.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	dots.custom_minimum_size = Vector2(limit * 26, 22)
	dots.offset_top = 6
	_dots_holder.add_child(dots)

	UiKit.ClearChildren(_ad_holder)
	if remain > 0:
		var ad_btn := UiKit.AdButton("观看视频 领取祈福", Fx.Nested("ad_ring_seconds", 15.0), Play)
		ad_btn.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		_ad_holder.add_child(ad_btn)
	else:
		var b := UiKit.BtnSecondary("明日再来", null, 52, 16)
		b.disabled = true
		b.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		_ad_holder.add_child(b)


func Play() -> void:
	if not AltarSystem.can_play():
		UiKit.Toast("今日次数已用完，明日再来")
		return
	Ads.show_altar(func(ok):
		if not ok:
			UiKit.Toast("广告暂不可用，次数未消耗，请稍后重试")
			return
		var rng := GameRandom.new(GameRandom.new_seed())
		var item := AltarSystem.roll(rng)
		var msg := AltarSystem.grant(item)
		SaveSystem.save_game()
		Refresh()
		UiKit.Toast(msg)
		_play_reward_anim(item, msg))


func _play_reward_anim(item: DrawItem, msg: String) -> void:
	# 光柱汇聚 → 奖池开启（简化演出）
	var flash := ColorRect.new()
	flash.color = Color(1, 0.92, 0.6, 0)
	flash.mouse_filter = Control.MOUSE_FILTER_IGNORE
	flash.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(flash)
	var t := flash.create_tween()
	t.tween_property(flash, "color:a", 0.55, 0.2)
	t.tween_property(flash, "color:a", 0.0, 0.5)
	t.tween_callback(func(): flash.queue_free())

	var v := UiKit.VBox(8)
	var icon_box := Control.new()
	icon_box.custom_minimum_size = Vector2(0, 96)
	icon_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var tex := Art.draw_icon(item)
	if tex != null:
		var tr := TextureRect.new()
		tr.texture = tex
		tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
		tr.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
		tr.custom_minimum_size = Vector2(88, 88)
		tr.position = Vector2(-44, 0)
		icon_box.add_child(tr)
	v.add_child(icon_box)
	var label := UiKit.L(msg, 16, Palette.Cinnabar, true)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(label)
	UiKit.ShowPopup("祈 福 结 果", v, true, 300)


## 七灯次数点。
class DotsRow extends Control:
	var _total := 7
	var _used := 0

	func _init(total := 7, used := 0) -> void:
		_total = total
		_used = used
		mouse_filter = Control.MOUSE_FILTER_IGNORE
		custom_minimum_size = Vector2(total * 26, 22)

	func _draw() -> void:
		for i in _total:
			var c := Color(1, 1, 1, 0.25) if i < _used else Palette.Gamboge
			var center := Vector2(13 + i * 26, size.y / 2.0)
			draw_circle(center, 6, c)
			if i >= _used:
				draw_circle(center, 3, Color(1, 1, 1, 0.85))
