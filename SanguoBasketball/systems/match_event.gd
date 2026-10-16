class_name MatchEvent
## 单条比赛事件

var quarter := 1               # 1..4（>4 为加时）
var at := 0.0                  # 节内进度 0..1
var type := ""                 # opening/qend/normal/three/dunk/combo/block/steal/foul/foulout/three_rain/clutch
var text := ""
var score_a := 0
var score_b := 0
var delta_a := 0
var delta_b := 0
var actor_uid := ""
var actor_is_a := false
var fouls_now := -1
var sub_in_name := ""
var sub_index := -1            # 六犯事件序号（覆写 key）
var sub_pending := false       # 我方六犯：UI 可 3 秒内改选替补
