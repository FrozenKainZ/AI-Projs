using System;
using System.Collections.Generic;
using System.Linq;
using SanguoBasketball.Core;

namespace SanguoBasketball.Systems;

// ============================================================
//  比赛模拟引擎（SYS-05，02 文档 §8）★核心算法★
//  纯函数式：内部克隆输入，支持种子复现与替补覆写重演（比赛数据先算后播）。
// ============================================================

public sealed class BattlePlayer
{
    public string Uid;
    public string Name;
    public string DefId;
    public string Quality;
    public string Pref;                // 卡的偏好位置
    public int Star;
    public HeroInst Inst;              // 玩家侧实例（体力结算）；对手侧为 null
    public string SlotPos = "PG";
    public float[] Core = new float[SanguoBasketball.Core.Attr.COUNT]; // 基础×S×D + 材料（不含位置/体力/光环）
    public float[] Attr = new float[SanguoBasketball.Core.Attr.COUNT]; // 比赛属性（槽位×体力+光环）
    public bool OnCourt;
    public bool PlayedThisMatch;
    public bool FromBench;

    public int Fouls;
    public bool FouledOut;
    public bool FoulTrouble;           // 无替补时继续出场（×0.60，不再累计犯规）

    // 统计
    public int Points, Threes, Blocks, Steals, RebEvents, Made, Attempts;
    public int PossessionsOnCourt;

    public BattlePlayer Clone()
    {
        var c = (BattlePlayer)MemberwiseClone();
        c.Core = (float[])Core.Clone();
        c.Attr = (float[])Attr.Clone();
        return c;
    }
}

public sealed class BattleTeam
{
    public string Name;
    public bool IsPlayer;
    public BattlePlayer[] Court = new BattlePlayer[5]; // 对齐 Slots.Field
    public List<BattlePlayer> Bench = new();
    public List<HeroInst> CoachInsts = new();
    public float Aura;

    public BattleTeam Clone()
    {
        var t = new BattleTeam { Name = Name, IsPlayer = IsPlayer, Aura = Aura, CoachInsts = CoachInsts };
        for (int i = 0; i < 5; i++) t.Court[i] = Court[i]?.Clone();
        t.Bench = Bench.Select(b => b.Clone()).ToList();
        return t;
    }

    public List<BattlePlayer> LiveCourt()
    {
        var l = new List<BattlePlayer>(5);
        for (int i = 0; i < 5; i++) if (Court[i] != null) l.Add(Court[i]);
        return l;
    }

    public float AvgAttr(int attr)
    {
        var l = LiveCourt();
        if (l.Count == 0) return 1;
        float s = 0;
        foreach (var p in l) s += p.Attr[attr];
        return s / l.Count;
    }
}

public sealed class MatchEvent
{
    public int Quarter;            // 1..4（>4 为加时）
    public float At;               // 节内进度 0..1
    public string Type;            // opening/qend/normal/three/dunk/combo/block/steal/foul/foulout/three_rain/clutch
    public string Text;
    public int ScoreA, ScoreB;
    public int DeltaA, DeltaB;
    public string ActorUid;
    public bool ActorIsA;
    public int FoulsNow = -1;
    public string SubInName;
    public int SubIndex = -1;      // 六犯事件序号（覆写 key）
    public bool SubPending;        // 我方六犯：UI 可 3 秒内改选替补
}

public sealed class PlayerBox
{
    public string Name;
    public string Uid;
    public bool IsPlayerSide;
    public int Points, Blocks, Steals, Fouls, Rebounds;
    public float MinutesPct;
    public bool Starter;
    public bool FouledOut;
    public int Contribution => Points + 3 * Blocks + 3 * Steals + (int)(0.5f * Rebounds);
}

public sealed class MatchResult
{
    public string TeamAName, TeamBName;
    public bool PlayerIsA;
    public int FinalA, FinalB;
    public int[] QA = new int[4];
    public int[] QB = new int[4];
    public List<int> QATotal = new();
    public List<int> QBTotal = new();
    public List<MatchEvent> Events = new();
    public List<PlayerBox> BoxA = new(), BoxB = new();
    public string MvpName;
    public string MvpSide; // A / B
    public int Margin => Math.Abs(FinalA - FinalB);
    public bool PlayerWon => PlayerIsA ? FinalA > FinalB : FinalB > FinalA;
    public int PlayerScore => PlayerIsA ? FinalA : FinalB;
    public int OppScore => PlayerIsA ? FinalB : FinalA;

    /// <summary>全场比分最大分差（用于"最高分差"结算）。</summary>
    public int MaxMargin()
    {
        int mx = 0;
        foreach (var e in Events) mx = Math.Max(mx, Math.Abs(e.ScoreA - e.ScoreB));
        return mx;
    }
}

public static class MatchEngine
{
    public const int SlotCount = 5;

    // ---------- 属性应用（统一入口） ----------
    public static void ApplyAttr(BattlePlayer p, float aura, string slot, float staMult)
    {
        bool field = Slots.IsField(slot);
        float pos = field && p.Pref == slot ? (float)ConfigDb.I.C.PosBonusMatch : (float)ConfigDb.I.C.PosBonusOther;
        if (!field) pos = 1f;
        for (int i = 0; i < Attr.COUNT; i++)
            p.Attr[i] = Math.Max(1f, p.Core[i] * pos * staMult + aura);
    }

    // ---------- 构建我方队伍 ----------
    public static BattleTeam BuildPlayerTeam(string teamName = "我方战队")
    {
        var team = new BattleTeam { IsPlayer = true, Name = teamName };
        var coaches = LineupUtil.CoachPlayers();
        team.CoachInsts = coaches.Where(c => c != null).ToList();
        team.Aura = HeroCalc.CoachAura(coaches);

        var field = LineupUtil.FieldPlayers();
        for (int i = 0; i < SlotCount; i++)
        {
            var inst = field[i];
            if (inst == null) continue;
            string slot = Slots.Field[i];
            var p = NewPlayer(inst);
            p.SlotPos = slot;
            ApplyAttr(p, team.Aura, slot, HeroCalc.StaminaMult(inst.Sta));
            p.OnCourt = true;
            p.PlayedThisMatch = true;
            team.Court[i] = p;
        }
        foreach (var b in LineupUtil.BenchPlayers())
            if (b != null) team.Bench.Add(NewPlayer(b));
        return team;
    }

    private static BattlePlayer NewPlayer(HeroInst inst)
    {
        return new BattlePlayer
        {
            Uid = inst.Uid,
            Name = inst.Def.Name,
            DefId = inst.DefId,
            Quality = inst.Def.QualityKey,
            Pref = inst.Def.Pref,
            Star = inst.Star,
            Inst = inst,
            Core = HeroCalc.CoreAttrs(inst),
        };
    }

    // ---------- 构建对手队伍 ----------
    public static BattleTeam BuildOpponentTeam(OpponentTeam gen)
    {
        var team = new BattleTeam { IsPlayer = false, Name = gen.Name, Aura = gen.Aura };
        for (int i = 0; i < SlotCount; i++)
        {
            var op = i < gen.Court.Count ? gen.Court[i] : gen.Court[gen.Court.Count - 1];
            var p = FromOpp(op);
            p.SlotPos = Slots.Field[i];
            ApplyAttr(p, gen.Aura, p.SlotPos, 1f);
            p.OnCourt = true;
            p.PlayedThisMatch = true;
            team.Court[i] = p;
        }
        foreach (var b in gen.Bench) team.Bench.Add(FromOpp(b));
        foreach (var c in gen.Coaches) team.CoachInsts.Add(null);
        return team;
    }

    private static BattlePlayer FromOpp(OpponentPlayer op)
    {
        return new BattlePlayer
        {
            Uid = op.Uid,
            Name = op.Name,
            DefId = op.DefId,
            Quality = op.Quality,
            Pref = op.Pref,
            Star = op.Star,
            Core = (float[])op.Core.Clone(),
        };
    }

    // ---------- 主模拟入口 ----------
    public static MatchResult Simulate(BattleTeam rawA, BattleTeam rawB, int seed, bool playerIsA,
        Dictionary<int, string> subOverrides = null)
    {
        var st = new SimState
        {
            A = rawA.Clone(),
            B = rawB.Clone(),
            Rng = new GameRandom(seed),
            Overrides = subOverrides,
        };
        return st.Run(playerIsA);
    }

    private sealed class SimState
    {
        public BattleTeam A, B;
        public GameRandom Rng;
        public Dictionary<int, string> Overrides;

        private readonly Dictionary<string, int> _lastTpl = new();
        private int _subIdx;
        private bool _clutchDone;
        private int _scoreA, _scoreB;

        private string Fill(string tpl, string actor, string oppo, int num, string sub, string qname, int x, int y, string trailing)
        {
            return tpl.Replace("{A}", actor ?? "").Replace("{B}", oppo ?? "")
                .Replace("{N}", num.ToString()).Replace("{S}", sub ?? "无人可换")
                .Replace("{Q}", qname ?? "").Replace("{X}", x.ToString()).Replace("{Y}", y.ToString())
                .Replace("{T}", trailing ?? "");
        }

        private string Line(string category, string actor = null, string oppo = null, int num = 0,
            string sub = null, string qname = null, int x = 0, int y = 0, string trailing = null)
        {
            var pool = ConfigDb.I.Lines(category);
            int idx;
            if (pool.Length <= 1) idx = 0;
            else
            {
                int guard = 0;
                do { idx = Rng.RangeInt(0, pool.Length); guard++; }
                while (_lastTpl.TryGetValue(category, out var l) && l == idx && guard < 8);
            }
            _lastTpl[category] = idx;
            return Fill(pool[idx], actor, oppo, num, sub, qname, x, y, trailing);
        }

        public MatchResult Run(bool playerIsA)
        {
            var result = new MatchResult { TeamAName = A.Name, TeamBName = B.Name, PlayerIsA = playerIsA };
            var c = ConfigDb.I.C;

            // 球权与篮板修正（§8.1）
            float rebA = A.AvgAttr(Attr.REB);
            float rebB = B.AvgAttr(Attr.REB);
            int adj = (int)Math.Round(c.PossessionRebCap * (rebA - rebB) / Math.Max(1f, rebA + rebB));
            adj = Math.Clamp(adj, -c.PossessionRebCap, c.PossessionRebCap);
            var qA = SplitPossessions(c.PossessionsBase + adj);
            var qB = SplitPossessions(c.PossessionsBase - adj);

            var events = new List<MatchEvent>
            {
                new MatchEvent
                {
                    Quarter = 1, At = 0f, Type = "opening",
                    Text = Line("opening", qname: c.Ext.QuarterNames[0]),
                    ScoreA = 0, ScoreB = 0,
                }
            };

            for (int q = 1; q <= 4; q++)
            {
                RunSegment(q, qA[q - 1], qB[q - 1], events, result, false);
            }

            // 平局 → 加时（罕见兜底，每节各 10 回合）
            int ot = 5;
            while (_scoreA == _scoreB && ot < 9)
            {
                RunSegment(ot, 10, 10, events, result, true);
                ot++;
            }

            result.FinalA = _scoreA;
            result.FinalB = _scoreB;
            result.Events = events;
            result.BoxA = BuildBoxes(A, true);
            result.BoxB = BuildBoxes(B, false);

            PlayerBox mvp = null;
            foreach (var b in result.BoxA.Concat(result.BoxB))
                if (mvp == null || b.Contribution > mvp.Contribution) mvp = b;
            if (mvp != null)
            {
                result.MvpName = mvp.Name;
                result.MvpSide = (mvp.IsPlayerSide == playerIsA) ? "A" : "B";
            }
            return result;
        }

        private void RunSegment(int q, int pa, int pb, List<MatchEvent> events, MatchResult result, bool isOt)
        {
            var cand = new List<MatchEvent>();
            var threeCount = new Dictionary<bool, int>();
            var quarterScores = new Dictionary<string, int>();
            var comboTripped = new HashSet<string>();
            int scoreA0 = _scoreA, scoreB0 = _scoreB;
            int doneA = 0, doneB = 0;
            int total = Math.Max(1, pa + pb);

            while (doneA < pa || doneB < pb)
            {
                float at = (float)(doneA + doneB) / total;
                if (doneA < pa)
                {
                    Possession(A, B, q, cand, true, threeCount, quarterScores, comboTripped, at);
                    doneA++;
                }
                if (doneB < pb)
                {
                    Possession(B, A, q, cand, false, threeCount, quarterScores, comboTripped, at);
                    doneB++;
                }
            }

            // 节末（必保留）
            string qname = isOt ? "加时" : ConfigDb.I.C.Ext.QuarterNames[Math.Clamp(q, 1, 4) - 1];
            string trailing = _scoreA >= _scoreB ? B.Name : A.Name;
            cand.Add(new MatchEvent
            {
                Quarter = q, At = 1f, Type = "qend",
                Text = isOt
                    ? $"加时战罢 {_scoreA}:{_scoreB}！"
                    : Line("qend", qname: qname, x: _scoreA, y: _scoreB, trailing: trailing),
                ScoreA = _scoreA, ScoreB = _scoreB,
            });

            if (!isOt)
            {
                result.QA[q - 1] = _scoreA - scoreA0;
                result.QB[q - 1] = _scoreB - scoreB0;
            }
            result.QATotal.Add(_scoreA);
            result.QBTotal.Add(_scoreB);

            SelectEvents(cand, events);
        }

        private static int[] SplitPossessions(int total)
        {
            var q = new int[4];
            int baseEach = total / 4;
            int rem = total % 4;
            for (int i = 0; i < 4; i++) q[i] = baseEach;
            q[0] += rem; // 余数计入第 1 节（§8.1）
            return q;
        }

        // ---------- 单回合（§8.2 STEP 1–7） ----------
        private void Possession(BattleTeam off, BattleTeam def, int quarter, List<MatchEvent> cand, bool offIsA,
            Dictionary<bool, int> threeCount, Dictionary<string, int> quarterScores, HashSet<string> comboTripped, float at)
        {
            var c = ConfigDb.I.C;
            var offCourt = off.LiveCourt();
            var defCourt = def.LiveCourt();
            if (offCourt.Count == 0 || defCourt.Count == 0) return;

            foreach (var p in offCourt) p.PossessionsOnCourt++;
            foreach (var p in defCourt) p.PossessionsOnCourt++;

            // STEP 1 支配者
            var weights = new double[offCourt.Count];
            for (int i = 0; i < offCourt.Count; i++)
            {
                var p = offCourt[i];
                weights[i] = (p.Attr[Attr.P3] + p.Attr[Attr.MID] + p.Attr[Attr.RIM]) * (0.90 + 0.20 * Rng.Next());
            }
            var shooter = offCourt[Rng.WeightedIndex(weights)];

            // STEP 2 进攻类型（∝ 3PT/MID/RIM 归一化）
            int type = Rng.WeightedIndex(new[]
            {
                (double)shooter.Attr[Attr.P3], (double)shooter.Attr[Attr.MID], (double)shooter.Attr[Attr.RIM]
            });

            // STEP 3 对位防守（同位置，空位按均值 ×0.70）
            int slotIdx = Math.Max(0, Array.IndexOf(Slots.Field, shooter.SlotPos));
            var defender = slotIdx < 5 && def.Court[slotIdx] != null ? def.Court[slotIdx] : defCourt[0];
            bool missing = slotIdx < 5 && def.Court[slotIdx] == null;

            float DefVal(int attrIdx)
            {
                if (!missing) return defender.Attr[attrIdx];
                float sum = 0;
                foreach (var p in defCourt) sum += p.Attr[attrIdx];
                return sum / Math.Max(1, defCourt.Count) * 0.70f;
            }

            float atk, dfn;
            switch (type)
            {
                case 0:
                    atk = shooter.Attr[Attr.P3];
                    dfn = 0.55f * DefVal(Attr.STL) + 0.45f * DefVal(Attr.SPD);
                    break;
                case 1:
                    atk = shooter.Attr[Attr.MID];
                    dfn = 0.40f * DefVal(Attr.STL) + 0.30f * DefVal(Attr.BLK) + 0.30f * DefVal(Attr.PHY);
                    break;
                default:
                    atk = 0.80f * shooter.Attr[Attr.RIM] + 0.20f * shooter.Attr[Attr.PHY];
                    dfn = 0.60f * DefVal(Attr.BLK) + 0.40f * DefVal(Attr.PHY);
                    break;
            }

            // STEP 4 团队传球修正
            float pasA = off.AvgAttr(Attr.PAS);
            float pasB = def.AvgAttr(Attr.PAS);
            float passBonus = Math.Clamp(0.5f * (pasA - pasB) / Math.Max(1f, pasA + pasB),
                -(float)c.PassBonusCap, (float)c.PassBonusCap);
            float atkFinal = atk * (1 + passBonus);

            // STEP 5 对抗与判定
            float k = Math.Clamp(atkFinal / Math.Max(1f, dfn), (float)c.KMin, (float)c.KMax);
            float hit = Math.Clamp((float)(c.HitBase + c.HitLogCoef * Math.Log2(k)), (float)c.HitMin, (float)c.HitMax);
            bool scored = Rng.Next() < hit;

            int pts = 0;
            bool isThreeRaw = type == 0;
            bool isThree = false;
            bool isDunk = false;
            if (scored)
            {
                double val = c.ValBase * Math.Pow(k, c.ValExp);
                val = Math.Clamp(val, c.ValMin, c.ValMax);
                pts = (int)Math.Round(val * (0.70 + 0.60 * Rng.Next()));
                if (pts <= 0) pts = 1;
                // 事件分类（§8.6）：三分得手且 ≥3；暴扣=攻框且单回合 ≥6。分值本身遵循 §8.2 公式，不额外修改。
                isThree = isThreeRaw && pts >= 3;
                isDunk = type == 2 && pts >= 6;
            }

            if (scored)
            {
                if (offIsA) _scoreA += pts; else _scoreB += pts;
                shooter.Points += pts;
                shooter.Made++;
                shooter.Attempts++;
                if (isThree) shooter.Threes++;

                // 连击（单节单人 ≥3 次得分）
                quarterScores[shooter.Uid] = quarterScores.GetValueOrDefault(shooter.Uid) + 1;
                if (quarterScores[shooter.Uid] == 3 && comboTripped.Add(shooter.Uid))
                {
                    cand.Add(new MatchEvent
                    {
                        Quarter = quarter, At = at, Type = "combo",
                        Text = Line("combo", shooter.Name, num: shooter.Points),
                        ScoreA = _scoreA, ScoreB = _scoreB, ActorUid = shooter.Uid, ActorIsA = offIsA,
                    });
                }

                cand.Add(new MatchEvent
                {
                    Quarter = quarter, At = at,
                    Type = isDunk ? "dunk" : (isThree ? "three" : "normal"),
                    Text = isDunk ? Line("dunk", shooter.Name, defender.Name)
                         : isThree ? Line("three", shooter.Name, defender.Name)
                         : Line("normal_score", shooter.Name, defender.Name),
                    ScoreA = _scoreA, ScoreB = _scoreB,
                    DeltaA = offIsA ? pts : 0, DeltaB = offIsA ? 0 : pts,
                    ActorUid = shooter.Uid, ActorIsA = offIsA,
                });

                if (isThree)
                {
                    threeCount[offIsA] = threeCount.GetValueOrDefault(offIsA) + 1;
                    if (threeCount[offIsA] == 3)
                    {
                        cand.Add(new MatchEvent
                        {
                            Quarter = quarter, At = at, Type = "three_rain",
                            Text = off.Name + "下起三分雨！连续三记远投命中！",
                            ScoreA = _scoreA, ScoreB = _scoreB, ActorIsA = offIsA,
                        });
                    }
                }
            }
            else
            {
                shooter.Attempts++;
                AttributeRebound(def); // 打铁 → 防守方篮板事件

                if (k < 0.4f) // 盖帽 / 抢断（k<0.4 且命中失败）
                {
                    if (type == 2)
                    {
                        defender.Blocks++;
                        cand.Add(new MatchEvent
                        {
                            Quarter = quarter, At = at, Type = "block",
                            Text = Line("block", defender.Name, shooter.Name),
                            ScoreA = _scoreA, ScoreB = _scoreB,
                            ActorUid = defender.Uid, ActorIsA = !offIsA,
                        });
                    }
                    else
                    {
                        defender.Steals++;
                        cand.Add(new MatchEvent
                        {
                            Quarter = quarter, At = at, Type = "steal",
                            Text = Line("steal", defender.Name, shooter.Name),
                            ScoreA = _scoreA, ScoreB = _scoreB,
                            ActorUid = defender.Uid, ActorIsA = !offIsA,
                        });
                    }
                }
            }

            // STEP 6 犯规（12%，独立于得分）
            if (Rng.Next() < c.FoulChance)
            {
                bool defSide = Rng.Next() < c.FoulSideDef;
                var team = defSide ? def : off;
                var court = team.LiveCourt().Where(p => !p.FoulTrouble && !p.FouledOut).ToList();
                if (court.Count > 0)
                {
                    var fw = new double[court.Count];
                    for (int i = 0; i < court.Count; i++)
                    {
                        double w = Math.Max(1f, court[i].Attr[Attr.PHY]);
                        if (court[i].Fouls >= 3) w *= 1.5;
                        fw[i] = w;
                    }
                    var fouler = court[Rng.WeightedIndex(fw)];
                    fouler.Fouls++;

                    cand.Add(new MatchEvent
                    {
                        Quarter = quarter, At = at, Type = "foul",
                        Text = Line("foul", fouler.Name),
                        ScoreA = _scoreA, ScoreB = _scoreB,
                        ActorUid = fouler.Uid, ActorIsA = team == A, FoulsNow = fouler.Fouls,
                    });

                    if (fouler.Fouls >= c.FoulOutCount && !fouler.FouledOut)
                        HandleFoulOut(team, fouler, cand, quarter, at);
                }
            }

            // 收官（分差 ≥150 首次）
            if (!_clutchDone && Math.Abs(_scoreA - _scoreB) >= 150)
            {
                _clutchDone = true;
                string trailing = _scoreA >= _scoreB ? B.Name : A.Name;
                cand.Add(new MatchEvent
                {
                    Quarter = quarter, At = at, Type = "clutch",
                    Text = Line("clutch", num: Math.Abs(_scoreA - _scoreB), trailing: trailing),
                    ScoreA = _scoreA, ScoreB = _scoreB,
                });
            }
        }

        /// <summary>篮板事件按 REB 占比归因（§8.7 简化：比例配额最大余数式分配）。</summary>
        private void AttributeRebound(BattleTeam def)
        {
            var court = def.LiveCourt();
            if (court.Count == 0) return;
            float sum = 0;
            foreach (var p in court) sum += Math.Max(1f, p.Attr[Attr.REB]);
            int totalReb = 0;
            foreach (var p in court) totalReb += p.RebEvents;
            BattlePlayer best = court[0];
            float bestScore = float.MinValue;
            foreach (var p in court)
            {
                float share = Math.Max(1f, p.Attr[Attr.REB]) / sum;
                float quota = share * (totalReb + 1) - p.RebEvents;
                if (quota > bestScore) { bestScore = quota; best = p; }
            }
            best.RebEvents++;
        }

        private void HandleFoulOut(BattleTeam team, BattlePlayer fouler, List<MatchEvent> cand, int quarter, float at)
        {
            int slotIdx = Array.IndexOf(team.Court, fouler);
            if (slotIdx < 0) slotIdx = 0;
            string slot = Slots.Field[slotIdx];
            var bench = team.Bench.Where(b => !b.PlayedThisMatch && !b.OnCourt).ToList();

            string subName;
            int evtSubIdx = -1;
            if (bench.Count > 0)
            {
                BattlePlayer chosen = null;
                if (team.IsPlayer && Overrides != null && Overrides.TryGetValue(_subIdx, out var uid))
                    chosen = bench.Find(b => b.Uid == uid);
                chosen ??= bench.OrderByDescending(b =>
                {
                    float score = 0;
                    for (int i = 0; i < Attr.COUNT; i++) score += b.Core[i];
                    if (b.Pref == slot) score *= 1.15f; // 位置偏好匹配优先
                    return score;
                }).First();

                DoSub(team, fouler, chosen, slot);
                fouler.FouledOut = true;
                subName = chosen.Name;
                evtSubIdx = team.IsPlayer ? _subIdx++ : -1;
            }
            else
            {
                // 「犯规困扰」继续出场：全属性 ×0.60，不再累计犯规（§8.4）
                fouler.FoulTrouble = true;
                fouler.FouledOut = true; // 不再计入犯规池，但仍站在场上
                for (int i = 0; i < Attr.COUNT; i++)
                    fouler.Attr[i] = Math.Max(1f, fouler.Attr[i] * (float)ConfigDb.I.C.FoulTroubleMult);
                subName = "无人可换";
            }

            cand.Add(new MatchEvent
            {
                Quarter = quarter, At = at, Type = "foulout",
                Text = Line("foulout", fouler.Name, sub: subName),
                ScoreA = _scoreA, ScoreB = _scoreB,
                ActorUid = fouler.Uid, ActorIsA = team == A,
                SubInName = subName, SubIndex = evtSubIdx,
                SubPending = team.IsPlayer && evtSubIdx >= 0,
            });
        }

        private void DoSub(BattleTeam team, BattlePlayer outP, BattlePlayer inP, string slot)
        {
            int idx = Array.IndexOf(team.Court, outP);
            if (idx < 0) return;
            inP.SlotPos = slot;
            float staMult = inP.Inst != null ? HeroCalc.StaminaMult(inP.Inst.Sta) : 1f;
            ApplyAttr(inP, team.Aura, slot, staMult);
            inP.OnCourt = true;
            inP.PlayedThisMatch = true;
            inP.FromBench = true;
            team.Court[idx] = inP;
        }

        // ---------- 每节事件挑选（8–12 条，优先级筛选，§8.6） ----------
        private static int Priority(string type) => type switch
        {
            "qend" => 100,
            "foulout" => 98,
            "dunk" => 92,
            "three_rain" => 88,
            "block" => 86,
            "steal" => 82,
            "combo" => 78,
            "three" => 70,
            "clutch" => 60,
            "foul" => 45,
            "normal" => 30,
            _ => 20
        };

        private static void SelectEvents(List<MatchEvent> cand, List<MatchEvent> outEvents)
        {
            var must = cand.Where(e => e.Type == "qend").ToList();
            var rest = cand.Where(e => e.Type != "qend")
                .OrderByDescending(e => Priority(e.Type))
                .ThenBy(e => e.At)
                .Take(Math.Max(0, 12 - must.Count))
                .ToList();
            var picked = rest.Concat(must).OrderBy(e => e.At).ToList();

            if (picked.Count < 8)
            {
                var leftover = cand.Where(e => !picked.Contains(e))
                    .OrderByDescending(e => Priority(e.Type))
                    .Take(8 - picked.Count);
                picked = picked.Concat(leftover).OrderBy(e => e.At).ToList();
            }
            outEvents.AddRange(picked);
        }

        private List<PlayerBox> BuildBoxes(BattleTeam team, bool isPlayerSide)
        {
            var boxes = new List<PlayerBox>();
            var seen = new HashSet<string>();
            int total = Math.Max(1, team.Court.Sum(p => p?.PossessionsOnCourt ?? 0));
            foreach (var p in team.Court)
            {
                if (p == null || !seen.Add(p.Uid)) continue;
                boxes.Add(new PlayerBox
                {
                    Name = p.Name, Uid = p.Uid, IsPlayerSide = isPlayerSide,
                    Points = p.Points, Blocks = p.Blocks, Steals = p.Steals, Fouls = p.Fouls,
                    Rebounds = p.RebEvents, Starter = !p.FromBench, FouledOut = p.FouledOut,
                    MinutesPct = p.PossessionsOnCourt / (float)total,
                });
            }
            return boxes.OrderByDescending(b => b.Contribution).ToList();
        }
    }
}
