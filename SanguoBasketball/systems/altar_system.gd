class_name AltarSystem
## 七星坛（SYS-10，02 文档 §11）。


static func remaining() -> int:
	GameState.ensure_altar_day()
	return maxi(0, ConfigDb.I.C.altar_daily_limit - GameState.D.altar.used)


static func can_play() -> bool:
	return remaining() > 0


## 抽奖：65% 金币档（等权 200–600）；35% 虎符（铜 85% / 玉 15%）。
static func roll(rng: GameRandom) -> DrawItem:
	var c := ConfigDb.I.C
	if rng.next_float() < c.altar_coin_rate:
		var coins: int = rng.pick(c.altar_coin_table)
		var item := DrawItem.new()
		item.type = "coin"
		item.quality = "gold"
		item.mat_type_id = str(coins)
		return item
	var t := rng.pick_weighted_key(c.altar_tali_dist)
	var item2 := DrawItem.new()
	item2.type = "tali"
	item2.tali_key = t
	return item2


## 发放奖励并记账（广告完成后调用）。返回文案。
static func grant(item: DrawItem) -> String:
	GameState.ensure_altar_day()
	GameState.D.altar.used += 1
	match item.type:
		"coin":
			var coins := int(item.mat_type_id)
			GameState.add_coins(coins)
			return "获得 %d 金币" % coins
		"tali":
			GameState.add_tali(item.tali_key, 1)
			return "获得 铜虎符 ×1" if item.tali_key == "copper" else "获得 玉虎符 ×1"
	SaveSystem.mark_dirty()
	return ""


## 广告失败时返还次数（禁止吞次数）。
static func refund_attempt() -> void:
	GameState.ensure_altar_day()
	GameState.D.altar.used = maxi(0, GameState.D.altar.used - 1)
	SaveSystem.mark_dirty()
