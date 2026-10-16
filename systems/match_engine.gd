class_name MatchEngine
## 比赛模拟引擎（SYS-05，02 文档 §8）★核心算法★
## 纯函数式：内部克隆输入，支持种子复现与替补覆写重演（比赛数据先算后播）。

const SLOT_COUNT := 5
const LOG2E_RECIP := 1.4426950408889634   # 1/ln(2)，用于 log2


# ---------- 属性应用（统一入口） ----------
static func apply_attr(p: BattlePlayer, aura: float, slot: String, sta_mult: float) -> void:
	var field := Slots.is_field(slot)
	var pos := 1.0
	if field and p.pref_matches(slot):
		pos = ConfigDb.I.C.pos_bonus_match
	elif field:
		pos = ConfigDb.I.C.pos_bonus_other
	for i in Attr.COUNT:
		p.attr[i] = maxf(1.0, p.core[i] * pos * sta_mult + aura)


# ---------- 构建我方队伍 ----------
static func build_player_team(team_name := "我方战队") -> BattleTeam:
	var team := BattleTeam.new()
	team.is_player = true
	team.name = team_name
	var coaches := LineupUtil.coach_players()
	for co in coaches:
		if co != null:
			team.coach_insts.append(co)
	team.aura = float(HeroCalc.coach_aura(coaches))

	var field := LineupUtil.field_players()
	for i in SLOT_COUNT:
		var inst = field[i]
		if inst == null:
			continue
		var slot: String = Slots.Field[i]
		var p := _new_player(inst)
		p.slot_pos = slot
		apply_attr(p, team.aura, slot, HeroCalc.stamina_mult(inst.sta))
		p.on_court = true
		p.played_this_match = true
		team.court[i] = p
	for b in LineupUtil.bench_players():
		if b != null:
			team.bench.append(_new_player(b))
	return team


static func _new_player(inst: HeroInst) -> BattlePlayer:
	var p := BattlePlayer.new()
	p.uid = inst.uid
	p.name = inst.def().name
	p.def_id = inst.def_id
	p.quality = inst.def().quality_key
	p.pref = inst.def().pref
	p.pref2 = inst.def().pref2
	p.star = inst.star
	p.inst = inst
	p.core = HeroCalc.core_attrs(inst)
	return p


# ---------- 构建对手队伍 ----------
static func build_opponent_team(gen) -> BattleTeam:
	var team := BattleTeam.new()
	team.is_player = false
	team.name = gen.name
	team.aura = gen.aura
	for i in SLOT_COUNT:
		var op = gen.court[i] if i < gen.court.size() else gen.court[gen.court.size() - 1]
		var p := _from_opp(op)
		p.slot_pos = Slots.Field[i]
		apply_attr(p, gen.aura, p.slot_pos, 1.0)
		p.on_court = true
		p.played_this_match = true
		team.court[i] = p
	for b in gen.bench:
		team.bench.append(_from_opp(b))
	for co in gen.coaches:
		team.coach_insts.append(null)
	return team


static func _from_opp(op) -> BattlePlayer:
	var p := BattlePlayer.new()
	p.uid = op.uid
	p.name = op.name
	p.def_id = op.def_id
	p.quality = op.quality
	p.pref = op.pref
	p.star = op.star
	p.core = op.core.duplicate()
	return p


# ---------- 主模拟入口 ----------
static func simulate(raw_a: BattleTeam, raw_b: BattleTeam, seed_in: int, player_is_a: bool, overrides = null) -> MatchResult:
	var st := SimState.new()
	st.a = raw_a.clone()
	st.b = raw_b.clone()
	st.rng = GameRandom.new(seed_in)
	st.overrides = overrides
	return st.run(player_is_a)


# ============================================================
#  模拟状态机（内部）
# ============================================================
class SimState:
	var a: BattleTeam
	var b: BattleTeam
	var rng: GameRandom
	var overrides = null

	var _last_tpl := {}
	var _sub_idx := 0
	var _clutch_done := false
	var _score_a := 0
	var _score_b := 0

	func _fill(tpl: String, actor, oppo, num: int, sub, qname, x: int, y: int, trailing) -> String:
		var s := tpl
		s = s.replace("{A}", "" if actor == null else str(actor))
		s = s.replace("{B}", "" if oppo == null else str(oppo))
		s = s.replace("{N}", str(num))
		s = s.replace("{S}", "无人可换" if sub == null else str(sub))
		s = s.replace("{Q}", "" if qname == null else str(qname))
		s = s.replace("{X}", str(x))
		s = s.replace("{Y}", str(y))
		s = s.replace("{T}", "" if trailing == null else str(trailing))
		return s

	func _line(category: String, actor = null, oppo = null, num := 0, sub = null, qname = null, x := 0, y := 0, trailing = null) -> String:
		var pool: Array = ConfigDb.I.lines(category)
		var idx := 0
		if pool.size() > 1:
			var guard := 0
			while true:
				idx = rng.range_int(0, pool.size())
				guard += 1
				if not (int(_last_tpl.get(category, -1)) == idx and guard < 8):
					break
		_last_tpl[category] = idx
		return _fill(str(pool[idx]), actor, oppo, num, sub, qname, x, y, trailing)

	func run(player_is_a: bool) -> MatchResult:
		var result := MatchResult.new()
		result.team_a_name = a.name
		result.team_b_name = b.name
		result.player_is_a = player_is_a
		var c := ConfigDb.I.C

		# 球权与篮板修正（§8.1）
		var reb_a := a.avg_attr(Attr.REB)
		var reb_b := b.avg_attr(Attr.REB)
		var adj := int(roundf(c.possession_reb_cap * (reb_a - reb_b) / maxf(1.0, reb_a + reb_b)))
		adj = clampi(adj, -c.possession_reb_cap, c.possession_reb_cap)
		var qa := _split_possessions(c.possessions_base + adj)
		var qb := _split_possessions(c.possessions_base - adj)

		var events: Array = []
		var opening := MatchEvent.new()
		opening.quarter = 1
		opening.at = 0.0
		opening.type = "opening"
		opening.text = _line("opening", null, null, 0, null, str(c.ext.quarter_names[0]))
		opening.score_a = 0
		opening.score_b = 0
		events.append(opening)

		for q in range(1, 5):
			_run_segment(q, qa[q - 1], qb[q - 1], events, result, false)

		# 平局 → 加时（罕见兜底，每节各 10 回合）
		var ot := 5
		while _score_a == _score_b and ot < 9:
			_run_segment(ot, 10, 10, events, result, true)
			ot += 1

		result.final_a = _score_a
		result.final_b = _score_b
		result.events = events
		result.box_a = MatchEngine._build_boxes(a, true)
		result.box_b = MatchEngine._build_boxes(b, false)

		var mvp: PlayerBox = null
		for bx in result.box_a + result.box_b:
			if mvp == null or bx.contribution > mvp.contribution:
				mvp = bx
		if mvp != null:
			result.mvp_name = mvp.name
			result.mvp_side = "A" if mvp.is_player_side == player_is_a else "B"
		return result

	func _run_segment(q: int, pa: int, pb: int, events: Array, result: MatchResult, is_ot: bool) -> void:
		var cand: Array = []
		var three_count := {}
		var quarter_scores := {}
		var combo_tripped := {}
		var score_a0 := _score_a
		var score_b0 := _score_b
		var done_a := 0
		var done_b := 0
		var total := maxi(1, pa + pb)

		while done_a < pa or done_b < pb:
			var at := float(done_a + done_b) / total
			if done_a < pa:
				_possession(a, b, q, cand, true, three_count, quarter_scores, combo_tripped, at)
				done_a += 1
			if done_b < pb:
				_possession(b, a, q, cand, false, three_count, quarter_scores, combo_tripped, at)
				done_b += 1

		# 节末（必保留）
		var qname := "加时" if is_ot else str(ConfigDb.I.C.ext.quarter_names[clampi(q, 1, 4) - 1])
		var trailing := b.name if _score_a >= _score_b else a.name
		var qend := MatchEvent.new()
		qend.quarter = q
		qend.at = 1.0
		qend.type = "qend"
		qend.text = ("加时战罢 %d:%d！" % [_score_a, _score_b]) if is_ot else _line("qend", null, null, 0, null, qname, _score_a, _score_b, trailing)
		qend.score_a = _score_a
		qend.score_b = _score_b
		cand.append(qend)

		if not is_ot:
			result.qa[q - 1] = _score_a - score_a0
			result.qb[q - 1] = _score_b - score_b0
		result.qa_total.append(_score_a)
		result.qb_total.append(_score_b)

		MatchEngine._select_events(cand, events)

	static func _split_possessions(total: int) -> Array:
		var q: Array = [0, 0, 0, 0]
		var base_each := total / 4
		var rem := total % 4
		for i in 4:
			q[i] = base_each
		q[0] += rem   # 余数计入第 1 节（§8.1）
		return q

	# ---------- 单回合（§8.2 STEP 1–7） ----------
	func _possession(off: BattleTeam, def: BattleTeam, quarter: int, cand: Array, off_is_a: bool,
			three_count: Dictionary, quarter_scores: Dictionary, combo_tripped: Dictionary, at: float) -> void:
		var c := ConfigDb.I.C
		var off_court := off.live_court()
		var def_court := def.live_court()
		if off_court.size() == 0 or def_court.size() == 0:
			return

		for p in off_court:
			p.possessions_on_court += 1
		for p in def_court:
			p.possessions_on_court += 1

		# STEP 1 支配者
		var weights: Array = []
		for p in off_court:
			weights.append((p.attr[Attr.P3] + p.attr[Attr.MID] + p.attr[Attr.RIM]) * (0.90 + 0.20 * rng.next_float()))
		var shooter: BattlePlayer = off_court[rng.weighted_index(weights)]

		# STEP 2 进攻类型（∝ 3PT/MID/RIM 归一化）
		var type := rng.weighted_index([float(shooter.attr[Attr.P3]), float(shooter.attr[Attr.MID]), float(shooter.attr[Attr.RIM])])

		# STEP 3 对位防守（同位置，空位按均值 ×0.70）
		var slot_idx := maxi(0, Slots.Field.find(shooter.slot_pos))
		var defender: BattlePlayer = def.court[slot_idx] if (slot_idx < 5 and def.court[slot_idx] != null) else def_court[0]
		var missing := slot_idx < 5 and def.court[slot_idx] == null

		var def_val := func(attr_idx: int) -> float:
			if not missing:
				return defender.attr[attr_idx]
			var s := 0.0
			for p in def_court:
				s += p.attr[attr_idx]
			return s / maxf(1.0, float(def_court.size())) * 0.70

		var atk := 0.0
		var dfn := 0.0
		match type:
			0:
				atk = shooter.attr[Attr.P3]
				dfn = 0.55 * def_val.call(Attr.STL) + 0.45 * def_val.call(Attr.SPD)
			1:
				atk = shooter.attr[Attr.MID]
				dfn = 0.40 * def_val.call(Attr.STL) + 0.30 * def_val.call(Attr.BLK) + 0.30 * def_val.call(Attr.PHY)
			_:
				atk = 0.80 * shooter.attr[Attr.RIM] + 0.20 * shooter.attr[Attr.PHY]
				dfn = 0.60 * def_val.call(Attr.BLK) + 0.40 * def_val.call(Attr.PHY)

		# STEP 4 团队传球修正
		var pas_a := off.avg_attr(Attr.PAS)
		var pas_b := def.avg_attr(Attr.PAS)
		var pass_bonus := clampf(0.5 * (pas_a - pas_b) / maxf(1.0, pas_a + pas_b), -c.pass_bonus_cap, c.pass_bonus_cap)
		var atk_final := atk * (1.0 + pass_bonus)

		# STEP 5 对抗与判定
		var k := clampf(atk_final / maxf(1.0, dfn), c.k_min, c.k_max)
		var hit := clampf(c.hit_base + c.hit_log_coef * (log(k) * LOG2E_RECIP), c.hit_min, c.hit_max)
		var scored := rng.next_float() < hit

		var pts := 0
		var is_three_raw := type == 0
		var is_three := false
		var is_dunk := false
		if scored:
			var val := c.val_base * pow(k, c.val_exp)
			val = clampf(val, c.val_min, c.val_max)
			pts = int(roundf(val * (0.70 + 0.60 * rng.next_float())))
			if pts <= 0:
				pts = 1
			# 事件分类（§8.6）：三分得手且 ≥3；暴扣=攻框且单回合 ≥6
			is_three = is_three_raw and pts >= 3
			is_dunk = type == 2 and pts >= 6

		if scored:
			if off_is_a:
				_score_a += pts
			else:
				_score_b += pts
			shooter.points += pts
			shooter.made += 1
			shooter.attempts += 1
			if is_three:
				shooter.threes += 1

			# 连击（单节单人 ≥3 次得分）
			quarter_scores[shooter.uid] = int(quarter_scores.get(shooter.uid, 0)) + 1
			if int(quarter_scores[shooter.uid]) == 3 and not combo_tripped.has(shooter.uid):
				combo_tripped[shooter.uid] = true
				var ce := MatchEvent.new()
				ce.quarter = quarter
				ce.at = at
				ce.type = "combo"
				ce.text = _line("combo", shooter.name, null, shooter.points)
				ce.score_a = _score_a
				ce.score_b = _score_b
				ce.actor_uid = shooter.uid
				ce.actor_is_a = off_is_a
				cand.append(ce)

			var sc := MatchEvent.new()
			sc.quarter = quarter
			sc.at = at
			sc.type = "dunk" if is_dunk else ("three" if is_three else "normal")
			if is_dunk:
				sc.text = _line("dunk", shooter.name, defender.name)
			elif is_three:
				sc.text = _line("three", shooter.name, defender.name)
			else:
				sc.text = _line("normal_score", shooter.name, defender.name)
			sc.score_a = _score_a
			sc.score_b = _score_b
			sc.delta_a = pts if off_is_a else 0
			sc.delta_b = 0 if off_is_a else pts
			sc.actor_uid = shooter.uid
			sc.actor_is_a = off_is_a
			cand.append(sc)

			if is_three:
				three_count[off_is_a] = int(three_count.get(off_is_a, 0)) + 1
				if int(three_count[off_is_a]) == 3:
					var tr := MatchEvent.new()
					tr.quarter = quarter
					tr.at = at
					tr.type = "three_rain"
					tr.text = off.name + "下起三分雨！连续三记远投命中！"
					tr.score_a = _score_a
					tr.score_b = _score_b
					tr.actor_is_a = off_is_a
					cand.append(tr)
		else:
			shooter.attempts += 1
			_attribute_rebound(def)  # 打铁 → 防守方篮板事件

			if k < 0.4:  # 盖帽 / 抢断（k<0.4 且命中失败）
				if type == 2:
					defender.blocks += 1
					var be := MatchEvent.new()
					be.quarter = quarter
					be.at = at
					be.type = "block"
					be.text = _line("block", defender.name, shooter.name)
					be.score_a = _score_a
					be.score_b = _score_b
					be.actor_uid = defender.uid
					be.actor_is_a = not off_is_a
					cand.append(be)
				else:
					defender.steals += 1
					var se := MatchEvent.new()
					se.quarter = quarter
					se.at = at
					se.type = "steal"
					se.text = _line("steal", defender.name, shooter.name)
					se.score_a = _score_a
					se.score_b = _score_b
					se.actor_uid = defender.uid
					se.actor_is_a = not off_is_a
					cand.append(se)

		# STEP 6 犯规（12%，独立于得分）
		if rng.next_float() < c.foul_chance:
			var def_side := rng.next_float() < c.foul_side_def
			var team := def if def_side else off
			var court: Array = []
			for p in team.live_court():
				if not p.foul_trouble and not p.fouled_out:
					court.append(p)
			if court.size() > 0:
				var fw: Array = []
				for p in court:
					var w: float = maxf(1.0, p.attr[Attr.PHY])
					if p.fouls >= 3:
						w *= 1.5
					fw.append(w)
				var fouler: BattlePlayer = court[rng.weighted_index(fw)]
				fouler.fouls += 1

				var fe := MatchEvent.new()
				fe.quarter = quarter
				fe.at = at
				fe.type = "foul"
				fe.text = _line("foul", fouler.name)
				fe.score_a = _score_a
				fe.score_b = _score_b
				fe.actor_uid = fouler.uid
				fe.actor_is_a = team == a
				fe.fouls_now = fouler.fouls
				cand.append(fe)

				if fouler.fouls >= c.foul_out_count and not fouler.fouled_out:
					_handle_foul_out(team, fouler, cand, quarter, at)

		# 收官（分差 ≥150 首次）
		if not _clutch_done and absi(_score_a - _score_b) >= 150:
			_clutch_done = true
			var trailing := b.name if _score_a >= _score_b else a.name
			var cle := MatchEvent.new()
			cle.quarter = quarter
			cle.at = at
			cle.type = "clutch"
			cle.text = _line("clutch", null, null, absi(_score_a - _score_b), null, null, 0, 0, trailing)
			cle.score_a = _score_a
			cle.score_b = _score_b
			cand.append(cle)

	## 篮板事件按 REB 占比归因（§8.7 简化：比例配额最大余数式分配）。
	func _attribute_rebound(def: BattleTeam) -> void:
		var court := def.live_court()
		if court.size() == 0:
			return
		var s := 0.0
		for p in court:
			s += maxf(1.0, p.attr[Attr.REB])
		var total_reb := 0
		for p in court:
			total_reb += p.reb_events
		var best: BattlePlayer = court[0]
		var best_score := -1.0e20
		for p in court:
			var share := maxf(1.0, p.attr[Attr.REB]) / s
			var quota: float = share * (total_reb + 1) - p.reb_events
			if quota > best_score:
				best_score = quota
				best = p
		best.reb_events += 1

	func _handle_foul_out(team: BattleTeam, fouler: BattlePlayer, cand: Array, quarter: int, at: float) -> void:
		var slot_idx := team.court.find(fouler)
		if slot_idx < 0:
			slot_idx = 0
		var slot: String = Slots.Field[slot_idx]
		var bench: Array = []
		for b in team.bench:
			if not b.played_this_match and not b.on_court:
				bench.append(b)

		var sub_name := ""
		var evt_sub_idx := -1
		if bench.size() > 0:
			var chosen: BattlePlayer = null
			if team.is_player and overrides != null and overrides.has(_sub_idx):
				var ouid = overrides[_sub_idx]
				for b in bench:
					if b.uid == ouid:
						chosen = b
			if chosen == null:
				var scored: Array = []
				for b in bench:
					var score := 0.0
					for i in Attr.COUNT:
						score += b.core[i]
					if b.pref_matches(slot):
						score *= 1.15  # 位置偏好匹配优先
					scored.append([score, b])
				scored.sort_custom(func(x, y): return x[0] > y[0])
				chosen = scored[0][1]

			_do_sub(team, fouler, chosen, slot)
			fouler.fouled_out = true
			sub_name = chosen.name
			if team.is_player:
				evt_sub_idx = _sub_idx
				_sub_idx += 1
		else:
			# 「犯规困扰」继续出场：全属性 ×0.60，不再累计犯规（§8.4）
			fouler.foul_trouble = true
			fouler.fouled_out = true  # 不再计入犯规池，但仍站在场上
			for i in Attr.COUNT:
				fouler.attr[i] = maxf(1.0, fouler.attr[i] * ConfigDb.I.C.foul_trouble_mult)
			sub_name = "无人可换"

		var fo := MatchEvent.new()
		fo.quarter = quarter
		fo.at = at
		fo.type = "foulout"
		fo.text = _line("foulout", fouler.name, null, 0, sub_name)
		fo.score_a = _score_a
		fo.score_b = _score_b
		fo.actor_uid = fouler.uid
		fo.actor_is_a = team == a
		fo.sub_in_name = sub_name
		fo.sub_index = evt_sub_idx
		fo.sub_pending = team.is_player and evt_sub_idx >= 0
		cand.append(fo)

	func _do_sub(team: BattleTeam, out_p: BattlePlayer, in_p: BattlePlayer, slot: String) -> void:
		var idx := team.court.find(out_p)
		if idx < 0:
			return
		in_p.slot_pos = slot
		var sta_mult := HeroCalc.stamina_mult(in_p.inst.sta) if in_p.inst != null else 1.0
		MatchEngine.apply_attr(in_p, team.aura, slot, sta_mult)
		in_p.on_court = true
		in_p.played_this_match = true
		in_p.from_bench = true
		team.court[idx] = in_p


# ---------- 每节事件挑选（8–12 条，优先级筛选，§8.6） ----------
static func _priority(type: String) -> int:
	match type:
		"qend":
			return 100
		"foulout":
			return 98
		"dunk":
			return 92
		"three_rain":
			return 88
		"block":
			return 86
		"steal":
			return 82
		"combo":
			return 78
		"three":
			return 70
		"clutch":
			return 60
		"foul":
			return 45
		"normal":
			return 30
	return 20


static func _select_events(cand: Array, out_events: Array) -> void:
	var must: Array = []
	var rest: Array = []
	for e in cand:
		if e.type == "qend":
			must.append(e)
		else:
			rest.append(e)
	rest.sort_custom(func(x, y):
		var px := _priority(x.type)
		var py := _priority(y.type)
		if px != py:
			return px > py
		return x.at < y.at)
	var take_n := maxi(0, 12 - must.size())
	var picked: Array = []
	for i in rest.size():
		if i >= take_n:
			break
		picked.append(rest[i])
	picked.append_array(must)
	picked.sort_custom(func(x, y): return x.at < y.at)

	if picked.size() < 8:
		var leftover: Array = []
		for e in cand:
			if not picked.has(e):
				leftover.append(e)
		leftover.sort_custom(func(x, y): return _priority(x.type) > _priority(y.type))
		for e in leftover:
			if picked.size() >= 8:
				break
			picked.append(e)
		picked.sort_custom(func(x, y): return x.at < y.at)
	out_events.append_array(picked)


static func _build_boxes(team: BattleTeam, is_player_side: bool) -> Array:
	var boxes: Array = []
	var seen := {}
	var total := 0
	for p in team.court:
		if p != null:
			total += p.possessions_on_court
	total = maxi(1, total)
	for p in team.court:
		if p == null or seen.has(p.uid):
			continue
		seen[p.uid] = true
		var b := PlayerBox.new()
		b.name = p.name
		b.uid = p.uid
		b.is_player_side = is_player_side
		b.points = p.points
		b.blocks = p.blocks
		b.steals = p.steals
		b.fouls = p.fouls
		b.rebounds = p.reb_events
		b.starter = not p.from_bench
		b.fouled_out = p.fouled_out
		b.minutes_pct = float(p.possessions_on_court) / total
		boxes.append(b)
	boxes.sort_custom(func(x, y): return x.contribution > y.contribution)
	return boxes
