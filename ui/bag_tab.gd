class_name BagTab
extends Control
## SCR_BAG · 军库（材料 / 兵粮 / 虎符 / 武将；含批量回收与武将分类）。

var _cat := "mat"
var _batch := false
var _selected := {}
var _quality_checked := {"green": false, "blue": false, "purple": false, "gold": false}

# 武将分类筛选
var _faction := "全部"
var _quality := "全部"
var _star := 0

var _root: VBoxContainer
var _list_box: VBoxContainer
var _filters_box: VBoxContainer
var _quality_bar: HBoxContainer
var _summary_bar: HBoxContainer
var _summary_label: Label
var _batch_btn: Button
var _cat_btns := {}


func _ready() -> void:
	Build()
	Refresh()


func Build() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var margin := UiKit.Pad(16, 4, 16, 4)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(margin)
	_root = UiKit.VBox(7)
	margin.add_child(_root)

	var head := UiKit.HBox(8)
	head.add_child(UiKit.L("军 库", 18, Palette.Ink, true))
	var sp := Control.new()
	sp.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	head.add_child(sp)
	_batch_btn = UiKit.BtnSecondary("批量回收", ToggleBatch, 34, 13)
	head.add_child(_batch_btn)
	_root.add_child(head)

	# 分类 Tab（聚焦状态清晰化）
	var cat_row := UiKit.HBox(6)
	for pair in [["mat", "材料"], ["food", "兵粮"], ["tali", "虎符"], ["hero", "武将"]]:
		var cid: String = pair[0]
		var b := UiKit.BtnSecondary(pair[1], func():
			_cat = cid
			_batch = false
			_selected.clear()
			_reset_quality_checks()
			Refresh(), 36, 13)
		b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		_cat_btns[cid] = b
		cat_row.add_child(b)
	_root.add_child(cat_row)

	# 武将筛选栏（仅武将页显示）
	_filters_box = UiKit.VBox(3)
	_root.add_child(_filters_box)

	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	_root.add_child(scroll)
	_list_box = UiKit.VBox(6)
	_list_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(_list_box)

	# 批量汇总条（含四色勾选框）
	_summary_bar = UiKit.HBox(6)
	_summary_label = UiKit.L("", 11, Palette.Ink)
	_summary_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_summary_bar.add_child(_summary_label)
	_quality_bar = UiKit.HBox(4)
	_summary_bar.add_child(_quality_bar)
	_summary_bar.add_child(UiKit.BtnDanger("回收", DoBatchRecycle, 34, 13))
	_root.add_child(_summary_bar)
	_summary_bar.visible = false


func _style_cat_tabs() -> void:
	for id in _cat_btns:
		var b: Button = _cat_btns[id]
		var active: bool = id == _cat
		b.add_theme_color_override("font_color", Color.WHITE if active else Palette.Ink)
		b.add_theme_color_override("font_pressed_color", Color.WHITE if active else Palette.Ink)
		b.add_theme_color_override("font_hover_color", Color.WHITE if active else Palette.Ink)
		b.add_theme_stylebox_override("normal", UiKit.SB(Palette.Cinnabar if active else Palette.CardBg, 12, Palette.Cinnabar if active else Palette.Ink, 2))
		b.add_theme_stylebox_override("hover", UiKit.SB(Palette.Cinnabar if active else Palette.Paper, 12, Palette.Cinnabar if active else Palette.Ink, 2))
		b.add_theme_stylebox_override("pressed", UiKit.SB(Palette.Cinnabar.darkened(0.1), 12, Palette.Cinnabar, 2))


func _rebuild_quality_bar() -> void:
	UiKit.ClearChildren(_quality_bar)
	for pair in [["green", "绿"], ["blue", "蓝"], ["purple", "紫"], ["gold", "金"]]:
		var quality: String = pair[0]
		var on: bool = _quality_checked[quality]
		var b := UiKit.BtnSecondary(("☑" if on else "☐") + pair[1], func(): ToggleQuality(quality), 30, 12)
		b.custom_minimum_size = Vector2(46, 30)
		if on:
			b.add_theme_color_override("font_color", Color.WHITE)
			b.add_theme_color_override("font_hover_color", Color.WHITE)
			b.add_theme_color_override("font_pressed_color", Color.WHITE)
			b.add_theme_stylebox_override("normal", UiKit.SB(Quality.color(quality).darkened(0.15), 12))
			b.add_theme_stylebox_override("hover", UiKit.SB(Quality.color(quality).darkened(0.05), 12))
			b.add_theme_stylebox_override("pressed", UiKit.SB(Quality.color(quality), 12))
		_quality_bar.add_child(b)


func _reset_quality_checks() -> void:
	for k in _quality_checked:
		_quality_checked[k] = false


func ToggleBatch() -> void:
	_batch = not _batch
	_selected.clear()
	_reset_quality_checks()
	_batch_btn.text = "退出批量" if _batch else "批量回收"
	Refresh()


func ToggleQuality(quality: String) -> void:
	var turn_on: bool = not _quality_checked[quality]
	_quality_checked[quality] = turn_on
	if _cat == "mat":
		for key in GameState.D.mats:
			var count := int(GameState.D.mats[key])
			if count <= 0:
				continue
			var parts: PackedStringArray = str(key).split(":")
			if parts.size() != 2 or parts[1] != quality:
				continue
			var sel_key := "mat:" + str(key)
			if turn_on:
				_selected[sel_key] = true
			else:
				_selected.erase(sel_key)
	elif _cat == "hero":
		for h in GameState.D.heroes:
			if h.def().quality_key != quality:
				continue
			var sel_key2: String = "hero:" + str(h.uid)
			if turn_on and not GameState.is_equipped(h):
				_selected[sel_key2] = true
			if not turn_on:
				_selected.erase(sel_key2)
	Refresh()


# ---------------- 列表 ----------------
func Refresh() -> void:
	_style_cat_tabs()
	UiKit.ClearChildren(_list_box)
	_filters_box.visible = _cat == "hero"
	match _cat:
		"mat":
			_build_mats()
		"food":
			_build_foods()
		"tali":
			_build_talis()
		"hero":
			_build_heroes()
	var total := BatchValue()
	_summary_bar.visible = _batch
	if _batch:
		_rebuild_quality_bar()
	_summary_label.text = "勾选品质或点选条目" if _selected.size() == 0 else \
		"已选 %d 件 · 可回收 %s 金币" % [_selected.size(), UiKit.Fmt(total)]


func _build_mats() -> void:
	var entries: Array = []
	for key in GameState.D.mats:
		var count := int(GameState.D.mats[key])
		if count <= 0:
			continue
		var parts: PackedStringArray = str(key).split(":")
		if parts.size() != 2:
			continue
		entries.append({"key": str(key), "type_id": parts[0], "quality": parts[1], "count": count})
	entries.sort_custom(func(a, b):
		var qa := Quality.tier(a["quality"])
		var qb := Quality.tier(b["quality"])
		if qa != qb:
			return qa > qb
		return a["type_id"] < b["type_id"])
	if entries.size() == 0:
		_list_box.add_child(UiKit.L("没有材料，去城池·市场抽一批吧", 12, Palette.Gray))
		return
	for e in entries:
		var key := "mat:" + str(e["key"])
		var price := int(ConfigDb.I.C.recycle_mat.get(e["quality"], 0))
		var selected: bool = _selected.has(key)
		var type_id: String = e["type_id"]
		var quality: String = e["quality"]
		var row: Control
		if _batch:
			row = ItemUi.MatRow(e["type_id"], e["quality"], e["count"],
				func(): ToggleSel(key),
				("✓ 已选" if selected else "点选"), true)
		else:
			row = ItemUi.MatRow(e["type_id"], e["quality"], e["count"],
				func(): _show_mat_detail(type_id, quality, price),
				"回收 %d 金/个" % price, true)
		if selected:
			_highlight(row)
		_list_box.add_child(row)


func _show_mat_detail(type_id: String, quality: String, unit_price: int) -> void:
	var special := ConfigDb.I.special_mat(type_id)
	var mt := ConfigDb.I.mat_type(type_id)
	var v := UiKit.VBox(8)
	var desc := ""
	if special != null:
		desc = "%s·%s：%s 提升，但大幅降低 %s（独立资源不参与乘区）。" % [special.name, Quality.cn(quality), special.stat_cn, Attr.Cns[special.sub_idx]]
	else:
		desc = "%s·%s：%s 主加成 / %s 副减益。给武将使用时永久提升属性。" % [mt.name, Quality.cn(quality), Attr.Cns[mt.main_idx], Attr.Cns[mt.sub_idx]]
	v.add_child(UiKit.LWrap(desc, 12, Palette.InkSoft))
	var row := UiKit.HBox(8)
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_child(UiKit.BtnPrimary("去武将详情使用", func():
		if UiPopup.Current != null:
			UiPopup.Current.Close()
		var best: HeroInst = null
		for h in GameState.D.heroes:
			if best == null or HeroCalc.ovr(h) > HeroCalc.ovr(best):
				best = h
		if best != null:
			UiKit.PushScreen(HeroDetailScreen.new(best)), 42, 13))
	row.add_child(UiKit.BtnDanger("回收 +%d 金" % unit_price, func():
		if GameState.use_mat(type_id, quality, 1):
			GameState.add_coins(unit_price)
			SaveSystem.save_game()
			if UiPopup.Current != null:
				UiPopup.Current.Close()
			Refresh()
			UiKit.Toast("回收成功 +%d 金币" % unit_price), 42, 13))
	v.add_child(row)
	UiKit.ShowPopup((special.name if special != null else mt.name) + " · 材料", v, true, 320)


func _build_foods() -> void:
	for fd in ConfigDb.I.C.ext.food_defs:
		var count := GameState.get_food(fd.key)
		var row := ItemUi.FoodRow(fd.key, count, func(): _show_food_use(fd))
		if count <= 0:
			row.modulate = Color(1, 1, 1, 0.6)
		_list_box.add_child(row)
	_list_box.add_child(UiKit.L("提示：体力已满的球员不可使用，避免浪费", 11, Palette.Gray))


func _show_food_use(fd: FoodDefDef) -> void:
	if GameState.get_food(fd.key) <= 0:
		UiKit.Toast("数量不足")
		return
	if fd.target == "team":
		UiKit.Confirm("使用 " + fd.name, "全队场上 5 人各恢复 %d 体力（不超过各自上限）。" % fd.effect, "使用", func():
			if not GameState.use_food(fd.key, 1):
				return
			for p in LineupUtil.field_players():
				if p == null:
					continue
				p.sta = mini(HeroCalc.sta_cap_total(p), p.sta + fd.effect)
			SaveSystem.save_game()
			Refresh()
			UiKit.Toast("全队恢复 %d 体力" % fd.effect), "取消", false)
		return

	var v := UiKit.VBox(6)
	v.add_child(UiKit.L("选择一名球员（体力 +%d）" % fd.effect, 12, Palette.Gray))
	var scroll := ScrollContainer.new()
	scroll.custom_minimum_size = Vector2(0, 320)
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	var list := UiKit.VBox(6)
	list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(list)
	v.add_child(scroll)

	var players: Array = GameState.D.heroes.duplicate()
	players.sort_custom(func(a, b):
		return float(a.sta) / HeroCalc.sta_cap_total(a) < float(b.sta) / HeroCalc.sta_cap_total(b))
	var shown := 0
	for h in players:
		if shown >= 40:
			break
		shown += 1
		var cap := HeroCalc.sta_cap_total(h)
		var full: bool = h.sta >= cap
		var row := ItemUi.HeroRow(h, "%d/%d" % [h.sta, cap], func():
			if not GameState.use_food(fd.key, 1):
				return
			h.sta = mini(cap, h.sta + fd.effect)
			SaveSystem.save_game()
			if UiPopup.Current != null:
				UiPopup.Current.Close()
			Refresh()
			UiKit.Toast("%s 体力 +%d" % [h.def().name, fd.effect]), not full, "体力已满" if full else null)
		list.add_child(row)
	UiKit.ShowPopup("使用 " + fd.name, v, false, 340)


func _build_talis() -> void:
	_list_box.add_child(ItemUi.TaliRow("copper", GameState.D.copper))
	_list_box.add_child(ItemUi.TaliRow("jade", GameState.D.jade))
	_list_box.add_child(UiKit.L("虎符用于兵营抽取武将，暂不支持回收。", 11, Palette.Gray))


# ---------------- 武将（双列、出战标记、分类筛选） ----------------
func _rebuild_hero_filters() -> void:
	UiKit.ClearChildren(_filters_box)
	_filters_box.add_child(UiKit.FilterRow("派系", ["全部", "蜀", "魏", "吴", "群"],
		func(): return _faction, func(v):
			_faction = v
			Refresh()))
	_filters_box.add_child(UiKit.FilterRow("品质", ["全部", "绿", "蓝", "紫", "金"],
		func(): return _quality, func(v):
			_quality = v
			Refresh()))
	_filters_box.add_child(UiKit.FilterRow("星级", ["全部", "1★", "2★", "3★", "4★", "5★", "6★"],
		func(): return "全部" if _star == 0 else str(_star) + "★", func(v):
			_star = 0 if v == "全部" else int(v.substr(0, 1))
			Refresh()))


## 出战状态：首（首发）/ 教（教练）/ 替（替补）/ ""。
static func SlotMark(inst: HeroInst) -> String:
	for slot in Slots.Field:
		if str(GameState.D.lineup.get(slot, "")) == inst.uid:
			return "首"
	for slot in Slots.Coach:
		if str(GameState.D.lineup.get(slot, "")) == inst.uid:
			return "教"
	for slot in Slots.Bench:
		if str(GameState.D.lineup.get(slot, "")) == inst.uid:
			return "替"
	return ""


func _build_heroes() -> void:
	_rebuild_hero_filters()
	var list: Array = []
	for h in GameState.D.heroes:
		if _faction != "全部" and h.def().faction != _faction:
			continue
		if _quality != "全部" and Quality.cn(h.def().quality_key) != _quality:
			continue
		if _star > 0 and h.star != _star:
			continue
		list.append(h)

	# 出战武将置顶
	list.sort_custom(func(a, b):
		var oa := _slot_order(SlotMark(a))
		var ob := _slot_order(SlotMark(b))
		if oa != ob:
			return oa < ob
		var qa := Quality.tier(a.def().quality_key)
		var qb := Quality.tier(b.def().quality_key)
		if qa != qb:
			return qa > qb
		return HeroCalc.ovr(a) > HeroCalc.ovr(b))

	if list.size() == 0:
		_list_box.add_child(UiKit.L("没有符合条件的武将", 12, Palette.Gray))
		return

	var grid := GridContainer.new()
	grid.columns = 2
	grid.add_theme_constant_override("h_separation", 6)
	grid.add_theme_constant_override("v_separation", 6)
	for h in list:
		grid.add_child(_build_hero_cell(h))
	_list_box.add_child(grid)


static func _slot_order(mark: String) -> int:
	match mark:
		"首":
			return 0
		"教":
			return 1
		"替":
			return 2
	return 3


func _build_hero_cell(inst: HeroInst) -> Control:
	var mark := SlotMark(inst)
	var in_lineup := not mark.is_empty()
	var key := "hero:" + inst.uid
	var selected: bool = _selected.has(key)

	var card := UiKit.Card(12,
		Color("#E7DFCE") if in_lineup else Palette.CardBg,
		Quality.color(inst.def().quality_key), 2, 6)
	card.mouse_filter = Control.MOUSE_FILTER_STOP
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL

	var h := UiKit.HBox(7)
	card.add_child(h)

	var hc := HeroCard.new(40)
	hc.setup(inst)
	hc.mouse_filter = Control.MOUSE_FILTER_IGNORE
	h.add_child(hc)

	var col := UiKit.VBox(1)
	col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	col.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var name_row := UiKit.HBox(4)
	name_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	name_row.add_child(UiKit.L(inst.def().name, 12, Palette.Ink, true))
	if in_lineup:
		var chip_color := Palette.Cinnabar if mark == "首" else (Palette.StoneBlue if mark == "教" else Palette.Bamboo)
		var chip := UiKit.Card(4, chip_color, null, 0, 1)
		var chip_label := UiKit.L(mark, 10, Color.WHITE, true)
		chip_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		chip.add_child(chip_label)
		chip.mouse_filter = Control.MOUSE_FILTER_IGNORE
		name_row.add_child(chip)
	col.add_child(name_row)
	col.add_child(UiKit.L("OVR %d · %d★" % [HeroCalc.ovr(inst), inst.star], 10, Palette.Gray))
	col.add_child(UiKit.L("偏好 " + inst.def().pref_text, 10, Palette.StoneBlue))
	h.add_child(col)

	if _batch:
		var sel := UiKit.L("✓" if selected else "○", 14, Palette.Bamboo if selected else Palette.Gray, true)
		sel.mouse_filter = Control.MOUSE_FILTER_IGNORE
		h.add_child(sel)

	card.gui_input.connect(func(ev):
		if ev is InputEventMouseButton and ev.pressed and ev.button_index == MOUSE_BUTTON_LEFT:
			if _batch:
				ToggleSel(key)
			else:
				UiKit.PushScreen(HeroDetailScreen.new(inst)))
	return card


static func _highlight(row: Control) -> void:
	var sp: HBoxContainer = row.get_child(0)
	if sp != null:
		var dot := UiKit.L("✓", 14, Palette.Bamboo, true)
		sp.add_child(dot)


func ToggleSel(key: String) -> void:
	if _selected.has(key):
		_selected.erase(key)
	else:
		_selected[key] = true
	Refresh()


# ---------------- 批量回收 ----------------
func BatchValue() -> int:
	var total := 0
	for key in _selected:
		var k := str(key)
		if k.begins_with("mat:"):
			var parts: PackedStringArray = k.substr(4).split(":")
			if parts.size() == 2:
				var count := GameState.get_mat(parts[0], parts[1])
				var price := int(ConfigDb.I.C.recycle_mat.get(parts[1], 0))
				total += count * price
		elif k.begins_with("hero:"):
			var h := GameState.hero_by_uid(k.substr(5))
			if h != null:
				var price2 := int(ConfigDb.I.C.recycle_hero.get(h.def().quality_key, 0))
				total += price2 * h.star
	return total


func DoBatchRecycle() -> void:
	if _selected.size() == 0:
		UiKit.Toast("先勾选品质或点选条目")
		return
	var total := BatchValue()
	var mat_count := 0
	var hero_count := 0
	for k in _selected:
		if str(k).begins_with("mat:"):
			mat_count += 1
		elif str(k).begins_with("hero:"):
			hero_count += 1

	UiKit.Confirm("批量回收确认",
		"将永久销毁 材料 ×%d 类 / 武将 ×%d 名，获得合计 %s 金币。\n回收后图鉴仍保留，此操作不可撤销。" % [mat_count, hero_count, UiKit.Fmt(total)],
		"确认回收 %s 金币" % UiKit.Fmt(total), func():
			var keys: Array = _selected.keys()
			for key in keys:
				var k := str(key)
				if k.begins_with("mat:"):
					var parts: PackedStringArray = k.substr(4).split(":")
					if parts.size() == 2:
						var count := GameState.get_mat(parts[0], parts[1])
						GameState.use_mat(parts[0], parts[1], count)
				elif k.begins_with("hero:"):
					var h := GameState.hero_by_uid(k.substr(5))
					# 取消“最后一张”保护，仅保护已装备
					if h != null and not GameState.is_equipped(h):
						GameState.remove_hero(h)
			GameState.add_coins(total)
			SaveSystem.save_game()
			_selected.clear()
			_reset_quality_checks()
			Refresh()
			UiKit.Toast("批量回收完成 +%s 金币" % UiKit.Fmt(total)), "取消", true)


# ---------------- 调试入口（截图/自测） ----------------
func DebugBatch() -> void:
	if not _batch:
		ToggleBatch()
	ToggleQuality("green")


func DebugHeroTab() -> void:
	_cat = "hero"
	_batch = false
	Refresh()
