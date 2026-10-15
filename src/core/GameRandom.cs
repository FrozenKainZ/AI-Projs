using System;
using System.Collections.Generic;

namespace SanguoBasketball.Core;

/// <summary>
/// 统一随机数封装（README §3.4：随机数统一走封装、支持种子复现）。
/// 采用 xorshift128 变体，跨运行/跨版本结果稳定，避免 .NET Random 的实现差异。
/// </summary>
public sealed class GameRandom
{
    private ulong _s0, _s1;

    public int Seed { get; }

    public GameRandom(int seed)
    {
        Seed = seed;
        ulong s = (ulong)(uint)seed * 2654435761UL + 0x9E3779B97F4A7C15UL;
        _s0 = s | 1UL;
        _s1 = (s ^ 0xBF58476D1CE4E5B9UL) | 1UL;
        for (int i = 0; i < 8; i++) NextU64(); // 预热
    }

    public static int NewSeed() => (int)(DateTime.Now.Ticks & 0x7FFFFFFF) ^ Environment.TickCount;

    private ulong NextU64()
    {
        ulong x = _s0;
        ulong y = _s1;
        _s0 = y;
        x ^= x << 23;
        _s1 = x ^ y ^ (x >> 17) ^ (y >> 26);
        return _s1 + y;
    }

    /// <summary>[0,1)</summary>
    public double Next() => (NextU64() >> 11) * (1.0 / 9007199254740992.0);

    public double Range(double min, double max) => min + (max - min) * Next();

    /// <summary>[minInclusive, maxExclusive)</summary>
    public int RangeInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return minInclusive + (int)(NextU64() % (ulong)(maxExclusive - minInclusive));
    }

    public T Pick<T>(IList<T> list) => list[RangeInt(0, list.Count)];

    public bool Chance(double p) => Next() < p;

    /// <summary>按权重数组返回下标；权重全为 0 时返回 0。</summary>
    public int WeightedIndex(IReadOnlyList<double> weights)
    {
        double total = 0;
        for (int i = 0; i < weights.Count; i++) total += Math.Max(0, weights[i]);
        if (total <= 0) return 0;
        double r = Next() * total;
        double acc = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            acc += Math.Max(0, weights[i]);
            if (r < acc) return i;
        }
        return weights.Count - 1;
    }

    public T PickWeighted<T>(IReadOnlyList<T> items, Func<T, double> weight)
    {
        var w = new double[items.Count];
        for (int i = 0; i < items.Count; i++) w[i] = weight(items[i]);
        return items[WeightedIndex(w)];
    }

    /// <summary>按 key 权重抽取（供配置内字典权重使用）。</summary>
    public string PickWeightedKey(IReadOnlyDictionary<string, double> dist)
    {
        var keys = new List<string>(dist.Keys);
        var w = new List<double>();
        foreach (var k in keys) w.Add(dist[k]);
        return keys[WeightedIndex(w)];
    }

    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = RangeInt(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
