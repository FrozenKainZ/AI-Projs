class_name AttrBar
extends Control
## 属性条（对数归一，长条视觉）。

var _v := 0.0


func _init() -> void:
	custom_minimum_size = Vector2(0, 6)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func SetValue(v: float) -> void:
	_v = v
	queue_redraw()


func _draw() -> void:
	var rel := clampf(400.0 / (400.0 + maxf(0.0, _v)), 0.06, 1.0)
	draw_rect(Rect2(Vector2.ZERO, size), Color(0, 0, 0, 0.08))
	draw_rect(Rect2(Vector2.ZERO, Vector2(size.x * rel, size.y)), Palette.StoneBlue)
