class_name Ftue
## 新手引导（FTUE，01 文档 §10）：3 屏开场 → 发资源 → 教学局 → 免费十连 → 阵容。
## 期间不出现任何需要阅读超过 2 行的弹窗。


static func Step() -> int:
	return GameState.D.ftue.step


static func MaybeIntro(after) -> void:
	if Step() > 0:
		if after != null:
			after.call()
		return
	_show_intro_page(1, after)


static func _show_intro_page(page: int, after) -> void:
	var title := ""
	var text := ""
	var art_key := ""
	match page:
		1:
			title = "群英穿越"
			text = "三国群英，一夜之间穿越到了篮球场。而你，是这支球队的领队。"
			art_key = "banner_market"
		2:
			title = "你的使命"
			text = "招募武将、锤炼阵容，让关羽投三分、吕布抢篮板、诸葛亮当教练。"
			art_key = "banner_barrack"
		_:
			title = "全军出击"
			text = "收下启程物资，去赢下第一场比赛吧！"
			art_key = "bg_main"

	var v := UiKit.VBox(10)
	var tex := Art.bg(art_key)
	if tex != null:
		var tr := TextureRect.new()
		tr.texture = tex
		tr.custom_minimum_size = Vector2(0, 132)
		tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		tr.clip_contents = true
		v.add_child(tr)
	v.add_child(UiKit.LWrap(text, 14, Palette.InkSoft))
	var btn := UiKit.BtnPrimary("继 续" if page < 3 else "领取物资，出发！", func():
		if UiPopup.Current != null:
			UiPopup.Current.Close()
		if page < 3:
			(func(): _show_intro_page(page + 1, after)).call_deferred()
		else:
			GrantStartResources()
			if after != null:
				after.call()
			(func(): TipMainStart()).call_deferred()
	, 48, 17)
	v.add_child(btn)

	UiKit.ShowPopup("序章 · " + title, v, false, 320)


## 发放初始资源（12 绿卡、金币 500、铜虎符 5、干粮 3）。
static func GrantStartResources() -> void:
	for def in ConfigDb.I.by_quality("green"):
		GameState.add_hero(def.id, 1)
	GameState.add_coins(ConfigDb.I.C.ext.ftue_coins)
	GameState.add_tali("copper", ConfigDb.I.C.ext.ftue_copper)
	GameState.add_food("dry", ConfigDb.I.C.ext.ftue_dry)
	LineupUtil.auto_fill()
	GameState.D.ftue.step = 1
	SaveSystem.save_game()
	UiKit.Toast("启程物资已到账：12 名将士 · 金币 500 · 铜虎符 5 · 干粮 3")
	# 刷新主页（阵容与资源即时可见）
	(func():
		var tabs := TabsScreen.I
		if tabs != null and tabs.GetTab() is MainTab:
			tabs.SwitchTo("main")).call_deferred()


## 教学局保送：若失败则换种子重演，直至胜利（上限 12 次）。
static func SimulateTutorial(a: BattleTeam, b: BattleTeam, seed_in: int) -> MatchResult:
	for i in 12:
		var res := MatchEngine.simulate(a, b, seed_in + i, true)
		if res.player_won:
			return res
	return MatchEngine.simulate(a, b, seed_in, true)


static func TipMainStart() -> void:
	if Step() != 1:
		return
	var tab := TabsScreen.I
	if tab == null:
		return
	var cta = tab.MainTabRef.CtaButton if tab.MainTabRef != null else null
	if cta != null:
		FtueOverlay.Show(cta.get_global_rect(), "点击「开赛」，完成你的第一场比赛（对手很弱，稳赢）", null)


static func OnFirstBattleWin() -> void:
	if Step() == 1:
		GameState.D.ftue.step = 2
		SaveSystem.mark_dirty()


static func TipGacha() -> void:
	if Step() != 2:
		return
	var tab := TabsScreen.I
	if tab == null or tab.Nav == null:
		return
	var circle = tab.Nav.CircleOf("gacha")
	if circle != null:
		FtueOverlay.Show(circle.get_global_rect(), "去抽卡！市场池有一次免费十连", null)


static func OnFreeTenUsed() -> void:
	if Step() == 2:
		GameState.D.ftue.step = 3
		SaveSystem.mark_dirty()


static func TipLineup() -> void:
	if Step() != 3:
		return
	var tab := TabsScreen.I
	if tab == null or tab.Nav == null:
		return
	var circle = tab.Nav.CircleOf("lineup")
	if circle != null:
		FtueOverlay.Show(circle.get_global_rect(), "进入阵容，把最强的卡放上场上五席", null)


static func OnLineupTouched() -> void:
	if Step() == 3:
		GameState.D.ftue.step = 4
		SaveSystem.mark_dirty()
		UiKit.Toast("恭喜出师！自由发挥吧，主公")
