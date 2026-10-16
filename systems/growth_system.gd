class_name GrowthSystem
## 武将成长系统（SYS-02）：材料使用、进化、体力。所有硬校验集中在此。


## 使用材料（§4.2，含 v2 独立资源材料）。返回实际使用数量。
static func use_material(hero: HeroInst, type_id: String, quality: String, count: int) -> int:
	var special := ConfigDb.I.special_mat(type_id)
	var mt = null
	if special == null:
		mt = ConfigDb.I.mat_type(type_id)
	if special == null and mt == null:
		return 0

	var mq: MatQ = null
	if special != null:
		mq = special.q.get(quality, MatQ.new())
	else:
		mq = ConfigDb.I.C.mat_quality.get(quality, MatQ.new())

	var used := 0
	for i in count:
		if not GameState.use_mat(type_id, quality, 1):
			break
		if special != null:
			if special.stat == "iq":
				hero.iq_bonus += mq.main
			else:
				hero.sta_cap_bonus += mq.main
			hero.mat[special.sub_idx] += mq.sub
		else:
			hero.mat[mt.main_idx] += mq.main
			hero.mat[mt.sub_idx] += mq.sub
		used += 1
	if used > 0:
		SaveSystem.mark_dirty()
	return used


## 材料使用预览：[main, sub]。
static func material_preview(type_id: String, quality: String) -> Array:
	var special := ConfigDb.I.special_mat(type_id)
	if special != null:
		var sq = special.q.get(quality)
		if sq != null:
			return [sq.main, sq.sub]
	var mq = ConfigDb.I.C.mat_quality.get(quality)
	if mq == null:
		return [0, 0]
	return [mq.main, mq.sub]


## 进化校验（v2：祭品必须为“相同的卡牌”同一武将）。err 空串即通过。
static func can_evolve(main: HeroInst, sac: HeroInst) -> String:
	if main == null or sac == null:
		return "请选择祭品卡"
	if main.uid == sac.uid:
		return "祭品不能是主卡"
	if main.star >= ConfigDb.I.C.evolve_max_star:
		return "已达最高星级"
	# v2 硬规则：只能献祭同一武将（例如山贼只能用山贼献祭）
	if sac.def_id != main.def_id:
		return "只能献祭相同的武将卡"
	# 硬校验：禁止献祭已装备于阵容的卡
	if GameState.is_equipped(sac):
		return "祭品已装备于阵容"
	return ""


## 执行进化：消耗祭品，主卡星级 +1。
static func evolve(main: HeroInst, sac: HeroInst) -> bool:
	if can_evolve(main, sac) != "":
		return false
	GameState.remove_hero(sac)
	main.star = mini(ConfigDb.I.C.evolve_max_star, main.star + 1)
	SaveSystem.mark_dirty()
	SaveSystem.save_game()
	return true


## 比赛后体力结算（§3.4）：上场过的球员 −12（下限 0）。
static func consume_stamina_after_match(played_players: Array) -> void:
	var cost := ConfigDb.I.C.stamina_cost_per_match
	for p in played_players:
		if p == null:
			continue
		p.sta = maxi(0, p.sta - cost)
	SaveSystem.mark_dirty()


## 回满（复活/局末结算；上限为各自体力上限）。
static func restore_all(players: Array) -> void:
	for p in players:
		if p != null:
			p.sta = HeroCalc.sta_cap_total(p)
	SaveSystem.mark_dirty()
