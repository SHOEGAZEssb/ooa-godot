using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateNpcInitializationVisibility()
    {
        // bank0.s:clearDynamicInteractions/interactionDelete clear Object.visible;
        // getFreeInteractionSlot only enables the cleared slot. objectQueueDraw
        // rejects bit 7 clear. interactionInitGraphics/interactionSetAnimation
        // do not set it. Expectations apply to every imported record, including
        // family variants, rather than deriving a whitelist from visibility data.
        var records = new NpcDatabase().AllRecords;
        var actor = new NpcCharacter();
        var player = new Player { Position = new Vector2(0x50, 0x70) };
        AddChild(actor);
        try
        {
            foreach (NpcRecord record in records)
            {
                actor.Initialize(record);
                actor.SetScriptAnimation(record.UpAnimation);
                actor.AdvanceAnimationUpdates(1);
                actor.SetActive(false);
                actor.SetActive(true);
                actor.SetFlagVisible(false);
                actor.SetFlagVisible(true);
                FailIf(!actor.Active || actor.Visible || actor.IsVisibleInTree(),
                    $"NPC {record.Group:x}:{record.Room:x2} ${record.Id:x2}:${record.SubId:x2}/v${record.Var03:x2} drew before its native visibility write.");

                // objectSetVisible80 and Link-relative draw priority both set
                // bit 7. Existing flag/script suppression must still win.
                actor.SetFixedDrawPriority(ObjectDrawPriority.FixedHighPriorityZIndex);
                FailIf(!actor.Visible || !actor.IsVisibleInTree(),
                    $"NPC ${record.Id:x2}:${record.SubId:x2} did not honor objectSetVisible80.");
                actor.SetFlagVisible(false);
                actor.UpdateDrawPriority(player.Position);
                FailIf(actor.Visible, "A native visibility write bypassed an imported deletion predicate.");
                actor.SetFlagVisible(true);
                FailIf(!actor.Visible, "Restoring an initialized NPC's flag lost its visibility.");
                actor.SetScriptVisible(false);
                actor.UpdateDrawPriority(player.Position);
                actor.SetActive(false);
                actor.SetActive(true);
                FailIf(actor.Visible, "Animation, priority or activation bypassed script hiding.");
                actor.SetScriptVisible(true);
                FailIf(!actor.Visible, "An explicit script show did not restore the initialized NPC.");
            }

            // A future interaction ID must inherit the rule without an ID list.
            NpcRecord future = records.First() with
            {
                Id = 0xff, SubId = 0xfe,
                Implementation = NpcImplementationClassification.EventOwned
            };
            actor.Initialize(future);
            var deferred = new EventOwnedNpcRoomEntity(actor);
            FailIf(deferred.PrepareForScreenTransition(new List<RoomEntitySpawn>()) !=
                ScreenTransitionPresentation.Hidden || actor.Visible,
                "Preloading an unresolved event actor published its default pose.");
            for (int update = 0; update < 8; update++)
                deferred.Update(1.0 / 60.0, player);
            FailIf(actor.Visible || actor.IsVisibleInTree(),
                "Generic room updates exposed a deferred native actor.");
            actor.UpdateDrawPriority(player.Position);
            FailIf(!actor.Visible || deferred.PrepareForScreenTransition(new List<RoomEntitySpawn>()) !=
                ScreenTransitionPresentation.Visible,
                "The native initializer did not publish its resolved event actor.");
            actor.Initialize(future);
            FailIf(actor.Visible, "Reusing a renderer retained its previous slot's native visibility.");
            actor.SetActive(false);
            actor.SetScriptVisible(true);
            FailIf(actor.Visible, "A script show resurrected a deleted actor.");
        }
        finally { actor.Free(); player.Free(); }
        GD.Print($"Validated native visibility initialization for all {records.Count} imported NPC/family records, deferred updates, preloads, explicit show/hide and renderer reuse.");
    }
}
