class_name CodexHeroDetailScreen
extends Control
## 图鉴 · 武将详情（白板数值版）：
## 星级选择栏 → 查看该星级下的白板基础属性 / 倍率 / 智商 / 体力上限。
## 不含强化、升星、回收等操作。

var _def: HeroDef
var _star := 1
var _root: VBoxContainer


func _init(def: HeroDef = null) -> void:
	_def = def
	var star := 1
	var owned := GameState.heroes_of_def(def.id)
	if owned.size() > 0:
		for h in owned:
			star = maxi(star, h.star)
	_star = mini(6, maxi(1, star))


## v1.2：直接叠在势力底图上的文字统一加对比描边
func _outline(l: Label) -> Label:
	UiKit.Outline(l)
	return l


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	Rebuild()


func Rebuild() -> void:
	UiKit.ClearChildren(self)

	var backdrop := UIArt.FactionBackdrop(_def.faction, _def.quality_key)
	backdrop.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(backdrop)

	var margin := UiKit.Pad(16, 46, 16, 12)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(margin)
	_root = UiKit.VBox(8)
	margin.add_child(_root)

	var head := UiKit.HBox(8)
	head.add_child(UiKit.BtnSecondary("← 返回", func(): UiKit.PopScreen(), 36, 13))
	var sp := Control.new()
	sp.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	head.add_child(sp)
	head.add_child(_outline(UiKit.L("图鉴 · " + _def.name, 15, Palette.InkSoft, true)))
	_root.add_child(head)

	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	_root.add_child(scroll)
	var col := UiKit.VBox(10)
	col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(col)

	# 卡面（图鉴不显示星级）
	var card := HeroCard.new(140)
	card.setup_raw(_def.id, _def.name, _def.quality_key, 0, null)
	card.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var card_holder := Control.new()
	card_holder.custom_minimum_size = Vector2(0, card.card_h() + 14)
	card_holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	card.offset_left = -70
	card.offset_right = 70
	card_holder.add_child(card)
	col.add_child(card_holder)

	# 名称 + 偏好位置
	var name_row := UiKit.HBox(8)
	name_row.alignment = BoxContainer.ALIGNMENT_CENTER
	name_row.add_child(_outline(UiKit.L(_def.full_name, 20, Palette.Ink, true)))
	var pref_chip := UiKit.Card(8, Color(Palette.StoneBlue.r, Palette.StoneBlue.g, Palette.StoneBlue.b, 0.12), null, 0, 5)
	var pref_label := UiKit.L("偏好位置 " + _def.pref_text, 12, Palette.StoneBlue, true)
	_outline(pref_label)
	pref_chip.add_child(pref_label)
	name_row.add_child(pref_chip)
	col.add_child(name_row)

	# 星级选择栏（勾选后查看该星级白板）
	col.add_child(_outline(UiKit.L("选择星级查看白板数值：", 11, Palette.Gray)))
	var star_row := UiKit.HBox(5)
	for s in range(1, 7):
		var star := s
		var b := UiKit.BtnSecondary("%d★" % s, func():
			_star = star
			Rebuild(), 30, 12)
		b.custom_minimum_size = Vector2(46, 30)
		if s == _star:
			b.add_theme_color_override("font_color", Color.WHITE)
			b.add_theme_color_override("font_hover_color", Color.WHITE)
			b.add_theme_color_override("font_pressed_color", Color.WHITE)
			b.add_theme_stylebox_override("normal", UiKit.SB(Palette.Gamboge, 12))
			b.add_theme_stylebox_override("hover", UiKit.SB(Palette.Gamboge, 12))
			b.add_theme_stylebox_override("pressed", UiKit.SB(Palette.Gamboge.darkened(0.1), 12))
		star_row.add_child(b)
	col.add_child(star_row)

	# 倍率说明
	var s_mul := ConfigDb.I.star_s(_star)
	var d_mul := ConfigDb.I.star_d(_star)
	var mul_label := _outline(UiKit.L("全部属性 ×%.2f · 得意属性 ×%.2f" % [s_mul, d_mul], 12, Palette.Cinnabar, true))
	mul_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(mul_label)

	# 得意属性
	var sig_parts: Array = []
	for k in _def.signature:
		sig_parts.append(Attr.Cns[Attr.key_to_index(str(k))])
	var sig_label := _outline(UiKit.L("得意: " + " ".join(PackedStringArray(sig_parts)), 12, Palette.InkSoft))
	sig_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(sig_label)

	# 白板基础属性（该星级，无材料加成）
	var panel := UiKit.Card(14, Palette.CardBg, Color(0, 0, 0, 0.12), 1)
	var pv := UiKit.VBox(4)
	panel.add_child(pv)
	var grid := UiKit.HBox(12)
	var left := UiKit.VBox(4)
	var right := UiKit.VBox(4)
	left.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	for i in Attr.COUNT:
		var v: float = _def.base[i] * s_mul * (d_mul if _def.is_signature(i) else 1.0)
		var row := HeroDetailScreen._attr_row(i, v)
		if i % 2 == 0:
			left.add_child(row)
		else:
			right.add_child(row)
	grid.add_child(left)
	grid.add_child(right)
	pv.add_child(grid)
	col.add_child(panel)

	# 独立资源白板（智商 / 体力上限，随星级加值，不含材料）
	var iq := _def.iq + (_star - 1) * ConfigDb.I.C.ext.iq_star_step
	var cap := ConfigDb.I.C.ext.stacap_base + (_star - 1) * ConfigDb.I.C.ext.stacap_star_step
	var res_row := UiKit.HBox(8)
	res_row.alignment = BoxContainer.ALIGNMENT_CENTER
	res_row.add_child(_outline(UiKit.L("智商 %d" % iq, 13, Palette.Ink, true)))
	res_row.add_child(_outline(UiKit.L("体力上限 %d" % cap, 13, Palette.Ink, true)))
	res_row.add_child(_outline(UiKit.L("（%d★ 白板 · 未含材料加成）" % _star, 11, Palette.Gray)))
	col.add_child(res_row)

	var tip := _outline(UiKit.L("图鉴数值为该星级白板属性，仅供对照；实际战斗属性还受材料、位置偏好与教练光环影响。", 10, Palette.Gray))
	tip.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	col.add_child(tip)
