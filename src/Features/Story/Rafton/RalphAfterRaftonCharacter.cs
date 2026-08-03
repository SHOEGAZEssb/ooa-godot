using Godot;

namespace oracleofages;

/// <summary>
/// INTERAC_RALPH $37:$03's native and script-owned animation wrapper.
/// </summary>
internal sealed partial class RalphAfterRaftonCharacter : NpcCharacter
{
    internal void InitializeRalph(
        NpcRecord record,
        RalphAfterRaftonEventRecord source)
    {
        Initialize(record);
        if (record.SpriteName != source.Sprite ||
            record.TileBase != source.TileBase ||
            record.Palette != source.Palette ||
            record.DefaultAnimation != source.DefaultAnimation ||
            record.UpAnimation != source.Animation0 ||
            record.RightAnimation != source.Animation1 ||
            record.DownAnimation != source.Animation2 ||
            record.LeftAnimation != source.Animation3)
        {
            throw new System.InvalidOperationException(
                "INTERAC_RALPH $37:$03 placed metadata diverges from its native event.");
        }

        SetFacingDirection(Vector2I.Right);
        SetScriptAnimation(source.Animation(source.InitialAnimation));
        SetAnimationRate(0.0f);
    }

    internal void SetRalphAnimation(
        int animation,
        RalphAfterRaftonEventRecord source) =>
        SetScriptAnimation(source.Animation(animation));

    internal void AdvanceRalphAnimation(int updates) =>
        AdvanceAnimationUpdates(updates);
}
