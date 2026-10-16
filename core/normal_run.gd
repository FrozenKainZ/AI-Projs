class_name NormalRun
## 普通模式当前局（运行时，不落盘）。

var active := false
var streak := 0            # 已赢场次
var coins_earned := 0
var best_margin := 0
var revive_used := false
var fire_count := 0        # 一局内比赛场次（含复活再战）
var current_opponent = null
var current_k := 0         # 当前第 k 场（1-based）
