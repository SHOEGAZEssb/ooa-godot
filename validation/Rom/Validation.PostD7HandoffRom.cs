using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePostD7HandoffRom()
    {
        foreach (bool linked in new[] { false, true })
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetLinkedGame(linked);
            _inventory.GiveTreasure(TreasureId.Essence, 6);
            _saveData.SetRoomFlag(2, 0x90, 2);
            LoadValidationRoom(2, 0x90); _player.WarpTo(new(72, 72), recordSafe: false);
            StepGameplayUpdates(1, Vector2.Zero);
            var owner = _roomEvents.Get<PostD7Event>();
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 2, 72, 72)
                { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay(); rom[0xcc01] = (byte)(linked ? 1 : 0);
            rom[0xc2ef] = 0x21; rom[0xcc03] = 0; rom[0xcc8a] = rom[0xcc02] = 1;
            int hold = 0, generation = 0, update = 1;
            void Compare()
            {
                update++;
                rom.AdvanceWarpPalette();
                if (rom[0xcc04] != 0)
                {
                    rom.SelectTriggeredCutscene();
                    rom[0xcc00] = (byte)_entities.FrameCounter;
                }
                else
                {
                    rom.AdvancePostD7();
                    rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                }
                // The source publishes its pending warp here. The normal
                // room loader's placement RNG belongs to the next fixture.
                if (owner.Stage == PostD7Stage.Ascent) return;
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls - seed.Calls != rom.RandomCalls,
                    $"Post-D7 update{update}, linked={linked}: RNG calls {rng.Calls - seed.Calls}/{rom.RandomCalls}, stage={owner.Stage}, state={owner.State}/{rom[0xcc03]}.");
                if (owner.Stage != PostD7Stage.Ascent)
                    FailIf(owner.State != rom[0xcc03] || _dialogue.IsOpen != (rom[0xcba0] != 0) ||
                        _transitions.NativePaletteFadeActive != (rom[0xc4ab] != 0),
                        $"Post-D7 update{update}, linked={linked}: stage={owner.Stage}, state={owner.State}/{rom[0xcc03]}, text={_dialogue.IsOpen}/{rom[0xcba0]}, fade={_transitions.NativePaletteFadeActive}/{rom[0xc4ab]}.");
                var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_interactionSlots");
                if (owner.Stage != PostD7Stage.Ascent)
                foreach (var actor in _entities.EntityAdapters<PostD7ActorRoomEntity>())
                {
                    int s = 0xd040 + slots[actor] * 256;
                    FailIf(rom[s + 1] != actor.Id || rom[s + 2] != actor.SubId || rom[s + 4] != actor.State || rom[s + 5] != actor.Substate,
                        $"Post-D7 update{update} slot${slots[actor]:x2}: port=${actor.Id:x2}:${actor.SubId:x2}/{actor.State}/{actor.Substate}, ROM=${rom[s + 1]:x2}:${rom[s + 2]:x2}/{rom[s + 4]}/{rom[s + 5]}.");
                }
                if (generation != rom.TextGeneration)
                {
                    generation = rom.TextGeneration; hold = 3;
                    FailIf(_dialogue.SourceTextId + 0x0400 != rom.Word(0xcba2),
                        $"Post-D7 update{update}: text=${_dialogue.SourceTextId:x4}/${rom.Word(0xcba2):x4} (native primary-bank high index +$04).");
                }
                if (hold > 0 && --hold == 0) { _dialogue.Close(); rom[0xcba0] = 0; }
            }
            for (int limit = 0; owner.Stage != PostD7Stage.Ascent && limit < 1800; limit += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batch, afterUpdate: Compare);
            FailIf(owner.Stage != PostD7Stage.Ascent || rom[0xcc47] != 0x84 || rom[0xcc48] != 0xf6 ||
                rom[0xcc49] != 0x0f || rom[0xcc4a] != 0x47 || rom[0xcc4b] != 3,
                $"The native completed-tower scroll must publish invisible transition $0f to room $4:$f6/$47, parameter $03: stage={owner.Stage}, warp=${rom[0xcc47]:x2}/${rom[0xcc48]:x2}/${rom[0xcc49]:x2}/${rom[0xcc4a]:x2}/${rom[0xcc4b]:x2}.");
        }
    }
}
