using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace SanguoBasketball.Core;

/// <summary>9 项属性索引与显示名（02 文档 §1.1）。</summary>
public static class Attr
{
    public const int P3 = 0, MID = 1, RIM = 2, REB = 3, PAS = 4, STL = 5, BLK = 6, PHY = 7, SPD = 8, COUNT = 9;

    public static readonly string[] Keys =
        { "three_point", "mid_range", "rim_finish", "rebound", "pass", "steal", "block", "physical", "speed" };
    public static readonly string[] Shorts = { "3PT", "MID", "RIM", "REB", "PAS", "STL", "BLK", "PHY", "SPD" };
    public static readonly string[] Cns = { "三分", "中投", "攻框", "篮板", "传球", "抢断", "盖帽", "对抗", "速度" };

    public static int KeyToIndex(string key)
    {
        int i = Array.IndexOf(Keys, key);
        return i < 0 ? 0 : i;
    }
}

/// <summary>品质：绿/蓝/紫/金（02 文档 §4.2 边框色）。</summary>
public static class Quality
{
    public static readonly string[] Order = { "green", "blue", "purple", "gold" };

    public static string Cn(string q) => q switch
    {
        "green" => "绿",
        "blue" => "蓝",
        "purple" => "紫",
        "gold" => "金",
        _ => "?"
    };

    public static int Tier(string q) => Math.Max(0, Array.IndexOf(Order, q));

    public static Color Color(string q) => q switch
    {
        "green" => new Color("#4CAF6A"),
        "blue" => new Color("#3E86C6"),
        "purple" => new Color("#8A5BC6"),
        "gold" => new Color("#E0A32E"),
        _ => new Color("#8B8B85")
    };
}

/// <summary>全局配色板（01 文档 §8.2）。</summary>
public static class Palette
{
    public static readonly Color Paper = new("#F5F0E6");      // 宣纸白
    public static readonly Color Ink = new("#1A1A1A");        // 墨黑
    public static readonly Color InkSoft = new("#3A3A3A");
    public static readonly Color Gray = new("#8B8B85");       // 淡墨灰
    public static readonly Color Cinnabar = new("#C8402A");   // 朱砂红
    public static readonly Color StoneBlue = new("#2E6E8E");  // 石青
    public static readonly Color Gamboge = new("#D9A22C");    // 藤黄
    public static readonly Color Bamboo = new("#4A7C59");     // 竹青
    public static readonly Color CardBg = new("#FFFDF7");
    public static readonly Color DarkBoard = new("#2A2F3A");  // 比分牌底
    public static readonly Color StaminaGreen = new("#4A7C59");
    public static readonly Color StaminaYellow = new("#D9A22C");
    public static readonly Color StaminaRed = new("#C8402A");
}

/// <summary>11 槽位定义（02 文档 §2.1）。</summary>
public static class Slots
{
    public static readonly string[] Field = { "PG", "SG", "SF", "PF", "C" };
    public static readonly string[] Coach = { "HC", "AC1", "AC2" };
    public static readonly string[] Bench = { "B1", "B2", "B3" };
    public static readonly string[] All = { "PG", "SG", "SF", "PF", "C", "HC", "AC1", "AC2", "B1", "B2", "B3" };

    public static bool IsField(string slot) => Array.IndexOf(Field, slot) >= 0;
    public static bool IsCoach(string slot) => Array.IndexOf(Coach, slot) >= 0;
    public static bool IsBench(string slot) => Array.IndexOf(Bench, slot) >= 0;

    public static string Cn(string slot) => slot switch
    {
        "PG" => "控卫", "SG" => "分卫", "SF" => "小前", "PF" => "大前", "C" => "中锋",
        "HC" => "主教练", "AC1" => "助教一", "AC2" => "助教二",
        "B1" => "替补一", "B2" => "替补二", "B3" => "替补三",
        _ => slot
    };
}

/// <summary>武将静态定义（由 data/heroes.json 生成）。</summary>
public sealed class HeroDef
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("faction")] public string Faction { get; set; }
    [JsonPropertyName("quality")] public string QualityKey { get; set; }
    [JsonPropertyName("pref")] public string Pref { get; set; }
    /// <summary>次要位置偏好（可选；同样享受 ×1.15）</summary>
    [JsonPropertyName("pref2")] public string Pref2 { get; set; }
    [JsonPropertyName("designOvr")] public int DesignOvr { get; set; }
    [JsonPropertyName("signature")] public string[] Signature { get; set; } = Array.Empty<string>();
    [JsonPropertyName("base")] public Dictionary<string, int> BaseMap { get; set; } = new();
    /// <summary>独立资源：智商（影响教练光环倍率；升星加值，不参与乘区）</summary>
    [JsonPropertyName("iq")] public int Iq { get; set; } = 50;

    [JsonIgnore] public int[] Base { get; private set; } = new int[Attr.COUNT];

    /// <summary>反序列化后展平 base 属性。</summary>
    public void Flatten()
    {
        for (int i = 0; i < Attr.COUNT; i++)
        {
            Base[i] = BaseMap.TryGetValue(Attr.Keys[i], out int v) ? v : 1;
        }
    }

    public bool IsSignature(int attrIdx)
    {
        var key = Attr.Keys[attrIdx];
        for (int i = 0; i < Signature.Length; i++)
            if (Signature[i] == key) return true;
        return false;
    }

    /// <summary>势力·名（列表展示用）。</summary>
    [JsonIgnore] public string FullName => Faction + "·" + Name;

    /// <summary>位置偏好文本（支持双位置，如 "C/PF"）。</summary>
    [JsonIgnore] public string PrefText => string.IsNullOrEmpty(Pref2) ? Pref : Pref + "/" + Pref2;

    public bool PrefMatches(string slot) => Pref == slot || Pref2 == slot;
}

public sealed class MatQ
{
    [JsonPropertyName("main")] public int Main { get; set; }
    [JsonPropertyName("sub")] public int Sub { get; set; }
}

public sealed class MatTypeDef
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("main")] public string Main { get; set; }
    [JsonPropertyName("sub")] public string Sub { get; set; }
    [JsonIgnore] public int MainIdx => Attr.KeyToIndex(Main);
    [JsonIgnore] public int SubIdx => Attr.KeyToIndex(Sub);
}

public sealed class FoodDefDef
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("key")] public string Key { get; set; }
    [JsonPropertyName("effect")] public int Effect { get; set; }
    [JsonPropertyName("target")] public string Target { get; set; } // single / team
}

/// <summary>独立资源材料（智商 / 体力上限）。</summary>
public sealed class SpecialMatDef
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("stat")] public string Stat { get; set; }   // iq / stamina_cap
    [JsonPropertyName("sub")] public string Sub { get; set; }     // 惩罚属性
    [JsonPropertyName("q")] public Dictionary<string, MatQ> Q { get; set; } = new();
    [JsonIgnore] public int SubIdx => Attr.KeyToIndex(Sub);
    [JsonIgnore] public string StatCn => Stat == "iq" ? "智商" : "体力上限";
}

public sealed class RankTitleDef
{
    [JsonPropertyName("min")] public long Min { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
}

public sealed class ExtCfg
{
    [JsonPropertyName("REVIVE_PER_RUN")] public int RevivePerRun { get; set; } = 1;
    [JsonPropertyName("FOOD_EFFECT")] public Dictionary<string, int> FoodEffect { get; set; } = new();
    [JsonPropertyName("DUNGEON_MAX")] public int DungeonMax { get; set; } = 20;
    [JsonPropertyName("DUNGEON_TEAMS")] public int DungeonTeams { get; set; } = 10;
    [JsonPropertyName("DUNGEON_FEE_ROUND_TO")] public int DungeonFeeRoundTo { get; set; } = 10;
    [JsonPropertyName("QUARTER_NAMES")] public string[] QuarterNames { get; set; } = { "首节", "次节", "第三节", "末节" };
    [JsonPropertyName("MARKET_TEN_DISCOUNT")] public double MarketTenDiscount { get; set; } = 0.909;
    [JsonPropertyName("FTUE_COINS")] public int FtueCoins { get; set; } = 500;
    [JsonPropertyName("FTUE_COPPER")] public int FtueCopper { get; set; } = 5;
    [JsonPropertyName("FTUE_DRY")] public int FtueDry { get; set; } = 3;
    [JsonPropertyName("FTUE_TEACHER_OVR")] public int FtueTeacherOvr { get; set; } = 45;
    [JsonPropertyName("MAT_TYPES")] public List<MatTypeDef> MatTypes { get; set; } = new();
    [JsonPropertyName("SPECIAL_MATS")] public List<SpecialMatDef> SpecialMats { get; set; } = new();
    [JsonPropertyName("IQ_STAR_STEP")] public int IqStarStep { get; set; } = 8;
    [JsonPropertyName("STACAP_BASE")] public int StaCapBase { get; set; } = 100;
    [JsonPropertyName("STACAP_STAR_STEP")] public int StaCapStarStep { get; set; } = 5;
    [JsonPropertyName("AURA_IQ_BASE")] public double AuraIqBase { get; set; } = 0.5;
    [JsonPropertyName("AURA_IQ_DIV")] public double AuraIqDiv { get; set; } = 200;
    [JsonPropertyName("DEFAULT_SLOTS")] public List<string> DefaultSlots { get; set; } = new() { "PG", "SG", "SF", "PF", "C", "HC" };
    [JsonPropertyName("SLOT_UNLOCK_COST")] public Dictionary<string, long> SlotUnlockCost { get; set; } = new();
    [JsonPropertyName("RANK_TITLES")] public List<RankTitleDef> RankTitles { get; set; } = new();
    [JsonPropertyName("FOOD_DEFS")] public List<FoodDefDef> FoodDefs { get; set; } = new();
}

/// <summary>常量总表（02 文档 §14，字段名与文档一致）。</summary>
public sealed class Constants
{
    [JsonPropertyName("STAR_MULT_S")] public double[] StarMultS { get; set; } = { 1.0, 1.3, 1.65, 2.05, 2.5, 3.0 };
    [JsonPropertyName("STAR_MULT_D")] public double[] StarMultD { get; set; } = { 1.0, 1.08, 1.17, 1.27, 1.38, 1.5 };
    [JsonPropertyName("EVOLVE_MAX_STAR")] public int EvolveMaxStar { get; set; } = 6;
    [JsonPropertyName("EVOLVE_SACRIFICE")] public int EvolveSacrifice { get; set; } = 1;

    [JsonPropertyName("STAMINA_MAX")] public int StaminaMax { get; set; } = 100;
    [JsonPropertyName("STAMINA_COST_PER_MATCH")] public int StaminaCostPerMatch { get; set; } = 12;
    [JsonPropertyName("STAMINA_FACTOR_MIN")] public double StaminaFactorMin { get; set; } = 0.5;
    [JsonPropertyName("STAMINA_FACTOR_MAX")] public double StaminaFactorMax { get; set; } = 1.0;

    [JsonPropertyName("POS_BONUS_MATCH")] public double PosBonusMatch { get; set; } = 1.15;
    [JsonPropertyName("POS_BONUS_OTHER")] public double PosBonusOther { get; set; } = 1.0;
    [JsonPropertyName("AURA_HC_RATE")] public double AuraHcRate { get; set; } = 0.12;
    [JsonPropertyName("AURA_AC_RATE")] public double AuraAcRate { get; set; } = 0.06;

    [JsonPropertyName("MAT_QUALITY")] public Dictionary<string, MatQ> MatQuality { get; set; } = new();

    [JsonPropertyName("MARKET_PRICE_1")] public int MarketPrice1 { get; set; } = 88;
    [JsonPropertyName("MARKET_PRICE_10")] public int MarketPrice10 { get; set; } = 800;
    [JsonPropertyName("MARKET_WEIGHT")] public Dictionary<string, double> MarketWeight { get; set; } = new();
    [JsonPropertyName("MARKET_MAT_DIST")] public Dictionary<string, double> MarketMatDist { get; set; } = new();
    [JsonPropertyName("MARKET_FOOD_DIST")] public Dictionary<string, double> MarketFoodDist { get; set; } = new();
    [JsonPropertyName("MARKET_TALI_DIST")] public Dictionary<string, double> MarketTaliDist { get; set; } = new();

    [JsonPropertyName("BARRACK_STAR_DIST")] public Dictionary<string, double> BarrackStarDist { get; set; } = new();
    [JsonPropertyName("BARRACK_Q1_DIST")] public Dictionary<string, double> BarrackQ1Dist { get; set; } = new();
    [JsonPropertyName("BARRACK_Q2_DIST")] public Dictionary<string, double> BarrackQ2Dist { get; set; } = new();

    [JsonPropertyName("WIN_COIN_BASE")] public int WinCoinBase { get; set; } = 80;
    [JsonPropertyName("WIN_COIN_GROWTH")] public int WinCoinGrowth { get; set; } = 22;
    [JsonPropertyName("COIN_DUNGEON_BONUS")] public double CoinDungeonBonus { get; set; } = 0.3;
    [JsonPropertyName("RECYCLE_HERO")] public Dictionary<string, int> RecycleHero { get; set; } = new();
    [JsonPropertyName("RECYCLE_MAT")] public Dictionary<string, int> RecycleMat { get; set; } = new();
    [JsonPropertyName("RECYCLE_FOOD")] public Dictionary<string, int> RecycleFood { get; set; } = new();
    [JsonPropertyName("RECYCLE_TALI")] public Dictionary<string, int> RecycleTali { get; set; } = new();

    [JsonPropertyName("POSSESSIONS_BASE")] public int PossessionsBase { get; set; } = 160;
    [JsonPropertyName("POSSESSION_REB_CAP")] public int PossessionRebCap { get; set; } = 30;
    [JsonPropertyName("HIT_BASE")] public double HitBase { get; set; } = 0.55;
    [JsonPropertyName("HIT_LOG_COEF")] public double HitLogCoef { get; set; } = 0.15;
    [JsonPropertyName("HIT_MIN")] public double HitMin { get; set; } = 0.10;
    [JsonPropertyName("HIT_MAX")] public double HitMax { get; set; } = 0.88;
    [JsonPropertyName("VAL_BASE")] public double ValBase { get; set; } = 2.4;
    [JsonPropertyName("VAL_EXP")] public double ValExp { get; set; } = 1.35;
    [JsonPropertyName("VAL_MIN")] public double ValMin { get; set; } = 0.5;
    [JsonPropertyName("VAL_MAX")] public double ValMax { get; set; } = 28;
    [JsonPropertyName("K_MIN")] public double KMin { get; set; } = 0.05;
    [JsonPropertyName("K_MAX")] public double KMax { get; set; } = 20;
    [JsonPropertyName("PASS_BONUS_CAP")] public double PassBonusCap { get; set; } = 0.08;
    [JsonPropertyName("FOUL_CHANCE")] public double FoulChance { get; set; } = 0.12;
    [JsonPropertyName("FOUL_SIDE_DEF")] public double FoulSideDef { get; set; } = 0.70;
    [JsonPropertyName("FOUL_OUT_COUNT")] public int FoulOutCount { get; set; } = 6;
    [JsonPropertyName("FOUL_TROUBLE_MULT")] public double FoulTroubleMult { get; set; } = 0.60;

    [JsonPropertyName("NORMAL_OPP_BASE")] public int NormalOppBase { get; set; } = 55;
    [JsonPropertyName("NORMAL_OPP_GROWTH")] public double NormalOppGrowth { get; set; } = 1.06;

    [JsonPropertyName("DUNGEON_FEE_BASE")] public int DungeonFeeBase { get; set; } = 300;
    [JsonPropertyName("DUNGEON_FEE_GROWTH")] public double DungeonFeeGrowth { get; set; } = 1.35;
    [JsonPropertyName("DUNGEON_OPP_BASE")] public int DungeonOppBase { get; set; } = 120;
    [JsonPropertyName("DUNGEON_OPP_GROWTH")] public double DungeonOppGrowth { get; set; } = 1.11;
    [JsonPropertyName("DUNGEON_TEAM_STEP")] public double DungeonTeamStep { get; set; } = 0.05;

    [JsonPropertyName("ALTAR_DAILY_LIMIT")] public int AltarDailyLimit { get; set; } = 7;
    [JsonPropertyName("ALTAR_COIN_RATE")] public double AltarCoinRate { get; set; } = 0.65;
    [JsonPropertyName("ALTAR_COIN_TABLE")] public int[] AltarCoinTable { get; set; } = { 200, 300, 400, 500, 600 };
    [JsonPropertyName("ALTAR_TALI_DIST")] public Dictionary<string, double> AltarTaliDist { get; set; } = new();

    [JsonPropertyName("EXT")] public ExtCfg Ext { get; set; } = new();
}

public sealed class DungeonDef
{
    [JsonPropertyName("n")] public int N { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
}

public sealed class TeamCfg
{
    [JsonPropertyName("places")] public string[] Places { get; set; } = Array.Empty<string>();
    [JsonPropertyName("suffixes")] public string[] Suffixes { get; set; } = Array.Empty<string>();
    [JsonPropertyName("dungeons")] public List<DungeonDef> Dungeons { get; set; } = new();
}

/// <summary>动效 token（04 文档 §2，data/motion.json）。数值统一从配置读取。</summary>
public sealed class MotionCfg
{
    public Dictionary<string, JsonElement> Root { get; private set; } = new();

    public static MotionCfg FromJson(string json)
    {
        var m = new MotionCfg();
        using var doc = JsonDocument.Parse(json);
        foreach (var p in doc.RootElement.EnumerateObject())
            m.Root[p.Name] = p.Value.Clone();
        return m;
    }

    public float F(string key, float fallback)
    {
        if (Root.TryGetValue(key, out var e) && e.ValueKind == JsonValueKind.Number) return e.GetSingle();
        return fallback;
    }

    public float Nested(string path, float fallback)
    {
        var parts = path.Split('.');
        if (parts.Length != 2) return fallback;
        if (Root.TryGetValue(parts[0], out var e) && e.ValueKind == JsonValueKind.Object &&
            e.TryGetProperty(parts[1], out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetSingle();
        return fallback;
    }
}

/// <summary>全局配置库：从 res://data/*.json 加载（数据驱动，禁止硬编码数值）。</summary>
public sealed class ConfigDb
{
    public static ConfigDb I { get; private set; } = new();

    public Constants C { get; private set; } = new();
    public List<HeroDef> HeroList { get; private set; } = new();
    public Dictionary<string, HeroDef> Heroes { get; private set; } = new();
    public Dictionary<string, string[]> Commentary { get; private set; } = new();
    public TeamCfg Teams { get; private set; } = new();
    public MotionCfg Motion { get; private set; } = MotionCfg.FromJson("{}");

    private readonly Dictionary<string, List<HeroDef>> _byQuality = new();

    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static string ReadRes(string path)
    {
        using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (f == null) throw new Exception("配置缺失: " + path);
        return f.GetAsText();
    }

    public static void Load()
    {
        var db = new ConfigDb();
        db.HeroList = JsonSerializer.Deserialize<List<HeroDef>>(ReadRes("res://data/heroes.json"), Opts);
        foreach (var h in db.HeroList)
        {
            h.Flatten();
            db.Heroes[h.Id] = h;
        }
        foreach (var q in Quality.Order)
            db._byQuality[q] = db.HeroList.FindAll(h => h.QualityKey == q);

        db.C = JsonSerializer.Deserialize<Constants>(ReadRes("res://data/constants.json"), Opts);
        db.Commentary = new Dictionary<string, string[]>(
            JsonSerializer.Deserialize<Dictionary<string, string[]>>(ReadRes("res://data/commentary.json"), Opts) ?? new());
        db.Teams = JsonSerializer.Deserialize<TeamCfg>(ReadRes("res://data/teams.json"), Opts);
        db.Motion = MotionCfg.FromJson(ReadRes("res://data/motion.json"));

        I = db;
        GD.Print($"[ConfigDb] heroes={db.HeroList.Count} commentary={db.Commentary.Count} dungeons={db.Teams.Dungeons.Count}");
    }

    public HeroDef Hero(string id) => Heroes.TryGetValue(id, out var h) ? h : null;

    public List<HeroDef> ByQuality(string q) => _byQuality.TryGetValue(q, out var l) ? l : new List<HeroDef>();

    public string[] Lines(string category)
        => Commentary.TryGetValue(category, out var l) && l.Length > 0 ? l : new[] { "{A}得分！" };

    public MatTypeDef MatType(string id) => C.Ext.MatTypes.Find(m => m.Id == id);

    public SpecialMatDef SpecialMat(string id) => C.Ext.SpecialMats.Find(m => m.Id == id);

    public FoodDefDef FoodDef(string key) => C.Ext.FoodDefs.Find(f => f.Key == key);

    /// <summary>头衔（按历史最强球队综评）。</summary>
    public string RankTitle(long bestTeamOvr)
    {
        string name = C.Ext.RankTitles.Count > 0 ? C.Ext.RankTitles[0].Name : "主公";
        foreach (var r in C.Ext.RankTitles)
            if (bestTeamOvr >= r.Min) name = r.Name;
        return name;
    }

    /// <summary>星系数 S(L)，L 为 1–6。</summary>
    public double StarS(int star) => C.StarMultS[Math.Clamp(star, 1, 6) - 1];

    /// <summary>得意属性额外系数 D(L)。</summary>
    public double StarD(int star) => C.StarMultD[Math.Clamp(star, 1, 6) - 1];
}
