namespace Oracles;

internal sealed partial class OldZoraCharacter : NpcCharacter
{
    internal void InitializeOldZora(NpcRecord record)
    {
        AssetRoot = AgesGameContent.AssetRoot;
        Initialize(record, AgesGameContent.AssetRoot);
        SetScriptAnimation(record.UpAnimation);
        SetAnimationRate(0.0f);
    }

    internal void AdvanceOldZora(Player player)
    {
        AdvanceAnimationUpdates(1);
        PreventPlayerPassing(player);
        UpdateDrawPriority(player.Position);
    }
}
