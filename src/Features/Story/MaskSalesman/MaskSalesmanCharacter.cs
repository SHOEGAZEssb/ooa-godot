using Godot;

namespace oracleofages;

/// <summary>
/// INTERAC_MASK_SALESMAN's script-owned animation and collision wrapper.
/// </summary>
internal sealed partial class MaskSalesmanCharacter : NpcCharacter
{
    internal void InitializeMaskSalesman(
        NpcRecord record,
        MaskSalesmanEventRecord maskSalesman)
    {
        Initialize(record);
        // interactionInitGraphics loads INTERAC_MASK_SALESMAN's default
        // animation $00. Its signed Y=-$10 OAM cells require positioned
        // composition instead of the ordinary fixed 32-by-32 NPC canvas.
        SetScriptAnimation(maskSalesman.Animation(maskSalesman.InitialAnimation));
        SetAnimationRate(0.0f);
    }

    internal void AdvanceMaskSalesman(Player player)
    {
        AdvanceAnimationUpdates(1);
        PreventPlayerPassing(player);
        UpdateDrawPriority(player.Position);
    }
}
