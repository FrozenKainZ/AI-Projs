class_name ConfigDb
## 全局配置库：从 res://data/*.json 加载（数据驱动，禁止硬编码数值）。

static var I: ConfigDb

var C := Constants.new()
var hero_list: Array = []          # Array[HeroDef]
var heroes := {}                   # id -> HeroDef
var commentary := {}               # category -> Array[String]
var teams := TeamCfg.new()
var motion := MotionCfg.new()
var _by_quality := {}              # quality -> Array[HeroDef]


static func _static_init() -> void:
	I = ConfigDb.new()


static func _read_res(path: String) -> String:
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		push_error("配置缺失: " + path)
		return "{}"
	return f.get_as_text()


static func load_config() -> void:
	var db := ConfigDb.new()
	var hero_arr = JSON.parse_string(_read_res("res://data/heroes.json"))
	if hero_arr is Array:
		for hd in hero_arr:
			var h := HeroDef.from_json(hd)
			db.hero_list.append(h)
			db.heroes[h.id] = h
	for q in Quality.Order:
		var list: Array = []
		for h in db.hero_list:
			if h.quality_key == q:
				list.append(h)
		db._by_quality[q] = list

	db.C = Constants.from_json(JSON.parse_string(_read_res("res://data/constants.json")))

	var cm = JSON.parse_string(_read_res("res://data/commentary.json"))
	if cm is Dictionary:
		db.commentary = cm

	db.teams = TeamCfg.from_json(JSON.parse_string(_read_res("res://data/teams.json")))
	db.motion = MotionCfg.from_json(_read_res("res://data/motion.json"))

	I = db
	print("[ConfigDb] heroes=%d commentary=%d dungeons=%d" % [
		db.hero_list.size(), db.commentary.size(), db.teams.dungeons.size()])


func hero(id: String) -> HeroDef:
	return heroes.get(id)


func by_quality(q: String) -> Array:
	return _by_quality.get(q, [])


func lines(category: String) -> Array:
	var l = commentary.get(category)
	if l is Array and l.size() > 0:
		return l
	return ["{A}得分！"]


func mat_type(id: String) -> MatTypeDef:
	for m in C.ext.mat_types:
		if m.id == id:
			return m
	return null


func special_mat(id: String) -> SpecialMatDef:
	for m in C.ext.special_mats:
		if m.id == id:
			return m
	return null


func food_def(key: String) -> FoodDefDef:
	for f in C.ext.food_defs:
		if f.key == key:
			return f
	return null


## 头衔（按历史最强球队综评）
func rank_title(best_team_ovr: int) -> String:
	var title: String = C.ext.rank_titles[0].name if C.ext.rank_titles.size() > 0 else "主公"
	for r in C.ext.rank_titles:
		if best_team_ovr >= r.min_val:
			title = r.name
	return title


## 星系数 S(L)，L 为 1–6。
func star_s(star: int) -> float:
	return float(C.star_mult_s[clampi(star, 1, 6) - 1])


## 得意属性额外系数 D(L)。
func star_d(star: int) -> float:
	return float(C.star_mult_d[clampi(star, 1, 6) - 1])
