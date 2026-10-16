class_name CodexTab
extends Control
## SCR_CODEX · 武将图鉴：拥有过即解锁；筛选（派系/品质）。

var _faction := "全部"
var _quality := "全部"

var _grid: VBoxContainer
var _filters_box: VBoxContainer
var _progress: Label


func _ready() -> void:
	Build()
	Refresh()


func Build() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var margin := UiKit.Pad(16, 48, 16, 8)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(margin)
	var root := UiKit.VBox(6)
	margin.add_child(root)

	var head := UiKit.HBox(8)
	head.add_child(UiKit.L("武将图鉴", 18, Palette.Ink, true))
	var sp := Control.new()
	sp.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	head.add_child(sp)
	_progress = UiKit.L("", 13, Palette.StoneBlue, true)
	head.add_child(_progress)
	root.add_child(head)

	_filters_box = UiKit.VBox(3)
	root.add_child(_filters_box)

	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	root.add_child(scroll)
	_grid = UiKit.VBox(8)
	_grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(_grid)


func _rebuild_filters() -> void:
	UiKit.ClearChildren(_filters_box)
	_filters_box.add_child(UiKit.FilterRow("派系", ["全部", "蜀", "魏", "吴", "群"],
		func(): return _faction, func(v):
			_faction = v
			Refresh()))
	_filters_box.add_child(UiKit.FilterRow("品质", ["全部", "绿", "蓝", "紫", "金"],
		func(): return _quality, func(v):
			_quality = v
			Refresh()))


func Refresh() -> void:
	var total := ConfigDb.I.hero_list.size()
	var unlocked := GameState.D.codex.size()
	_progress.text = "进度 %d/%d" % [unlocked, total]

	_rebuild_filters()
	UiKit.ClearChildren(_grid)
	var filtered: Array = []
	for h in ConfigDb.I.hero_list:
		if _faction != "全部" and h.faction != _faction:
			continue
		if _quality != "全部" and Quality.cn(h.quality_key) != _quality:
			continue
		filtered.append(h)

	var grid := GridContainer.new()
	grid.columns = 4
	grid.add_theme_constant_override("h_separation", 10)
	grid.add_theme_constant_override("v_separation", 10)
	_grid.add_child(grid)

	var i := 0
	for def in filtered:
		var owned := GameState.D.codex.has(def.id)
		var holder := Control.new()
		holder.custom_minimum_size = Vector2(76, 100)
		holder.mouse_filter = Control.MOUSE_FILTER_STOP
		var card := HeroCard.new(72)
		if owned:
			# 图鉴卡不显示星级
			card.setup_raw(def.id, def.name, def.quality_key, 0, null)
		else:
			card.setup_silhouette("?", def.quality_key, 0, null)
		card.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
		card.offset_left = -36
		card.offset_right = 36
		card.mouse_filter = Control.MOUSE_FILTER_IGNORE
		holder.add_child(card)
		var d: HeroDef = def
		holder.gui_input.connect(func(ev):
			if ev is InputEventMouseButton and ev.pressed and ev.button_index == MOUSE_BUTTON_LEFT:
				if not GameState.D.codex.has(d.id):
					UiKit.Toast("尚未结识 · 去兵营抽卡")
					return
				UiKit.PushScreen(CodexHeroDetailScreen.new(d)))
		grid.add_child(holder)
		Fx.CardEnter(holder, mini(i, 10) * 0.04)
		i += 1
	if filtered.size() == 0:
		_grid.add_child(UiKit.L("没有符合条件的武将", 12, Palette.Gray))
