class_name GameRandom
## 统一随机数封装（README §3.4：随机数统一走封装、支持种子复现）。
## 采用 xorshift128 变体（64 位整数位运算，溢出回绕，跨运行结果稳定）。

var seed_value := 0
var _s0 := 0
var _s1 := 0


func _init(seed_in: int = 0) -> void:
	seed_value = seed_in
	var s: int = (seed_in & 0xFFFFFFFF) * 2654435761 + (-7046029254386353131)  # 0x9E3779B97F4A7C15
	_s0 = s | 1
	_s1 = (s ^ (-4658895280553007687)) | 1                            # 0xBF58476D1CE4E5B9
	for i in 8:
		_next_u64()  # 预热


static func new_seed() -> int:
	return (int(Time.get_unix_time_from_system() * 1000.0) ^ Time.get_ticks_msec()) & 0x7FFFFFFF


## 逻辑右移（64 位无符号语义；GDScript 的 >> 是算术移位，需掩码补齐）
static func _lsr(x: int, n: int) -> int:
	return (x >> n) & ((1 << (64 - n)) - 1)


static func _to_positive(x: int) -> int:
	return x & 0x7FFFFFFFFFFFFFFF


func _next_u64() -> int:
	var x := _s0
	var y := _s1
	_s0 = y
	x ^= x << 23
	_s1 = x ^ y ^ _lsr(x, 17) ^ _lsr(y, 26)
	return _s1 + y


## [0,1)
func next_float() -> float:
	return float(_lsr(_next_u64(), 11)) * (1.0 / 9007199254740992.0)


func range_f(min_v: float, max_v: float) -> float:
	return min_v + (max_v - min_v) * next_float()


## [min_inclusive, max_exclusive)
func range_int(min_inclusive: int, max_exclusive: int) -> int:
	if max_exclusive <= min_inclusive:
		return min_inclusive
	var raw := _to_positive(_next_u64())
	return min_inclusive + raw % (max_exclusive - min_inclusive)


func pick(list: Array):
	return list[range_int(0, list.size())]


func chance(p: float) -> bool:
	return next_float() < p


## 按权重数组返回下标；权重全为 0 时返回 0。
func weighted_index(weights: Array) -> int:
	var total := 0.0
	for w in weights:
		total += maxf(0.0, float(w))
	if total <= 0.0:
		return 0
	var r := next_float() * total
	var acc := 0.0
	for i in weights.size():
		acc += maxf(0.0, float(weights[i]))
		if r < acc:
			return i
	return weights.size() - 1


## 按 key 权重抽取（供配置内字典权重使用；保持字典插入序，确定性）。
func pick_weighted_key(dist: Dictionary) -> String:
	var keys := dist.keys()
	var w: Array = []
	for k in keys:
		w.append(float(dist[k]))
	return str(keys[weighted_index(w)])


## items 中按权重函数抽取
func pick_weighted(items: Array, weight_fn: Callable):
	var w: Array = []
	for it in items:
		w.append(float(weight_fn.call(it)))
	return items[weighted_index(w)]


func shuffle(arr: Array) -> void:
	for i in range(arr.size() - 1, 0, -1):
		var j := range_int(0, i + 1)
		var tmp = arr[i]
		arr[i] = arr[j]
		arr[j] = tmp
