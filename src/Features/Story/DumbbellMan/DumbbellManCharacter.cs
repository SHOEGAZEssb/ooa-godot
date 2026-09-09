using Godot;

namespace oracleofages;

/// <summary>
/// INTERAC_DUMBBELL_MAN's script-owned animation and collision wrapper.
/// </summary>
internal sealed partial class DumbbellManCharacter : NpcCharacter
{
    internal void InitializeDumbbellMan(
        NpcRecord record,
        DumbbellManEventRecord dumbbellMan)
    {
        Initialize(record);
        // Native initialization loads animation $00 before the script selects its pose.
        SetScriptAnimation(dumbbellMan.Animation(dumbbellMan.InitialAnimation));
        SetAnimationRate(0.0f);
    }

    internal void AdvanceDumbbellMan(Player player)
    {
        AdvanceAnimationUpdates(1);
        PreventPlayerPassing(player);
        UpdateDrawPriority(player.Position);
    }
}
