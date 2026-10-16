class_name FtueData
## 新手引导进度

var step := 0                # 0=全新
var intro_done := false
var free_ten_used := false
var lineup_touched := false
var lineup_reward := false


static func from_dict(d) -> FtueData:
	var f := FtueData.new()
	if d is Dictionary:
		f.step = int(d.get("step", 0))
		f.intro_done = bool(d.get("introDone", false))
		f.free_ten_used = bool(d.get("freeTenUsed", false))
		f.lineup_touched = bool(d.get("lineupTouched", false))
		f.lineup_reward = bool(d.get("lineupReward", false))
	return f


func to_dict() -> Dictionary:
	return {
		"step": step,
		"introDone": intro_done,
		"freeTenUsed": free_ten_used,
		"lineupTouched": lineup_touched,
		"lineupReward": lineup_reward,
	}
