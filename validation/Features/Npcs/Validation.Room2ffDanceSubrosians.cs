using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom2ffDanceSubrosians()
    {
        // objects/ages/mainData.s:group2MapffObjectData (US branch) places
        // green and red after the four Gorons. subrosian.s:$02 indexes the
        // scripts with var03 and both scripts check the linked byte once.
        NpcRecord[] records = new NpcDatabase().GetRoomNpcs(2, 0xff)
            .Where(n => n.Id == InteractionId.Subrosian && n.SubId == 2).ToArray();
        FailIf(!records.Select(n => (n.Var03, n.Y, n.X, n.TextId, n.Palette,
                n.DefaultAnimation, n.CanFace, n.Implementation)).SequenceEqual(new[]
            {
                (0, 0x18, 0x18, 0x1c14, 0, 2, true,
                    NpcImplementationClassification.OrdinaryGeneric),
                (1, 0x18, 0x28, 0x1c15, 2, 2, true,
                    NpcImplementationClassification.OrdinaryGeneric)
            }) ||
            PlainWords(records[0].Message) !=
                "That Goron Dance is way cool! I'm gonna make it popular in Subrosia, too!" ||
            PlainWords(records[1].Message) !=
                "The Graceful Goron is the bomb! I'm gonna be the Graceful Subrosian!",
            "Room 2:ff $4e:$02 lost US object order, TX_1c14/TX_1c15, red OAM $02, or script text.");

        foreach (bool batched in new[] { false, true })
        foreach (bool linked in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            LoadValidationRoom(2, 0xff);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            NpcCharacter[] actors = _entities.Entities<NpcCharacter>()
                .Where(n => n.Record.Id == InteractionId.Subrosian && n.Record.SubId == 2)
                .ToArray();
            FailIf(actors.Length != 2 ||
                actors[0].Position != new Vector2(0x18, 0x18) ||
                actors[1].Position != new Vector2(0x28, 0x18) ||
                actors.Any(n => n.Active != linked || n.Visible != linked ||
                    n.CanTalkTo(_player) && !linked ||
                    !linked && _entities.BlocksLink(n.Position)),
                "Room 2:ff $4e:$02/v$00-$01 lost linked state-zero admission, pose, or collision.");
            if (!linked) continue;

            for (int i = 0; i < actors.Length; i++)
            {
                NpcCharacter actor = actors[i];
                string words = i == 0
                    ? "That Goron Dance is way cool! I'm gonna make it popular in Subrosia, too!"
                    : "The Graceful Goron is the bomb! I'm gonna be the Graceful Subrosian!";
                FailIf(actor.CurrentAnimationOpaquePixels == 0 ||
                    actor.ObjectCollisionBounds.Size != new Vector2(12, 12) ||
                    !_entities.BlocksLink(actor.Position),
                    $"Room 2:ff $4e:$02/v${i:x2} lacked visible sprite or $06/$06 collision.");
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    ApproachLibraryActor(actor, batched,
                        counterpartReachable: () => _entities.FindTalkTarget(_player) == actor);
                    StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                    FailIf(!_dialogue.IsOpen || PlainWords(_dialogue.CurrentMessage) != words,
                        $"Room 2:ff $4e:$02/v${i:x2} dialogue unreachable on talk {repeat}.");
                    int frame = actor.CurrentAnimationFrame;
                    StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                    FailIf(actor.CurrentAnimationFrame != frame,
                        $"Room 2:ff $4e:$02/v${i:x2} animated during text.");
                    _dialogue.Close();
                    StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                }
            }
            _saveData.SetLinkedGame(false);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(actors.Any(n => !n.Active || !n.Visible),
                "Room 2:ff $4e:$02 repeated linked check after state zero.");
            LoadValidationRoom(2, 0xfe);
            _dialogue.Close();
            LoadValidationRoom(2, 0xff);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<NpcCharacter>().Where(n =>
                    n.Record.Id == InteractionId.Subrosian && n.Record.SubId == 2)
                .Any(n => n.Active || n.Visible),
                "Room 2:ff $4e:$02 failed to delete on unlinked re-entry.");
        }
        GD.Print("Validated room 2:ff $4e:$02/v$00-$01 source scripts, linked admission, palette, room collision dialogue, repeat, re-entry and split/batched updates.");
    }
}
