class_name Art
## 美术资源加载（统一入口，缺文件时返回 null，由控件画占位）。

static var _cache := {}


static func load_tex(path: String) -> Texture2D:
	if _cache.has(path):
		return _cache[path]
	var tex: Texture2D = null
	if ResourceLoader.exists(path):
		tex = load(path)
	_cache[path] = tex
	return tex


static func hero(def_id: String) -> Texture2D:
	return load_tex("res://art/heroes/" + def_id + ".png")


static func bg(key: String) -> Texture2D:
	return load_tex("res://art/bg/" + key + ".png")


## 势力详情背景（武将详情页用）
static func faction_bg(faction: String) -> Texture2D:
	match faction:
		"蜀":
			return bg("bg_faction_shu")
		"魏":
			return bg("bg_faction_wei")
		"吴":
			return bg("bg_faction_wu")
	return bg("bg_faction_qun")


static func icon(key: String) -> Texture2D:
	return load_tex("res://art/icons/" + key + ".png")


## 材料图标：mat_bracer 风格（按材料类型）
static func mat_icon(mat_type_id: String) -> Texture2D:
	return load_tex("res://art/icons/" + mat_type_id.to_lower() + ".png")


static func food_icon(key: String) -> Texture2D:
	return load_tex("res://art/icons/food_" + key + ".png")


static func tali_icon(key: String) -> Texture2D:
	return load_tex("res://art/icons/tali_" + key + ".png")


static func coin_icon() -> Texture2D:
	return load_tex("res://art/icons/coin.png")


## 抽卡产出条目图标
static func draw_icon(item: DrawItem) -> Texture2D:
	match item.type:
		"mat":
			return mat_icon(item.mat_type_id)
		"food":
			return food_icon(item.food_key)
		"tali":
			return tali_icon(item.tali_key)
		"hero":
			return hero(item.hero_def_id)
	return null
