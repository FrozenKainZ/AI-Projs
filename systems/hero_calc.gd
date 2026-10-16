class_name HeroCalc
## 武将数值计算（02 文档 §1/§2/§3）。
## 公式链：卡片属性 = 基础×S(星)×(得意?D(星):1) + 材料加成
##         槽位属性 = 基础×S×D×位置系数 + 材料加成（§3.1）
##         比赛属性 = 槽位属性×STA系数 + 教练光环加值（§8.2/§8.5）


## 卡片属性（不含位置系数），进详情页/OVR 用。
static func core_attrs(h: HeroInst) -> Array:
	var def := h.def()
	var s := ConfigDb.I.star_s(h.star)
	var d := ConfigDb.I.star_d(h.star)
	var arr: Array = []
	for i in Attr.COUNT:
		var v: float = def.base[i] * s * (d if def.is_signature(i) else 1.0)
		arr.append(v + h.mat[i])
	return arr


## 总评 OVR = 9 项属性平均，展示用整数（§1.1）。
static func ovr(h: HeroInst) -> int:
	var a := core_attrs(h)
	var sum := 0.0
	for v in a:
		sum += float(v)
	return int(roundf(sum / Attr.COUNT))


## 用于挑人/排序的等价 OVR（含位置偏好），不改变展示 OVR。
static func slot_ovr(h: HeroInst, slot: String) -> int:
	var a := slot_attrs(h, slot)
	var sum := 0.0
	for v in a:
		sum += float(v)
	return int(roundf(sum / Attr.COUNT))


## 槽位属性（含位置系数，02 §2.2：偏好 ×1.15，其余 ×1.00；双位置均享受）。
static func slot_attrs(h: HeroInst, slot: String) -> Array:
	var def := h.def()
	var bonus := Slots.is_field(slot) and def.pref_matches(slot)
	var pos := ConfigDb.I.C.pos_bonus_match if bonus else ConfigDb.I.C.pos_bonus_other
	var s := ConfigDb.I.star_s(h.star)
	var d := ConfigDb.I.star_d(h.star)
	var arr: Array = []
	for i in Attr.COUNT:
		var v: float = def.base[i] * s * (d if def.is_signature(i) else 1.0) * pos
		arr.append(v + h.mat[i])
	return arr


## 体力系数（§3.4）：0.50 + 0.50 × STA / 100，封顶 ×1.00（体力上限可超 100 但不放大）。
static func stamina_mult(sta: int) -> float:
	var c := ConfigDb.I.C
	var v := c.stamina_factor_min + (c.stamina_factor_max - c.stamina_factor_min) * clampf(float(sta), 0.0, float(c.stamina_max)) / float(c.stamina_max)
	return clampf(v, c.stamina_factor_min, c.stamina_factor_max)


## 智商合计 = 基础 + (星级-1)×每星加值 + 材料加成（不参与乘区）。
static func iq_total(h: HeroInst) -> int:
	return h.def().iq + (clampi(h.star, 1, 6) - 1) * ConfigDb.I.C.ext.iq_star_step + h.iq_bonus


## 体力上限 = 基础 100 + (星级-1)×每星加值 + 材料加成。
static func sta_cap_total(h: HeroInst) -> int:
	return ConfigDb.I.C.ext.stacap_base + (clampi(h.star, 1, 6) - 1) * ConfigDb.I.C.ext.stacap_star_step + h.sta_cap_bonus


## 教练光环（§2.3 v2）：单教练值 = round(OVR × 系数 × 智力倍率)，
## 智力倍率 = AURA_IQ_BASE + 智商 / AURA_IQ_DIV（智商越高光环越强）。
static func coach_aura(coaches: Array) -> int:
	if coaches == null or coaches.size() == 0:
		return 0
	var c := ConfigDb.I.C
	var total := 0
	for i in coaches.size():
		var co = coaches[i]
		if co == null:
			continue
		var o := ovr(co)
		var rate := c.aura_hc_rate if i == 0 else c.aura_ac_rate
		var iq_mult := c.ext.aura_iq_base + float(iq_total(co)) / c.ext.aura_iq_div
		total += int(roundf(o * rate * iq_mult))
	return total


## 队伍总评（场上 5 人等价 OVR 之和，含位置偏好）。
static func team_ovr(lineup: Dictionary) -> int:
	var total := 0
	for slot in Slots.Field:
		var inst := GameState.hero_by_uid(str(lineup.get(slot, "")))
		if inst != null:
			total += slot_ovr(inst, slot)
	return total
