class_name SaveData
## 本地存档（user://save.json，01 文档 §6）。字段带 version，缺字段补默认值。

var version := 1

var coins := 0
var copper := 0
var jade := 0

var heroes: Array = []             # Array[HeroInst]
var mats := {}                     # "MAT_XXX:quality" -> count
var foods := {}                    # dry/ration/forage -> count
var gacha_history: Array = []      # 最近抽卡历史（≤20 条文本）

var lineup := {}                   # slot -> uid
var codex: Array = []              # 已解锁 defId

var stats := StatsSave.new()
var dungeon_best := {}             # 副本 n -> 历史最高击败队伍数 (0–10)
var dungeon_cleared: Array = []    # 已通关副本 n 列表

var altar := AltarSave.new()
var ftue := FtueData.new()
var settings := SettingsSave.new()

var unlocked_slots: Array = []     # 已解锁的阵容槽位（新号仅 5 首发 + 主教练）

var hero_seq := 0


var dungeon_cleared_count: int:
	get:
		return dungeon_cleared.size()


func to_dict() -> Dictionary:
	var hs: Array = []
	for h in heroes:
		hs.append(h.to_dict())
	var dc: Array = []
	for n in dungeon_cleared:
		dc.append(int(n))
	return {
		"version": version,
		"coins": coins,
		"copper": copper,
		"jade": jade,
		"heroes": hs,
		"mats": mats.duplicate(),
		"foods": foods.duplicate(),
		"gachaHistory": gacha_history.duplicate(),
		"lineup": lineup.duplicate(),
		"codex": codex.duplicate(),
		"stats": stats.to_dict(),
		"dungeonBest": dungeon_best.duplicate(),
		"dungeonCleared": dc,
		"altar": altar.to_dict(),
		"ftue": ftue.to_dict(),
		"settings": settings.to_dict(),
		"unlockedSlots": unlocked_slots.duplicate(),
		"heroSeq": hero_seq,
	}


static func from_dict(d: Dictionary) -> SaveData:
	var s := SaveData.new()
	s.version = int(d.get("version", 1))
	s.coins = int(d.get("coins", 0))
	s.copper = int(d.get("copper", 0))
	s.jade = int(d.get("jade", 0))

	var hs = d.get("heroes", [])
	if hs is Array:
		for hd in hs:
			if hd is Dictionary:
				s.heroes.append(HeroInst.from_dict(hd))

	var mts = d.get("mats", {})
	if mts is Dictionary:
		for k in mts:
			s.mats[str(k)] = int(mts[k])
	var fds = d.get("foods", {})
	if fds is Dictionary:
		for k in fds:
			s.foods[str(k)] = int(fds[k])

	var gh = d.get("gachaHistory", [])
	if gh is Array:
		for t in gh:
			s.gacha_history.append(str(t))

	var lu = d.get("lineup", {})
	if lu is Dictionary:
		for k in lu:
			s.lineup[str(k)] = str(lu[k])
	var cx = d.get("codex", [])
	if cx is Array:
		for c in cx:
			s.codex.append(str(c))

	s.stats = StatsSave.from_dict(d.get("stats", {}))
	var dbest = d.get("dungeonBest", {})
	if dbest is Dictionary:
		for k in dbest:
			s.dungeon_best[str(k)] = int(dbest[k])
	var dclr = d.get("dungeonCleared", [])
	if dclr is Array:
		for n in dclr:
			s.dungeon_cleared.append(int(n))

	s.altar = AltarSave.from_dict(d.get("altar", {}))
	s.ftue = FtueData.from_dict(d.get("ftue", {}))
	s.settings = SettingsSave.from_dict(d.get("settings", {}))

	var us = d.get("unlockedSlots", null)
	if us is Array and us.size() > 0:
		for u in us:
			s.unlocked_slots.append(str(u))
	else:
		s.unlocked_slots = ConfigDb.I.C.ext.default_slots.duplicate()

	s.hero_seq = int(d.get("heroSeq", 0))
	return s
