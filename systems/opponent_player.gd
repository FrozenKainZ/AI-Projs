class_name OpponentPlayer
## 对手球员（生成数据，core 已按目标 OVR 缩放）。

var uid := ""
var name := ""
var def_id := ""
var quality := ""
var pref := ""
var star := 1
var core: Array = []


func _init() -> void:
	core = BattlePlayer.zeros()


func ovr() -> int:
	var s := 0.0
	for v in core:
		s += float(v)
	return int(roundf(s / Attr.COUNT))
