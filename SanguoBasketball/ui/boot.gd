class_name Boot
## 启动引导（BOOT → LOADING → MAIN_MENU）。

static var ShotsMode := false


static func run() -> void:
	UiKit.Init()
	ConfigDb.load_config()
	SaveSystem.load_game()
	Ads.detect()
	MockAdService.player = func(placement_id, done): MockAdOverlay.Play(placement_id, done)

	var args := OS.get_cmdline_user_args()
	if has_flag(args, "--selftest"):
		var fails := SelfTest.run()
		print("SELFTEST_FAILS=" + str(fails))
		Engine.get_main_loop().quit(fails)
		return

	ShotsMode = has_flag(args, "--shots")
	if has_flag(args, "--freshtest"):
		GameState.D = SaveSystem.create_fresh()
	if has_flag(args, "--rich"):
		# 截图/演示用：发一笔资源（开发用途）
		GameState.add_coins(120000)
		GameState.add_tali("copper", 60)
		GameState.add_tali("jade", 6)
		for d in ConfigDb.I.by_quality("purple"):
			GameState.add_hero(d.id, 3)
		for d in ConfigDb.I.by_quality("gold"):
			GameState.add_hero(d.id, 3)
		for slot in Slots.All:
			if not GameState.is_slot_unlocked(slot):
				GameState.D.unlocked_slots.append(slot)
		GameState.add_mat("MAT_BRACER", "gold", 30)
		GameState.add_mat("MAT_PAD", "purple", 40)
		GameState.add_mat("MAT_BOOK", "purple", 20)
		GameState.add_mat("MAT_BOOK", "gold", 6)
		GameState.add_mat("MAT_SAND", "purple", 20)
		GameState.add_mat("MAT_SAND", "gold", 6)
		GameState.add_food("forage", 20)
		GameState.add_food("ration", 30)
		LineupUtil.auto_fill()
		SaveSystem.save_game()

	var root := Shell.instance
	root.theme = UiKit.UiTheme
	var mgr := ScreenManager.new()
	root.add_child(mgr)
	mgr.Push(TabsScreen.new(), false)

	if ShotsMode:
		ShotDriver.Start()
	else:
		Ftue.MaybeIntro(null)

	# FTUE 引导截图（开发校验）：开场页 → 发资源 + 主界面引导提示
	if has_flag(args, "--ftueshot"):
		var t1 := Timer.new()
		t1.wait_time = 1.0
		t1.one_shot = true
		t1.timeout.connect(func(): ShotDriver.CaptureNow("00_ftue_intro"))
		root.add_child(t1)
		t1.start()
		var t2 := Timer.new()
		t2.wait_time = 2.0
		t2.one_shot = true
		t2.timeout.connect(func():
			if UiPopup.Current != null:
				UiPopup.Current.Close()
			if Ftue.Step() == 0:
				Ftue.GrantStartResources()
			Ftue.TipMainStart())
		root.add_child(t2)
		t2.start()
		var t3 := Timer.new()
		t3.wait_time = 3.4
		t3.one_shot = true
		t3.timeout.connect(func():
			ShotDriver.CaptureNow("00_ftue_tip")
			Engine.get_main_loop().quit(0))
		root.add_child(t3)
		t3.start()

	if has_flag(args, "--uidebug"):
		print("[UIDebug] win=%s screen=%s scale=%s" % [DisplayServer.window_get_size(), DisplayServer.screen_get_size(), DisplayServer.screen_get_scale()])
		print("[UIDebug] vpRect=%s shellSize=%s" % [root.get_viewport_rect(), root.size])
		var t := Timer.new()
		t.wait_time = 1.0
		t.one_shot = true
		t.timeout.connect(func():
			var nav := TabsScreen.I.Nav
			print("[UIDebug] nav pos=%s size=%s" % [nav.global_position, nav.size])
			var tab := TabsScreen.I.GetTab()
			print("[UIDebug] tab=%s pos=%s size=%s" % [tab, tab.position, tab.size])
			_dump_tree(tab, 0, 4)
			var cs := ChallengeScreen.new()
			UiKit.PushScreen(cs)
			var t2 := Timer.new()
			t2.wait_time = 1.2
			t2.one_shot = true
			t2.timeout.connect(func():
				_dump_tree(cs, 0, 5)
				Engine.get_main_loop().quit(0))
			root.add_child(t2)
			t2.start())
		root.add_child(t)
		t.start()


static func has_flag(args: PackedStringArray, flag: String) -> bool:
	return args.has(flag)


static func _dump_tree(n: Node, depth: int, max_depth: int) -> void:
	if depth > max_depth:
		return
	var indent := " ".repeat(depth * 2)
	if n is Control:
		print("[UIDebug] %s%s pos=%s size=%s off=(%s,%s,%s,%s)" % [indent, n.get_class(), n.position, n.size, n.offset_left, n.offset_top, n.offset_right, n.offset_bottom])
	else:
		print("[UIDebug] %s%s" % [indent, n.get_class()])
	for child in n.get_children():
		_dump_tree(child, depth + 1, max_depth)
