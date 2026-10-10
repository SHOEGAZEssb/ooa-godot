using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateOctogonSpritesRom()
    {
        LoadValidationRoom(5,0x36);
        var actor = _entities.Entities<OctogonCharacter>().Single();
        var native = new ObjectAnimationRom(1,0x7d);
        var checkedTextures = new HashSet<(ulong, string, int)>();
        FailIf(actor.Record.Animations.Length != 22 || actor.Record.TileBase != 0 || actor.Record.Palette != 2,
            "ENEMY$7d surface sprites require 22 animations, tile base$00 and palette$02.");
        int frames = 0;
        for (int animation = 0; animation < 22; animation++)
        {
            native.Set(animation); actor.Animation.SetAnimation(animation);
            var definition = OracleGraphicsCache.GetAnimationDefinition(actor.Record.Animations[animation]);
            for (int frame = 0; frame < definition.Frames.Length; frame++)
            {
                FailIf(definition.Frames[frame].EncodedOam != native.Oam ||
                    definition.Frames[frame].Duration != native.Counter ||
                    definition.Frames[frame].Parameter != native.Parameter,
                    $"ENEMY$7d animation${animation:x2} frame{frame}: imported OAM/clock differs from clean-US execution.");
                ValidateOctogonOamTexture(actor,actor.Animation.CurrentTexture,native.Oam,0,2,checkedTextures);
                ValidateOctogonOamTexture(actor,actor.Animation.DamageTexture,native.Oam,0,5,checkedTextures);
                ValidateOctogonOamTexture(actor,actor.Animation.CurrentTextureForPalette(6),native.Oam,0,6,checkedTextures);
                // enemyOamData4e9b5/$4ea39/$4eabd/$4eb41 are the four
                // cardinal idle poses. Their full 32x32 bounds straddle the
                // old fixed compositor's edges by eight pixels.
                if (frame == 0 && animation is 0 or 4 or 8 or 12)
                {
                    Vector2 expected = animation switch
                    { 0 => new(-16,-8),4 => new(-24,-16),8 => new(-16,-24),_ => new(-8,-16) };
                    FailIf(actor.Animation.CurrentOffset != expected || actor.Animation.CurrentTexture.GetSize() != new Vector2(32,32),
                        $"ENEMY$7d animation${animation:x2}: full cardinal OAM bounds/offset differ from source.");
                }
                // $10/$11/$13/$14/$15 have no terminator in enemyAnimations.s.
                // Their handlers reset/change the pose without enemyAnimate;
                // advancing beyond them would execute the adjacent stream.
                if (animation is not (0x10 or 0x11 or 0x13 or 0x14 or 0x15))
                {
                    int duration = native.Counter == 0 ? 256 : native.Counter;
                    for (int tick = 0; tick < duration; tick++) { native.Advance(); actor.Animation.Advance(); }
                }
                frames++;
            }
            FailIf(actor.Animation.FrameIndex != definition.LoopStart ||
                definition.Frames[definition.LoopStart].EncodedOam != native.Oam,
                $"ENEMY$7d animation${animation:x2}: loop did not return to the native OAM frame.");
        }
        GD.Print($"Validated all 22 Octogon animations/{frames} frames against clean-US OAM/clock execution, full bounds/offsets and surface/underwater/damage pixels.");
    }

    private static string OctogonNativeOam(int pointer)
    {
        ReadOnlyMemory<byte> bytes = ValidationRom.LoadCleanUs();
        int offset = 0x13*0x4000+(pointer&0x3fff);
        int count = bytes.Span[offset++];
        if (count is < 1 or > 16) throw new InvalidOperationException($"ENEMY$7d: invalid native OAM pointer${pointer:x4}.");
        return string.Join(';',Enumerable.Range(0,count).Select(cell => string.Join(',',
            Enumerable.Range(0,4).Select(field => bytes.Span[offset+cell*4+field]))));
    }

    private void ValidateOctogonOamTexture(OctogonCharacter actor,Texture2D texture,string nativeOam,
        int tileBase,int palette,HashSet<(ulong, string, int)> checkedTextures)
    {
        if (!checkedTextures.Add((texture.GetInstanceId(),nativeOam,palette))) return;
        // common sprite palettes$02/$05 and PALH_88 paletteData4960.
        // Literal source colors keep expected pixels independent of runtime
        // palette selection and the imported Octogon palette bytes.
        Color[] colors = palette switch
        {
            2 => [Colors.Transparent,new(0,0,0),new(1,1/31f,5/31f),new(1,26/31f,17/31f)],
            5 => [Colors.Transparent,new(1,22/31f,6/31f),new(27/31f,0,0),new(0,0,0)],
            6 => [Colors.Transparent,new(0,0,0),new(25/31f,9/31f,1),new(27/31f,29/31f,1)],
            _ => throw new InvalidOperationException($"ENEMY$7d: unexpected native palette${palette:x2}.")
        };
        var expected = NpcCharacter.BuildPositionedOamTextureUncachedForValidation(
            EnemyVisualSource.LoadComposite(actor.Record.Sprites),nativeOam,tileBase,palette,colors,true);
        using Texture2D expectedTexture = expected.Texture;
        using Image expectedImage = expectedTexture.GetImage();
        using Image actualImage = texture.GetImage();
        FailIf(actor.Animation.CurrentOffset != expected.Offset || texture.GetSize() != expectedTexture.GetSize() ||
            !actualImage.GetData().SequenceEqual(expectedImage.GetData()),
            $"ENEMY$7d:${actor.Record.SubId:x2} animation${actor.AnimationIndex:x2}/frame{actor.AnimationFrame}, palette${palette:x2}: " +
            $"sprite pixels/bounds/offset differ from native OAM; actual={texture.GetSize()}/{actor.Animation.CurrentOffset}, expected={expectedTexture.GetSize()}/{expected.Offset}.");
    }
}
