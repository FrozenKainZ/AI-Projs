class_name DungeonSystem
## 挑战模式系统（SYS-07，02 文档 §10）。


static func max_dungeon() -> int:
	return ConfigDb.I.C.ext.dungeon_max


static func teams_per_dungeon() -> int:
	return ConfigDb.I.C.ext.dungeon_teams


## 进场费：round10(300 × 1.35^(n-1))。
static func fee(n: int) -> int:
	var c := ConfigDb.I.C
	var v := c.dungeon_fee_base * pow(c.dungeon_fee_growth, n - 1)
	var step := maxi(1, c.ext.dungeon_fee_round_to)
	return int(roundf(v / step) * step)


## 对手 OVR 显示（n, m）。
static func opp_ovr(n: int, m: int) -> int:
	var c := ConfigDb.I.C
	return int(roundf(c.dungeon_opp_base * pow(c.dungeon_opp_growth, n - 1) * (1 + c.dungeon_team_step * (m - 1))))


static func is_unlocked(n: int) -> bool:
	if n <= 1:
		return true
	return GameState.D.dungeon_cleared.has(n - 1)


static func is_cleared(n: int) -> bool:
	return GameState.D.dungeon_cleared.has(n)


## 历史最高击败队伍数（0–10）。
static func best_defeated(n: int) -> int:
	return int(GameState.D.dungeon_best.get(str(n), 0))


static func record_progress(n: int, defeated: int) -> void:
	var key := str(n)
	var cur := int(GameState.D.dungeon_best.get(key, 0))
	if defeated > cur:
		GameState.D.dungeon_best[key] = defeated
	SaveSystem.mark_dirty()


## 通关奖励（§10.3，一次性发放）。
static func build_rewards(n: int) -> Array:
	var rng := GameRandom.new(7919 + n * 131)
	var list: Array = []
	for i in 6:
		var it := DrawItem.new()
		it.type = "mat"
		it.quality = "green"
		it.mat_type_id = rng.pick(ConfigDb.I.C.ext.mat_types).id
		list.append(it)
	for i in 4:
		var it2 := DrawItem.new()
		it2.type = "mat"
		it2.quality = "blue"
		it2.mat_type_id = rng.pick(ConfigDb.I.C.ext.mat_types).id
		list.append(it2)
	for i in mini(n + 1, 6):
		var it3 := DrawItem.new()
		it3.type = "mat"
		it3.quality = "purple"
		it3.mat_type_id = rng.pick(ConfigDb.I.C.ext.mat_types).id
		list.append(it3)
	if n >= 5 and n % 5 == 0:
		var it4 := DrawItem.new()
		it4.type = "mat"
		it4.quality = "gold"
		it4.mat_type_id = rng.pick(ConfigDb.I.C.ext.mat_types).id
		list.append(it4)
	for k in ["dry", "dry", "dry", "ration", "ration", "forage"]:
		var fd := DrawItem.new()
		fd.type = "food"
		fd.food_key = k
		list.append(fd)
	var copper := 2 + n / 2
	for i in copper:
		var tl := DrawItem.new()
		tl.type = "tali"
		tl.tali_key = "copper"
		list.append(tl)
	if n % 5 == 0:
		var jd := DrawItem.new()
		jd.type = "tali"
		jd.tali_key = "jade"
		list.append(jd)
	return list


## 通关结算：发奖 + 记录 + 解锁下一副本。已通关过则只记录不重复发奖。
static func on_clear(n: int) -> Array:
	var granted: Array = []
	if not is_cleared(n):
		granted = build_rewards(n)
		GachaSystem.apply_to_inventory(granted)
		GameState.D.dungeon_cleared.append(n)
		SaveSystem.mark_dirty()
		SaveSystem.save_game()
	return granted
