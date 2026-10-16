class_name Constants
## 常量总表（02 文档 §14，字段名与文档一致）。

var star_mult_s: Array = [1.0, 1.3, 1.65, 2.05, 2.5, 3.0]
var star_mult_d: Array = [1.0, 1.08, 1.17, 1.27, 1.38, 1.5]
var evolve_max_star := 6
var evolve_sacrifice := 1

var stamina_max := 100
var stamina_cost_per_match := 12
var stamina_factor_min := 0.5
var stamina_factor_max := 1.0

var pos_bonus_match := 1.15
var pos_bonus_other := 1.0
var aura_hc_rate := 0.12
var aura_ac_rate := 0.06

var mat_quality := {}          # quality -> MatQ

var market_price_1 := 88
var market_price_10 := 800
var market_weight := {}
var market_mat_dist := {}
var market_food_dist := {}
var market_tali_dist := {}

var barrack_star_dist := {}
var barrack_q1_dist := {}
var barrack_q2_dist := {}

var win_coin_base := 80
var win_coin_growth := 22
var coin_dungeon_bonus := 0.3
var recycle_hero := {}
var recycle_mat := {}
var recycle_food := {}
var recycle_tali := {}

var possessions_base := 160
var possession_reb_cap := 30
var hit_base := 0.55
var hit_log_coef := 0.15
var hit_min := 0.10
var hit_max := 0.88
var val_base := 2.4
var val_exp := 1.35
var val_min := 0.5
var val_max := 28.0
var k_min := 0.05
var k_max := 20.0
var pass_bonus_cap := 0.08
var foul_chance := 0.12
var foul_side_def := 0.70
var foul_out_count := 6
var foul_trouble_mult := 0.60

var normal_opp_base := 55
var normal_opp_growth := 1.06

var dungeon_fee_base := 300
var dungeon_fee_growth := 1.35
var dungeon_opp_base := 120
var dungeon_opp_growth := 1.11
var dungeon_team_step := 0.05

var altar_daily_limit := 7
var altar_coin_rate := 0.65
var altar_coin_table: Array = [200, 300, 400, 500, 600]
var altar_tali_dist := {}

var ext := ExtCfg.new()


static func _f(d: Dictionary, key: String, def: float) -> float:
	var v = d.get(key)
	return float(v) if (v is float or v is int) else def


static func _i(d: Dictionary, key: String, def: int) -> int:
	var v = d.get(key)
	return int(v) if (v is float or v is int) else def


static func _dict_f(d: Dictionary, key: String) -> Dictionary:
	var raw = d.get(key, {})
	var out := {}
	if raw is Dictionary:
		for k in raw:
			out[str(k)] = float(raw[k])
	return out


static func _dict_i(d: Dictionary, key: String) -> Dictionary:
	var raw = d.get(key, {})
	var out := {}
	if raw is Dictionary:
		for k in raw:
			out[str(k)] = int(raw[k])
	return out


static func from_json(d) -> Constants:
	var c := Constants.new()
	if not (d is Dictionary):
		return c
	if d.get("STAR_MULT_S", null) is Array:
		c.star_mult_s = d["STAR_MULT_S"]
	if d.get("STAR_MULT_D", null) is Array:
		c.star_mult_d = d["STAR_MULT_D"]
	c.evolve_max_star = _i(d, "EVOLVE_MAX_STAR", 6)
	c.evolve_sacrifice = _i(d, "EVOLVE_SACRIFICE", 1)

	c.stamina_max = _i(d, "STAMINA_MAX", 100)
	c.stamina_cost_per_match = _i(d, "STAMINA_COST_PER_MATCH", 12)
	c.stamina_factor_min = _f(d, "STAMINA_FACTOR_MIN", 0.5)
	c.stamina_factor_max = _f(d, "STAMINA_FACTOR_MAX", 1.0)

	c.pos_bonus_match = _f(d, "POS_BONUS_MATCH", 1.15)
	c.pos_bonus_other = _f(d, "POS_BONUS_OTHER", 1.0)
	c.aura_hc_rate = _f(d, "AURA_HC_RATE", 0.12)
	c.aura_ac_rate = _f(d, "AURA_AC_RATE", 0.06)

	var mq = d.get("MAT_QUALITY", {})
	if mq is Dictionary:
		for k in mq:
			c.mat_quality[str(k)] = MatQ.from_json(mq[k])

	c.market_price_1 = _i(d, "MARKET_PRICE_1", 88)
	c.market_price_10 = _i(d, "MARKET_PRICE_10", 800)
	c.market_weight = _dict_f(d, "MARKET_WEIGHT")
	c.market_mat_dist = _dict_f(d, "MARKET_MAT_DIST")
	c.market_food_dist = _dict_f(d, "MARKET_FOOD_DIST")
	c.market_tali_dist = _dict_f(d, "MARKET_TALI_DIST")

	c.barrack_star_dist = _dict_f(d, "BARRACK_STAR_DIST")
	c.barrack_q1_dist = _dict_f(d, "BARRACK_Q1_DIST")
	c.barrack_q2_dist = _dict_f(d, "BARRACK_Q2_DIST")

	c.win_coin_base = _i(d, "WIN_COIN_BASE", 80)
	c.win_coin_growth = _i(d, "WIN_COIN_GROWTH", 22)
	c.coin_dungeon_bonus = _f(d, "COIN_DUNGEON_BONUS", 0.3)
	c.recycle_hero = _dict_i(d, "RECYCLE_HERO")
	c.recycle_mat = _dict_i(d, "RECYCLE_MAT")
	c.recycle_food = _dict_i(d, "RECYCLE_FOOD")
	c.recycle_tali = _dict_i(d, "RECYCLE_TALI")

	c.possessions_base = _i(d, "POSSESSIONS_BASE", 160)
	c.possession_reb_cap = _i(d, "POSSESSION_REB_CAP", 30)
	c.hit_base = _f(d, "HIT_BASE", 0.55)
	c.hit_log_coef = _f(d, "HIT_LOG_COEF", 0.15)
	c.hit_min = _f(d, "HIT_MIN", 0.10)
	c.hit_max = _f(d, "HIT_MAX", 0.88)
	c.val_base = _f(d, "VAL_BASE", 2.4)
	c.val_exp = _f(d, "VAL_EXP", 1.35)
	c.val_min = _f(d, "VAL_MIN", 0.5)
	c.val_max = _f(d, "VAL_MAX", 28.0)
	c.k_min = _f(d, "K_MIN", 0.05)
	c.k_max = _f(d, "K_MAX", 20.0)
	c.pass_bonus_cap = _f(d, "PASS_BONUS_CAP", 0.08)
	c.foul_chance = _f(d, "FOUL_CHANCE", 0.12)
	c.foul_side_def = _f(d, "FOUL_SIDE_DEF", 0.70)
	c.foul_out_count = _i(d, "FOUL_OUT_COUNT", 6)
	c.foul_trouble_mult = _f(d, "FOUL_TROUBLE_MULT", 0.60)

	c.normal_opp_base = _i(d, "NORMAL_OPP_BASE", 55)
	c.normal_opp_growth = _f(d, "NORMAL_OPP_GROWTH", 1.06)

	c.dungeon_fee_base = _i(d, "DUNGEON_FEE_BASE", 300)
	c.dungeon_fee_growth = _f(d, "DUNGEON_FEE_GROWTH", 1.35)
	c.dungeon_opp_base = _i(d, "DUNGEON_OPP_BASE", 120)
	c.dungeon_opp_growth = _f(d, "DUNGEON_OPP_GROWTH", 1.11)
	c.dungeon_team_step = _f(d, "DUNGEON_TEAM_STEP", 0.05)

	c.altar_daily_limit = _i(d, "ALTAR_DAILY_LIMIT", 7)
	c.altar_coin_rate = _f(d, "ALTAR_COIN_RATE", 0.65)
	if d.get("ALTAR_COIN_TABLE", null) is Array:
		c.altar_coin_table = d["ALTAR_COIN_TABLE"]
	c.altar_tali_dist = _dict_f(d, "ALTAR_TALI_DIST")

	c.ext = ExtCfg.from_json(d.get("EXT", {}))
	return c
