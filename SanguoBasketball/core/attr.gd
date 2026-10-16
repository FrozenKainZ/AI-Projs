class_name Attr
## 9 项属性索引与显示名（02 文档 §1.1）。

const P3 := 0
const MID := 1
const RIM := 2
const REB := 3
const PAS := 4
const STL := 5
const BLK := 6
const PHY := 7
const SPD := 8
const COUNT := 9

const Keys := ["three_point", "mid_range", "rim_finish", "rebound", "pass", "steal", "block", "physical", "speed"]
const Shorts := ["3PT", "MID", "RIM", "REB", "PAS", "STL", "BLK", "PHY", "SPD"]
const Cns := ["三分", "中投", "攻框", "篮板", "传球", "抢断", "盖帽", "对抗", "速度"]


static func key_to_index(key: String) -> int:
	var i := Keys.find(key)
	return 0 if i < 0 else i
