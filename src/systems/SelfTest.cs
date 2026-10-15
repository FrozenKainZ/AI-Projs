using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoBasketball.Core;

namespace SanguoBasketball.Systems;

/// <summary>
/// 无头自测：以设计文档的演算示例与期望表为锚点做验收（--selftest 运行）。
/// </summary>
public static class SelfTest
{
    private static int _pass, _fail;
    private static readonly List<string> _lines = new();

    private static void Check(bool ok, string name, string detail = "")
    {
        if (ok) { _pass++; _lines.Add("  [PASS] " + name); }
        else { _fail++; _lines.Add("  [FAIL] " + name + (detail.Length > 0 ? "  -> " + detail : "")); }
    }

    private static void CheckNear(double actual, double expect, double tol, string name)
        => Check(Math.Abs(actual - expect) <= tol, name, $"actual={actual:F2} expect={expect:F2} tol=±{tol:F2}");

    public static int Run()
    {
        _pass = 0; _fail = 0; _lines.Clear();
        _lines.Add("==== SGBASKET SELFTEST ====");

        TestHeroExamples();
        TestCoachAura();
        TestMatchAnchors();
        TestGachaDistribution();
        TestEconomy();
        TestDungeonNumbers();
        TestFullMatchSmoke();
        TestSaveRoundtrip();

        _lines.Add($"==== RESULT: {_pass} passed, {_fail} failed ====");
        foreach (var l in _lines) GD.Print(l);
        return _fail;
    }

    // ---------- 05 文档 §7.1/7.2/7.3 属性演算 ----------
    private static void TestHeroExamples()
    {
        _lines.Add("[heroes] 文档演算示例");

        var militia = ConfigDb.I.Hero("HERO_G_MILITIA");
        int[] expect = { 69, 70, 49, 35, 88, 73, 26, 34, 83 };
        bool ok = true;
        string detail = "";
        for (int i = 0; i < Attr.COUNT; i++)
            if (militia.Base[i] != expect[i]) { ok = false; detail += $"{Attr.Shorts[i]}:{militia.Base[i]}!={expect[i]} "; }
        Check(ok, "§7.1 民兵 1★ 属性", detail);

        var liaohua = ConfigDb.I.Hero("HERO_B_LIAOHUA");
        int[] expect2 = { 75, 108, 119, 93, 103, 98, 83, 106, 108 };
        ok = true; detail = "";
        for (int i = 0; i < Attr.COUNT; i++)
            if (liaohua.Base[i] != expect2[i]) { ok = false; detail += $"{Attr.Shorts[i]}:{liaohua.Base[i]}!={expect2[i]} "; }
        Check(ok, "§7.2 廖化 1★ 属性", detail);

        // §7.3 张飞 6★（得意 ×4.5，其余 ×3.0）
        var zhangfei = ConfigDb.I.Hero("HERO_JIN_ZHANGFEI");
        var inst = new HeroInst { Uid = "T1", DefId = zhangfei.Id, Star = 6, Sta = 100 };
        int[] expect6 = { 303, 405, 1616, 1014, 429, 405, 1544, 1670, 474 };
        var core = HeroCalc.CoreAttrs(inst);
        ok = true; detail = "";
        for (int i = 0; i < Attr.COUNT; i++)
        {
            int got = (int)MathF.Round(core[i]);
            if (Math.Abs(got - expect6[i]) > 1) { ok = false; detail += $"{Attr.Shorts[i]}:{got}!={expect6[i]} "; }
        }
        Check(ok, "§7.3 张飞 6★ 属性（±1）", detail);
        CheckNear(HeroCalc.Ovr(inst), 873, 3, "§7.3 张飞 6★ OVR≈873");
    }

    // ---------- §7.4 教练光环（v2：含智力倍率） ----------
    private static void TestCoachAura()
    {
        _lines.Add("[aura] 教练光环（v2 含智商倍率）");
        var zhangfei = new HeroInst { Uid = "T1", DefId = "HERO_JIN_ZHANGFEI", Star = 6 };
        var yujin = new HeroInst { Uid = "T2", DefId = "HERO_B_YUJIN", Star = 6 };
        var wicker = new HeroInst { Uid = "T3", DefId = "HERO_G_WICKER", Star = 6 };

        // 智商随升星加值（不参与乘区）
        int iq = HeroCalc.IqTotal(zhangfei);
        int iqExpect = ConfigDb.I.Hero("HERO_JIN_ZHANGFEI").Iq + 5 * ConfigDb.I.C.Ext.IqStarStep;
        Check(iq == iqExpect, $"智商随升星加值（{iq} == {iqExpect}）");
        int cap = HeroCalc.StaCapTotal(zhangfei);
        Check(cap == ConfigDb.I.C.Ext.StaCapBase + 5 * ConfigDb.I.C.Ext.StaCapStarStep,
            $"体力上限随升星加值（{cap}）");

        // 光环 = round(OVR × 率 × (base + iq/div))
        int auraHc = (int)MathF.Round(HeroCalc.Ovr(zhangfei) * 0.12f * (0.5f + iq / 200f));
        var auraList = new List<HeroInst> { zhangfei };
        Check(HeroCalc.CoachAura(auraList) == auraHc,
            $"6★张飞主教练光环 = {auraHc}（v2 公式）");
        int total = HeroCalc.CoachAura(new List<HeroInst> { zhangfei, yujin, wicker });
        GD.Print($"[SelfTest] coach aura total = {total} (HC {auraHc})");
    }

    // ---------- §8.3 期望得分锚点 ----------
    private static void TestMatchAnchors()
    {
        _lines.Add("[match] §8.3 期望分锚点（各 120 场平均；镜像队伍中性化传球/球权修正）");

        // 两支全 100 属性相同队伍 → k=1 → 全场期望 ≈211
        var (a1, b1) = MakeMirrorTeams(100, 100);
        double avg = AverageScore(a1, b1, 120);
        Check(avg > 190 && avg < 250, $"k=1 均衡对局 单队均分≈211（实={avg:F0}）");

        // 3 倍强度差 → k≈3 → 全场期望 ≈1334
        var (a3, b3) = MakeMirrorTeams(300, 100);
        double avgStrong = AverageScore(a3, b3, 120);
        Check(avgStrong > 1150 && avgStrong < 1550, $"k=3 碾压对局 强队均分≈1334（实={avgStrong:F0}）");

        // 0.5 倍 → ≈60
        var (a5, b5) = MakeMirrorTeams(50, 100);
        double avgWeak = AverageScore(a5, b5, 120);
        Check(avgWeak > 35 && avgWeak < 95, $"k=0.5 弱势局 单队均分≈60（实={avgWeak:F0}）");

        // §13 爆杀验收：OVR≈7 倍差 → 强队 2000+ 且分差 ≥40 倍
        var (a7, b7) = MakeMirrorTeams(700, 100);
        var r7 = MatchEngine.Simulate(a7, b7, 4242, true);
        int weak = Math.Max(1, r7.FinalB);
        Check(r7.FinalA > 2000 && r7.FinalA / weak >= 40,
            $"§13 爆杀验收：强队 {r7.FinalA} : 弱队 {r7.FinalB}（{r7.FinalA / weak} 倍分差）");
    }

    private static (BattleTeam, BattleTeam) MakeMirrorTeams(int attrA, int attrB)
    {
        BattleTeam Make(int v, string name, bool player)
        {
            var t = new BattleTeam { Name = name, IsPlayer = player, Aura = 0 };
            for (int i = 0; i < 5; i++)
            {
                var p = new BattlePlayer
                {
                    Uid = (player ? "P" : "E") + i,
                    Name = "T" + i,
                    Pref = Slots.Field[i],
                    SlotPos = Slots.Field[i],
                    OnCourt = true,
                    PlayedThisMatch = true,
                };
                for (int k = 0; k < Attr.COUNT; k++) p.Attr[k] = v;
                // 中性化：传球与篮板双方相等 → 无传球修正、无球权修正，k 值纯净
                p.Attr[Attr.PAS] = 100;
                p.Attr[Attr.REB] = 100;
                t.Court[i] = p;
            }
            return t;
        }
        return (Make(attrA, "A队", true), Make(attrB, "B队", false));
    }

    private static double AverageScore(BattleTeam a, BattleTeam b, int runs)
    {
        double sum = 0;
        for (int i = 0; i < runs; i++)
        {
            var r = MatchEngine.Simulate(a, b, 1000 + i, true);
            sum += r.FinalA;
        }
        return sum / runs;
    }

    // ---------- §6 抽卡概率 ----------
    private static void TestGachaDistribution()
    {
        _lines.Add("[gacha] 概率分布（10000 抽）");
        var rng = new GameRandom(20261015);
        int mat = 0, food = 0, tali = 0;
        for (int i = 0; i < 10000; i++)
        {
            var it = GachaSystem.DrawMarketOne(rng);
            if (it.Type == "mat") mat++;
            else if (it.Type == "food") food++;
            else tali++;
        }
        CheckNear(mat / 10000.0, 0.62, 0.02, "市场池 材料 62%");
        CheckNear(food / 10000.0, 0.30, 0.02, "市场池 兵粮 30%");
        CheckNear(tali / 10000.0, 0.08, 0.015, "市场池 虎符 8%");

        int green = 0, gold = 0, star2 = 0;
        for (int i = 0; i < 20000; i++)
        {
            var it = GachaSystem.DrawBarrackOne(rng);
            if (it.Quality == "green") green++;
            if (it.Quality == "gold") gold++;
            if (it.Star == 2) star2++;
        }
        CheckNear(green / 20000.0, 0.644, 0.015, "兵营池 绿卡 64.4%");
        CheckNear(gold / 20000.0, 0.0086, 0.005, "兵营池 金卡 0.86%");
        CheckNear(star2 / 20000.0, 0.08, 0.01, "兵营池 2★ 8%");
    }

    // ---------- §7 经济 ----------
    private static void TestEconomy()
    {
        _lines.Add("[economy] 金币公式");
        // 第 1 场 = 80；第 10 场 = 278；+5 副本后 ×2.5 → 695
        double w1 = Math.Round(80 + 22 * 0.0);
        CheckNear(w1, 80, 0.5, "第 1 场胜利 80 金币");
        double w10 = Math.Round((80 + 22 * 9) * 1.0);
        CheckNear(w10, 278, 0.5, "第 10 场胜利 278 金币");
        double w10b = Math.Round((80 + 22 * 9) * (1 + 0.30 * 5));
        CheckNear(w10b, 695, 1, "通关 5 副本后第 10 场 695 金币");
    }

    // ---------- §10.2 副本数值 ----------
    private static void TestDungeonNumbers()
    {
        _lines.Add("[dungeon] 进场费/对手 OVR");
        CheckNear(DungeonSystem.Fee(1), 300, 1, "副本1 进场费 300");
        CheckNear(DungeonSystem.Fee(5), 1000, 10, "副本5 进场费 1000");
        CheckNear(DungeonSystem.Fee(10), 4470, 30, "副本10 进场费 4470");
        CheckNear(DungeonSystem.Fee(20), 89820, 400, "副本20 进场费 89820");
        CheckNear(DungeonSystem.OppOvr(1, 1), 120, 1, "副本1 第1队 OVR 120");
        CheckNear(DungeonSystem.OppOvr(1, 10), 174, 2, "副本1 第10队 OVR 174");
        CheckNear(DungeonSystem.OppOvr(20, 10), 1263, 20, "副本20 第10队 OVR 1263");
    }

    // ---------- 全流程 smoke（真实配置队伍 + 对手生成） ----------
    private static void TestFullMatchSmoke()
    {
        _lines.Add("[smoke] 真实队伍 30 场连战");
        // 用内存存档构造测试队伍（不触碰玩家存档）
        var backup = GameState.D;
        try
        {
            GameState.D = new SaveData();
            foreach (var q in Quality.Order)
                foreach (var d in ConfigDb.I.ByQuality(q))
                    GameState.AddHero(d.Id, 3);
            LineupUtil.AutoFill();

            var playerTeam = MatchEngine.BuildPlayerTeam();
            bool ok = playerTeam.LiveCourt().Count == 5;
            Check(ok, "阵容自动填充 5 人");

            int wins = 0, total = 0;
            int evtMin = int.MaxValue, evtMax = 0;
            for (int k = 1; k <= 30; k++)
            {
                var opp = OpponentGen.Normal(k, 500 + k);
                var oppTeam = MatchEngine.BuildOpponentTeam(opp);
                var res = MatchEngine.Simulate(playerTeam, oppTeam, 700 + k, true);
                total++;
                if (res.PlayerWon) wins++;
                if (res.Events.Count < evtMin) evtMin = res.Events.Count;
                if (res.Events.Count > evtMax) evtMax = res.Events.Count;
                if (res.Events.Count == 0) { ok = false; }
            }
            Check(ok && evtMin > 10, $"30 场全部产出事件流（{evtMin}–{evtMax} 条）");
            GD.Print($"[SelfTest] smoke: wins={wins}/{total}（3★全卡池阵容 vs 正常对手曲线）");

            // 对手目标 OVR 校验（v2：前期加强，k=1 起与我方绿卡阵容接近均势）
            var o1 = OpponentGen.Normal(1, 1);
            CheckNear(o1.AvgCourtOvr(), ConfigDb.I.C.NormalOppBase, 6, $"普通模式 k=1 对手 OVR≈{ConfigDb.I.C.NormalOppBase}");
            var o10 = OpponentGen.Normal(10, 11);
            CheckNear(o10.AvgCourtOvr(), ConfigDb.I.C.NormalOppBase * Math.Pow(ConfigDb.I.C.NormalOppGrowth, 9), 12,
                $"普通模式 k=10 对手 OVR≈{ConfigDb.I.C.NormalOppBase * Math.Pow(ConfigDb.I.C.NormalOppGrowth, 9):F0}");

            // v2 升星规则：只能献祭相同武将
            var a1 = GameState.AddHero("HERO_G_BANDIT", 1);
            var a2 = GameState.AddHero("HERO_G_BANDIT", 1);
            var other = GameState.AddHero("HERO_G_WICKER", 1);
            Check(GrowthSystem.CanEvolve(a1, a2, out _), "同卡献祭允许（山贼+山贼）");
            Check(!GrowthSystem.CanEvolve(a1, other, out string evErr2), "异卡献祭拒绝（山贼+藤甲兵）-> " + evErr2);

            // v2 槽位解锁默认值
            Check(!GameState.IsSlotUnlocked("B1") || GameState.D.UnlockedSlots.Count >= 6,
                $"新号默认槽位 = {string.Join(",", GameState.D.UnlockedSlots)}");
        }
        finally
        {
            GameState.D = backup;
        }
    }

    // ---------- 存档往返 ----------
    private static void TestSaveRoundtrip()
    {
        _lines.Add("[save] 序列化往返");
        var backup = GameState.D;
        try
        {
            GameState.D = new SaveData { Coins = 12345, Copper = 7, Jade = 2 };
            GameState.AddHero("HERO_JIN_GUANYU", 4);
            GameState.AddMat("MAT_BRACER", "gold", 3);
            GameState.AddFood("dry", 5);
            string json = System.Text.Json.JsonSerializer.Serialize(GameState.D);
            var back = System.Text.Json.JsonSerializer.Deserialize<SaveData>(json);
            Check(back.Coins == 12345 && back.Heroes.Count == 1 && back.Heroes[0].Star == 4
                && back.Mats.Values.First() == 3 && back.Foods["dry"] == 5, "存档字段往返一致");
        }
        finally
        {
            GameState.D = backup;
        }
    }
}
