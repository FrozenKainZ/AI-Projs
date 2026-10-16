class_name MatQ
## 材料品质档数值（main 主属性加成 / sub 副属性惩罚）

var main := 0
var sub := 0


static func from_json(d) -> MatQ:
	var m := MatQ.new()
	if d is Dictionary:
		m.main = int(d.get("main", 0))
		m.sub = int(d.get("sub", 0))
	return m
