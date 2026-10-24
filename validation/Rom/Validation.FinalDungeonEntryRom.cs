using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFinalDungeonEntryRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(5, 0xf4, 0x40, false);
            LoadValidationRoom(5, 0xf4);
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(0x78, 0x58)); _player.Face(Vector2I.Up); _player.Visible = false;
            // The incoming native warp retains wEnteredWarpPosition=$57;
            // arrange that arrival guard while isolating this interaction.
            _transitions.DeactivateWarpAtPlayerPosition(_player);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x81);
            _runtimeState.SetWramByte(WramAddress.wMenuDisabled, 0x81);
            _runtimeState.SetWramByte(0xcbc3, 0xff);
            var actor = _entities.EntityAdapters<FinalDungeonEnergyRoomEntity>().Single();
            var decoration = _entities.EntityAdapters<DecorationRoomEntity>().Single();
            var animation = (EnemyAnimationPlayer)typeof(DungeonInteractionVisualEntity)
                .GetField("_animation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(decoration)!;
            var animationCounter = typeof(EnemyAnimationPlayer).GetField("_frameCounter", BindingFlags.Instance | BindingFlags.NonPublic)!;
            int at = 0xd040 + _entities.NativeInteractionSlot(actor) * 256;
            int decorationAt = 0xd040 + _entities.NativeInteractionSlot(decoration) * 256;
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 0x78, 0x58)
                { HostilePartsEnabled = true, PostObjectCollisionsEnabled = false };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd01a] &= 0x7f;
            rom[0xcc8a] = rom[0xcc02] = 0x81; rom[0xcbc3] = 0xff;
            for (int address = 0xcc24; address <= 0xcc28; address++) rom[address] = _runtimeState.ReadWramByte(address);
            rom[at] = 1; rom[at + 1] = 0xb5;
            rom[decorationAt] = 1; rom[decorationAt + 1] = 0x80;
            rom[decorationAt + 11] = 0x58; rom[decorationAt + 13] = 0x78;
            var sounds = new ValidationSoundRequestAudit(); _sound.SetRequestObserver(sounds);
            int update = 0;
            StepGameplayUpdates(260, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                rom.AdvanceWarpPalette();
                rom.UpdateGameplay(0, 0, 0xff, ++update);
                FailIf((int)animationCounter.GetValue(animation)! != rom[decorationAt + 0x20] ||
                    decoration.Visible != ((rom[decorationAt + 0x1a] & 128) != 0) ||
                    decoration.ZIndex != ObjectDrawPriority.FromVisible(rom[decorationAt + 0x1a]),
                    $"Entrance decoration $80:$00 update{update}: native animation counter, visibility or draw priority differs.");
                FailIf(actor.Finished != (rom[at] == 0) || !actor.Finished &&
                    (actor.State != rom[at + 4] || actor.Substate != rom[at + 5] || actor.Counter != rom[at + 6]) ||
                    _runtimeState.ReadWramByte(0xcbb3) != rom[0xcbb3] || _runtimeState.ReadWramByte(0xcbba) != rom[0xcbba] ||
                    _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
                    _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != rom[0xcc02] ||
                    _runtimeState.ReadWramByte(0xcbc3) != rom[0xcbc3] ||
                    _transitions.NativePaletteFadeActive != (rom[0xc4ab] != 0) || _player.Visible != ((rom[0xd01a] & 0x80) != 0),
                    $"Final dungeon entry update{update}: native state/counter, shared flash, masks, simulated-input, fade or Link visibility differs; state={actor.State}:{actor.Substate}/{actor.Counter}, native={rom[at + 4]}:{rom[at + 5]}/{rom[at + 6]}.");
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls) ||
                    !sounds.Requests.SequenceEqual(rom.Sounds) ||
                    Enumerable.Range(0, 6).Any(i => _saveData.ReadWramByte(0xc62b + i) != rom[0xc62b + i]),
                    $"Final dungeon entry update{update}: native RNG/calls={_random.CaptureState().Rng1:x2},{_random.CaptureState().Rng2:x2}/{_random.Calls-seed.Calls} vs {rom[0xff94]:x2},{rom[0xff95]:x2}/{rom.RandomCalls}; sounds={string.Join(',',sounds.Requests)} vs {string.Join(',',rom.Sounds)}; checkpoint={string.Join(',',Enumerable.Range(0,6).Select(i=>_saveData.ReadWramByte(0xc62b+i)))} vs {string.Join(',',Enumerable.Range(0,6).Select(i=>rom[0xc62b+i]))}.");
                for (int slot = 0; slot < 16; slot++)
                {
                    int part = 0xd0c0 + slot * 256;
                    var bead = _entities.ResolvePartSlot(slot) as BlueEnergyBeadRoomEntity;
                    FailIf((bead is not null) != (rom[part] != 0), $"Final dungeon entry update{update}: PART slot${slot:x2} occupancy differs.");
                    if (bead is null) continue;
                    FailIf(bead.Index != rom[part + 3] || bead.Duration != rom[part + 6] || bead.Delay != rom[part + 7] ||
                        bead.Visible != ((rom[part + 0x1a] & 128) != 0) ||
                        bead.PrecisePosition != new Vector2(rom.Word(part + 12) / 256f, rom.Word(part + 10) / 256f),
                        $"Final dungeon entry update{update}: PART$53 slot${slot:x2} index, countdown, position or visibility differs.");
                }
            });
            _sound.SetRequestObserver(null);
            FailIf(!actor.Finished || !_saveData.HasRoomFlag(5, 0xf4, 0x40) || !_player.Visible || _player.CutsceneControlled ||
                _roomEvents.Get<FinalDungeonEntryEvent>().HasState || _entities.EntityAdapters<BlueEnergyBeadRoomEntity>().Any(),
                $"Final dungeon entry final state: finished={actor.Finished}, flag40={_saveData.HasRoomFlag(5,0xf4,0x40)}, visible={_player.Visible}, control={_player.CutsceneControlled}, event={_roomEvents.Get<FinalDungeonEntryEvent>().HasState}, beads={_entities.EntityAdapters<BlueEnergyBeadRoomEntity>().Count()}, substate={actor.Substate}, fade={_transitions.NativePaletteFadeActive}, rawmask={_runtimeState.ReadWramByte(WramAddress.wDisabledObjects)}, nativeLink={rom[0xd004]}:{rom[0xd005]}.");
            LoadValidationRoom(5, 0xf3); LoadValidationRoom(5, 0xf4);
            int requests = _sound.PlayRequestsFor(SoundId.SndEnergyThing);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(_entities.EntityAdapters<FinalDungeonEnergyRoomEntity>().Any() ||
                _entities.EntityAdapters<BlueEnergyBeadRoomEntity>().Any() || _sound.PlayRequestsFor(SoundId.SndEnergyThing) != requests,
                "Final dungeon entry replayed its flag-$40 sequence after re-entry.");
        }
        GD.Print("Validated Room of Rites entry $b5:$00: checkpoint/flag order, 120-update swirl, eight-update Link handoff, native flash/fade, masks, RNG, ordered PART lifetimes and non-repeating entry against clean ROM in split/batched gameplay.");
    }
}
