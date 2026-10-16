class_name ExtCfg
## 扩展配置段（非文档 §14，属实现补充）

var revive_per_run := 1
var food_effect := {}                       # dry/ration/forage -> 效果值
var dungeon_max := 20
var dungeon_teams := 10
var dungeon_fee_round_to := 10
var quarter_names: Array = ["首节", "次节", "第三节", "末节"]
var market_ten_discount := 0.909
var ftue_coins := 500
var ftue_copper := 5
var ftue_dry := 3
var ftue_teacher_ovr := 45
var mat_types: Array = []                   # Array[MatTypeDef]
var special_mats: Array = []                # Array[SpecialMatDef]
var iq_star_step := 8
var stacap_base := 100
var stacap_star_step := 5
var aura_iq_base := 0.5
var aura_iq_div := 200.0
var default_slots: Array = ["PG", "SG", "SF", "PF", "C", "HC"]
var slot_unlock_cost := {}                  # slot -> long
var rank_titles: Array = []                 # Array[RankTitleDef]
var food_defs: Array = []                   # Array[FoodDefDef]


static func from_json(d) -> ExtCfg:
	var e := ExtCfg.new()
	if not (d is Dictionary):
		return e
	e.revive_per_run = int(d.get("REVIVE_PER_RUN", 1))
	var fe = d.get("FOOD_EFFECT", {})
	if fe is Dictionary:
		for k in fe:
			e.food_effect[str(k)] = int(fe[k])
	e.dungeon_max = int(d.get("DUNGEON_MAX", 20))
	e.dungeon_teams = int(d.get("DUNGEON_TEAMS", 10))
	e.dungeon_fee_round_to = int(d.get("DUNGEON_FEE_ROUND_TO", 10))
	var qn = d.get("QUARTER_NAMES", null)
	if qn is Array and qn.size() == 4:
		e.quarter_names = qn
	e.market_ten_discount = float(d.get("MARKET_TEN_DISCOUNT", 0.909))
	e.ftue_coins = int(d.get("FTUE_COINS", 500))
	e.ftue_copper = int(d.get("FTUE_COPPER", 5))
	e.ftue_dry = int(d.get("FTUE_DRY", 3))
	e.ftue_teacher_ovr = int(d.get("FTUE_TEACHER_OVR", 45))
	var mats = d.get("MAT_TYPES", [])
	if mats is Array:
		for m in mats:
			e.mat_types.append(MatTypeDef.from_json(m))
	var sps = d.get("SPECIAL_MATS", [])
	if sps is Array:
		for m in sps:
			e.special_mats.append(SpecialMatDef.from_json(m))
	e.iq_star_step = int(d.get("IQ_STAR_STEP", 8))
	e.stacap_base = int(d.get("STACAP_BASE", 100))
	e.stacap_star_step = int(d.get("STACAP_STAR_STEP", 5))
	e.aura_iq_base = float(d.get("AURA_IQ_BASE", 0.5))
	e.aura_iq_div = float(d.get("AURA_IQ_DIV", 200.0))
	var ds = d.get("DEFAULT_SLOTS", null)
	if ds is Array and ds.size() > 0:
		e.default_slots = ds
	var suc = d.get("SLOT_UNLOCK_COST", {})
	if suc is Dictionary:
		for k in suc:
			e.slot_unlock_cost[str(k)] = int(suc[k])
	var rts = d.get("RANK_TITLES", [])
	if rts is Array:
		for r in rts:
			e.rank_titles.append(RankTitleDef.from_json(r))
	var fds = d.get("FOOD_DEFS", [])
	if fds is Array:
		for f in fds:
			e.food_defs.append(FoodDefDef.from_json(f))
	return e
