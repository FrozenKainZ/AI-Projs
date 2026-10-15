using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using SanguoBasketball.Systems;

namespace SanguoBasketball.Core;

/// <summary>武将实例（存档单位）。Mat 为 9 项材料加成固定值（02 §4.2）。</summary>
public sealed class HeroInst
{
    [JsonPropertyName("uid")] public string Uid { get; set; }
    [JsonPropertyName("defId")] public string DefId { get; set; }
    [JsonPropertyName("star")] public int Star { get; set; } = 1;
    [JsonPropertyName("mat")] public int[] Mat { get; set; } = new int[Attr.COUNT];
    [JsonPropertyName("sta")] public int Sta { get; set; } = 100;
    /// <summary>智商材料加成（独立资源，不参与乘区）</summary>
    [JsonPropertyName("iqBonus")] public int IqBonus { get; set; }
    /// <summary>体力上限材料加成（独立资源）</summary>
    [JsonPropertyName("staCapBonus")] public int StaCapBonus { get; set; }

    [JsonIgnore] public HeroDef Def => ConfigDb.I.Hero(DefId);

    public HeroInst Clone() => new()
    {
        Uid = Uid, DefId = DefId, Star = Star, Sta = Sta, Mat = (int[])Mat.Clone(),
        IqBonus = IqBonus, StaCapBonus = StaCapBonus,
    };
}

public sealed class FtueData
{
    [JsonPropertyName("step")] public int Step { get; set; } = 0;    // 0=全新
    [JsonPropertyName("introDone")] public bool IntroDone { get; set; } = false;
    [JsonPropertyName("freeTenUsed")] public bool FreeTenUsed { get; set; } = false;
    [JsonPropertyName("lineupTouched")] public bool LineupTouched { get; set; } = false;
    [JsonPropertyName("lineupReward")] public bool LineupReward { get; set; } = false;
}

public sealed class AltarSave
{
    [JsonPropertyName("date")] public string Date { get; set; } = "";
    [JsonPropertyName("used")] public int Used { get; set; } = 0;
}

public sealed class StatsSave
{
    [JsonPropertyName("maxWinStreak")] public int MaxWinStreak { get; set; } = 0;
    [JsonPropertyName("totalWins")] public int TotalWins { get; set; } = 0;
    [JsonPropertyName("totalRuns")] public int TotalRuns { get; set; } = 0;
    [JsonPropertyName("bestMargin")] public int BestMargin { get; set; } = 0;
    [JsonPropertyName("totalMatches")] public int TotalMatches { get; set; } = 0;
    /// <summary>历史最强球队综评（头衔系统依据）</summary>
    [JsonPropertyName("bestTeamOvr")] public long BestTeamOvr { get; set; } = 0;
}

public sealed class SettingsSave
{
    [JsonPropertyName("master")] public float Master { get; set; } = 1f;
    [JsonPropertyName("sfx")] public float Sfx { get; set; } = 1f;
    [JsonPropertyName("bgm")] public float Bgm { get; set; } = 1f;
}

/// <summary>本地存档（user://save.json，01 文档 §6）。字段带 version，缺字段补默认值。</summary>
public sealed class SaveData
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;

    [JsonPropertyName("coins")] public long Coins { get; set; } = 0;
    [JsonPropertyName("copper")] public int Copper { get; set; } = 0;
    [JsonPropertyName("jade")] public int Jade { get; set; } = 0;

    [JsonPropertyName("heroes")] public List<HeroInst> Heroes { get; set; } = new();
    /// <summary>key = "MAT_XXX:quality" -> count</summary>
    [JsonPropertyName("mats")] public Dictionary<string, int> Mats { get; set; } = new();
    /// <summary>key = dry/ration/forage -> count</summary>
    [JsonPropertyName("foods")] public Dictionary<string, int> Foods { get; set; } = new();
    /// <summary>最近抽卡历史（≤20 条文本）</summary>
    [JsonPropertyName("gachaHistory")] public List<string> GachaHistory { get; set; } = new();

    [JsonPropertyName("lineup")] public Dictionary<string, string> Lineup { get; set; } = new();
    [JsonPropertyName("codex")] public List<string> Codex { get; set; } = new();

    [JsonPropertyName("stats")] public StatsSave Stats { get; set; } = new();
    /// <summary>副本 n -> 历史最高击败队伍数 (0–10)</summary>
    [JsonPropertyName("dungeonBest")] public Dictionary<string, int> DungeonBest { get; set; } = new();
    /// <summary>已通关副本 n 列表</summary>
    [JsonPropertyName("dungeonCleared")] public List<int> DungeonCleared { get; set; } = new();

    [JsonPropertyName("altar")] public AltarSave Altar { get; set; } = new();
    [JsonPropertyName("ftue")] public FtueData Ftue { get; set; } = new();
    [JsonPropertyName("settings")] public SettingsSave Settings { get; set; } = new();

    /// <summary>已解锁的阵容槽位（新号仅 5 首发 + 主教练）</summary>
    [JsonPropertyName("unlockedSlots")] public List<string> UnlockedSlots { get; set; } = new();

    [JsonPropertyName("heroSeq")] public int HeroSeq { get; set; } = 0;

    [JsonIgnore] public int DungeonClearedCount => DungeonCleared.Count;
}

/// <summary>普通模式当前局（运行时，不落盘）。</summary>
public sealed class NormalRun
{
    public bool Active;
    public int Streak;          // 已赢场次
    public long CoinsEarned;
    public int BestMargin;
    public bool ReviveUsed;
    public int FireCount;       // 一局内比赛场次（含复活再战）
    public OpponentTeam CurrentOpponent;
    public int CurrentK;        // 当前第 k 场（1-based）
}

/// <summary>挑战模式当前局（运行时，不落盘）。</summary>
public sealed class ChallengeRun
{
    public bool Active;
    public int DungeonN;
    public int Index;           // 下一支要打的队伍（1-based），10 支打完即通关
    public OpponentTeam CurrentOpponent;
    public long Fee;            // 已付进场费（用于总结展示）
}

/// <summary>全局游戏状态：存档 + 运行时局状态。</summary>
public static class GameState
{
    public static SaveData D { get; set; } = new();

    public static string SelectedMode = "normal"; // normal / challenge
    public static NormalRun Run = new();
    public static ChallengeRun Challenge = new();

    // ---------- 资源 ----------
    public static void AddCoins(long v)
    {
        D.Coins = Math.Max(0, D.Coins + v);
        SaveSystem.MarkDirty();
    }

    public static bool TrySpendCoins(long v)
    {
        if (D.Coins < v) return false;
        D.Coins -= v;
        SaveSystem.MarkDirty();
        return true;
    }

    public static void AddTali(string key, int v)
    {
        if (key == "copper") D.Copper = Math.Max(0, D.Copper + v);
        else D.Jade = Math.Max(0, D.Jade + v);
        SaveSystem.MarkDirty();
    }

    public static bool TrySpendTali(string key, int v)
    {
        if (key == "copper")
        {
            if (D.Copper < v) return false;
            D.Copper -= v;
        }
        else
        {
            if (D.Jade < v) return false;
            D.Jade -= v;
        }
        SaveSystem.MarkDirty();
        return true;
    }

    public static void AddMat(string typeId, string quality, int count)
    {
        string k = typeId + ":" + quality;
        D.Mats.TryGetValue(k, out int cur);
        D.Mats[k] = cur + count;
        SaveSystem.MarkDirty();
    }

    public static bool UseMat(string typeId, string quality, int count)
    {
        string k = typeId + ":" + quality;
        D.Mats.TryGetValue(k, out int cur);
        if (cur < count) return false;
        D.Mats[k] = cur - count;
        SaveSystem.MarkDirty();
        return true;
    }

    public static int GetMat(string typeId, string quality)
    {
        D.Mats.TryGetValue(typeId + ":" + quality, out int cur);
        return cur;
    }

    public static void AddFood(string key, int count)
    {
        D.Foods.TryGetValue(key, out int cur);
        D.Foods[key] = Math.Max(0, cur + count);
        SaveSystem.MarkDirty();
    }

    public static bool UseFood(string key, int count)
    {
        D.Foods.TryGetValue(key, out int cur);
        if (cur < count) return false;
        D.Foods[key] = cur - count;
        SaveSystem.MarkDirty();
        return true;
    }

    public static int GetFood(string key)
    {
        D.Foods.TryGetValue(key, out int cur);
        return cur;
    }

    // ---------- 武将 ----------
    public static HeroInst AddHero(string defId, int star = 1)
    {
        D.HeroSeq++;
        var inst = new HeroInst { Uid = "H" + D.HeroSeq, DefId = defId, Star = Math.Clamp(star, 1, 6), Sta = 100 };
        D.Heroes.Add(inst);
        UnlockCodex(defId);
        SaveSystem.MarkDirty();
        return inst;
    }

    public static void UnlockCodex(string defId)
    {
        if (!D.Codex.Contains(defId)) D.Codex.Add(defId);
    }

    public static void RemoveHero(HeroInst inst) => D.Heroes.Remove(inst);

    public static HeroInst HeroByUid(string uid)
        => string.IsNullOrEmpty(uid) ? null : D.Heroes.Find(h => h.Uid == uid);

    public static List<HeroInst> HeroesOfDef(string defId) => D.Heroes.FindAll(h => h.DefId == defId);

    /// <summary>某卡是否被阵容装备（进化/回收硬校验用，02 §3.3）。</summary>
    public static bool IsEquipped(HeroInst inst)
    {
        foreach (var kv in D.Lineup)
            if (kv.Value == inst.Uid) return true;
        return false;
    }

    /// <summary>该 defId 是否只剩这一张（图鉴最后一张保护）。</summary>
    public static bool IsLastOfDef(HeroInst inst) => HeroesOfDef(inst.DefId).Count <= 1;

    public static int EquippedSlot(HeroInst inst)
    {
        int i = 0;
        foreach (var slot in Slots.All)
        {
            D.Lineup.TryGetValue(slot, out var uid);
            if (uid == inst.Uid) return i;
            i++;
        }
        return -1;
    }

    // ---------- 七星坛日期（04:00 跨天） ----------
    public static string GameDay()
        => DateTime.Now.AddHours(-4).ToString("yyyy-MM-dd");

    public static void EnsureAltarDay()
    {
        string today = GameDay();
        if (D.Altar.Date != today)
        {
            D.Altar.Date = today;
            D.Altar.Used = 0;
            SaveSystem.MarkDirty();
        }
    }

    // ---------- 槽位解锁（v2：助教/替补需金币解锁） ----------
    public static bool IsSlotUnlocked(string slot)
        => D.UnlockedSlots.Contains(slot);

    public static long SlotUnlockCost(string slot)
        => ConfigDb.I.C.Ext.SlotUnlockCost.TryGetValue(slot, out long v) ? v : 0;

    public static bool TryUnlockSlot(string slot)
    {
        if (IsSlotUnlocked(slot)) return true;
        long cost = SlotUnlockCost(slot);
        if (!TrySpendCoins(cost)) return false;
        D.UnlockedSlots.Add(slot);
        SaveSystem.MarkDirty();
        SaveSystem.Save();
        return true;
    }

    // ---------- 头衔（历史最强球队综评） ----------
    public static void TouchBestTeamOvr()
    {
        int cur = Systems.HeroCalc.TeamOvr(D.Lineup);
        if (cur > D.Stats.BestTeamOvr)
        {
            D.Stats.BestTeamOvr = cur;
            SaveSystem.MarkDirty();
        }
    }

    public static string RankTitle() => ConfigDb.I.RankTitle(D.Stats.BestTeamOvr);

    /// <summary>结束一局后：全队体力回满（体力消耗只在一局内呈现）</summary>
    public static void RestoreAllStamina()
    {
        foreach (var h in D.Heroes)
            h.Sta = Systems.HeroCalc.StaCapTotal(h);
        SaveSystem.MarkDirty();
    }
}

/// <summary>存档读写（自动保存于抽卡/比赛/消耗后，01 §6）。</summary>
public static class SaveSystem
{
    private const string Path = "user://save.json";
    private static bool _dirty;
    private static double _lastSave;

    /// <summary>全新存档（含默认解锁槽位）。</summary>
    public static SaveData CreateFresh()
    {
        var d = new SaveData();
        d.UnlockedSlots = new List<string>(ConfigDb.I.C.Ext.DefaultSlots);
        return d;
    }

    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static void Load()
    {
        try
        {
            if (Godot.FileAccess.FileExists(Path))
            {
                string json = Godot.FileAccess.GetFileAsString(Path);
                var d = JsonSerializer.Deserialize<SaveData>(json, Opts);
                if (d != null)
                {
                    GameState.D = d;
                    d.Heroes ??= new List<HeroInst>();
                    d.Mats ??= new Dictionary<string, int>();
                    d.Foods ??= new Dictionary<string, int>();
                    d.GachaHistory ??= new List<string>();
                    d.Lineup ??= new Dictionary<string, string>();
                    d.Codex ??= new List<string>();
                    d.DungeonBest ??= new Dictionary<string, int>();
                    d.DungeonCleared ??= new List<int>();
                    d.UnlockedSlots ??= new List<string>(ConfigDb.I.C.Ext.DefaultSlots);
                    foreach (var h in d.Heroes)
                        if (h.Mat == null || h.Mat.Length != Attr.COUNT) h.Mat = new int[Attr.COUNT];
                    if (d.UnlockedSlots == null || d.UnlockedSlots.Count == 0)
                        d.UnlockedSlots = new List<string>(ConfigDb.I.C.Ext.DefaultSlots);
                    GD.Print($"[Save] loaded v{d.Version} heroes={d.Heroes.Count} coins={d.Coins}");
                    return;
                }
            }
        }
        catch (Exception e)
        {
            GD.PushWarning("[Save] load failed, start fresh: " + e.Message);
        }
        GameState.D = CreateFresh();
    }

    public static void MarkDirty() => _dirty = true;

    public static void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(GameState.D, Opts);
            using var f = Godot.FileAccess.Open(Path, Godot.FileAccess.ModeFlags.Write);
            f.StoreString(json);
            _dirty = false;
        }
        catch (Exception e)
        {
            GD.PushError("[Save] write failed: " + e.Message);
        }
    }

    /// <summary>节流自动保存（每 2s 最多一次），在 _Process 中调用。</summary>
    public static void Tick(double now)
    {
        if (!_dirty) return;
        if (now - _lastSave < 2.0) return;
        _lastSave = now;
        Save();
    }

    public static void SaveOnExit()
    {
        if (_dirty) Save();
    }
}
