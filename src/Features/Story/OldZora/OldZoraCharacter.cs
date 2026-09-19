namespace oracleofages;

internal sealed partial class OldZoraCharacter : NpcCharacter
{
    internal void InitializeOldZora(NpcRecord record)
    {
        Initialize(record);
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
