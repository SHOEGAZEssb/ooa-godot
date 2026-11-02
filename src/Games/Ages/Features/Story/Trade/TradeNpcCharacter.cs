namespace Oracles;

internal sealed partial class TradeNpcCharacter : NpcCharacter
{
    internal void InitializeTradeNpc(NpcRecord record, TradeNpcEventRecord data)
    {
        AssetRoot = AgesGameContent.AssetRoot;
        Initialize(record, AssetRoot);
        // Both native initializers load animation $00 before their script pose.
        SetScriptAnimation(data.Animation(data.InitialAnimation));
        SetAnimationRate(0.0f);
    }

    internal void AdvanceTradeNpc(Player player)
    {
        AdvanceAnimationUpdates(1);
        PreventPlayerPassing(player);
        UpdateDrawPriority(player.Position);
    }
}
