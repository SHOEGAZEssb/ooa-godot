using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static string PlainWords(string message) => string.Join(
        " ",
        DialogueBox.PlainText(message).Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries));

    private static void ValidateNpcPaletteRebuildOffsets()
    {
        // Use the actual LINK_ANIM_MODE_SWIM up frames: graphic $d4/$d8,
        // OAM $10, six interactionAnimate updates per frame.
        const string animation = "6,0@12,0,0,0;12,8,2,0|6,1@12,0,0,0;12,8,2,0";
        NpcRecord record = new NpcDatabase().GetRoomNpcs(0, 0x66).First() with
        {
            SpriteName = "spr_link", TileBase = 0, Palette = 1,
            UpAnimation = animation, RightAnimation = animation,
            DownAnimation = animation, LeftAnimation = animation
        };
        var actor = new NpcCharacter();
        var reference = new NpcCharacter();
        try
        {
            actor.Initialize(record);
            reference.Initialize(record);
            foreach (Action<NpcCharacter> change in new Action<NpcCharacter>[]
            {
                npc => npc.SetBasePalette(2),
                npc => npc.SetScriptPaletteOverride(NpcCharacter.GetStandardSpritePalette(5)),
                npc => npc.SetSourceGrayscaleInverted(false)
            })
            {
                actor.SetScriptAnimation(animation, [0x0e00, 0x0e40]);
                actor.AdvanceAnimationUpdates(8);
                change(actor);
                // Construct the expected frame independently using a single
                // graphics-header offset, with no per-frame offset array.
                change(reference);
                reference.SetGraphicsSourceOffset(0x0e40);
                reference.SetScriptAnimation(animation);
                reference.AdvanceAnimationUpdates(8);
                FailIf(actor.CurrentAnimationFrame != 1 ||
                    actor.CurrentAnimationPixelHash != reference.CurrentAnimationPixelHash ||
                    actor.CurrentAnimationOffset != reference.CurrentAnimationOffset,
                    "NPC palette rebuild lost LINK graphic $d8's source offset or frame position.");
                actor.AdvanceAnimationUpdates(3);
                FailIf(actor.CurrentAnimationFrame != 1,
                    "NPC palette rebuild advanced the six-update animation counter early.");
                actor.AdvanceAnimationUpdates(1);
                FailIf(actor.CurrentAnimationFrame != 0,
                    "NPC palette rebuild reset the counter instead of looping on update 12.");
            }
        }
        finally
        {
            actor.Free();
            reference.Free();
        }
        GD.Print("Validated NPC palette/grayscale rebuild graphics offsets, OAM origin and animation counters.");
    }

}
