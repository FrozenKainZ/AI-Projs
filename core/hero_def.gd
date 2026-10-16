class_name HeroDef
## 武将静态定义（由 data/heroes.json 生成）。

var id := ""
var name := ""
var faction := ""
var quality_key := ""
var pref := ""
var pref2 := ""           # 次要位置偏好（可选；同样享受 ×1.15）
var design_ovr := 0
var signature: Array = []
var base_map := {}
var iq := 50              # 独立资源：智商（影响教练光环倍率；升星加值，不参与乘区）
var base: Array = []      # 展平后的 9 项基础属性


static func from_json(d: Dictionary) -> HeroDef:
	var h := HeroDef.new()
	h.id = str(d.get("id", ""))
	h.name = str(d.get("name", ""))
	h.faction = str(d.get("faction", "群"))
	h.quality_key = str(d.get("quality", "green"))
	h.pref = str(d.get("pref", "PG"))
	h.pref2 = str(d.get("pref2", ""))
	h.design_ovr = int(d.get("designOvr", 0))
	h.signature = d.get("signature", []) if d.get("signature", []) is Array else []
	h.base_map = d.get("base", {}) if d.get("base", {}) is Dictionary else {}
	h.iq = int(d.get("iq", 50))
	h.flatten()
	return h


func flatten() -> void:
	base = []
	for i in Attr.COUNT:
		base.append(int(base_map.get(Attr.Keys[i], 1)))


func is_signature(attr_idx: int) -> bool:
	return signature.has(Attr.Keys[attr_idx])


## 势力·名（列表展示用）
var full_name: String:
	get:
		return faction + "·" + name


## 位置偏好文本（支持双位置，如 "C/PF"）
var pref_text: String:
	get:
		return pref if pref2.is_empty() else pref + "/" + pref2


func pref_matches(slot: String) -> bool:
	return pref == slot or pref2 == slot
