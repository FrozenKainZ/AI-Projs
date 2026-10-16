class_name FoodDefDef
## 兵粮定义

var id := ""
var name := ""
var key := ""
var effect := 0
var target := ""    # single / team


static func from_json(d: Dictionary) -> FoodDefDef:
	var f := FoodDefDef.new()
	f.id = str(d.get("id", ""))
	f.name = str(d.get("name", ""))
	f.key = str(d.get("key", ""))
	f.effect = int(d.get("effect", 0))
	f.target = str(d.get("target", "single"))
	return f
