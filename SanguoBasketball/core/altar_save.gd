class_name AltarSave
## 七星坛每日状态

var date := ""
var used := 0


static func from_dict(d) -> AltarSave:
	var a := AltarSave.new()
	if d is Dictionary:
		a.date = str(d.get("date", ""))
		a.used = int(d.get("used", 0))
	return a


func to_dict() -> Dictionary:
	return {"date": date, "used": used}
