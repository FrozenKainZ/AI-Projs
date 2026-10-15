using System;
using System.Collections.Generic;
using SanguoBasketball.Core;
using SanguoBasketball.Systems;

namespace SanguoBasketball.Core;

/// <summary>抽卡产出条目（市场池/兵营池通用）。</summary>
public sealed class DrawItem
{
    public string Type;        // mat / food / tali / hero
    public string MatTypeId;   // MAT_XXX
    public string Quality;     // green/blue/purple/gold
    public string FoodKey;     // dry/ration/forage
    public string TaliKey;     // copper/jade
    public string HeroDefId;
    public int Star = 1;

    public string DisplayName => Type switch
    {
        "mat" => ConfigDb.I.MatType(MatTypeId).Name,
        "food" => ConfigDb.I.FoodDef(FoodKey).Name,
        "tali" => TaliKey == "copper" ? "铜虎符" : "玉虎符",
        "hero" => ConfigDb.I.Hero(HeroDefId).Name,
        _ => "?"
    };

    public string SubLabel => Type switch
    {
        "mat" => SanguoBasketball.Core.Quality.Cn(Quality) + "·" + Attr.Cns[ConfigDb.I.MatType(MatTypeId).MainIdx],
        "hero" => SanguoBasketball.Core.Quality.Cn(ConfigDb.I.Hero(HeroDefId).QualityKey) + "·" + Star + "★",
        _ => ""
    };

    /// <summary>稀有度（用于揭示排序/光效层级）。</summary>
    public int Rarity => Type switch
    {
        "mat" => SanguoBasketball.Core.Quality.Tier(Quality),
        "food" => FoodKey switch { "dry" => 0, "ration" => 1, _ => 2 },
        "tali" => TaliKey == "copper" ? 1 : 3,
        "hero" => SanguoBasketball.Core.Quality.Tier(ConfigDb.I.Hero(HeroDefId).QualityKey),
        _ => 0
    };

    public string IconKey => Type switch
    {
        "mat" => MatTypeId,
        "food" => "FOOD_" + FoodKey.ToUpperInvariant(),
        "tali" => "TALI_" + TaliKey.ToUpperInvariant(),
        "hero" => HeroDefId,
        _ => "?"
    };
}

/// <summary>
/// 抽卡系统（SYS-03，02 文档 §6）。
/// 市场池只产材料/兵粮/虎符；兵营池只产武将。
/// </summary>
public static class GachaSystem
{
    /// <summary>市场池单抽（不含扣费，扣费在外层）。</summary>
    public static DrawItem DrawMarketOne(GameRandom rng)
    {
        var c = ConfigDb.I.C;
        string big = rng.PickWeightedKey(c.MarketWeight);
        if (big == "material") big = "mat"; // 归一化大类 key
        var item = new DrawItem { Type = big };
        switch (big)
        {
            case "mat":
                item.Quality = rng.PickWeightedKey(c.MarketMatDist);
                item.MatTypeId = rng.Pick(c.Ext.MatTypes).Id;
                break;
            case "food":
                item.FoodKey = rng.PickWeightedKey(c.MarketFoodDist);
                break;
            case "tali":
                item.TaliKey = rng.PickWeightedKey(c.MarketTaliDist);
                break;
        }
        return item;
    }

    public static List<DrawItem> DrawMarket(GameRandom rng, int count)
    {
        var list = new List<DrawItem>();
        for (int i = 0; i < count; i++) list.Add(DrawMarketOne(rng));
        return list;
    }

    /// <summary>兵营池单抽（§6.2）：92% 1星 / 8% 2星，品质按星级内分布。</summary>
    public static DrawItem DrawBarrackOne(GameRandom rng)
    {
        var c = ConfigDb.I.C;
        string starKey = rng.PickWeightedKey(c.BarrackStarDist);
        int star = starKey == "star2" ? 2 : 1;
        string q = star == 2 ? rng.PickWeightedKey(c.BarrackQ2Dist) : rng.PickWeightedKey(c.BarrackQ1Dist);
        var pool = ConfigDb.I.ByQuality(q);
        var def = rng.Pick(pool);
        return new DrawItem { Type = "hero", HeroDefId = def.Id, Star = star, Quality = q };
    }

    public static List<DrawItem> DrawBarrack(GameRandom rng, int count)
    {
        var list = new List<DrawItem>();
        for (int i = 0; i < count; i++) list.Add(DrawBarrackOne(rng));
        return list;
    }

    /// <summary>入包：把抽卡结果写入背包/武将列表，返回新增的武将实例。</summary>
    public static List<HeroInst> ApplyToInventory(List<DrawItem> items)
    {
        var heroes = new List<HeroInst>();
        foreach (var it in items)
        {
            switch (it.Type)
            {
                case "mat":
                    GameState.AddMat(it.MatTypeId, it.Quality, 1);
                    break;
                case "food":
                    GameState.AddFood(it.FoodKey, 1);
                    break;
                case "tali":
                    GameState.AddTali(it.TaliKey, 1);
                    break;
                case "hero":
                    var inst = GameState.AddHero(it.HeroDefId, it.Star);
                    heroes.Add(inst);
                    break;
            }
        }
        SaveSystem.MarkDirty();
        SaveSystem.Save();
        return heroes;
    }

    /// <summary>抽卡历史（最近 20 条，简化为文本行）。</summary>
    public static void PushHistory(string line)
    {
        var d = GameState.D;
        d.GachaHistory.Insert(0, line);
        while (d.GachaHistory.Count > 20) d.GachaHistory.RemoveAt(d.GachaHistory.Count - 1);
    }
}

/// <summary>七星坛（SYS-10，02 文档 §11）。</summary>
public static class AltarSystem
{
    public static int Remaining()
    {
        GameState.EnsureAltarDay();
        return Math.Max(0, ConfigDb.I.C.AltarDailyLimit - GameState.D.Altar.Used);
    }

    public static bool CanPlay() => Remaining() > 0;

    /// <summary>抽奖：65% 金币档（等权 200–600）；35% 虎符（铜 85% / 玉 15%）。</summary>
    public static DrawItem Roll(GameRandom rng)
    {
        var c = ConfigDb.I.C;
        if (rng.Next() < c.AltarCoinRate)
        {
            int coins = rng.Pick(c.AltarCoinTable);
            return new DrawItem { Type = "coin", TaliKey = "", HeroDefId = null, Quality = "gold", MatTypeId = coins.ToString() };
        }
        string t = rng.PickWeightedKey(c.AltarTaliDist);
        return new DrawItem { Type = "tali", TaliKey = t };
    }

    /// <summary>发放奖励并记账（广告完成后调用）。</summary>
    public static string Grant(DrawItem item)
    {
        GameState.EnsureAltarDay();
        GameState.D.Altar.Used++;
        switch (item.Type)
        {
            case "coin":
                int coins = int.Parse(item.MatTypeId);
                GameState.AddCoins(coins);
                return $"获得 {coins} 金币";
            case "tali":
                GameState.AddTali(item.TaliKey, 1);
                return item.TaliKey == "copper" ? "获得 铜虎符 ×1" : "获得 玉虎符 ×1";
        }
        return "";
    }

    /// <summary>广告失败时返还次数（禁止吞次数）。</summary>
    public static void RefundAttempt()
    {
        GameState.EnsureAltarDay();
        GameState.D.Altar.Used = Math.Max(0, GameState.D.Altar.Used - 1);
        SaveSystem.MarkDirty();
    }
}
