class_name ChallengeScreen
extends Control
## SCR_CHALLENGE · 挑战模式：20 副本纵向地图（02 文档 §10）。

var _list: VBoxContainer


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var bg := ColorRect.new()
	bg.color = Palette.Paper
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(bg)

	var margin := UiKit.Pad(16, 48, 16, 8)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(margin)
	var root := UiKit.VBox(8)
	margin.add_child(root)

	var head := UiKit.HBox(8)
	head.add_child(UiKit.BtnSecondary("← 返回", func(): UiKit.PopScreen(), 36, 13))
	var sp := Control.new()
	sp.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	head.add_child(sp)
	head.add_child(UiKit.L("挑战模式", 17, Palette.Ink, true))
	head.add_child(UiKit.BtnGhost("奖励预览", func(): UiKit.Toast("点选副本节点查看通关奖励"), 12, Palette.StoneBlue))
	root.add_child(head)

	root.add_child(UiKit.L("20 副本 · 每关 10 队 · 通关解锁下一关（失败不退费）", 11, Palette.Gray))

	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	root.add_child(scroll)
	_list = UiKit.VBox(6)
	_list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(_list)
	_build_list()


## 刷新地图（进度变化后调用）。
func Rebuild() -> void:
	if _list == null:
		return
	UiKit.ClearChildren(_list)
	_build_list()


func _build_list() -> void:
	var map_tex := Art.bg("bg_challenge")
	var i := 0
	for n in range(1, DungeonSystem.max_dungeon() + 1):
		_list.add_child(_build_node(n, map_tex, i))
		i += 1


func _build_node(n: int, _map_tex: Texture2D, idx: int) -> Control:
	var cleared := DungeonSystem.is_cleared(n)
	var unlocked := DungeonSystem.is_unlocked(n)
	var best := DungeonSystem.best_defeated(n)
	var name := OpponentGen.dungeon_name(n)
	var fee := DungeonSystem.fee(n)
	var ovr_from := DungeonSystem.opp_ovr(n, 1)
	var ovr_to := DungeonSystem.opp_ovr(n, 10)

	var border := Palette.Cinnabar if cleared else (Palette.Ink if unlocked else Color(0, 0, 0, 0.18))
	var card := UiKit.Card(12, Color("#FBEAE6") if cleared else Palette.CardBg, border, 2, 10)
	if not unlocked:
		card.modulate = Color(1, 1, 1, 0.62)

	var h := UiKit.HBox(10)
	card.add_child(h)

	var no_box := UiKit.Card(10, Palette.Cinnabar if cleared else (Palette.StoneBlue if unlocked else Palette.Gray), null, 0, 0)
	no_box.custom_minimum_size = Vector2(38, 38)
	var no_label := UiKit.L("✓" if cleared else str(n), 15, Color.WHITE, true)
	no_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	no_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	no_box.add_child(no_label)
	h.add_child(no_box)

	var col := UiKit.VBox(2)
	col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	col.add_child(UiKit.L("第 %d 关 · %s%s" % [n, name, "（已通关）" if cleared else ("" if unlocked else "（未解锁）")], 14, Palette.Ink, true))
	col.add_child(UiKit.L("对手 OVR %d → %d · 10 队 · 进度 %d/10" % [ovr_from, ovr_to, best], 11, Palette.Gray))
	col.add_child(UiKit.L(("进场费 %s 金币" % UiKit.Fmt(fee)) if unlocked else ("通关「%s」后解锁" % OpponentGen.dungeon_name(n - 1)), 11, Palette.Gamboge if unlocked else Palette.Gray))
	h.add_child(col)

	if unlocked:
		card.mouse_filter = Control.MOUSE_FILTER_STOP
		var dn := n
		card.gui_input.connect(func(ev):
			if ev is InputEventMouseButton and ev.pressed and ev.button_index == MOUSE_BUTTON_LEFT:
				ShowDungeonPanel(dn))
		if not cleared:
			# 当前可打节点脉冲光
			var pulse := Panel.new()
			pulse.mouse_filter = Control.MOUSE_FILTER_IGNORE
			pulse.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
			pulse.add_theme_stylebox_override("panel", UiKit.SB(Color(0, 0, 0, 0), 12, Palette.Bamboo, 2))
			card.add_child(pulse)
			var t := pulse.create_tween()
			t.set_loops()
			t.tween_property(pulse, "modulate:a", 0.25, 0.8)
			t.tween_property(pulse, "modulate:a", 1.0, 0.8)

	Fx.CardEnter(card, mini(idx, 8) * 0.03)
	return card


# ---------------- 调试入口（截图/自测） ----------------
func DebugShowDungeon(n: int) -> void:
	ShowDungeonPanel(n)


func ShowDungeonPanel(n: int) -> void:
	var cleared := DungeonSystem.is_cleared(n)
	var best := DungeonSystem.best_defeated(n)
	var fee := DungeonSystem.fee(n)
	var ovr_from := DungeonSystem.opp_ovr(n, 1)
	var ovr_to := DungeonSystem.opp_ovr(n, 10)

	var v := UiKit.VBox(8)
	v.add_child(UiKit.L("第 %d 关 · %s%s" % [n, OpponentGen.dungeon_name(n), " ✓ 已通关" if cleared else ""], 16, Palette.Ink, true))
	v.add_child(UiKit.L("对手 OVR %d → %d · 共 10 队" % [ovr_from, ovr_to], 12, Palette.InkSoft))
	v.add_child(UiKit.L("进度：已击败 %d/10（重进从第 1 队重打，已击败队名有 ✓）" % best, 11, Palette.Gray))

	# 通关奖励预览
	v.add_child(UiKit.L("通关奖励（首次通关发放）：", 12, Palette.InkSoft, true))
	var rewards := DungeonSystem.build_rewards(n)
	var r_grid := GridContainer.new()
	r_grid.columns = 6
	r_grid.add_theme_constant_override("h_separation", 4)
	r_grid.add_theme_constant_override("v_separation", 4)
	for r in rewards:
		r_grid.add_child(ItemUi.DrawCell(r, 48))
	var r_scroll := ScrollContainer.new()
	r_scroll.custom_minimum_size = Vector2(0, 192 if rewards.size() > 24 else 120)
	r_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	r_scroll.add_child(r_grid)
	v.add_child(r_scroll)

	var btns := UiKit.HBox(8)
	var enter_btn := UiKit.BtnPrimary("进入 %s 金币" % UiKit.Fmt(fee), func():
		if UiPopup.Current != null:
			UiPopup.Current.Close()
		ConfirmEnter(n, fee), 46, 15)
	enter_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	btns.add_child(enter_btn)
	var list_btn := UiKit.BtnSecondary("查看队伍列表", func(): ShowTeamList(n), 46, 14)
	list_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	btns.add_child(list_btn)
	v.add_child(btns)

	UiKit.ShowPopup("副本详情", v, true, 348)


func ConfirmEnter(n: int, fee: int) -> void:
	if GameState.D.coins < fee:
		UiKit.Toast("金币不足（还差 %s），去打普通模式" % UiKit.Fmt(fee - GameState.D.coins))
		return
	UiKit.Confirm("进入第 %d 关 · %s" % [n, OpponentGen.dungeon_name(n)],
		"支付进场费 %s 金币。\n注意：中途失败不退费，可从第 1 队重来。" % UiKit.Fmt(fee),
		"支付 %s 金币 进入" % UiKit.Fmt(fee), func():
			if not GameState.try_spend_coins(fee):
				return
			var run := ChallengeRun.new()
			run.active = true
			run.dungeon_n = n
			run.index = 1
			run.fee = fee
			run.current_opponent = OpponentGen.dungeon(n, 1, OpponentGen.dungeon_seed(n, 1))
			GameState.challenge = run
			SaveSystem.save_game()
			UiKit.PushScreen(BattleScreen.new(BattleScreen.BattleMode.CHALLENGE)), "取消", true)


func ShowTeamList(n: int) -> void:
	var v := UiKit.VBox(4)
	var scroll := ScrollContainer.new()
	scroll.custom_minimum_size = Vector2(0, 360)
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	var list := UiKit.VBox(4)
	list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(list)
	v.add_child(scroll)

	var best := DungeonSystem.best_defeated(n)
	for m in range(1, 11):
		var team := OpponentGen.dungeon(n, m, OpponentGen.dungeon_seed(n, m))
		var h := UiKit.HBox(6)
		var beaten := m <= best
		var badge := UiKit.L("✓" if beaten else str(m), 13, Palette.Cinnabar if beaten else Palette.InkSoft, true)
		badge.custom_minimum_size = Vector2(22, 0)
		h.add_child(badge)
		var col := UiKit.VBox(1)
		col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		col.add_child(UiKit.L("%s · OVR %d" % [team.name, team.avg_court_ovr()], 13, Palette.Ink, true))
		var comp_parts: Array = []
		for p in team.court:
			comp_parts.append(Quality.cn(p.quality) + p.name)
		col.add_child(UiKit.L(" ".join(PackedStringArray(comp_parts)), 10, Palette.Gray))
		h.add_child(col)
		var card := UiKit.Card(10, Palette.CardBg, Palette.Cinnabar if beaten else Color(0, 0, 0, 0.1), 2 if beaten else 1, 6)
		card.add_child(h)
		list.add_child(card)
	UiKit.ShowPopup("第 %d 关 · 队伍侦查" % n, v, true, 344)
