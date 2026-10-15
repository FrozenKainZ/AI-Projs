using System.Collections.Generic;
using Godot;

namespace SanguoBasketball.Core;

/// <summary>美术资源加载（统一入口，缺文件时返回 null，由控件画占位）。</summary>
public static class Art
{
    private static readonly Dictionary<string, Texture2D> Cache = new();

    public static Texture2D Load(string path)
    {
        if (Cache.TryGetValue(path, out var t)) return t;
        Texture2D tex = null;
        if (ResourceLoader.Exists(path)) tex = GD.Load<Texture2D>(path);
        Cache[path] = tex;
        return tex;
    }

    public static Texture2D Hero(string defId) => Load($"res://art/heroes/{defId}.png");

    public static Texture2D Bg(string key) => Load($"res://art/bg/{key}.png");

    /// <summary>势力详情背景（武将详情页用）。</summary>
    public static Texture2D FactionBg(string faction) => Bg(faction switch
    {
        "蜀" => "bg_faction_shu",
        "魏" => "bg_faction_wei",
        "吴" => "bg_faction_wu",
        _ => "bg_faction_qun",
    });

    public static Texture2D Icon(string key) => Load($"res://art/icons/{key}.png");

    /// <summary>材料图标：mat_bracer 风格（按材料类型）。</summary>
    public static Texture2D MatIcon(string matTypeId)
        => Load("res://art/icons/" + matTypeId.ToLowerInvariant() + ".png");

    public static Texture2D FoodIcon(string key) => Load("res://art/icons/food_" + key + ".png");

    public static Texture2D TaliIcon(string key) => Load("res://art/icons/tali_" + key + ".png");

    public static Texture2D CoinIcon() => Load("res://art/icons/coin.png");

    /// <summary>抽卡产出条目图标。</summary>
    public static Texture2D DrawIcon(DrawItem item)
    {
        return item.Type switch
        {
            "mat" => MatIcon(item.MatTypeId),
            "food" => FoodIcon(item.FoodKey),
            "tali" => TaliIcon(item.TaliKey),
            "hero" => Hero(item.HeroDefId),
            _ => null
        };
    }
}

public static class U
{
    public static Color Col(string hex) => new Color(hex);
}
