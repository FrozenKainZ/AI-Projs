class_name BottomNav
extends PanelContainer
## 底部五宫格导航（作战 / 阵容 / 城池 / 军库 / 图鉴）。

var OnTab: Callable = Callable()
var _items := {}
var _active := "main"

const Tabs := [
	["main", "作战", "战"],
	["lineup", "阵容", "阵"],
	["gacha", "城池", "城"],
	["bag", "军库", "库"],
	["codex", "图鉴", "鉴"],
]


func _init() -> void:
	var sb := UiKit.SB(Palette.CardBg, 0)
	sb.border_color = Color(0, 0, 0, 0.08)
	sb.border_width_top = 1
	add_theme_stylebox_override("panel", sb)
	custom_minimum_size = Vector2(0, 84)

	var row := UiKit.HBox(0)
	row.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(row)

	for tab in Tabs:
		var id: String = tab[0]
		var cn: String = tab[1]
		var icon: String = tab[2]
		var item := Control.new()
		item.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		item.custom_minimum_size = Vector2(0, 78)
		var btn := Button.new()
		btn.flat = true
		btn.focus_mode = Control.FOCUS_NONE
		btn.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		var tab_id := id
		btn.pressed.connect(func():
			if OnTab.is_valid():
				OnTab.call(tab_id))
		item.add_child(btn)

		var circle := Panel.new()
		circle.custom_minimum_size = Vector2(28, 28)
		circle.mouse_filter = Control.MOUSE_FILTER_IGNORE
		circle.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
		circle.position = Vector2(-14, 10)
		item.add_child(circle)
		var icon_label := UiKit.L(icon, 13, Palette.Ink, true)
		icon_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		icon_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		icon_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		icon_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		circle.add_child(icon_label)

		var label := UiKit.L(cn, 12, Palette.Gray)
		label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		label.anchor_left = 0.0
		label.anchor_right = 1.0
		label.anchor_top = 0.0
		label.anchor_bottom = 0.0
		label.offset_top = 44
		label.offset_left = 0
		label.offset_right = 0
		label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		item.add_child(label)

		_items[id] = {"circle": circle, "label": label, "icon": icon_label}
		row.add_child(item)
	SetActive(_active)


func SetActive(id: String) -> void:
	_active = id
	for tid in _items:
		var it = _items[tid]
		var active: bool = tid == id
		it["circle"].add_theme_stylebox_override("panel", UiKit.SB(Palette.Cinnabar if active else Color("#E2E6EE"), 14))
		it["label"].add_theme_color_override("font_color", Palette.Cinnabar if active else Palette.Gray)
		it["icon"].add_theme_color_override("font_color", Color.WHITE if active else Palette.Ink)


## FX-03 Tab 切换弹跳。
func Bounce(id: String, node: Control) -> void:
	var t := node.create_tween()
	t.tween_property(node, "scale", Vector2(1.15, 1.15), 0.12).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	t.tween_property(node, "scale", Vector2.ONE, 0.12)


func CircleOf(id: String):
	return _items[id]["circle"] if _items.has(id) else null
