using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateImpaGraphicsContinuation()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false);
            _saveData.SetRoomFlag(0, 0x7a, 0x40);
            var impa = _roomEvents.Get<ImpaIntroEvent>();
            // Executed ROM frames 1350-1353: placement, Impa's $0f
            // transfer, fake Octorok's $8f transfer, then the same object
            // pass completes. Playtime increments only at its first entry.
            EnterRoom();
            byte entryTime = _saveData.ReadWramByte(0xc622);
            Vector2 entryPosition = _player.Position;
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            byte objectTime = unchecked((byte)(entryTime + 1));
            FailIf(!impa.ObjectUpdateSuspended || _player.NativeCutsceneControlled ||
                _saveData.ReadWramByte(0xc622) != objectTime || _player.Position != entryPosition ||
                _runtimeState.ReadWramByte(0xcc08) != 0x0f || impa.Actor!.Visible,
                $"Impa graphics: pending={impa.ObjectUpdateSuspended}, control={_player.NativeCutsceneControlled}, " +
                $"time={_saveData.ReadWramByte(0xc622)}/{objectTime}, position={_player.Position}/{entryPosition}, " +
                $"gfx={_runtimeState.ReadWramByte(0xcc08):x2}, visible={impa.Actor!.Visible}.");
            StepGameplayUpdates(1, Vector2.Right, batched: batched);
            FailIf(!impa.ObjectUpdateSuspended || !_player.NativeCutsceneControlled ||
                _saveData.ReadWramByte(0xc622) != objectTime || _player.Position != entryPosition ||
                _runtimeState.ReadWramByte(0xcc0a) != 0x8f || impa.FakeOctoroks.Count != 3 ||
                impa.FakeOctoroks[0].Visible || !impa.Actor.Visible,
                "Resuming Impa must install Link's override before yielding for the first fake Octorok.");
            AssertOctorokProjection(new Vector2(0, -128));
            StepGameplayUpdates(1, Vector2.Right, batched: batched);
            FailIf(impa.ObjectUpdateSuspended || _saveData.ReadWramByte(0xc622) != objectTime ||
                _runtimeState.ReadWramByte(0xcc0c) != 0 ||
                !impa.FakeOctoroks[0].Visible || !impa.FakeOctoroks[1].Visible || !impa.FakeOctoroks[2].Visible,
                "Shared fake Octorok graphics were loaded repeatedly or advanced the main thread.");
            AssertOctorokProjection(new Vector2(0, -128));
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_saveData.ReadWramByte(0xc622) != unchecked((byte)(objectTime + 2)) ||
                impa.Counter != 0x76 || !_transitions.ScrollActive,
                "Link's $78 counter must decrement in its first update and continue during scrolling.");
            for (int limit = 0; _transitions.ScrollActive && limit < 80; limit += 4)
                StepGameplayUpdates(4, Vector2.Zero, batched: batched,
                    afterUpdate: () => AssertOctorokProjection(impa.Actor!.TransitionDrawOffset));
            FailIf(_transitions.IsTransitioning, "Impa $31:$00 entry scroll did not finish.");
            AssertOctorokProjection(Vector2.Zero);

            // Cancel a suspended load, then return. Native graphics residency
            // survives room placement; a cached header must not yield again.
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
            _saveData.SetRoomFlag(0, 0x7a, 0x40);
            impa = _roomEvents.Get<ImpaIntroEvent>();
            LoadValidationRoom(0, 0x6a);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            LoadValidationRoom(0, 0x47);
            FailIf(impa.ObjectUpdateSuspended || _player.NativeCutsceneControlled,
                "Cancelling an Impa graphics load retained the suspended interaction.");
            EnterRoom();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(!impa.ObjectUpdateSuspended || !_player.NativeCutsceneControlled ||
                _runtimeState.ReadWramByte(0xcc0a) != 0x8f,
                "Returning to a cached Impa header did not proceed directly to the missing Octorok header.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(impa.ObjectUpdateSuspended, "The cached return retained a redundant graphics continuation.");
            AssertOctorokProjection(new Vector2(0, -128));

            void AssertOctorokProjection(Vector2 expectedOffset)
            {
                // bank0.s:_getObjectPositionOnScreen_duringScreenTransition
                // applies the incoming projection on the first draw, including
                // slots born after placement and during a graphics continuation.
                Vector2[] positions = [new(0x48, 0x18), new(0x38, 0x38), new(0x58, 0x38)];
                for (int index = 0; index < 3; index++)
                {
                    NpcCharacter actor = impa.FakeOctoroks[index];
                    FailIf(actor.Position != positions[index] || actor.TransitionDrawOffset != expectedOffset,
                        $"Room $00:$6a fake Octorok $32:$00/{index}: position={actor.Position}, " +
                        $"draw offset={actor.TransitionDrawOffset}, expected {positions[index]} + {expectedOffset}.");
                }
            }

            void EnterRoom()
            {
                LoadValidationRoom(0, 0x7a);
                _player.WarpTo(new Vector2(0x48, 8));
                StepGameplayUpdates(3, Vector2.Up, ["move_up"], batched: batched);
                FailIf(!_transitions.ScrollActive || _currentRoom.Id != 0x6a ||
                    _player.NativeCutsceneControlled || impa.FakeOctoroks.Count != 0,
                    $"Impa placement: room={_currentRoom.Id:x2}, scroll={_transitions.ScrollActive}, " +
                    $"Link={_player.Position}, controlled={_player.NativeCutsceneControlled}, fakes={impa.FakeOctoroks.Count}.");
            }
        }
        GD.Print("Validated Impa graphics continuations, Octorok scroll projection, shared header reuse, playtime, cancellation and batched updates.");
    }
}
