class_name GachaSystem
## 抽卡系统（SYS-03，02 文档 §6）。
## 市场池只产材料/兵粮/虎符；兵营池只产武将。


## 市场池单抽（不含扣费，扣费在外层）。
static func draw_market_one(rng: GameRandom) -> DrawItem:
	var c := ConfigDb.I.C
	var big := rng.pick_weighted_key(c.market_weight)
	if big == "material":
		big = "mat"  # 归一化大类 key
	var item := DrawItem.new()
	item.type = big
	match big:
		"mat":
			item.quality = rng.pick_weighted_key(c.market_mat_dist)
			item.mat_type_id = rng.pick(c.ext.mat_types).id
		"food":
			item.food_key = rng.pick_weighted_key(c.market_food_dist)
		"tali":
			item.tali_key = rng.pick_weighted_key(c.market_tali_dist)
	return item


static func draw_market(rng: GameRandom, count: int) -> Array:
	var list: Array = []
	for i in count:
		list.append(draw_market_one(rng))
	return list


## 兵营池单抽（§6.2）：92% 1星 / 8% 2星，品质按星级内分布。
static func draw_barrack_one(rng: GameRandom) -> DrawItem:
	var c := ConfigDb.I.C
	var star_key := rng.pick_weighted_key(c.barrack_star_dist)
	var star := 2 if star_key == "star2" else 1
	var q := rng.pick_weighted_key(c.barrack_q2_dist) if star == 2 else rng.pick_weighted_key(c.barrack_q1_dist)
	var pool := ConfigDb.I.by_quality(q)
	var def: HeroDef = rng.pick(pool)
	var item := DrawItem.new()
	item.type = "hero"
	item.hero_def_id = def.id
	item.star = star
	item.quality = q
	return item


static func draw_barrack(rng: GameRandom, count: int) -> Array:
	var list: Array = []
	for i in count:
		list.append(draw_barrack_one(rng))
	return list


## 入包：把抽卡结果写入背包/武将列表，返回新增的武将实例。
static func apply_to_inventory(items: Array) -> Array:
	var heroes_list: Array = []
	for it in items:
		match it.type:
			"mat":
				GameState.add_mat(it.mat_type_id, it.quality, 1)
			"food":
				GameState.add_food(it.food_key, 1)
			"tali":
				GameState.add_tali(it.tali_key, 1)
			"hero":
				var inst := GameState.add_hero(it.hero_def_id, it.star)
				heroes_list.append(inst)
	SaveSystem.mark_dirty()
	SaveSystem.save_game()
	return heroes_list


## 抽卡历史（最近 20 条，简化为文本行）。
static func push_history(line: String) -> void:
	var d := GameState.D
	d.gacha_history.insert(0, line)
	while d.gacha_history.size() > 20:
		d.gacha_history.remove_at(d.gacha_history.size() - 1)
