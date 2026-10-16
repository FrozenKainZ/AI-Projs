class_name DrawItem
## 抽卡产出条目（市场池/兵营池通用）。

var type := ""         # mat / food / tali / hero / coin
var mat_type_id := ""  # MAT_XXX
var quality := ""      # green/blue/purple/gold
var food_key := ""     # dry/ration/forage
var tali_key := ""     # copper/jade
var hero_def_id := ""
var star := 1


var display_name: String:
	get:
		match type:
			"mat":
				return ConfigDb.I.mat_type(mat_type_id).name
			"food":
				return ConfigDb.I.food_def(food_key).name
			"tali":
				return "铜虎符" if tali_key == "copper" else "玉虎符"
			"hero":
				return ConfigDb.I.hero(hero_def_id).name
		return "?"


var sub_label: String:
	get:
		match type:
			"mat":
				return Quality.cn(quality) + "·" + Attr.Cns[ConfigDb.I.mat_type(mat_type_id).main_idx]
			"hero":
				return Quality.cn(ConfigDb.I.hero(hero_def_id).quality_key) + "·" + str(star) + "★"
		return ""


## 稀有度（用于揭示排序/光效层级）
var rarity: int:
	get:
		match type:
			"mat":
				return Quality.tier(quality)
			"food":
				match food_key:
					"dry":
						return 0
					"ration":
						return 1
				return 2
			"tali":
				return 1 if tali_key == "copper" else 3
			"hero":
				return Quality.tier(ConfigDb.I.hero(hero_def_id).quality_key)
		return 0


var icon_key: String:
	get:
		match type:
			"mat":
				return mat_type_id
			"food":
				return "FOOD_" + food_key.to_upper()
			"tali":
				return "TALI_" + tali_key.to_upper()
			"hero":
				return hero_def_id
		return "?"
