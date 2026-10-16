class_name HeroInst
## 武将实例（存档单位）。mat 为 9 项材料加成固定值（02 §4.2）。

var uid := ""
var def_id := ""
var star := 1
var mat: Array = []
var sta := 100
var iq_bonus := 0          # 智商材料加成（独立资源，不参与乘区）
var sta_cap_bonus := 0     # 体力上限材料加成（独立资源）


func _init() -> void:
	mat = make_mat()


static func make_mat() -> Array:
	var a: Array = []
	for i in Attr.COUNT:
		a.append(0)
	return a


func def() -> HeroDef:
	return ConfigDb.I.hero(def_id)


func clone() -> HeroInst:
	var h := HeroInst.new()
	h.uid = uid
	h.def_id = def_id
	h.star = star
	h.sta = sta
	h.mat = mat.duplicate()
	h.iq_bonus = iq_bonus
	h.sta_cap_bonus = sta_cap_bonus
	return h


static func norm_mat(raw) -> Array:
	var a: Array = []
	for i in Attr.COUNT:
		if raw is Array and i < raw.size():
			a.append(int(raw[i]))
		else:
			a.append(0)
	return a


static func from_dict(d: Dictionary) -> HeroInst:
	var h := HeroInst.new()
	h.uid = str(d.get("uid", ""))
	h.def_id = str(d.get("defId", ""))
	h.star = clampi(int(d.get("star", 1)), 1, 6)
	h.mat = norm_mat(d.get("mat", []))
	h.sta = int(d.get("sta", 100))
	h.iq_bonus = int(d.get("iqBonus", 0))
	h.sta_cap_bonus = int(d.get("staCapBonus", 0))
	return h


func to_dict() -> Dictionary:
	return {
		"uid": uid,
		"defId": def_id,
		"star": star,
		"mat": mat.duplicate(),
		"sta": sta,
		"iqBonus": iq_bonus,
		"staCapBonus": sta_cap_bonus,
	}
