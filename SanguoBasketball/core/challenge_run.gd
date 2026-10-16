class_name ChallengeRun
## 挑战模式当前局（运行时，不落盘）。

var active := false
var dungeon_n := 1
var index := 1             # 下一支要打的队伍（1-based），10 支打完即通关
var current_opponent = null
var fee := 0               # 已付进场费（用于总结展示）
