using System;
using System.Collections.Generic;
using System.Linq;
using SanguoBasketball.Core;

namespace SanguoBasketball.Systems;

/// <summary>对手球员（生成数据，Core 已按目标 OVR 缩放）。</summary>
public sealed class OpponentPlayer
{
    public string Uid;
    public string Name;
    public string DefId;
    public string Quality;
    public string Pref;
    public int Star;
    public float[] Core = new float[Attr.COUNT];

    public int Ovr()
    {
        float s = 0;
        for (int i = 0; i < Attr.COUNT; i++) s += Core[i];
        return (int)MathF.Round(s / Attr.COUNT);
    }
}

/// <summary>对手队伍（普通模式/挑战模式通用）。</summary>
public sealed class OpponentTeam
{
    public string Name;
    public int TargetOvr;
    public int Seed;
    public List<OpponentPlayer> Court = new();
    public List<OpponentPlayer> Coaches = new();
    public List<OpponentPlayer> Bench = new();
    public float Aura;

    public int AvgCourtOvr() => Court.Count == 0 ? 0 : (int)Math.Round(Court.Average(p => p.Ovr()));
}

/// <summary>
/// 对手生成器（SYS-06/07，02 文档 §9.2 / §10.2 + 05 文档 §8）。
/// </summary>
public static class OpponentGen
{
    private static (double green, double blue, double purple, double gold) WeightForK(int k)
    {
        if (k <= 5) return (0.60, 0.35, 0.04, 0.01);
        if (k <= 15) return (0.40, 0.40, 0.17, 0.03);
        if (k <= 30) return (0.20, 0.40, 0.33, 0.07);
        if (k <= 50) return (0.05, 0.30, 0.50, 0.15);
        return (0.00, 0.20, 0.55, 0.25);
    }

    private static HeroDef PickByWeight(GameRandom rng, double g, double b, double p, double j)
    {
        string q = rng.WeightedIndex(new[] { g, b, p, j }) switch
        {
            0 => "green",
            1 => "blue",
            2 => "purple",
            _ => "gold"
        };
        var pool = ConfigDb.I.ByQuality(q);
        return rng.Pick(pool);
    }

    /// <summary>普通模式第 k 场对手（§9.2）。tutorial=保送局（OVR 45）。</summary>
    public static OpponentTeam Normal(int k, int seed, bool tutorial = false)
    {
        var c = ConfigDb.I.C;
        var rng = new GameRandom(seed);
        int target = tutorial
            ? c.Ext.FtueTeacherOvr
            : (int)Math.Round(c.NormalOppBase * Math.Pow(c.NormalOppGrowth, k - 1));
        int star = Math.Clamp(1 + (k - 1) / 8, 1, 6);
        var (wg, wb, wp, wj) = WeightForK(k);

        // 抽 5 张，位置尽量不重复；禁止与玩家完全相同的 5 人组合（重抽至多 5 次）
        var playerDefs = new HashSet<string>(LineupUtil.FieldPlayers().Where(x => x != null).Select(x => x.DefId));
        List<OpponentPlayer> court = null;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            court = PickPlayers(rng, 5, wg, wb, wp, wj, star, distinctPref: true, rng2: rng);
            if (!court.Select(p => p.DefId).ToHashSet().SetEquals(playerDefs)) break;
        }
        court ??= PickPlayers(rng, 5, wg, wb, wp, wj, star, true, rng);

        // 缩放到目标 OVR
        float actual = TeamOvr(court);
        float scale = target / Math.Max(1f, actual);
        ApplyScale(court, scale);

        // 教练 3 张（k≥10 起才启用光环）
        var coaches = PickPlayers(rng, 3, wg, wb, wp, wj, star, false, rng);
        float aura = k >= 10 ? ComputeAura(coaches) : 0;

        int benchCount = k >= 20 ? 3 : k >= 15 ? 2 : k >= 10 ? 1 : 0;

        return new OpponentTeam
        {
            Name = TeamName(rng),
            TargetOvr = target,
            Seed = seed,
            Court = court,
            Coaches = coaches,
            Bench = PickPlayers(rng, benchCount, wg, wb, wp, wj, star, false, rng),
            Aura = aura,
        };
    }

    /// <summary>挑战模式副本 n 第 m 支队伍（§10.2）。</summary>
    public static OpponentTeam Dungeon(int n, int m, int seed)
    {
        var c = ConfigDb.I.C;
        var rng = new GameRandom(seed);
        double target = c.DungeonOppBase * Math.Pow(c.DungeonOppGrowth, n - 1) * (1 + c.DungeonTeamStep * (m - 1));
        int star = Math.Clamp(1 + n / 4, 1, 6);
        double gold = Math.Min(0.05 + 0.03 * n, 0.45);
        double blue = n < 5 ? 0.20 : 0.0;
        double purple = Math.Max(0.0, 1.0 - gold - blue);

        var court = PickPlayers(rng, 5, 0, blue, purple, gold, star, true, rng);
        float actual = TeamOvr(court);
        float scale = (float)target / Math.Max(1f, actual);
        ApplyScale(court, scale);

        var coaches = PickPlayers(rng, 3, 0, blue, purple, gold, star, false, rng);
        float aura = n >= 8 ? ComputeAura(coaches) : 0;
        int benchCount = n >= 18 ? 3 : n >= 12 ? 2 : n >= 5 ? 1 : 0;

        return new OpponentTeam
        {
            Name = TeamName(rng, n),
            TargetOvr = (int)Math.Round(target),
            Seed = seed,
            Court = court,
            Coaches = coaches,
            Bench = PickPlayers(rng, benchCount, 0, blue, purple, gold, star, false, rng),
            Aura = aura,
        };
    }

    /// <summary>副本固定队伍种子（生成后固定，可重进，§10.1）。</summary>
    public static int DungeonSeed(int n, int m) => 100000 + n * 1000 + m * 7;

    // ---------- 内部 ----------
    private static List<OpponentPlayer> PickPlayers(GameRandom rng, int count,
        double wg, double wb, double wp, double wj, int star, bool distinctPref, GameRandom rng2)
    {
        var list = new List<OpponentPlayer>();
        var usedPref = new HashSet<string>();
        int guard = 0;
        while (list.Count < count && guard++ < 200)
        {
            var def = PickByWeight(rng, wg, wb, wp, wj);
            if (distinctPref && usedPref.Contains(def.Pref) && usedPref.Count < 5) continue;
            usedPref.Add(def.Pref);
            list.Add(MakePlayer(def, star, list.Count));
        }
        return list;
    }

    private static OpponentPlayer MakePlayer(HeroDef def, int star, int index)
    {
        var op = new OpponentPlayer
        {
            Uid = "E" + index + "_" + def.Id,
            Name = def.Name,
            DefId = def.Id,
            Quality = def.QualityKey,
            Pref = def.Pref,
            Star = star,
        };
        double s = ConfigDb.I.StarS(star);
        double d = ConfigDb.I.StarD(star);
        for (int i = 0; i < Attr.COUNT; i++)
            op.Core[i] = (float)(def.Base[i] * s * (def.IsSignature(i) ? d : 1.0));
        return op;
    }

    private static float TeamOvr(List<OpponentPlayer> players)
        => players.Count == 0 ? 1f : (float)players.Average(p => p.Core.Average());

    private static void ApplyScale(List<OpponentPlayer> players, float scale)
    {
        if (scale <= 0.0001f) scale = 0.0001f;
        foreach (var p in players)
            for (int i = 0; i < Attr.COUNT; i++)
                p.Core[i] = Math.Max(1f, MathF.Round(p.Core[i] * scale, MidpointRounding.AwayFromZero));
    }

    private static float ComputeAura(List<OpponentPlayer> coaches)
    {
        var c = ConfigDb.I.C;
        float total = 0;
        for (int i = 0; i < coaches.Count; i++)
        {
            int ovr = coaches[i].Ovr();
            total += MathF.Round(ovr * (i == 0 ? (float)c.AuraHcRate : (float)c.AuraAcRate));
        }
        return total;
    }

    private static string TeamName(GameRandom rng, int dungeonN = 0)
    {
        var t = ConfigDb.I.Teams;
        if (dungeonN > 0 && dungeonN <= t.Dungeons.Count)
        {
            // 副本队伍：战役名 + 后缀（如「长坂曹军铁骑队」由 地名+后缀池拼装）
        }
        string place = rng.Pick(t.Places);
        string suffix = rng.Pick(t.Suffixes);
        return place + suffix;
    }

    /// <summary>展示用：副本名。</summary>
    public static string DungeonName(int n)
    {
        var list = ConfigDb.I.Teams.Dungeons;
        var d = list.Find(x => x.N == n);
        return d != null ? d.Name : "第" + n + "关";
    }
}

/// <summary>挑战模式系统（SYS-07，02 文档 §10）。</summary>
public static class DungeonSystem
{
    public static int MaxDungeon => ConfigDb.I.C.Ext.DungeonMax;
    public static int TeamsPerDungeon => ConfigDb.I.C.Ext.DungeonTeams;

    /// <summary>进场费：round10(300 × 1.35^(n-1))。</summary>
    public static long Fee(int n)
    {
        var c = ConfigDb.I.C;
        double v = c.DungeonFeeBase * Math.Pow(c.DungeonFeeGrowth, n - 1);
        int step = Math.Max(1, c.Ext.DungeonFeeRoundTo);
        return (long)(Math.Round(v / step) * step);
    }

    /// <summary>对手 OVR 显示（n, m）。</summary>
    public static int OppOvr(int n, int m)
    {
        var c = ConfigDb.I.C;
        return (int)Math.Round(c.DungeonOppBase * Math.Pow(c.DungeonOppGrowth, n - 1) * (1 + c.DungeonTeamStep * (m - 1)));
    }

    public static bool IsUnlocked(int n)
    {
        if (n <= 1) return true;
        return GameState.D.DungeonCleared.Contains(n - 1);
    }

    public static bool IsCleared(int n) => GameState.D.DungeonCleared.Contains(n);

    /// <summary>历史最高击败队伍数（0–10）。</summary>
    public static int BestDefeated(int n)
    {
        GameState.D.DungeonBest.TryGetValue(n.ToString(), out int v);
        return v;
    }

    public static void RecordProgress(int n, int defeated)
    {
        string key = n.ToString();
        GameState.D.DungeonBest.TryGetValue(key, out int cur);
        if (defeated > cur) GameState.D.DungeonBest[key] = defeated;
        SaveSystem.MarkDirty();
    }

    /// <summary>通关奖励（§10.3，一次性发放）。</summary>
    public static List<DrawItem> BuildRewards(int n)
    {
        var rng = new GameRandom(7919 + n * 131);
        var list = new List<DrawItem>();
        void Mat(string q, int count)
        {
            for (int i = 0; i < count; i++)
            {
                list.Add(new DrawItem { Type = "mat", Quality = q, MatTypeId = rng.Pick(ConfigDb.I.C.Ext.MatTypes).Id });
            }
        }
        Mat("green", 6);
        Mat("blue", 4);
        Mat("purple", Math.Min(n + 1, 6));
        if (n >= 5 && n % 5 == 0) Mat("gold", 1);
        list.Add(new DrawItem { Type = "food", FoodKey = "dry" });
        list.Add(new DrawItem { Type = "food", FoodKey = "dry" });
        list.Add(new DrawItem { Type = "food", FoodKey = "dry" });
        list.Add(new DrawItem { Type = "food", FoodKey = "ration" });
        list.Add(new DrawItem { Type = "food", FoodKey = "ration" });
        list.Add(new DrawItem { Type = "food", FoodKey = "forage" });
        int copper = 2 + n / 2;
        for (int i = 0; i < copper; i++) list.Add(new DrawItem { Type = "tali", TaliKey = "copper" });
        if (n % 5 == 0) list.Add(new DrawItem { Type = "tali", TaliKey = "jade" });
        return list;
    }

    /// <summary>通关结算：发奖 + 记录 + 解锁下一副本。已通关过则只记录不重复发奖。</summary>
    public static List<DrawItem> OnClear(int n)
    {
        var granted = new List<DrawItem>();
        if (!IsCleared(n))
        {
            granted = BuildRewards(n);
            GachaSystem.ApplyToInventory(granted);
            GameState.D.DungeonCleared.Add(n);
            SaveSystem.MarkDirty();
            SaveSystem.Save();
        }
        return granted;
    }
}
