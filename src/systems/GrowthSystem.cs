using System;
using System.Collections.Generic;
using SanguoBasketball.Core;

namespace SanguoBasketball.Systems;

/// <summary>
/// 武将数值计算（02 文档 §1/§2/§3）。
/// 公式链：卡片属性 = 基础×S(星)×(得意?D(星):1) + 材料加成
///         槽位属性 = 基础×S×D×位置系数 + 材料加成（§3.1）
///         比赛属性 = 槽位属性×STA系数 + 教练光环加值（§8.2/§8.5）
/// </summary>
public static class HeroCalc
{
    /// <summary>卡片属性（不含位置系数），进详情页/OVR 用。</summary>
    public static float[] CoreAttrs(HeroInst h)
    {
        var def = h.Def;
        double s = ConfigDb.I.StarS(h.Star);
        double d = ConfigDb.I.StarD(h.Star);
        var arr = new float[Attr.COUNT];
        for (int i = 0; i < Attr.COUNT; i++)
        {
            double v = def.Base[i] * s * (def.IsSignature(i) ? d : 1.0);
            arr[i] = (float)(v + h.Mat[i]);
        }
        return arr;
    }

    /// <summary>总评 OVR = 9 项属性平均，展示用整数（§1.1）。</summary>
    public static int Ovr(HeroInst h)
    {
        var a = CoreAttrs(h);
        float sum = 0;
        for (int i = 0; i < Attr.COUNT; i++) sum += a[i];
        return (int)MathF.Round(sum / Attr.COUNT);
    }

    /// <summary>用于挑人/排序的等价 OVR（含位置偏好），不改变展示 OVR。</summary>
    public static int SlotOvr(HeroInst h, string slot)
    {
        var a = SlotAttrs(h, slot);
        float sum = 0;
        for (int i = 0; i < Attr.COUNT; i++) sum += a[i];
        return (int)MathF.Round(sum / Attr.COUNT);
    }

    /// <summary>槽位属性（含位置系数，02 §2.2：偏好 ×1.15，其余 ×1.00；双位置均享受）。</summary>
    public static float[] SlotAttrs(HeroInst h, string slot)
    {
        var def = h.Def;
        bool bonus = Slots.IsField(slot) && def.PrefMatches(slot);
        double pos = bonus ? ConfigDb.I.C.PosBonusMatch : ConfigDb.I.C.PosBonusOther;
        double s = ConfigDb.I.StarS(h.Star);
        double d = ConfigDb.I.StarD(h.Star);
        var arr = new float[Attr.COUNT];
        for (int i = 0; i < Attr.COUNT; i++)
        {
            double v = def.Base[i] * s * (def.IsSignature(i) ? d : 1.0) * pos;
            arr[i] = (float)(v + h.Mat[i]);
        }
        return arr;
    }

    /// <summary>体力系数（§3.4）：0.50 + 0.50 × STA / 100，封顶 ×1.00（体力上限可超 100 但不放大）。</summary>
    public static float StaminaMult(int sta)
    {
        var c = ConfigDb.I.C;
        double v = c.StaminaFactorMin + (c.StaminaFactorMax - c.StaminaFactorMin) * Math.Clamp(sta, 0, c.StaminaMax) / (double)c.StaminaMax;
        return (float)Math.Clamp(v, c.StaminaFactorMin, c.StaminaFactorMax);
    }

    /// <summary>智商合计 = 基础 + (星级-1)×每星加值 + 材料加成（不参与乘区）。</summary>
    public static int IqTotal(HeroInst h)
        => h.Def.Iq + (Math.Clamp(h.Star, 1, 6) - 1) * ConfigDb.I.C.Ext.IqStarStep + h.IqBonus;

    /// <summary>体力上限 = 基础 100 + (星级-1)×每星加值 + 材料加成。</summary>
    public static int StaCapTotal(HeroInst h)
        => ConfigDb.I.C.Ext.StaCapBase + (Math.Clamp(h.Star, 1, 6) - 1) * ConfigDb.I.C.Ext.StaCapStarStep + h.StaCapBonus;

    /// <summary>
    /// 教练光环（§2.3 v2）：单教练值 = round(OVR × 系数 × 智力倍率)，
    /// 智力倍率 = AURA_IQ_BASE + 智商 / AURA_IQ_DIV（智商越高光环越强）。
    /// </summary>
    public static int CoachAura(List<HeroInst> coaches)
    {
        if (coaches == null || coaches.Count == 0) return 0;
        var c = ConfigDb.I.C;
        int total = 0;
        for (int i = 0; i < coaches.Count; i++)
        {
            if (coaches[i] == null) continue;
            int ovr = Ovr(coaches[i]);
            double rate = i == 0 ? c.AuraHcRate : c.AuraAcRate;
            double iqMult = c.Ext.AuraIqBase + IqTotal(coaches[i]) / c.Ext.AuraIqDiv;
            total += (int)MathF.Round(ovr * (float)rate * (float)iqMult);
        }
        return total;
    }

    /// <summary>队伍总评（场上 5 人等价 OVR 之和，含位置偏好）。</summary>
    public static int TeamOvr(Dictionary<string, string> lineup)
    {
        int total = 0;
        foreach (var slot in Slots.Field)
        {
            var inst = GameState.HeroByUid(LineupUid(lineup, slot));
            if (inst != null) total += SlotOvr(inst, slot);
        }
        return total;
    }

    private static string LineupUid(Dictionary<string, string> lineup, string slot)
        => lineup.TryGetValue(slot, out var uid) ? uid : null;
}

/// <summary>
/// 武将成长系统（SYS-02）：材料使用、进化、体力。所有硬校验集中在此。
/// </summary>
public static class GrowthSystem
{
    /// <summary>使用材料（§4.2，含 v2 独立资源材料）。返回实际使用数量。</summary>
    public static int UseMaterial(HeroInst hero, string typeId, string quality, int count)
    {
        var special = ConfigDb.I.SpecialMat(typeId);
        var mt = special == null ? ConfigDb.I.MatType(typeId) : null;
        if (special == null && mt == null) return 0;

        MatQ mq;
        if (special != null)
            mq = special.Q.TryGetValue(quality, out var sqv) ? sqv : new MatQ();
        else
            mq = ConfigDb.I.C.MatQuality.TryGetValue(quality, out var mqv) ? mqv : new MatQ();

        int used = 0;
        for (int i = 0; i < count; i++)
        {
            if (!GameState.UseMat(typeId, quality, 1)) break;
            if (special != null)
            {
                if (special.Stat == "iq") hero.IqBonus += mq.Main;
                else hero.StaCapBonus += mq.Main;
                hero.Mat[special.SubIdx] += mq.Sub;
            }
            else
            {
                hero.Mat[mt.MainIdx] += mq.Main;
                hero.Mat[mt.SubIdx] += mq.Sub;
            }
            used++;
        }
        if (used > 0) SaveSystem.MarkDirty();
        return used;
    }

    /// <summary>材料使用预览（+主 / −副）。</summary>
    public static (int main, int sub) MaterialPreview(string typeId, string quality)
    {
        var special = ConfigDb.I.SpecialMat(typeId);
        if (special != null && special.Q.TryGetValue(quality, out var sq))
            return (sq.Main, sq.Sub);
        var mq = ConfigDb.I.C.MatQuality[quality];
        return (mq.Main, mq.Sub);
    }

    /// <summary>进化校验（v2：祭品必须为“相同的卡牌”同一武将）。err 非空即失败原因。</summary>
    public static bool CanEvolve(HeroInst main, HeroInst sac, out string err)
    {
        err = null;
        if (main == null || sac == null) { err = "请选择祭品卡"; return false; }
        if (main.Uid == sac.Uid) { err = "祭品不能是主卡"; return false; }
        if (main.Star >= ConfigDb.I.C.EvolveMaxStar) { err = "已达最高星级"; return false; }
        // v2 硬规则：只能献祭同一武将（例如山贼只能用山贼献祭）
        if (sac.DefId != main.DefId) { err = "只能献祭相同的武将卡"; return false; }
        // 硬校验：禁止献祭已装备于阵容的卡
        if (GameState.IsEquipped(sac)) { err = "祭品已装备于阵容"; return false; }
        return true;
    }

    /// <summary>执行进化：消耗祭品，主卡星级 +1。</summary>
    public static bool Evolve(HeroInst main, HeroInst sac)
    {
        if (!CanEvolve(main, sac, out _)) return false;
        GameState.RemoveHero(sac);
        main.Star = Math.Min(ConfigDb.I.C.EvolveMaxStar, main.Star + 1);
        SaveSystem.MarkDirty();
        SaveSystem.Save();
        return true;
    }

    /// <summary>比赛后体力结算（§3.4）：上场过的球员 −12（下限 0）。</summary>
    public static void ConsumeStaminaAfterMatch(List<HeroInst> playedPlayers)
    {
        int cost = ConfigDb.I.C.StaminaCostPerMatch;
        foreach (var p in playedPlayers)
        {
            if (p == null) continue;
            p.Sta = Math.Max(0, p.Sta - cost);
        }
        SaveSystem.MarkDirty();
    }

    /// <summary>回满（复活/局末结算；上限为各自体力上限）。</summary>
    public static void RestoreAll(List<HeroInst> players)
    {
        foreach (var p in players)
            if (p != null) p.Sta = HeroCalc.StaCapTotal(p);
        SaveSystem.MarkDirty();
    }
}

/// <summary>
/// 阵容工具：一键最优、候选排序、总评（SYS-01）。
/// </summary>
public static class LineupUtil
{
    public static List<HeroInst> AllInsts() => GameState.D.Heroes;

    public static bool IsInLineup(HeroInst inst)
    {
        foreach (var kv in GameState.D.Lineup)
            if (kv.Value == inst.Uid) return true;
        return false;
    }

    /// <summary>一键最优：按 OVR×位置偏好 贪心填充（仅已解锁槽位）。</summary>
    public static void AutoFill()
    {
        var pool = new List<HeroInst>(GameState.D.Heroes);
        var result = new Dictionary<string, string>();
        // 场上五槽：每槽取 得分 = SlotOvr 最高者
        foreach (var slot in Slots.Field)
        {
            HeroInst best = null;
            int bestScore = int.MinValue;
            foreach (var h in pool)
            {
                int sc = HeroCalc.SlotOvr(h, slot);
                if (sc > bestScore) { bestScore = sc; best = h; }
            }
            if (best != null)
            {
                result[slot] = best.Uid;
                pool.Remove(best);
            }
        }
        // 教练：OVR 降序取前三（仅已解锁）
        pool.Sort((a, b) => HeroCalc.Ovr(b).CompareTo(HeroCalc.Ovr(a)));
        int idx = 0;
        foreach (var slot in Slots.Coach)
        {
            if (!GameState.IsSlotUnlocked(slot)) continue;
            if (idx >= pool.Count) break;
            result[slot] = pool[idx].Uid;
            idx++;
        }
        // 替补：接下来三名（仅已解锁）
        foreach (var slot in Slots.Bench)
        {
            if (!GameState.IsSlotUnlocked(slot)) continue;
            if (idx >= pool.Count) break;
            result[slot] = pool[idx].Uid;
            idx++;
        }

        GameState.D.Lineup = result;
        SaveSystem.MarkDirty();
        GameState.TouchBestTeamOvr();
    }

    /// <summary>确保场上 5 人齐（FTUE 后自动补位）。</summary>
    public static void EnsureFieldFilled()
    {
        var lineup = GameState.D.Lineup;
        var usedUids = new HashSet<string>(lineup.Values);
        bool changed = false;
        foreach (var slot in Slots.Field)
        {
            lineup.TryGetValue(slot, out var uid);
            if (!string.IsNullOrEmpty(uid) && GameState.HeroByUid(uid) != null) continue;
            HeroInst best = null;
            int bestScore = int.MinValue;
            foreach (var h in GameState.D.Heroes)
            {
                if (usedUids.Contains(h.Uid)) continue;
                int sc = HeroCalc.SlotOvr(h, slot);
                if (sc > bestScore) { bestScore = sc; best = h; }
            }
            if (best != null)
            {
                lineup[slot] = best.Uid;
                usedUids.Add(best.Uid);
                changed = true;
            }
        }
        if (changed) SaveSystem.MarkDirty();
    }

    /// <summary>某槽位候选排序（阵容页候选列表）：偏好匹配优先 + 等价 OVR 降序。</summary>
    public static List<HeroInst> CandidatesFor(string slot)
    {
        var list = new List<HeroInst>(GameState.D.Heroes);
        list.Sort((a, b) =>
        {
            int sa = HeroCalc.SlotOvr(a, slot);
            int sb = HeroCalc.SlotOvr(b, slot);
            return sb.CompareTo(sa);
        });
        return list;
    }

    /// <summary>替补席是否还有可用者（供比赛换人）。</summary>
    public static List<HeroInst> AvailableBench()
    {
        var list = new List<HeroInst>();
        foreach (var slot in Slots.Bench)
        {
            if (!GameState.IsSlotUnlocked(slot)) continue;
            var h = GameState.HeroByUid(GameState.D.Lineup.TryGetValue(slot, out var uid) ? uid : null);
            if (h != null) list.Add(h);
        }
        return list;
    }

    public static List<HeroInst> FieldPlayers()
    {
        var list = new List<HeroInst>();
        foreach (var slot in Slots.Field)
        {
            GameState.D.Lineup.TryGetValue(slot, out var uid);
            list.Add(GameState.HeroByUid(uid));
        }
        return list;
    }

    public static List<HeroInst> CoachPlayers()
    {
        var list = new List<HeroInst>();
        foreach (var slot in Slots.Coach)
        {
            GameState.D.Lineup.TryGetValue(slot, out var uid);
            list.Add(GameState.HeroByUid(uid));
        }
        return list;
    }

    public static List<HeroInst> BenchPlayers()
    {
        var list = new List<HeroInst>();
        foreach (var slot in Slots.Bench)
        {
            GameState.D.Lineup.TryGetValue(slot, out var uid);
            list.Add(GameState.HeroByUid(uid));
        }
        return list;
    }
}
