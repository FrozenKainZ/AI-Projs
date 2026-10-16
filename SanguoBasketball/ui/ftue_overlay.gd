class_name FtueOverlay
extends Control
## 引导手势浮层：暗化 + 高亮框 + 单行提示。

var _target := Rect2()
var _text := ""
var _done = null
var _closing := false


static func Show(target_rect: Rect2, text: String, on_done) -> FtueOverlay:
	var ov := FtueOverlay.new()
	ov._target = target_rect
	ov._text = text
	ov._done = on_done
	ov.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	ov.mouse_filter = Control.MOUSE_FILTER_STOP
	ScreenManager.I.add_child(ov)
	return ov


func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Color(0, 0, 0, 0.42))
	var r := _target.grow(8)
	draw_rect(Rect2(r.position, Vector2(r.size.x, 3)), Palette.Bamboo)
	draw_rect(Rect2(Vector2(r.position.x, r.end.y - 3), Vector2(r.size.x, 3)), Palette.Bamboo)
	draw_rect(Rect2(r.position, Vector2(3, r.size.y)), Palette.Bamboo)
	draw_rect(Rect2(Vector2(r.end.x - 3, r.position.y), Vector2(3, r.size.y)), Palette.Bamboo)


func _ready() -> void:
	var panel := UiKit.Card(12, Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.88), null, 0, 12)
	var lbl := UiKit.LWrap(_text, 13, Color.WHITE)
	lbl.custom_minimum_size = Vector2(240, 0)
	panel.add_child(lbl)
	add_child(panel)
	(func():
		var w := panel.size.x
		var y := _target.end.y + 16
		if y > size.y - 140:
			y = maxf(60.0, _target.position.y - panel.size.y - 16)
		panel.position = Vector2(clampf((size.x - w) / 2.0, 12, size.x - w - 12), y)
	).call_deferred()

	gui_input.connect(func(ev):
		if ev is InputEventMouseButton and ev.pressed:
			_close()
		if ev is InputEventScreenTouch and ev.pressed:
			_close())


func _close() -> void:
	if _closing:
		return
	_closing = true
	var t := create_tween()
	t.tween_property(self, "modulate:a", 0.0, 0.15)
	t.tween_callback(func():
		if _done != null:
			_done.call()
		queue_free())
