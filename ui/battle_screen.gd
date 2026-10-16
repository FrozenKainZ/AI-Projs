class_name BattleScreen
extends Control
## SCR_BATTLE · 比赛直播（SYS-05/06/07/08 的 UI 载体）。
## 数据先算后播；1x/2x/4x/跳节/跳过；六犯换人条（3s 可改选，重演保持一致）；托管两类。

enum BattleMode { NORMAL, CHALLENGE }

static var Current: BattleScreen

var _mode: int

# 数据
var _player_team: BattleTeam
var _opp_team: BattleTeam
var _opp_gen: OpponentTeam
var _result: MatchResult
var _seed := 0
var _overrides := {}
var _entered_bench := {}
var _fouls := {}

# 播放
var _event_idx := -1
var _timer := 0.0
var _speed := 1.0
var _finished := false
var _skip_armed := false
var _skip_armed_at := 0.0

# 托管（SYS-08）
var _autopilot := false
var _autopilot_food := false
var _auto_continue_pending := false
var _auto_continue_at := 0.0

# UI
var _name_a: Label
var _name_b: Label
var _score_a: Label
var _score_b: Label
var _quarter_label: Label
var _time_label: Label
var _progress_fill: Control
var _progress_track: Control
var _feed: VBoxContainer
var _feed_scroll: ScrollContainer
var _my_row: HBoxContainer
var _opp_row: HBoxContainer
var _coach_row: HBoxContainer
var _my_cards := {}
var _auto_btn: Button
var _auto_capsule: Label
var _b1: Button
var _b2: Button
var _b4: Button
var _skip_q_btn: Button
var _skip_btn: Button
var _gear_btn: Button
var _sub_bar_holder: Control
var _margin_stamp: Label
var _shown_a := 0
var _shown_b := 0

# 信息面板（球员属性 / 数据统计）+ 聚焦球员 + 实时数据
var _info_content: Control
var _info_tab := "stats"
var _focused: BattlePlayer = null
var _attr_tab_btn: Button
var _stats_tab_btn: Button
var _live_stats := {}


func _init(mode := BattleMode.NORMAL) -> void:
	_mode = mode


func _ready() -> void:
	Current = self
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	BuildChrome()
	StartMatch()
	if Boot.ShotsMode:
		_speed = 4.0
		SetSpeedVisual()
	set_process(true)


func _exit_tree() -> void:
	if Current == self:
		Current = null


## 调试入口：聚焦我方某位置球员并切换信息面板（截图/自测用）。
func DebugFocus(slot_idx: int, tab: String) -> void:
	_info_tab = tab
	if slot_idx >= 0 and slot_idx < 5 and _player_team != null and _player_team.court[slot_idx] != null:
		_focused = _player_team.court[slot_idx]
	RefreshInfoPanel()


## 调试入口：直接跳到终场（截图/自测用）。
func DebugSkip() -> void:
	OnSkipPressed()


# ================= 界面骨架 =================
func BuildChrome() -> void:
	var bg := ColorRect.new()
	bg.color = Palette.Paper
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	var tex := Art.bg("bg_battle")
	if tex != null:
		var tr := TextureRect.new()
		tr.texture = tex
		tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
		tr.modulate = Color(1, 1, 1, 0.35)
		tr.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		add_child(tr)

	var margin := UiKit.Pad(12, 44, 12, 8)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(margin)
	var root := UiKit.VBox(8)
	margin.add_child(root)

	# ---- 比分牌 ----
	var board := UiKit.Card(14, Palette.DarkBoard, null, 0, 10)
	var bv := UiKit.VBox(6)
	board.add_child(bv)
	var names := UiKit.HBox(0)
	_name_a = UiKit.L("", 12, Color("#B9C2CF"))
	_name_a.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_name_b = UiKit.L("", 12, Color("#E2E6EE"))
	_name_b.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_name_b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	names.add_child(_name_a)
	names.add_child(_name_b)
	bv.add_child(names)

	var score_row := UiKit.HBox(0)
	_score_a = UiKit.L("0", 44, Color.WHITE, true)
	_score_a.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_score_a.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var colon := UiKit.L(":", 22, Palette.Gray, true)
	colon.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_score_b = UiKit.L("0", 44, Palette.Gamboge, true)
	_score_b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_score_b.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	score_row.add_child(_score_a)
	score_row.add_child(colon)
	score_row.add_child(_score_b)
	bv.add_child(score_row)

	var q_row := UiKit.HBox(8)
	_quarter_label = UiKit.L("Q1", 12, Color("#B9C2CF"))
	q_row.add_child(_quarter_label)
	_progress_track = Control.new()
	_progress_track.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_progress_track.custom_minimum_size = Vector2(0, 8)
	_progress_track.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_progress_fill = Control.new()
	_progress_fill.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_progress_fill.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_progress_fill.anchor_right = 0.0
	_progress_track.add_child(_progress_fill)
	q_row.add_child(_progress_track)
	_time_label = UiKit.L("剩 12'00\"", 12, Color("#B9C2CF"))
	q_row.add_child(_time_label)
	bv.add_child(q_row)

	_margin_stamp = UiKit.L("碾 压 局", 12, Palette.Cinnabar, true)
	_margin_stamp.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_margin_stamp.visible = false
	bv.add_child(_margin_stamp)
	root.add_child(board)

	# ---- 对手席 ----
	var opp_panel := UiKit.Card(12, Color(1, 1, 1, 0.72), Color(0, 0, 0, 0.10), 1, 6)
	var ov := UiKit.VBox(4)
	opp_panel.add_child(ov)
	ov.add_child(UiKit.L("对手席", 10, Palette.Gray))
	_opp_row = UiKit.HBox(6)
	ov.add_child(_opp_row)
	root.add_child(opp_panel)

	# ---- 直播文字流 ----
	var feed_panel := UiKit.Card(12, Color(1, 1, 1, 0.82), Palette.Ink, 2, 6)
	feed_panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	var fv := UiKit.VBox(4)
	feed_panel.add_child(fv)
	fv.add_child(UiKit.L("直播文字流（自动上滚）", 10, Palette.Gray))
	_feed_scroll = ScrollContainer.new()
	_feed_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_feed_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	_feed = UiKit.VBox(4)
	_feed.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_feed_scroll.add_child(_feed)
	fv.add_child(_feed_scroll)
	root.add_child(feed_panel)

	# ---- 我方席 ----
	var my_panel := UiKit.Card(12, Color(1, 1, 1, 0.82), Palette.Ink, 2, 6)
	var mv := UiKit.VBox(4)
	my_panel.add_child(mv)
	var my_head := UiKit.HBox(6)
	my_head.add_child(UiKit.L("我方席（体力/犯规实时）", 10, Palette.Gray))
	var sp := Control.new()
	sp.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	my_head.add_child(sp)
	_auto_capsule = UiKit.L("", 10, Palette.Bamboo, true)
	my_head.add_child(_auto_capsule)
	mv.add_child(my_head)
	_my_row = UiKit.HBox(6)
	mv.add_child(_my_row)
	# 教练席（顶替原替补席展示位）
	_coach_row = UiKit.HBox(4)
	mv.add_child(_coach_row)
	root.add_child(my_panel)

	# ---- 信息面板（球员属性 / 数据统计 + 替补席） ----
	var info_panel := UiKit.Card(12, Color(1, 1, 1, 0.82), Color(0, 0, 0, 0.12), 1, 6)
	var iv := UiKit.VBox(4)
	info_panel.add_child(iv)
	var tab_row := UiKit.HBox(5)
	_attr_tab_btn = UiKit.BtnSecondary("球员属性", func():
		_info_tab = "attrs"
		RefreshInfoPanel(), 26, 11)
	_attr_tab_btn.custom_minimum_size = Vector2(64, 26)
	_stats_tab_btn = UiKit.BtnSecondary("数据统计", func():
		_info_tab = "stats"
		RefreshInfoPanel(), 26, 11)
	_stats_tab_btn.custom_minimum_size = Vector2(64, 26)
	tab_row.add_child(_attr_tab_btn)
	tab_row.add_child(_stats_tab_btn)
	var sp2 := Control.new()
	sp2.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	tab_row.add_child(sp2)
	var bench_btn := UiKit.BtnSecondary("替补席", ShowBenchPopup, 26, 11)
	bench_btn.custom_minimum_size = Vector2(58, 26)
	tab_row.add_child(bench_btn)
	iv.add_child(tab_row)
	_info_content = Control.new()
	_info_content.custom_minimum_size = Vector2(0, 44)
	_info_content.mouse_filter = Control.MOUSE_FILTER_IGNORE
	iv.add_child(_info_content)
	root.add_child(info_panel)

	# ---- 控制排（拇指区）----
	var ctrl := UiKit.HBox(5)
	_b1 = UiKit.BtnRound("1x", func(): SetSpeed(1.0), 42, 13)
	_b2 = UiKit.BtnRound("2x", func(): SetSpeed(2.0), 42, 13)
	_b4 = UiKit.BtnRound("4x", func(): SetSpeed(4.0), 42, 13)
	_skip_q_btn = UiKit.BtnRound("跳节", OnSkipQuarter, 42, 12)
	_skip_q_btn.custom_minimum_size = Vector2(52, 42)
	_skip_btn = UiKit.BtnRound("跳过", OnSkipPressed, 42, 12)
	_skip_btn.custom_minimum_size = Vector2(52, 42)
	ctrl.add_child(_b1)
	ctrl.add_child(_b2)
	ctrl.add_child(_b4)
	ctrl.add_child(_skip_q_btn)
	ctrl.add_child(_skip_btn)
	var fill := Control.new()
	fill.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	ctrl.add_child(fill)
	_auto_btn = UiKit.BtnSecondary("托管：关", ToggleAutopilot, 42, 12)
	# 长按弹出模式选择
	var hold := Timer.new()
	hold.wait_time = 0.6
	hold.one_shot = true
	add_child(hold)
	hold.timeout.connect(ShowAutoModePopup)
	_auto_btn.button_down.connect(func(): hold.start())
	_auto_btn.button_up.connect(func(): hold.stop())
	ctrl.add_child(_auto_btn)
	_gear_btn = UiKit.BtnRound("⚙", ShowAutoModePopup, 42, 14)
	ctrl.add_child(_gear_btn)
	root.add_child(ctrl)
	var ctrl_hint := UiKit.L("跳节=快进一节 · ⚙/长按托管=选择是否耗粮", 10, Palette.Gray)
	ctrl_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	root.add_child(ctrl_hint)

	# ---- 换人条容器（叠加）----
	_sub_bar_holder = Control.new()
	_sub_bar_holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_sub_bar_holder.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(_sub_bar_holder)

	SetSpeedVisual()


# ================= 比赛启动 =================
func StartMatch() -> void:
	_player_team = MatchEngine.build_player_team()
	_opp_gen = GameState.run.current_opponent if _mode == BattleMode.NORMAL else GameState.challenge.current_opponent
	_opp_team = MatchEngine.build_opponent_team(_opp_gen)
	_seed = GameRandom.new_seed()
	_overrides.clear()
	_entered_bench.clear()
	_fouls.clear()
	_live_stats.clear()
	_focused = null
	_event_idx = -1
	_timer = 0.6
	_finished = false
	_shown_a = 0
	_shown_b = 0

	var tutorial := _mode == BattleMode.NORMAL and Ftue.Step() == 1
	_result = Ftue.SimulateTutorial(_player_team, _opp_team, _seed) if tutorial \
		else MatchEngine.simulate(_player_team, _opp_team, _seed, true, _overrides)

	_name_a.text = _player_team.name
	_name_b.text = _opp_team.name
	_score_a.text = "0"
	_score_b.text = "0"
	_quarter_label.text = "Q1"
	_margin_stamp.visible = false

	# 对手席（正常立绘 + 教练席）
	_opp_row.add_theme_constant_override("separation", 5)
	UiKit.ClearChildren(_opp_row)
	var taken := 0
	for p in _opp_gen.court:
		if taken >= 5:
			break
		taken += 1
		var holder := Control.new()
		holder.custom_minimum_size = Vector2(42, 66)
		holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var card := HeroCard.new(40)
		card.setup_raw(p.def_id, p.name, p.quality, p.star, null)
		card.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
		card.offset_left = -20
		card.offset_right = 20
		card.mouse_filter = Control.MOUSE_FILTER_IGNORE
		holder.add_child(card)
		_opp_row.add_child(holder)
	var taken2 := 0
	for co in _opp_gen.coaches:
		if taken2 >= 3:
			break
		taken2 += 1
		var holder2 := Control.new()
		holder2.custom_minimum_size = Vector2(34, 66)
		holder2.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var card2 := HeroCard.new(32)
		card2.setup_raw(co.def_id, co.name, co.quality, co.star, "教")
		card2.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
		card2.offset_left = -16
		card2.offset_right = 16
		card2.mouse_filter = Control.MOUSE_FILTER_IGNORE
		holder2.add_child(card2)
		_opp_row.add_child(holder2)

	# 我方席
	_my_cards.clear()
	RefreshMyRow()
	RefreshCoachRow()
	RefreshInfoPanel()

	# 文字流
	UiKit.ClearChildren(_feed)
	UpdateProgress(0, 0)

	# 开赛提示
	var k := GameState.run.current_k if _mode == BattleMode.NORMAL else GameState.challenge.index
	AddFeedLine("第 %d 场开打！双方亮阵完毕。" % k, "opening")


## 我方教练席（顶替原替补席展示位）。
func RefreshCoachRow() -> void:
	UiKit.ClearChildren(_coach_row)
	var total_aura := 0
	for slot in Slots.Coach:
		var inst := GameState.hero_by_uid(_lineup_uid(slot))
		var holder := Control.new()
		holder.custom_minimum_size = Vector2(34, 60)
		holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
		if inst != null:
			var card := HeroCard.new(32)
			card.setup(inst, "教", false)
			card.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
			card.offset_left = -16
			card.offset_right = 16
			card.mouse_filter = Control.MOUSE_FILTER_IGNORE
			holder.add_child(card)
			total_aura += HeroCalc.coach_aura([inst])
		elif GameState.is_slot_unlocked(slot):
			var dashed := Panel.new()
			dashed.custom_minimum_size = Vector2(30, 42)
			dashed.mouse_filter = Control.MOUSE_FILTER_IGNORE
			dashed.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
			dashed.offset_left = -15
			dashed.offset_right = 15
			dashed.offset_top = 4
			dashed.offset_bottom = 46
			dashed.add_theme_stylebox_override("panel", UiKit.SB(Color(1, 1, 1, 0.3), 8, Palette.Gray, 1))
			holder.add_child(dashed)
		else:
			var lock_label := UiKit.L("🔒", 11, Palette.Gray)
			lock_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			lock_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
			lock_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
			lock_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
			holder.add_child(lock_label)
		_coach_row.add_child(holder)
	var aura_label := UiKit.L("教练光环 +%d" % total_aura, 10, Palette.Gamboge if total_aura > 0 else Palette.Gray, true)
	aura_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_coach_row.add_child(aura_label)


func RefreshMyRow() -> void:
	_my_row.add_theme_constant_override("separation", 4)
	UiKit.ClearChildren(_my_row)
	for slot in Slots.Field:
		var holder := Control.new()
		holder.custom_minimum_size = Vector2(44, 76)
		holder.mouse_filter = Control.MOUSE_FILTER_STOP
		var card := HeroCard.new(42)
		card.ShowStaminaBar = true
		var inst := GameState.hero_by_uid(_lineup_uid(slot))
		if inst != null:
			card.setup(inst, slot, true)
			_my_cards[inst.uid] = card
		else:
			card.setup_raw("__empty", "—", "green", 0, slot)
		card.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
		card.offset_left = -21
		card.offset_right = 21
		card.mouse_filter = Control.MOUSE_FILTER_IGNORE
		holder.add_child(card)
		var uid := inst.uid if inst != null else ""
		holder.gui_input.connect(func(ev):
			if ev is InputEventMouseButton and ev.pressed and ev.button_index == MOUSE_BUTTON_LEFT:
				FocusPlayer(uid))
		_my_row.add_child(holder)


## 聚焦场上球员（点击我方席卡片），信息面板显示其数据/属性。
func FocusPlayer(uid: String) -> void:
	if uid.is_empty():
		return
	if _focused != null and _focused.uid == uid:
		uid = ""  # 再次点击取消聚焦
	_focused = null
	if not uid.is_empty():
		for p in _player_team.court:
			if p != null and p.uid == uid:
				_focused = p
				break
	RefreshInfoPanel()


## 信息面板：球员属性 / 数据统计（无聚焦显示 -）。
func RefreshInfoPanel() -> void:
	if _info_content == null:
		return

	# Tab 选中态
	_style_tab(_attr_tab_btn, _info_tab == "attrs")
	_style_tab(_stats_tab_btn, _info_tab == "stats")

	UiKit.ClearChildren(_info_content)
	var row := UiKit.HBox(10)
	row.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_info_content.add_child(row)

	if _focused == null:
		var hint := UiKit.L("点击场上球员查看" + ("属性" if _info_tab == "attrs" else "数据") + " —", 12, Palette.Gray)
		hint.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		row.add_child(hint)
		return

	var name_col := UiKit.VBox(1)
	name_col.custom_minimum_size = Vector2(64, 0)
	name_col.add_child(UiKit.L(_focused.name, 13, Palette.Ink, true))
	name_col.add_child(UiKit.L(_focused.slot_pos, 10, Palette.Gray))
	row.add_child(name_col)

	if _info_tab == "stats":
		var st: Dictionary = _live_stats.get(_focused.uid, {"points": 0, "threes": 0, "blocks": 0, "steals": 0, "fouls": 0})
		_add_stat_cell(row, "得分", str(st["points"]))
		_add_stat_cell(row, "三分", str(st["threes"]))
		_add_stat_cell(row, "盖帽", str(st["blocks"]))
		_add_stat_cell(row, "抢断", str(st["steals"]))
		_add_stat_cell(row, "犯规", str(st["fouls"]))
		_add_stat_cell(row, "篮板", "—")
	else:
		# 比赛属性近似：核心×位置×体力 + 光环
		var inst: HeroInst = _focused.inst
		if inst == null:
			row.add_child(UiKit.L("对手球员属性暂不可查", 11, Palette.Gray))
			return
		var core := HeroCalc.core_attrs(inst)
		var pos := ConfigDb.I.C.pos_bonus_match if (Slots.is_field(_focused.slot_pos) and inst.def().pref_matches(_focused.slot_pos)) else ConfigDb.I.C.pos_bonus_other
		var sta := HeroCalc.stamina_mult(inst.sta)
		var aura := _player_team.aura
		var grid := GridContainer.new()
		grid.columns = 5
		grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		grid.add_theme_constant_override("h_separation", 6)
		for i in Attr.COUNT:
			var v := int(roundf(core[i] * pos * sta + aura))
			var cell := UiKit.VBox(0)
			var kl := UiKit.L(Attr.Shorts[i], 9, Palette.Gray)
			kl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			var vl := UiKit.L(str(v), 11, Palette.Ink, true)
			vl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			cell.add_child(kl)
			cell.add_child(vl)
			grid.add_child(cell)
		row.add_child(grid)


func _style_tab(b: Button, active: bool) -> void:
	b.add_theme_color_override("font_color", Color.WHITE if active else Palette.Ink)
	b.add_theme_color_override("font_hover_color", Color.WHITE if active else Palette.Ink)
	b.add_theme_stylebox_override("normal", UiKit.SB(Palette.Ink if active else Palette.CardBg, 10, Palette.Ink, 2))
	b.add_theme_stylebox_override("hover", UiKit.SB(Palette.Ink if active else Palette.Paper, 10, Palette.Ink, 2))
	b.add_theme_stylebox_override("pressed", UiKit.SB(Palette.Ink, 10, Palette.Ink, 2))


func _add_stat_cell(row: HBoxContainer, k: String, v: String) -> void:
	var col := UiKit.VBox(0)
	col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var kl := UiKit.L(k, 9, Palette.Gray)
	kl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var vl := UiKit.L(v, 13, Palette.Ink, true)
	vl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(kl)
	col.add_child(vl)
	row.add_child(col)


## 替补席弹窗（点击替补席按钮查看）。
func ShowBenchPopup() -> void:
	var v := UiKit.VBox(6)
	v.add_child(UiKit.L("替补席（比赛不耗体力 · 六犯顶替登场）", 11, Palette.Gray))
	var bench := LineupUtil.available_bench()
	if bench.size() == 0:
		var any_locked := false
		for s in Slots.Bench:
			if not GameState.is_slot_unlocked(s):
				any_locked = true
		v.add_child(UiKit.L("替补席尚未解锁（阵容页可用金币解锁）" if any_locked else "替补席没有球员", 12, Palette.Gray))
	for h in bench:
		var card := UiKit.Card(10, Palette.CardBg, Quality.color(h.def().quality_key), 2, 6)
		var hr := UiKit.HBox(8)
		card.add_child(hr)
		var hc := HeroCard.new(36)
		hc.setup(h)
		hc.mouse_filter = Control.MOUSE_FILTER_IGNORE
		hr.add_child(hc)
		var col := UiKit.VBox(1)
		col.add_child(UiKit.L(h.def().name, 13, Palette.Ink, true))
		col.add_child(UiKit.L("OVR %d · 体力 %d/%d · 偏好 %s" % [HeroCalc.ovr(h), h.sta, HeroCalc.sta_cap_total(h), h.def().pref_text], 10, Palette.Gray))
		hr.add_child(col)
		v.add_child(card)
	UiKit.ShowPopup("替补席", v, true, 320)


static func _lineup_uid(slot: String) -> String:
	return str(GameState.D.lineup.get(slot, ""))


# ================= 播放循环 =================
func _process(delta: float) -> void:
	if _finished:
		# 托管自动续战
		if _auto_continue_pending and Time.get_ticks_msec() / 1000.0 >= _auto_continue_at:
			_auto_continue_pending = false
			ContinueNextMatch()
		return

	if _skip_armed and Time.get_ticks_msec() / 1000.0 - _skip_armed_at > 2.5:
		_skip_armed = false

	_timer -= delta
	if _timer > 0:
		return
	AdvanceOneEvent()
	_timer = 1.7 / _speed


func _stat_for(uid: String) -> Dictionary:
	if not _live_stats.has(uid):
		_live_stats[uid] = {"points": 0, "threes": 0, "blocks": 0, "steals": 0, "fouls": 0}
	return _live_stats[uid]


func AdvanceOneEvent() -> void:
	if _event_idx + 1 >= _result.events.size():
		FinishMatch()
		return
	_event_idx += 1
	var e: MatchEvent = _result.events[_event_idx]

	# 比分跳变（FX-09）
	if e.score_a != _shown_a or e.score_b != _shown_b:
		var d_a := e.score_a - _shown_a
		var d_b := e.score_b - _shown_b
		_shown_a = e.score_a
		_shown_b = e.score_b
		if d_a > 0:
			ScorePop(_score_a, e.score_a, d_a, Palette.Gamboge)
		if d_b > 0:
			ScorePop(_score_b, e.score_b, d_b, Color.WHITE)

	# 节次与进度
	var q := clampi(e.quarter, 1, 4)
	_quarter_label.text = "加时" if e.quarter > 4 else "Q" + str(q)
	UpdateProgress(e.quarter, e.at)

	# 文字流（FX-15）
	AddFeedLine(e.text, e.type)

	# 实时数据归集（供信息面板）
	if e.delta_a > 0 and not e.actor_uid.is_empty():
		var st := _stat_for(e.actor_uid)
		st["points"] += e.delta_a
		if e.type == "three":
			st["threes"] += 1
	if e.actor_is_a and not e.actor_uid.is_empty():
		if e.type == "block":
			_stat_for(e.actor_uid)["blocks"] += 1
		if e.type == "steal":
			_stat_for(e.actor_uid)["steals"] += 1
		if e.type == "foul" and e.fouls_now > 0:
			_stat_for(e.actor_uid)["fouls"] = e.fouls_now
	RefreshInfoPanel()

	# 犯规/换人 UI
	if not e.actor_uid.is_empty():
		if e.type == "foul" and e.fouls_now > 0:
			_fouls[e.actor_uid] = e.fouls_now
			if _my_cards.has(e.actor_uid):
				_my_cards[e.actor_uid].set_fouls(e.fouls_now, e.fouls_now >= 5)
		elif e.type == "foulout":
			if _my_cards.has(e.actor_uid):
				_my_cards[e.actor_uid].set_fouls(ConfigDb.I.C.foul_out_count, false)
			# 替补上场：更新席位
			if _my_cards.has(e.actor_uid) and e.sub_in_name != "无人可换":
				var sub_inst := FindBenchInstByName(e.sub_in_name)
				if sub_inst != null:
					_entered_bench[sub_inst.uid] = true
			if e.sub_pending:
				ShowSubBar(e)

	# 碾压局印章
	if absi(e.score_a - e.score_b) >= 100:
		_margin_stamp.visible = true


func FindBenchInstByName(name_in: String) -> HeroInst:
	for slot in Slots.Bench:
		var inst := GameState.hero_by_uid(_lineup_uid(slot))
		if inst != null and inst.def().name == name_in:
			return inst
	return null


func ScorePop(label: Label, value: int, delta: int, color: Color) -> void:
	label.text = str(value)
	label.pivot_offset = label.size / 2.0
	var t := label.create_tween()
	t.tween_property(label, "scale", Vector2(Fx.Nested("score_pop.scale", 1.3), Fx.Nested("score_pop.scale", 1.3)), Fx.Nested("score_pop.dur", 0.2) * 0.5) \
		.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	t.tween_property(label, "scale", Vector2.ONE, Fx.Nested("score_pop.dur", 0.2) * 0.5)

	# 飘分
	var float_label := UiKit.L("+" + str(delta), 14, color, true)
	float_label.position = Vector2(label.global_position.x - 8, label.global_position.y + 8)
	float_label.z_index = 50
	add_child(float_label)
	var ft := float_label.create_tween()
	ft.set_parallel(true)
	ft.tween_property(float_label, "position:y", float_label.position.y - 26, 0.6).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	ft.tween_property(float_label, "modulate:a", 0.0, 0.6)
	ft.chain().tween_callback(func(): float_label.queue_free())


func UpdateProgress(quarter: int, at: float) -> void:
	var overall := (clampi(quarter, 1, 4) - 1 + clampf(at, 0.0, 1.0)) / 4.0
	_progress_fill.anchor_right = overall
	var remain_sec := int((1.0 - at) * 12 * 60)
	_time_label.text = "剩 %d'%02d\"" % [remain_sec / 60, remain_sec % 60]


static func _feed_color(type: String) -> Color:
	match type:
		"dunk":
			return Palette.Cinnabar
		"block":
			return Palette.StoneBlue
		"steal":
			return Palette.StoneBlue
		"three":
			return Palette.Gamboge
		"three_rain":
			return Palette.Gamboge
		"combo":
			return Palette.Cinnabar
		"foulout":
			return Palette.Cinnabar
		"clutch":
			return Palette.Cinnabar
		"foul":
			return Palette.Gray
		"opening":
			return Palette.StoneBlue
		"qend":
			return Palette.Gray
	return Palette.InkSoft


func AddFeedLine(text: String, type: String) -> void:
	var row := UiKit.HBox(6)
	var bar := ColorRect.new()
	bar.color = _feed_color(type)
	bar.custom_minimum_size = Vector2(4, 16)
	bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(bar)
	var bold := type == "dunk" or type == "block" or type == "foulout" or type == "combo" or type == "three_rain" or type == "three"
	var lbl := UiKit.LWrap(text, 12, Palette.Gray if (type == "opening" or type == "qend") else Palette.Ink)
	lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	if bold:
		UiKit.SetFont(lbl, true)
	row.add_child(lbl)
	_feed.add_child(row)

	if _feed.get_child_count() > 60:
		_feed.get_child(0).queue_free()

	Fx.CardEnter(row, 0.0)
	(func():
		var vs := _feed_scroll.get_v_scroll_bar()
		_feed_scroll.scroll_vertical = int(vs.max_value)).call_deferred()


# ================= 换人条（§8.4）=================
func ShowSubBar(e: MatchEvent) -> void:
	UiKit.ClearChildren(_sub_bar_holder)
	var panel := UiKit.Card(14, Color(Palette.CardBg.r, Palette.CardBg.g, Palette.CardBg.b, 0.98), Palette.Cinnabar, 2, 8)
	panel.anchor_left = 0.0
	panel.anchor_right = 1.0
	panel.anchor_top = 1.0
	panel.anchor_bottom = 1.0
	panel.offset_left = 12
	panel.offset_right = -12
	panel.offset_top = -150
	panel.offset_bottom = -96
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_sub_bar_holder.add_child(panel)

	var v := UiKit.VBox(6)
	panel.add_child(v)
	var head := UiKit.HBox(6)
	head.add_child(UiKit.L("六犯离场！%s 顶替登场（3 秒内可改选）" % e.sub_in_name, 12, Palette.Cinnabar, true))
	var sp := Control.new()
	sp.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	head.add_child(sp)
	var countdown := UiKit.L("3", 14, Palette.Gray, true)
	head.add_child(countdown)
	v.add_child(head)

	var row := UiKit.HBox(6)
	v.add_child(row)
	var bench_insts: Array = []
	for slot in Slots.Bench:
		var inst := GameState.hero_by_uid(_lineup_uid(slot))
		if inst != null and not _entered_bench.has(inst.uid):
			bench_insts.append(inst)
	if bench_insts.size() == 0:
		row.add_child(UiKit.L("替补席无人可用", 11, Palette.Gray))

	var sub_idx := e.sub_index
	var picked := [false]
	var taken := 0
	for inst in bench_insts:
		if taken >= 3:
			break
		taken += 1
		var b := UiKit.BtnSecondary(inst.def().name, func():
			if picked[0] or sub_idx < 0:
				return
			picked[0] = true
			_overrides[sub_idx] = inst.uid
			_result = MatchEngine.simulate(_player_team, _opp_team, _seed, true, _overrides)
			UiKit.ClearChildren(_sub_bar_holder)
			UiKit.Toast("%s 替补登场（已重演后续赛况）" % inst.def().name)
			RefreshMyRow(), 40, 13)
		row.add_child(b)

	# 3 秒倒计时（超时自动 = 引擎自动选择）
	var holder := _sub_bar_holder
	var tw := countdown.create_tween()
	tw.tween_method(func(v2: float):
		if is_instance_valid(countdown):
			countdown.text = str(maxi(0, int(ceil(v2)))), 3.0, 0.0, 3.0) \
		.set_trans(Tween.TRANS_LINEAR)
	tw.tween_callback(func():
		if is_instance_valid(holder):
			UiKit.ClearChildren(holder))

	Fx.RiseIn(panel, 20)


# ================= 控制 =================
func SetSpeed(s: float) -> void:
	_speed = s
	SetSpeedVisual()


func SetSpeedVisual() -> void:
	_style_speed(_b1, is_equal_approx(_speed, 1.0))
	_style_speed(_b2, is_equal_approx(_speed, 2.0))
	_style_speed(_b4, is_equal_approx(_speed, 4.0))


func _style_speed(b: Button, active: bool) -> void:
	b.add_theme_color_override("font_color", Color.WHITE if active else Palette.Ink)
	b.add_theme_color_override("font_hover_color", Color.WHITE if active else Palette.Ink)
	b.add_theme_stylebox_override("normal", UiKit.SB(Palette.Ink if active else Palette.CardBg, 22, Palette.Ink, 2))
	b.add_theme_stylebox_override("hover", UiKit.SB(Palette.Ink if active else Palette.Paper, 22, Palette.Ink, 2))
	b.add_theme_stylebox_override("pressed", UiKit.SB(Palette.Ink, 22, Palette.Ink, 2))


## 跳过：单击直接结算整场比赛。
func OnSkipPressed() -> void:
	while _event_idx + 1 < _result.events.size():
		_event_idx += 1
		var e: MatchEvent = _result.events[_event_idx]
		_shown_a = e.score_a
		_shown_b = e.score_b
	_score_a.text = str(_shown_a)
	_score_b.text = str(_shown_b)
	UpdateProgress(4, 1.0)
	AddFeedLine("比赛直接跳到终场。", "qend")
	FinishMatch()


## 跳节：快进一节比赛（走到下一节开头结算）。
func OnSkipQuarter() -> void:
	if _finished:
		return
	var cur_q: int = _result.events[_event_idx].quarter if _event_idx >= 0 else 1
	var guard := 0
	while _event_idx + 1 < _result.events.size() and guard < 2000:
		guard += 1
		var next: MatchEvent = _result.events[_event_idx + 1]
		if next.quarter > cur_q:
			break
		_event_idx += 1
		var e: MatchEvent = _result.events[_event_idx]
		_shown_a = e.score_a
		_shown_b = e.score_b
	_score_a.text = str(_shown_a)
	_score_b.text = str(_shown_b)
	var last: MatchEvent = _result.events[_event_idx] if _event_idx >= 0 else null
	var q := clampi(last.quarter, 1, 4) if last != null else 1
	_quarter_label.text = "加时" if q > 4 else "Q" + str(q)
	UpdateProgress(q, 1.0)
	AddFeedLine("（已快进至第 %d 节结束）" % q, "qend")
	# 同步犯规显示
	for uid in _fouls:
		if _my_cards.has(uid):
			var fv: int = _fouls[uid]
			_my_cards[uid].set_fouls(fv, fv >= 5)
	RefreshInfoPanel()
	if _event_idx + 1 >= _result.events.size():
		FinishMatch()


func ToggleAutopilot() -> void:
	_autopilot = not _autopilot
	RefreshAutoVisual()


func ShowAutoModePopup() -> void:
	var v := UiKit.VBox(8)
	v.add_child(UiKit.L("托管模式：自动连战，输球时自动暂停", 12, Palette.InkSoft))
	var b1 := UiKit.BtnPrimary("不耗粮托管（体力 0 也继续）", func():
		_autopilot = true
		_autopilot_food = false
		RefreshAutoVisual()
		if UiPopup.Current != null:
			UiPopup.Current.Close(), 46, 14)
	var b2 := UiKit.BtnSecondary("耗粮托管（赛前自动补至 ≥90）", func():
		_autopilot = true
		_autopilot_food = true
		RefreshAutoVisual()
		if UiPopup.Current != null:
			UiPopup.Current.Close(), 46, 14)
	v.add_child(b1)
	v.add_child(b2)
	UiKit.ShowPopup("托管设置", v, true, 330)


func RefreshAutoVisual() -> void:
	_auto_btn.text = "托管中 ⇄" if _autopilot else "托管：关"
	_auto_capsule.text = ("托管中 · 自动开赛（耗粮）" if _autopilot_food else "托管中 · 自动开赛") if _autopilot else ""
	_auto_capsule.add_theme_color_override("font_color", Palette.Bamboo if _autopilot else Palette.Gray)


# ================= 结算 =================
func FinishMatch() -> void:
	if _finished:
		return
	_finished = true

	var won := _result.player_won
	var my_score := _result.player_score
	var opp_score := _result.opp_score
	var margin := _result.margin

	# 体力结算（§3.4：上场过 −12）
	ConsumeStamina()
	GameState.D.stats.total_matches += 1
	if margin > GameState.D.stats.best_margin:
		GameState.D.stats.best_margin = margin
	SaveSystem.mark_dirty()

	if _mode == BattleMode.NORMAL:
		if won:
			GameState.D.stats.total_wins += 1
			var run := GameState.run
			run.streak += 1
			run.current_k += 1
			var coins := WinCoins(run.streak)
			run.coins_earned += coins
			GameState.add_coins(coins)
			if run.streak > GameState.D.stats.max_win_streak:
				GameState.D.stats.max_win_streak = run.streak
			if Ftue.Step() == 1:
				Ftue.OnFirstBattleWin()
			SaveSystem.save_game()
			ShowResultOverlay(true, my_score, opp_score, coins, run.streak)
		else:
			ShowRevive()
	else:
		if won:
			GameState.D.stats.total_wins += 1
			var run2 := GameState.challenge
			DungeonSystem.record_progress(run2.dungeon_n, run2.index)
			if run2.index >= DungeonSystem.teams_per_dungeon():
				var reward := DungeonSystem.on_clear(run2.dungeon_n)
				SaveSystem.save_game()
				ShowDungeonClear(reward)
			else:
				run2.index += 1
				run2.current_opponent = OpponentGen.dungeon(run2.dungeon_n, run2.index, OpponentGen.dungeon_seed(run2.dungeon_n, run2.index))
				SaveSystem.save_game()
				ShowResultOverlay(true, my_score, opp_score, 0, 0)
		else:
			ShowResultOverlay(false, my_score, opp_score, 0, 0)


static func WinCoins(streak: int) -> int:
	var c := ConfigDb.I.C
	var v: float = (c.win_coin_base + c.win_coin_growth * (streak - 1)) * (1 + c.coin_dungeon_bonus * GameState.D.dungeon_cleared_count)
	return int(roundf(v))


func ConsumeStamina() -> void:
	var cost := ConfigDb.I.C.stamina_cost_per_match
	var seen := {}
	var slots_concat: Array = []
	slots_concat.append_array(Slots.Field)
	slots_concat.append_array(Slots.Bench)
	for slot in slots_concat:
		var inst := GameState.hero_by_uid(_lineup_uid(slot))
		if inst == null:
			continue
		var played := Slots.is_field(slot) or _entered_bench.has(inst.uid)
		if played and not seen.has(inst.uid):
			seen[inst.uid] = true
			inst.sta = maxi(0, inst.sta - cost)
	SaveSystem.mark_dirty()


func ShowResultOverlay(won: bool, my: int, opp: int, coins: int, streak: int) -> void:
	var panel := UiKit.Card(16, Color(Palette.CardBg.r, Palette.CardBg.g, Palette.CardBg.b, 0.98),
		Palette.Gamboge if won else Palette.Gray, 2, 14)
	panel.anchor_left = 0.5
	panel.anchor_right = 0.5
	panel.anchor_top = 0.5
	panel.anchor_bottom = 0.5
	panel.offset_left = -160
	panel.offset_right = 160
	panel.offset_top = -170
	panel.offset_bottom = 170
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(panel)
	Fx.PopIn(panel)

	var v := UiKit.VBox(8)
	panel.add_child(v)

	var title := UiKit.L("胜 利" if won else "惜 败", 30, Palette.Cinnabar if won else Palette.Gray, true)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(title)

	var score_line := UiKit.L("%d : %d" % [my, opp], 26, Palette.Ink, true)
	score_line.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(score_line)

	if won and _mode == BattleMode.NORMAL:
		v.add_child(UiKit.L("连胜 %d 场 · 金币 +%s" % [streak, UiKit.Fmt(coins)], 14, Palette.Gamboge, true))
	if won and _mode == BattleMode.CHALLENGE:
		v.add_child(UiKit.L("已击败 %d/10 队" % (GameState.challenge.index - 1), 13, Palette.StoneBlue, true))

	# MVP 与数据
	var mvp: PlayerBox = _result.box_a[0] if _result.box_a.size() > 0 else null
	if mvp != null:
		v.add_child(UiKit.L("MVP：%s %d 分 · %d 帽 · %d 断" % [mvp.name, mvp.points, mvp.blocks, mvp.steals], 12, Palette.InkSoft))
	v.add_child(UiKit.L("终场 %d:%d · 分差 %d" % [_result.final_a, _result.final_b, _result.margin], 11, Palette.Gray))

	var btn_row := UiKit.HBox(6)
	if won:
		var next_text := "下一场" if _mode == BattleMode.NORMAL else "下一队"
		var next := UiKit.BtnPrimary(next_text, func():
			panel.queue_free()
			ContinueNextMatch(), 50, 17)
		next.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		btn_row.add_child(next)

		# 结算界面可直接更换球员（替补席换上）
		var swap := UiKit.BtnSecondary("更换球员", func(): ShowSwapPopup(), 50, 13)
		swap.custom_minimum_size = Vector2(96, 50)
		btn_row.add_child(swap)

		var exit_btn := UiKit.BtnSecondary("结束本局" if _mode == BattleMode.NORMAL else "退出挑战", func():
			panel.queue_free()
			EndRun(), 50, 13)
		exit_btn.custom_minimum_size = Vector2(88, 50)
		btn_row.add_child(exit_btn)
	else:
		var back := UiKit.BtnPrimary("结束本局" if _mode == BattleMode.NORMAL else "返回挑战地图", func():
			panel.queue_free()
			EndRun(), 50, 17)
		back.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		btn_row.add_child(back)
	v.add_child(btn_row)

	if _autopilot and won:
		var tip := UiKit.L("托管中 · 1.5 秒后自动开赛（点此处取消）", 10, Palette.Bamboo)
		tip.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		v.add_child(tip)
		tip.gui_input.connect(func(ev):
			if ev is InputEventMouseButton and ev.pressed:
				_autopilot = false
				RefreshAutoVisual()
				tip.text = "已取消自动续战")
		_auto_continue_pending = true
		_auto_continue_at = Time.get_ticks_msec() / 1000.0 + 1.5


# ================= 复活（ADP_REVIVE）=================
func ShowRevive() -> void:
	var run := GameState.run
	var v := UiKit.VBox(8)
	v.add_child(UiKit.LWrap(
		"本轮已连胜 %d 场，累计 %s 金币。\n看一段视频，全队体力回满并再战一次！" % [run.streak, UiKit.Fmt(run.coins_earned)],
		13, Palette.InkSoft))

	var revive_left := not run.revive_used
	if revive_left:
		var ad_wrap := UiKit.AdButton("看视频 · 全队体力回满 + 再战 1 次", Fx.Nested("ad_ring_seconds", 15.0), func():
			Ads.show_revive(func(ok):
				if not ok:
					UiKit.Toast("广告暂不可用，可直接结束本局")
					return
				if UiPopup.Current != null:
					UiPopup.Current.Close()
				ReviveAndRematch()))
		v.add_child(ad_wrap)
	else:
		v.add_child(UiKit.L("本局复活机会已用完", 12, Palette.Gray))

	var give_up := UiKit.BtnSecondary("结束本局", func():
		if UiPopup.Current != null:
			UiPopup.Current.Close()
		EndRun(), 46, 15)
	v.add_child(give_up)

	UiKit.ShowPopup("复活一次？", v, false, 336)


func ReviveAndRematch() -> void:
	# 全队体力回满（上限为各自体力上限）
	for p in LineupUtil.field_players():
		if p != null:
			p.sta = HeroCalc.sta_cap_total(p)
	for b in LineupUtil.bench_players():
		if b != null:
			b.sta = HeroCalc.sta_cap_total(b)
	SaveSystem.save_game()
	UiKit.Toast("全队体力已回满！再战同一对手")
	StartMatch()  # 重新开一场（同对手，新种子）


# ================= 续战 / 结束 =================
func ContinueNextMatch() -> void:
	if _mode == BattleMode.NORMAL:
		var run := GameState.run
		# 耗粮托管：赛前补体力
		if _autopilot and _autopilot_food:
			RefillWithFood()
		run.current_opponent = OpponentGen.normal(run.current_k, GameRandom.new_seed())
		SaveSystem.mark_dirty()
		StartMatch()
	else:
		var run2 := GameState.challenge
		if run2.current_opponent == null:
			run2.current_opponent = OpponentGen.dungeon(run2.dungeon_n, run2.index, OpponentGen.dungeon_seed(run2.dungeon_n, run2.index))
		StartMatch()


func RefillWithFood() -> void:
	var field: Array = []
	for p in LineupUtil.field_players():
		if p != null:
			field.append(p)
	var need := func() -> bool:
		for p in field:
			if p.sta < 90:
				return true
		return false
	# 顺序：粮草（全队） → 军粮 → 干粮（上限为各自体力上限）
	while need.call() and GameState.get_food("forage") > 0:
		GameState.use_food("forage", 1)
		for p in field:
			p.sta = mini(HeroCalc.sta_cap_total(p), p.sta + 35)
	while need.call() and GameState.get_food("ration") > 0:
		GameState.use_food("ration", 1)
		var lowest: HeroInst = null
		for p in field:
			if lowest == null or p.sta < lowest.sta:
				lowest = p
		lowest.sta = mini(HeroCalc.sta_cap_total(lowest), lowest.sta + 50)
	while need.call() and GameState.get_food("dry") > 0:
		GameState.use_food("dry", 1)
		var lowest2: HeroInst = null
		for p in field:
			if lowest2 == null or p.sta < lowest2.sta:
				lowest2 = p
		lowest2.sta = mini(HeroCalc.sta_cap_total(lowest2), lowest2.sta + 25)
	if need.call():
		_autopilot_food = false
		RefreshAutoVisual()
		UiKit.Toast("兵粮不足，已自动降级为「不耗粮」托管")
	SaveSystem.mark_dirty()


## 结算界面 · 更换球员：替补席换上，下一场生效。
func ShowSwapPopup(bench_uid := "") -> void:
	var v := UiKit.VBox(6)
	if bench_uid.is_empty():
		v.add_child(UiKit.L("先选择要换上场的替补球员", 12, Palette.Gray))
		var bench := LineupUtil.available_bench()
		if bench.size() == 0:
			v.add_child(UiKit.L("替补席没有球员（可在阵容页解锁并指派）", 12, Palette.Gray))
		for h in bench:
			var uid: String = h.uid
			var row := ItemUi.HeroRow(h, "换上", func():
				if UiPopup.Current != null:
					UiPopup.Current.Close()
				ShowSwapPopup(uid), true)
			v.add_child(row)
	else:
		v.add_child(UiKit.L("再选择被换下的场上球员", 12, Palette.Gray))
		for slot in Slots.Field:
			var inst := GameState.hero_by_uid(_lineup_uid(slot))
			if inst == null:
				continue
			var s: String = slot
			var row2 := ItemUi.HeroRow(inst, "换下（" + Slots.cn(s) + "）", func(): SwapLineup(bench_uid, s), true)
			v.add_child(row2)
	UiKit.ShowPopup("更换球员", v, true, 330)


func SwapLineup(bench_uid: String, field_slot: String) -> void:
	var lineup := GameState.D.lineup
	var bench_slot := ""
	for s in Slots.Bench:
		if str(lineup.get(s, "")) == bench_uid:
			bench_slot = s
			break
	if bench_slot.is_empty():
		UiKit.Toast("该球员不在替补席")
		return
	var old_uid := str(lineup.get(field_slot, ""))
	if old_uid.is_empty():
		lineup.erase(bench_slot)
	else:
		lineup[bench_slot] = old_uid
	lineup[field_slot] = bench_uid
	SaveSystem.save_game()
	GameState.touch_best_team_ovr()
	if UiPopup.Current != null:
		UiPopup.Current.Close()
	UiKit.Toast("已更换球员，下一场生效")


func EndRun() -> void:
	_auto_continue_pending = false
	# 体力消耗只在一局内呈现 —— 局末全队回满
	GameState.restore_all_stamina()
	GameState.touch_best_team_ovr()
	if _mode == BattleMode.NORMAL:
		var run := GameState.run
		GameState.D.stats.total_runs += 1
		run.active = false
		SaveSystem.save_game()
		UiKit.PopScreen()
		UiKit.PushScreen(RunSummaryScreen.new(run))
	else:
		GameState.challenge.active = false
		SaveSystem.save_game()
		UiKit.PopScreen()
		(func():
			if ScreenManager.I.Top() is ChallengeScreen:
				ScreenManager.I.Top().Rebuild()).call_deferred()


func ShowDungeonClear(reward: Array) -> void:
	var v := UiKit.VBox(8)
	v.add_child(UiKit.L("大 捷！本副本 10 队全部击破", 15, Palette.Cinnabar, true))
	v.add_child(UiKit.L("解锁下一副本，通关奖励已入包：", 12, Palette.InkSoft))
	var grid := GridContainer.new()
	grid.columns = 6
	for r in reward:
		grid.add_child(ItemUi.DrawCell(r, 48))
	var scroll := ScrollContainer.new()
	scroll.custom_minimum_size = Vector2(0, 190 if reward.size() > 18 else 100)
	scroll.add_child(grid)
	v.add_child(scroll)
	var back := UiKit.BtnPrimary("返回挑战地图", func():
		if UiPopup.Current != null:
			UiPopup.Current.Close()
		UiKit.PopScreen()
		(func():
			if ScreenManager.I.Top() is ChallengeScreen:
				ScreenManager.I.Top().Rebuild()).call_deferred(), 48, 16)
	v.add_child(back)
	UiKit.ShowPopup("通关结算", v, false, 344)
