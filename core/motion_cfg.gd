class_name MotionCfg
## 动效 token（04 文档 §2，data/motion.json）。数值统一从配置读取。

var root := {}


static func from_json(text: String) -> MotionCfg:
	var m := MotionCfg.new()
	var parsed = JSON.parse_string(text)
	if parsed is Dictionary:
		m.root = parsed
	return m


## 顶层数值（如 "dur_fast"）
func f(key: String, fallback: float) -> float:
	var v = root.get(key)
	if v is float or v is int:
		return float(v)
	return fallback


## 嵌套数值（如 "card_enter.dur"）
func nested(path: String, fallback: float) -> float:
	var parts := path.split(".")
	if parts.size() != 2:
		return fallback
	var sub = root.get(parts[0])
	if sub is Dictionary:
		var v = sub.get(parts[1])
		if v is float or v is int:
			return float(v)
	return fallback
