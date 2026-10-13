using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePostD7StoneRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _inventory.GiveTreasure(TreasureId.Essence, 6);
            _saveData.SetRoomFlag(2, 0x90, 2);
            LoadValidationRoom(4, 0xf6); _player.WarpTo(new(120, 72), recordSafe: false);
            var owner = _roomEvents.Get<PostD7Event>();
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 2, 120, 72)
                { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay(); rom[0xcc8a] = rom[0xcc02] = 1;
            // The invisible warp keeps essence.s state7's room-music gate;
            // MUS_DISASTER was queued by the preceding native cutscene.
            _sound.ClearActiveMusic(); _sound.PlayNativeSound(0x21); rom[0xcc35] = 0;
            var sounds = _sound.AttachPlayRequestAudit();
            var ambi = _entities.EntityAdapters<PostD7ActorRoomEntity>().Single();
            int slot = _entities.InteractionSlot(ambi.Node), s = 0xd040 + slot * 256;
            rom[s] = 1; rom[s + 1] = 0x4d; rom[s + 2] = 3; rom[s + 11] = 0xf8; rom[s + 13] = 0x78;
            int hold = 0, generation = 0, update = 0;
            void Compare()
            {
                update++; rom.AdvanceWarpPalette();
                if (rom[0xc2ef] == 0x10) rom.AdvancePostD7();
                rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                rom.SelectTriggeredCutscene();
                var rng = _random.CaptureState();
                FailIf(!sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    $"Stone update{update}: ordered native sound requests differ, port={string.Join(',', sounds.Requests)}, ROM={string.Join(',', rom.Sounds)}.");
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls - seed.Calls != rom.RandomCalls,
                    $"Stone update{update}: RNG {rng.Calls - seed.Calls}/{rom.RandomCalls}, stage={owner.Stage}, state={owner.State}/{rom[0xcc03]}.");
                FailIf(_dialogue.IsOpen != (rom[0xcba0] != 0) || _rooms.ActiveGroup != rom[0xcc2d] || _rooms.CurrentRoom.Id != rom[0xcc30] ||
                    owner.Stage == PostD7Stage.Stone && owner.State != rom[0xcc03],
                    $"Stone update{update}: stage={owner.Stage}, state={owner.State}/{rom[0xcc03]}, room={_rooms.ActiveGroup:x}:{_rooms.CurrentRoom.Id:x2}/{rom[0xcc2d]:x}:{rom[0xcc30]:x2}, text={_dialogue.IsOpen}/{rom[0xcba0]}.");
                foreach (int address in new[] { 0xcfc0, 0xcfd1, 0xcfd2, 0xcfd3, 0xcfde, 0xcfdf })
                    FailIf(_entities.RuntimeState.ReadWramByte(address) != rom[address],
                        $"Stone update{update}: signal ${address:x4}={_entities.RuntimeState.ReadWramByte(address):x2}/{rom[address]:x2}.");
                bool nativeRemoteStarted = Enumerable.Range(2, 14).Any(i => rom[0xd041 + i * 256] == 0x8a && rom[0xd044 + i * 256] == 1);
                FailIf(_roomEvents.Get<RemoteMakuSeventhEssenceEvent>().HasState != nativeRemoteStarted,
                    $"Stone update{update}: dynamic remote Maku must start in the return room's original object pass.");
                FailIf(_entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
                    _entities.RuntimeState.ReadWramByte(WramAddress.wMenuDisabled) != rom[0xcc02],
                    $"Stone update{update}: native input/menu ownership differs.");
                var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_interactionSlots");
                foreach (var actor in _entities.EntityAdapters<PostD7ActorRoomEntity>())
                {
                    int a = 0xd040 + slots[actor] * 256;
                    FailIf(rom[a + 1] != actor.Id || rom[a + 2] != actor.SubId || rom[a + 4] != actor.State ||
                        rom[a + 5] != actor.Substate || actor.PrecisePosition != new Vector2(rom.Word(a + 12) / 256f, rom.Word(a + 10) / 256f) ||
                        actor.ZFixed != unchecked((short)rom.Word(a + 14)),
                        $"Stone update{update} slot${slots[actor]:x2} ${actor.Id:x2}:${actor.SubId:x2}: state/sub/XYZ={actor.State}/{actor.Substate}/{actor.PrecisePosition}/{actor.ZFixed}; ROM=${rom[a + 1]:x2}:${rom[a + 2]:x2}/{rom[a + 4]}/{rom[a + 5]}/{rom.Word(a + 12) / 256f},{rom.Word(a + 10) / 256f}/{(short)rom.Word(a + 14)}.");
                    ComparePostD7AnimationRom(rom, actor, a, $"Stone update{update}");
                    if (actor.Id is 0x3a or 0x43 && ((NpcCharacter)actor.Node).Record.Palette == 6)
                        CompareInteractionPalettePixelsRom(rom, (NpcCharacter)actor.Node, 6,
                            $"Stone update{update} ${actor.Id:x2}:${actor.SubId:x2}");
                }
                if (generation != rom.TextGeneration)
                {
                    generation = rom.TextGeneration; hold = 3;
                    FailIf(_dialogue.SourceTextId + 0x0400 != rom.Word(0xcba2), "Stone native text index differs.");
                }
                if (hold > 0 && --hold == 0) { _dialogue.Close(); rom[0xcba0] = 0; }
            }
            for (int limit = 0; owner.HasState && limit < 2600; limit += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batch, afterUpdate: Compare);
            FailIf(owner.HasState || rom[0xc2ef] != 1 || !_saveData.HasRoomFlag(4, 0xf6, 0x40) ||
                (rom[0xc9f6] & 0x40) == 0 || !_roomEvents.Get<RemoteMakuSeventhEssenceEvent>().HasState,
                "The native stone scenes must finish in $2:$90 with tower flag$40 and the dynamic Maku handoff running.");
        }
    }
}
