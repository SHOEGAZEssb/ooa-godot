using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEyesoarSourceData()
    {
        var bosses = new DungeonBossDatabase();
        for (int subid = 0; subid < 2; subid++)
        {
            var record = bosses.Enemy(0x7b, subid);
            FailIf(record.Sprites is not ["spr_eyesoar"] || record.TileBase != 0 || record.Palette != 1 ||
                record.Health != 20 || record.RadiusX != 6 || record.RadiusY != 6 || record.DamageQuarters != 4,
                "ENEMY_EYESOAR $7b lost source gfx$c4, flags$10 or extra-data$27.");
            var normal = OracleGraphicsCache.GetAnimationDefinition(record.Animations[0]);
            var spinning = OracleGraphicsCache.GetAnimationDefinition(record.Animations[1]);
            FailIf(normal.Frames.Length != 2 || normal.LoopStart != 0 || normal.Frames.Any(frame => frame.Duration != 8) ||
                spinning.Frames.Length != 4 || spinning.LoopStart != 0 || spinning.Frames.Any(frame => frame.Duration != 6),
                "Eyesoar normal and vulnerable animations lost source two/eight and four/six frame timing.");
        }
        for (int subid = 0; subid < 4; subid++)
        {
            var child = bosses.Enemy(0x11, subid);
            FailIf(child.Sprites is not ["spr_eyesoar"] || child.TileBase != 18 || child.Palette != 3 ||
                child.Health != 4 || child.RadiusX != 4 || child.RadiusY != 4 || child.DamageQuarters != 1,
                "ENEMY_EYESOAR_CHILD $11 lost source shared gfx$c4, flags$39 or extra-data$2d.");
        }
        var data = new EyesoarDatabase();
        int[] distances = [0x18, 0x28, 0x30, 0x20, 0x30, 0x18, 0x28, 0x20];
        for (int index = 0; index < 8; index++)
            FailIf(data.FormationDistance(index) != distances[index], "Eyesoar formation-distance source cycle changed.");
        for (int index = 0; index < 4; index++)
            FailIf(data.CenterAngle(index) != new[] { 8, 0, 16, 24 }[index] ||
                data.ChildAngle(index) != index * 8 || data.ChildReadyFlags(index) != 0x11 << index,
                "Eyesoar quadrant steering, child angles or split-nibble ready flags changed.");
        const string parentMask = "11111111111101100000011101111110";
        const string childMask = "11111111111101110000001111111110";
        for (int item = 0; item < 32; item++)
            FailIf(data.CollisionEnabled(0x7b, item) != (parentMask[item] == '1') ||
                data.CollisionEnabled(0x11, item) != (childMask[item] == '1'),
                "Eyesoar body/child collision masks changed, including the body's disabled bomb row.");
        foreach (var (mode, sword, hook, mystery, bomb, beam) in new[] {
            (0x15, 0x0b, 0x0b, 0x20, 0x0b, 0x0b),
            (0x4c, 0x21, 0x2e, 0x20, 0, 0x20),
            (0x6d, 0x1b, 0x2e, 0x20, 0, 0x20) })
            FailIf(data.CollisionEffect(mode, 4) != sword || data.CollisionEffect(mode, 13) != hook ||
                data.CollisionEffect(mode, 26) != mystery || data.CollisionEffect(mode, 24) != bomb ||
                data.CollisionEffect(mode, 25) != beam || data.CollisionEffect(mode, 0x1d) != 0x20,
                "Eyesoar protected/vulnerable/child collision rows changed, including Pegasus absorption without stun or damage.");

        // SPEED_100 cardinal components are $0100; diagonals are $00b5.
        // The native multiply truncates signed high bytes, never rounding.
        Vector2I[] expected = [new(0,-24),new(16,-17),new(24,0),new(16,16),
            new(0,24),new(-17,16),new(-24,0),new(-17,-17)];
        for (int octant = 0; octant < 8; octant++)
            FailIf(OracleObjectMovement.Shared.CircleArcOffset(24, octant * 4) != expected[octant],
                "Eyesoar orbit offset lost signed high-byte multiplication.");
        FailIf(OracleObjectMovement.Shared.CircleArcOffset(255, 8) != new Vector2I(-1, 0),
            "Native circle offset must wrap its scaled high byte.");

        var visual = new DungeonInteractionVisualDatabase().Visual("eyesoar-spawn");
        FailIf(visual.TileBase != 8 || visual.Palette != 4 || visual.Animations.Length != 1 || visual.SourceGrayscaleInverted,
            "INTERAC_0b:$02 must clamp to the final subid row, tile$08/palette4/animation1.");
        var definition = OracleGraphicsCache.GetAnimationDefinition(visual.Animations[0]);
        FailIf(definition.Frames.Length != 3 ||
            !definition.Frames.Select(frame => frame.Duration).SequenceEqual(new[] { 4, 2, 127 }) ||
            !definition.Frames.Select(frame => frame.Parameter).SequenceEqual(new[] { 0, 0, 255 }),
            "Eyesoar spawn signal must retain interactionAnimation5a143's four/two/terminal sequence.");
        var node = new Node2D();
        var animation = new EnemyAnimationPlayer(node, 1);
        animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites), visual.Animations,
            visual.TileBase, visual.Palette, sourceGrayscaleInverted: visual.SourceGrayscaleInverted, positionedOam: true);
        for (int repeat = 0; repeat < 2; repeat++)
        {
            animation.SetAnimation(0);
            for (int tick = 0; tick < 6; tick++)
            {
                FailIf(animation.CurrentParameter != 0, "Eyesoar child became eligible before six spawn-animation updates.");
                ValidateEyesoarSpawnPixels(animation, tick >= 4);
                animation.Advance();
            }
            FailIf(animation.CurrentParameter != 255, "Eyesoar spawn effect failed to publish its terminal signal at update6.");
        }
        node.Free();
    }

    private void ValidateEyesoarSpawnPixels(EnemyAnimationPlayer animation, bool small)
    {
        // interactionAnimation5a143 selects tile $0a then $08 from spr_circlebeads.
        // Its invert:false source pixels map to OBJ4: black outline, blue fill,
        // pale-blue highlight. These coordinates come from the source tiles.
        using var pixels = animation.CurrentTexture.GetImage();
        int top = small ? 1 : 0;
        FailIf(pixels.GetSize() != new Vector2I(8, 16) || animation.CurrentOffset != new Vector2(-4, -8) ||
            pixels.GetPixel(0, 0).A != 0 ||
            !pixels.GetPixel(3, top).IsEqualApprox(Colors.Black) ||
            !pixels.GetPixel(3, top + 1).IsEqualApprox(new Color(0, 0, 1)) ||
            !pixels.GetPixel(3, top + 2).IsEqualApprox(new Color(115 / 255f, 172 / 255f, 1)),
            $"INTERAC_0b:$02 oval lost source bounds, transparency or OBJ4 outline/fill/highlight polarity: small={small}, size={pixels.GetSize()}, offset={animation.CurrentOffset}, pixels={pixels.GetPixel(0, 0)}/{pixels.GetPixel(3, top)}/{pixels.GetPixel(3, top + 1)}/{pixels.GetPixel(3, top + 2)}.");
    }
}
