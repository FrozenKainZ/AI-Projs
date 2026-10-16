class_name RankTitleDef
## 头衔档位（按历史最强球队综评）

var min_val := 0
var name := ""


static func from_json(d: Dictionary) -> RankTitleDef:
	var r := RankTitleDef.new()
	r.min_val = int(d.get("min", 0))
	r.name = str(d.get("name", ""))
	return r
