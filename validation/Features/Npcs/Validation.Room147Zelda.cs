using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom147Zelda()
    {
        // zelda.s:@initSubid08 admits only linked, pre-Black-Tower complete,
        // flame-unlit files; @actAsGenericNpc installs TX_060b once.
        NpcRecord record = new NpcDatabase().GetRoomNpcs(1, 0x47).Single(n =>
            n.Id == InteractionId.Zelda && n.SubId == 8);
        FailIf(record is not { Var03: 0, Y: 0x38, X: 0x58,
                TextId: 0x060b, CanFace: true, DefaultAnimation: 2,
                Implementation: NpcImplementationClassification.OrdinaryGeneric } ||
            record.Message != "Let me take care\nof the\nvillagers.\n\\Link, you\nmust take care\nof \\col(3)Veran\\col(0) and\n\\col(3)Ralph\\col(0)!",
            "Room 1:47 Zelda $ad:$08 lost source placement, TX_060b controls, or ordinary adapter.");

        foreach (bool batched in new[] { false, true })
        foreach ((bool linked, bool preDone, bool flame, bool present) in new[]
        {
            (false, true, false, false),
            (true, false, false, false),
            (true, true, true, false),
            (true, true, false, true)
        })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            _saveData.SetGlobalFlag(GlobalFlag.PreBlackTowerCutsceneDone, preDone);
            _saveData.SetGlobalFlag(GlobalFlag.FlameOfDespairLit, flame);
            LoadValidationRoom(1, 0x47);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            NpcCharacter zelda = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record.Id == InteractionId.Zelda && n.Record.SubId == 8);
            FailIf(zelda.Active != present || zelda.Visible != present ||
                zelda.Position != new Vector2(0x58, 0x38),
                $"Room 1:47 Zelda $ad:$08 wrong state-zero admission: linked={linked}, pre={preDone}, flame={flame}.");
            if (!present)
            {
                FailIf(zelda.CanTalkTo(_player) || _entities.BlocksLink(zelda.Position),
                    "Room 1:47 excluded Zelda $ad:$08 retained collision or conversation.");
                continue;
            }

            FailIf(zelda.CurrentAnimationOpaquePixels == 0 ||
                !_entities.BlocksLink(zelda.Position) || zelda.ObjectCollisionBounds.Size != new Vector2(12, 12),
                "Room 1:47 Zelda $ad:$08 lost initialized sprite or NPC collision.");
            ApproachLibraryActor(zelda, batched,
                counterpartReachable: () => _entities.FindTalkTarget(_player) == zelda);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!_dialogue.IsOpen ||
                    !PlainWords(_dialogue.CurrentMessage).Contains(
                        "you must take care of Veran and Ralph!", StringComparison.Ordinal),
                    $"Room 1:47 Zelda $ad:$08 TX_060b was unreachable on conversation {repeat}.");
                int frame = zelda.CurrentAnimationFrame;
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(zelda.CurrentAnimationFrame != frame,
                    "Room 1:47 Zelda $ad:$08 animated during active text.");
                _dialogue.Close();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }

            _saveData.SetGlobalFlag(GlobalFlag.FlameOfDespairLit);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!zelda.Active || zelda.TextId != 0x060b,
                "Room 1:47 Zelda $ad:$08 repeated her state-zero deletion while the room stayed loaded.");
            ApproachLibraryActor(zelda, batched,
                counterpartReachable: () => _entities.FindTalkTarget(_player) == zelda);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.IsOpen ||
                !PlainWords(_dialogue.CurrentMessage).Contains("Veran and Ralph!", StringComparison.Ordinal),
                "Room 1:47 Zelda $ad:$08 lost her selected dialogue after the flame flag changed.");
            LoadValidationRoom(1, 0x46);
            _dialogue.Close();
            FailIf(_player.CutsceneControlled,
                "Room 1:47 Zelda $ad:$08 room-exit cancellation retained input control.");
            LoadValidationRoom(1, 0x47);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            zelda = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record.Id == InteractionId.Zelda && n.Record.SubId == 8);
            FailIf(zelda.Active || zelda.Visible || zelda.CanTalkTo(_player),
                "Room 1:47 Zelda $ad:$08 failed to delete on flame-lit re-entry.");
        }
        GD.Print("Validated room 1:47 Zelda $ad:$08 linked/flag admission, TX_060b source text, collision approach, repeated dialogue, state-zero lifetime, cancellation and split/batched updates.");
    }
}
