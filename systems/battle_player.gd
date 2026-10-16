class_name BattlePlayer
## 比赛球员（模拟用，纯数据）

var uid := ""
var name := ""
var def_id := ""
var quality := ""
var pref := ""                    # 卡的偏好位置
var pref2 := ""                   # 副位置偏好（可选）
var star := 1
var inst: HeroInst = null         # 玩家侧实例（体力结算）；对手侧为 null
var slot_pos := "PG"
var core: Array = []              # 基础×S×D + 材料（不含位置/体力/光环）
var attr: Array = []              # 比赛属性（槽位×体力+光环）
var on_court := false
var played_this_match := false
var from_bench := false

var fouls := 0
var fouled_out := false
var foul_trouble := false         # 无替补时继续出场（×0.60，不再累计犯规）

# 统计
var points := 0
var threes := 0
var blocks := 0
var steals := 0
var reb_events := 0
var made := 0
var attempts := 0
var possessions_on_court := 0


func _init() -> void:
	core = zeros()
	attr = zeros()


static func zeros() -> Array:
	var a: Array = []
	for i in Attr.COUNT:
		a.append(0.0)
	return a


func clone() -> BattlePlayer:
	var c := BattlePlayer.new()
	c.uid = uid
	c.name = name
	c.def_id = def_id
	c.quality = quality
	c.pref = pref
	c.pref2 = pref2
	c.star = star
	c.inst = inst
	c.slot_pos = slot_pos
	c.core = core.duplicate()
	c.attr = attr.duplicate()
	c.on_court = on_court
	c.played_this_match = played_this_match
	c.from_bench = from_bench
	c.fouls = fouls
	c.fouled_out = fouled_out
	c.foul_trouble = foul_trouble
	c.points = points
	c.threes = threes
	c.blocks = blocks
	c.steals = steals
	c.reb_events = reb_events
	c.made = made
	c.attempts = attempts
	c.possessions_on_court = possessions_on_court
	return c


func pref_matches(slot: String) -> bool:
	return pref == slot or pref2 == slot
