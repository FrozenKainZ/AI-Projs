class_name DungeonDef
## 副本定义

var n := 1
var name := ""


static func from_json(d: Dictionary) -> DungeonDef:
	var r := DungeonDef.new()
	r.n = int(d.get("n", 1))
	r.name = str(d.get("name", ""))
	return r
