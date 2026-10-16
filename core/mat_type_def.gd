class_name MatTypeDef
## 强化材料类型（9 种篮球道具）

var id := ""
var name := ""
var main := ""      # 主属性 key
var sub := ""       # 副属性 key


static func from_json(d: Dictionary) -> MatTypeDef:
	var m := MatTypeDef.new()
	m.id = str(d.get("id", ""))
	m.name = str(d.get("name", ""))
	m.main = str(d.get("main", "three_point"))
	m.sub = str(d.get("sub", "speed"))
	return m


var main_idx: int:
	get:
		return Attr.key_to_index(main)


var sub_idx: int:
	get:
		return Attr.key_to_index(sub)
