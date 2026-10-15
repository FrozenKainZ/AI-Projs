using System;
using Godot;
using SanguoBasketball.Core;

namespace SanguoBasketball.Systems;

/// <summary>
/// 广告抽象层（01 文档 §7.2，MUST）。
/// 业务代码只允许调用本抽象层；核心逻辑禁止直接调用平台 SDK。
/// </summary>
public interface IAdService
{
    /// <summary>激励视频是否就绪</summary>
    bool IsRewardedReady(string placementId);

    /// <summary>播放激励视频；onDone(true)=完整观看，可发奖</summary>
    void ShowRewarded(string placementId, Action<bool> onDone);

    /// <summary>插屏（可选，默认不用）</summary>
    void ShowInterstitial(Action onClosed);
}

/// <summary>广告点位（01 文档 §7.3）。</summary>
public static class AdPlacement
{
    public const string Revive = "ADP_REVIVE";   // 普通模式输球弹窗，每局 1 次
    public const string Altar = "ADP_ALTAR";     // 七星坛颁奖，每天 7 次
}

/// <summary>
/// 平台 SDK 实现的占位（虎扑 / 微信小游戏接入点）。
/// TODO(接入)：在此实现对应平台 SDK 的激励视频调用，并在 Ads.Detect() 中启用。
/// </summary>
public sealed class SdkAdService : IAdService
{
    public bool IsRewardedReady(string placementId)
    {
        // TODO: 调用平台 SDK 查询激励视频加载状态
        return false;
    }

    public void ShowRewarded(string placementId, Action<bool> onDone)
    {
        // TODO: 调用平台 SDK 播放激励视频，回调中 onDone(true/false)
        onDone?.Invoke(false);
    }

    public void ShowInterstitial(Action onClosed)
    {
        // TODO: 插屏（默认不用）
        onClosed?.Invoke();
    }
}

/// <summary>
/// Mock 广告服务：开发期使用。用 UI 层注册的播放器模拟"观看广告"。
/// 未注册播放器时直接视为完整观看（便于自测/截图）。
/// </summary>
public sealed class MockAdService : IAdService
{
    /// <summary>UI 注册的模拟广告播放器：参数 = (点位ID, 完成回调)。</summary>
    public static Action<string, Action<bool>> Player;

    public bool IsRewardedReady(string placementId) => true;

    public void ShowRewarded(string placementId, Action<bool> onDone)
    {
        if (Player != null)
            Player(placementId, onDone);
        else
            onDone?.Invoke(true);
    }

    public void ShowInterstitial(Action onClosed) => onClosed?.Invoke();
}

/// <summary>广告入口（业务侧唯一调用点）+ 频控。</summary>
public static class Ads
{
    public static IAdService Service = new MockAdService();

    /// <summary>检测平台 SDK（TODO：发布到虎扑/微信时切换为 SdkAdService）。</summary>
    public static void Detect()
    {
        // 目前无广告 SDK：使用 Mock，保证玩法闭环可测。
        // if (Platform.HasSdk) Service = new SdkAdService();
        Service = new MockAdService();
    }

    /// <summary>复活广告（每局 1 次，频控由局状态承载）。</summary>
    public static void ShowRevive(Action<bool> onDone)
    {
        if (GameState.Run.ReviveUsed)
        {
            onDone?.Invoke(false);
            return;
        }
        Service.ShowRewarded(AdPlacement.Revive, ok =>
        {
            if (ok) GameState.Run.ReviveUsed = true;
            onDone?.Invoke(ok);
        });
    }

    /// <summary>七星坛广告（每天 7 次，次数在 AltarSystem 记账）。</summary>
    public static void ShowAltar(Action<bool> onDone)
    {
        Service.ShowRewarded(AdPlacement.Altar, ok => onDone?.Invoke(ok));
    }
}
