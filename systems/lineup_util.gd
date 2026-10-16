class_name LineupUtil
## 阵容工具：一键最优、候选排序、总评（SYS-01）。


static func all_insts() -> Array:
	return GameState.D.heroes


static func is_in_lineup(inst: HeroInst) -> bool:
	for uid in GameState.D.lineup.values():
		if uid == inst.uid:
			return true
	return false


## 一键最优：按 OVR×位置偏好 贪心填充（仅已解锁槽位）。
static func auto_fill() -> void:
	var pool: Array = GameState.D.heroes.duplicate()
	var result := {}
	# 场上五槽：每槽取等价 OVR 最高者
	for slot in Slots.Field:
		var best: HeroInst = null
		var best_score := -2147483648
		for h in pool:
			var sc := HeroCalc.slot_ovr(h, slot)
			if sc > best_score:
				best_score = sc
				best = h
		if best != null:
			result[slot] = best.uid
			pool.erase(best)
	# 教练：OVR 降序取前三（仅已解锁）
	pool.sort_custom(func(a, b): return HeroCalc.ovr(a) > HeroCalc.ovr(b))
	var idx := 0
	for slot in Slots.Coach:
		if not GameState.is_slot_unlocked(slot):
			continue
		if idx >= pool.size():
			break
		result[slot] = pool[idx].uid
		idx += 1
	# 替补：接下来三名（仅已解锁）
	for slot in Slots.Bench:
		if not GameState.is_slot_unlocked(slot):
			continue
		if idx >= pool.size():
			break
		result[slot] = pool[idx].uid
		idx += 1

	GameState.D.lineup = result
	SaveSystem.mark_dirty()
	GameState.touch_best_team_ovr()


## 确保场上 5 人齐（FTUE 后自动补位）。
static func ensure_field_filled() -> void:
	var lineup := GameState.D.lineup
	var used_uids := {}
	for uid in lineup.values():
		used_uids[uid] = true
	var changed := false
	for slot in Slots.Field:
		var uid := str(lineup.get(slot, ""))
		if not uid.is_empty() and GameState.hero_by_uid(uid) != null:
			continue
		var best: HeroInst = null
		var best_score := -2147483648
		for h in GameState.D.heroes:
			if used_uids.has(h.uid):
				continue
			var sc := HeroCalc.slot_ovr(h, slot)
			if sc > best_score:
				best_score = sc
				best = h
		if best != null:
			lineup[slot] = best.uid
			used_uids[best.uid] = true
			changed = true
	if changed:
		SaveSystem.mark_dirty()


## 某槽位候选排序（阵容页候选列表）：按该槽位等价 OVR 降序。
static func candidates_for(slot: String) -> Array:
	var list: Array = GameState.D.heroes.duplicate()
	list.sort_custom(func(a, b): return HeroCalc.slot_ovr(a, slot) > HeroCalc.slot_ovr(b, slot))
	return list


## 替补席是否还有可用者（供比赛换人）。
static func available_bench() -> Array:
	var list: Array = []
	for slot in Slots.Bench:
		if not GameState.is_slot_unlocked(slot):
			continue
		var h := GameState.hero_by_uid(str(GameState.D.lineup.get(slot, "")))
		if h != null:
			list.append(h)
	return list


static func field_players() -> Array:
	var list: Array = []
	for slot in Slots.Field:
		list.append(GameState.hero_by_uid(str(GameState.D.lineup.get(slot, ""))))
	return list


static func coach_players() -> Array:
	var list: Array = []
	for slot in Slots.Coach:
		list.append(GameState.hero_by_uid(str(GameState.D.lineup.get(slot, ""))))
	return list


static func bench_players() -> Array:
	var list: Array = []
	for slot in Slots.Bench:
		list.append(GameState.hero_by_uid(str(GameState.D.lineup.get(slot, ""))))
	return list
