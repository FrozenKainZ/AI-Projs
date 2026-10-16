class_name GameState
## 全局游戏状态：存档 + 运行时局状态。

static var D := SaveData.new()

static var selected_mode := "normal"    # normal / challenge
static var run := NormalRun.new()
static var challenge := ChallengeRun.new()


# ---------- 资源 ----------
static func add_coins(v: int) -> void:
	D.coins = maxi(0, D.coins + v)
	SaveSystem.mark_dirty()


static func try_spend_coins(v: int) -> bool:
	if D.coins < v:
		return false
	D.coins -= v
	SaveSystem.mark_dirty()
	return true


static func add_tali(key: String, v: int) -> void:
	if key == "copper":
		D.copper = maxi(0, D.copper + v)
	else:
		D.jade = maxi(0, D.jade + v)
	SaveSystem.mark_dirty()


static func try_spend_tali(key: String, v: int) -> bool:
	if key == "copper":
		if D.copper < v:
			return false
		D.copper -= v
	else:
		if D.jade < v:
			return false
		D.jade -= v
	SaveSystem.mark_dirty()
	return true


static func add_mat(type_id: String, quality: String, count: int) -> void:
	var k := type_id + ":" + quality
	D.mats[k] = int(D.mats.get(k, 0)) + count
	SaveSystem.mark_dirty()


static func use_mat(type_id: String, quality: String, count: int) -> bool:
	var k := type_id + ":" + quality
	var cur := int(D.mats.get(k, 0))
	if cur < count:
		return false
	D.mats[k] = cur - count
	SaveSystem.mark_dirty()
	return true


static func get_mat(type_id: String, quality: String) -> int:
	return int(D.mats.get(type_id + ":" + quality, 0))


static func add_food(key: String, count: int) -> void:
	D.foods[key] = maxi(0, int(D.foods.get(key, 0)) + count)
	SaveSystem.mark_dirty()


static func use_food(key: String, count: int) -> bool:
	var cur := int(D.foods.get(key, 0))
	if cur < count:
		return false
	D.foods[key] = cur - count
	SaveSystem.mark_dirty()
	return true


static func get_food(key: String) -> int:
	return int(D.foods.get(key, 0))


# ---------- 武将 ----------
static func add_hero(def_id: String, star := 1) -> HeroInst:
	D.hero_seq += 1
	var inst := HeroInst.new()
	inst.uid = "H" + str(D.hero_seq)
	inst.def_id = def_id
	inst.star = clampi(star, 1, 6)
	inst.sta = 100
	D.heroes.append(inst)
	unlock_codex(def_id)
	SaveSystem.mark_dirty()
	return inst


static func unlock_codex(def_id: String) -> void:
	if not D.codex.has(def_id):
		D.codex.append(def_id)


static func remove_hero(inst: HeroInst) -> void:
	D.heroes.erase(inst)


static func hero_by_uid(uid: String) -> HeroInst:
	if uid.is_empty():
		return null
	for h in D.heroes:
		if h.uid == uid:
			return h
	return null


static func heroes_of_def(def_id: String) -> Array:
	var out: Array = []
	for h in D.heroes:
		if h.def_id == def_id:
			out.append(h)
	return out


## 某卡是否被阵容装备（进化/回收硬校验用，02 §3.3）。
static func is_equipped(inst: HeroInst) -> bool:
	for slot in D.lineup:
		if D.lineup[slot] == inst.uid:
			return true
	return false


## 该 defId 是否只剩这一张（图鉴最后一张保护）。
static func is_last_of_def(inst: HeroInst) -> bool:
	return heroes_of_def(inst.def_id).size() <= 1


static func equipped_slot(inst: HeroInst) -> int:
	var i := 0
	for slot in Slots.All:
		if D.lineup.get(slot, "") == inst.uid:
			return i
		i += 1
	return -1


# ---------- 七星坛日期（04:00 跨天） ----------
static func game_day() -> String:
	var dt := Time.get_datetime_dict_from_system()
	var t := Time.get_unix_time_from_datetime_dict(dt) - 14400
	return Time.get_date_string_from_unix_time(t)


static func ensure_altar_day() -> void:
	var today := game_day()
	if D.altar.date != today:
		D.altar.date = today
		D.altar.used = 0
		SaveSystem.mark_dirty()


# ---------- 槽位解锁（v2：助教/替补需金币解锁） ----------
static func is_slot_unlocked(slot: String) -> bool:
	return D.unlocked_slots.has(slot)


static func slot_unlock_cost(slot: String) -> int:
	return int(ConfigDb.I.C.ext.slot_unlock_cost.get(slot, 0))


static func try_unlock_slot(slot: String) -> bool:
	if is_slot_unlocked(slot):
		return true
	var cost := slot_unlock_cost(slot)
	if not try_spend_coins(cost):
		return false
	D.unlocked_slots.append(slot)
	SaveSystem.mark_dirty()
	SaveSystem.save_game()
	return true


# ---------- 头衔（历史最强球队综评） ----------
static func touch_best_team_ovr() -> void:
	var cur := HeroCalc.team_ovr(D.lineup)
	if cur > D.stats.best_team_ovr:
		D.stats.best_team_ovr = cur
		SaveSystem.mark_dirty()


static func rank_title() -> String:
	return ConfigDb.I.rank_title(D.stats.best_team_ovr)


## 结束一局后：全队体力回满（体力消耗只在一局内呈现）
static func restore_all_stamina() -> void:
	for h in D.heroes:
		h.sta = HeroCalc.sta_cap_total(h)
	SaveSystem.mark_dirty()
