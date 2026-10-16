class_name FoulDots
extends Control
## 犯规点行。

var _n := 0
var _warn := false


func _init(w := 100.0, h := 7.0) -> void:
	custom_minimum_size = Vector2(w, h)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func set_fouls(n: int, warn: bool) -> void:
	_n = n
	_warn = warn
	queue_redraw()


func _draw() -> void:
	var r := size.y / 2.0 - 0.5
	for i in 6:
		var c := Palette.Cinnabar if i < _n else Color(0, 0, 0, 0.10)
		if _warn and i < _n:
			c = Palette.Cinnabar.lightened(0.3)
		draw_circle(Vector2(r + i * (r * 2 + 3), size.y / 2.0), r, c)
