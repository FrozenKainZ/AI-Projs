class_name BattleTeam
## 比赛球队（模拟用）

var name := ""
var is_player := false
var court: Array = [null, null, null, null, null]   # 对齐 Slots.Field
var bench: Array = []
var coach_insts: Array = []
var aura := 0.0


func clone() -> BattleTeam:
	var t := BattleTeam.new()
	t.name = name
	t.is_player = is_player
	t.aura = aura
	t.coach_insts = coach_insts
	for i in 5:
		t.court[i] = court[i].clone() if court[i] != null else null
	for b in bench:
		t.bench.append(b.clone())
	return t


func live_court() -> Array:
	var l: Array = []
	for i in 5:
		if court[i] != null:
			l.append(court[i])
	return l


func avg_attr(attr_idx: int) -> float:
	var l := live_court()
	if l.size() == 0:
		return 1.0
	var s := 0.0
	for p in l:
		s += p.attr[attr_idx]
	return s / l.size()
