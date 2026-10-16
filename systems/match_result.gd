class_name MatchResult
## 比赛结果（先算后播）

var team_a_name := ""
var team_b_name := ""
var player_is_a := true
var final_a := 0
var final_b := 0
var qa: Array = [0, 0, 0, 0]
var qb: Array = [0, 0, 0, 0]
var qa_total: Array = []
var qb_total: Array = []
var events: Array = []
var box_a: Array = []
var box_b: Array = []
var mvp_name := ""
var mvp_side := ""             # A / B


var margin: int:
	get:
		return absi(final_a - final_b)


var player_won: bool:
	get:
		return (final_a > final_b) if player_is_a else (final_b > final_a)


var player_score: int:
	get:
		return final_a if player_is_a else final_b


var opp_score: int:
	get:
		return final_b if player_is_a else final_a


## 全场比分最大分差（用于“最高分差”结算）。
func max_margin() -> int:
	var mx := 0
	for e in events:
		mx = maxi(mx, absi(e.score_a - e.score_b))
	return mx
