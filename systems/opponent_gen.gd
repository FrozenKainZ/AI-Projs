class_name OpponentGen
## 对手生成器（SYS-06/07，02 文档 §9.2 / §10.2 + 05 文档 §8）。


static func _weight_for_k(k: int) -> Array:
	if k <= 5:
		return [0.60, 0.35, 0.04, 0.01]
	if k <= 15:
		return [0.40, 0.40, 0.17, 0.03]
	if k <= 30:
		return [0.20, 0.40, 0.33, 0.07]
	if k <= 50:
		return [0.05, 0.30, 0.50, 0.15]
	return [0.00, 0.20, 0.55, 0.25]


static func _pick_by_weight(rng: GameRandom, w: Array) -> HeroDef:
	var q := "gold"
	match rng.weighted_index(w):
		0:
			q = "green"
		1:
			q = "blue"
		2:
			q = "purple"
	var pool := ConfigDb.I.by_quality(q)
	return rng.pick(pool)


## 普通模式第 k 场对手（§9.2）。tutorial=保送局（OVR 45）。
static func normal(k: int, seed_in: int, tutorial := false) -> OpponentTeam:
	var c := ConfigDb.I.C
	var rng := GameRandom.new(seed_in)
	var target := c.ext.ftue_teacher_ovr if tutorial else int(roundf(c.normal_opp_base * pow(c.normal_opp_growth, k - 1)))
	var star := clampi(1 + (k - 1) / 8, 1, 6)
	var w := _weight_for_k(k)

	# 抽 5 张，位置尽量不重复；禁止与玩家完全相同的 5 人组合（重抽至多 5 次）
	var player_defs := {}
	for x in LineupUtil.field_players():
		if x != null:
			player_defs[x.def_id] = true
	var court: Array = []
	for attempt in 5:
		court = _pick_players(rng, 5, w, star, true)
		var ds := {}
		for p in court:
			ds[p.def_id] = true
		if not _sets_equal(ds, player_defs):
			break

	# 缩放到目标 OVR
	var actual := _team_ovr(court)
	var scale := float(target) / maxf(1.0, actual)
	_apply_scale(court, scale)

	# 教练 3 张（k≥10 起才启用光环）
	var coaches := _pick_players(rng, 3, w, star, false)
	var aura := _compute_aura(coaches) if k >= 10 else 0.0

	var bench_count := 0
	if k >= 20:
		bench_count = 3
	elif k >= 15:
		bench_count = 2
	elif k >= 10:
		bench_count = 1

	var team := OpponentTeam.new()
	team.name = _team_name(rng)
	team.target_ovr = target
	team.seed_value = seed_in
	team.court = court
	team.coaches = coaches
	team.bench = _pick_players(rng, bench_count, w, star, false)
	team.aura = aura
	return team


## 挑战模式副本 n 第 m 支队伍（§10.2）。
static func dungeon(n: int, m: int, seed_in: int) -> OpponentTeam:
	var c := ConfigDb.I.C
	var rng := GameRandom.new(seed_in)
	var target: float = c.dungeon_opp_base * pow(c.dungeon_opp_growth, n - 1) * (1 + c.dungeon_team_step * (m - 1))
	var star := clampi(1 + n / 4, 1, 6)
	var gold := minf(0.05 + 0.03 * n, 0.45)
	var blue := 0.20 if n < 5 else 0.0
	var purple := maxf(0.0, 1.0 - gold - blue)
	var w := [0.0, blue, purple, gold]

	var court := _pick_players(rng, 5, w, star, true)
	var actual := _team_ovr(court)
	var scale := target / maxf(1.0, actual)
	_apply_scale(court, scale)

	var coaches := _pick_players(rng, 3, w, star, false)
	var aura := _compute_aura(coaches) if n >= 8 else 0.0
	var bench_count := 0
	if n >= 18:
		bench_count = 3
	elif n >= 12:
		bench_count = 2
	elif n >= 5:
		bench_count = 1

	var team := OpponentTeam.new()
	team.name = _team_name(rng)
	team.target_ovr = int(roundf(target))
	team.seed_value = seed_in
	team.court = court
	team.coaches = coaches
	team.bench = _pick_players(rng, bench_count, w, star, false)
	team.aura = aura
	return team


## 副本固定队伍种子（生成后固定，可重进，§10.1）。
static func dungeon_seed(n: int, m: int) -> int:
	return 100000 + n * 1000 + m * 7


# ---------- 内部 ----------
static func _sets_equal(a: Dictionary, b: Dictionary) -> bool:
	if a.size() != b.size():
		return false
	for k in a:
		if not b.has(k):
			return false
	return true


static func _pick_players(rng: GameRandom, count: int, w: Array, star: int, distinct_pref: bool) -> Array:
	var list: Array = []
	var used_pref := {}
	var guard := 0
	while list.size() < count and guard < 200:
		guard += 1
		var def := _pick_by_weight(rng, w)
		if distinct_pref and used_pref.has(def.pref) and used_pref.size() < 5:
			continue
		used_pref[def.pref] = true
		list.append(_make_player(def, star, list.size()))
	return list


static func _make_player(def: HeroDef, star: int, index: int) -> OpponentPlayer:
	var op := OpponentPlayer.new()
	op.uid = "E" + str(index) + "_" + def.id
	op.name = def.name
	op.def_id = def.id
	op.quality = def.quality_key
	op.pref = def.pref
	op.star = star
	var s := ConfigDb.I.star_s(star)
	var d := ConfigDb.I.star_d(star)
	for i in Attr.COUNT:
		op.core[i] = float(def.base[i] * s * (d if def.is_signature(i) else 1.0))
	return op


static func _team_ovr(players: Array) -> float:
	if players.is_empty():
		return 1.0
	var total := 0.0
	for p in players:
		for v in p.core:
			total += float(v)
	return total / (players.size() * Attr.COUNT)


static func _apply_scale(players: Array, scale_in: float) -> void:
	var scale := scale_in
	if scale <= 0.0001:
		scale = 0.0001
	for p in players:
		for i in Attr.COUNT:
			p.core[i] = maxf(1.0, roundf(p.core[i] * scale))


static func _compute_aura(coaches: Array) -> float:
	var c := ConfigDb.I.C
	var total := 0.0
	for i in coaches.size():
		var o: int = coaches[i].ovr()
		var rate := c.aura_hc_rate if i == 0 else c.aura_ac_rate
		total += roundf(o * rate)
	return total


static func _team_name(rng: GameRandom) -> String:
	var t := ConfigDb.I.teams
	var place: String = rng.pick(t.places)
	var suffix: String = rng.pick(t.suffixes)
	return place + suffix


## 展示用：副本名。
static func dungeon_name(n: int) -> String:
	for d in ConfigDb.I.teams.dungeons:
		if d.n == n:
			return d.name
	return "第" + str(n) + "关"
