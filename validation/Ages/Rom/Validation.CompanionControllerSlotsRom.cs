using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateCompanionControllerSlotsRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            foreach (int gate in new[] { 0, 1, 2 })
            foreach (bool pressure in gate == 2 ? new[] { false, true } : new[] { false })
            {
                InitializeCompletedIntroRoom(0, 0x11);
                _saveData.SetGlobalFlag(AgesGlobalFlag.TalkedToHeadCarpenter, gate != 0);
                _saveData.SetGlobalFlag(AgesGlobalFlag.CanBuyFlute);
                LoadValidationRoom(0, 0x34);
                var parent = _entities.EntityAdapters<CompanionForestControllerRoomEntity>().Single();
                F1Retain(parent);
                FailIf(_entities.NativeInteractionSlot(parent) != 2 || parent.State != 0,
                    "mainData.s: $0:$34 must allocate its source-leading $71:$08 before state0 save/direction gates.");
                int floorY = Enumerable.Range(2, 12).Select(row => row * 8).First(y =>
                    !_collision.Collides(new Vector2(80, y)) && !_collision.Collides(new Vector2(79, y)) &&
                    new[] { new Vector2(80, y), new Vector2(79, y), new Vector2(80, y + 4), new Vector2(79, y + 4) }
                        .All(point => _currentRoom.GetTerrainInfo(point) is { Type: TerrainType.Normal, Hazard: HazardType.None }));
                _player.WarpTo(new(80, floorY));
                FailIf(_collision.Collides(_player.Position), "Forest entry fixture requires real floor.");
                var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 80, floorY);
                native.InitializeLinkGameplay(); native[0xd240] = 1; native[0xd241] = 0x71; native[0xd242] = 8;
                byte direction = gate == 1 ? (byte)0 : (byte)3;
                _runtimeState.SetField("wScreenTransitionDirection", direction); native[0xcd02] = direction;
                for (int address = 0xcfd0; address < 0xcfe0; address++)
                { _runtimeState.SetWramByte(address, 0xa5); native[address] = 0xa5; }
                var aliases = new List<(int, byte)>();
                void Alias(int address, byte value) => aliases.Add((address, value));
                _entities.NativeSoundAllocationAliasWritten += Alias;
                int updates = 0;
                void Compare()
                {
                    native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                    CompareFairyInteractionSlotsRom(native, $"forest entry update{++updates}, gate={gate}, pressure={pressure}, batch={batched}");
                    foreach (int address in new[] { 0xcc02, 0xcc8a, 0xcbaf, 0xcfd0, 0xcfd1, 0xcfd2 })
                        FailIf(_runtimeState.ReadWramByte(address) != native[address], $"Forest entry WRAM${address:x4} differs.");
                    FailIf(_inventory.AnimalCompanion != 0x0d || _saveData.ReadField("wAnimalCompanion") != 0x0d ||
                        _saveData.HasGlobalFlag(AgesGlobalFlag.CanBuyFlute) || _inventory.HasTreasure(TreasureId.Flute),
                        "$71:$08 state0 must assign default Moosh without a flute before either eligibility guard.");
                }
                void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
                Step();
                if (gate == 2)
                {
                    Step();
                    FailIf(parent.State != 1, "Source entry state1 must wait at Link.xh==$50.");
                    if (pressure) AddFairySlotSparkles(native, 13);
                    _player.WarpTo(new(79, floorY)); native.Word(0xd00c, 79 * 256);
                    Step();
                    FailIf(parent.State != 2 || _roomEvents.Get<CompanionForestEvent>().Flights.Count != (pressure ? 0 : 1),
                        "Entry trigger must retain its parent, install the script and admit only the native child slot.");
                    if (pressure)
                        FailIf(!aliases.SequenceEqual(new[] { (0xc043, (byte)0x0f) }) || native[0xc043] != 0x0f,
                            "Unchecked entry spawn failure must write var03 through HL=$e043, aliasing audio RAM $c043.");
                    Step(3);
                }
                else FailIf(!parent.Finished, "A rejected native entry must retire after its state0 side effects.");
                _entities.NativeSoundAllocationAliasWritten -= Alias;
                _roomEvents.Get<CompanionForestEvent>().Cancel();
            }
        }
    }

    private void ValidateCompanionRewardBirthSlotsRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            foreach (int companion in new[] { 0x0b, 0x0c, 0x0d })
            foreach (bool pressure in new[] { false, true })
            {
                InitializeCompletedIntroRoom(0, 0x11);
                _inventory.AssignAnimalCompanion(companion);
                _saveData.SetGlobalFlag(AgesGlobalFlag.SavedCompanionFromForest);
                LoadValidationRoom(0, 0x63);
                var parent = _entities.EntityAdapters<CompanionForestControllerRoomEntity>().Single();
                var preset = _entities.EntityAdapters<CompanionPresetSpawnerRoomEntity>().Single();
                foreach (var entity in _entities.EntityAdapters<IRoomEntity>().ToArray())
                    if (!ReferenceEquals(entity, parent) && !ReferenceEquals(entity, preset)) _entities.RetireEntity(entity);
                FailIf(_entities.NativeInteractionSlot(parent) != 2 || _entities.NativeInteractionSlot(preset) != 3,
                    "Source $71:$0a and $67:$05 must occupy $d2/$d3 together before child admission.");
                _player.WarpTo(new(104, 88));
                var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 104, 88);
                native.InitializeLinkGameplay(); native[0xd240] = 1; native[0xd241] = 0x71; native[0xd242] = 0x0a;
                native[0xd340] = 1; native[0xd341] = 0x67; native[0xd342] = 5;
                if (pressure) AddFairySlotSparkles(native, 12);
                var aliases = new List<(int, byte)>();
                void Alias(int address, byte value) => aliases.Add((address, value));
                _entities.NativeSoundAllocationAliasWritten += Alias;
                StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                    CompareFairyInteractionSlotsRom(native, $"reward births, companion=${companion:x2}, pressure={pressure}, batch={batched}");
                    FailIf(_runtimeState.ReadField("wDisabledObjects") != native[0xcc8a] || _runtimeState.ReadField("wMenuDisabled") != native[0xcc02] ||
                        _runtimeState.ReadWramByte(0xcfd2) != native[0xcfd2] || native[0xd100] != 1 || native[0xd101] != companion,
                        "Reward state0 must publish $01/$01/signal1 and birth the reserved companion after its special-object phase.");
                    var actor = _entities.EntityAdapters<ScriptedCompanionRoomEntity>().Single();
                    FailIf(((IRoomEntity)actor).PresentationNode!.Position != new Vector2(native[0xd10d], native[0xd10b]) || !preset.Finished,
                        "The later $67:$05 must install its source $48/$68 preset and delete without advancing the new companion.");
                });
                if (pressure)
                    FailIf(!aliases.SequenceEqual(new[] { (0xc043, (byte)0x11), (0xc043, (byte)0x12), (0xc043, (byte)0x13) }) || native[0xc043] != 0x13,
                        "All three unchecked failed child creations must retain their ordered audio-alias writes.");
                _entities.NativeSoundAllocationAliasWritten -= Alias;
                _roomEvents.Get<CompanionForestEvent>().Cancel();
            }
        }
    }

    private void ValidateCompanionCleanupSlotsRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            foreach (var placement in new[] { (Group: 2, Room: 0x5e, Order: 4), (Group: 4, Room: 0xe6, Order: 1), (Group: 5, Room: 0xf6, Order: 3) })
            {
                InitializeCompletedIntroRoom(0, 0x11);
                _saveData.WriteWramByte(AgesWramAddress.wDimitriState, 0x20);
                LoadValidationRoom(placement.Group, placement.Room);
                var parent = _entities.EntityAdapters<CompanionForestControllerRoomEntity>().Single();
                FailIf(_entities.NativeInteractionSlot(parent) != placement.Order + 2,
                    $"{placement.Group:x}:{placement.Room:x2} $71:$0c must follow all {placement.Order} source placements before initialization.");
                int slot = _entities.NativeInteractionSlot(parent); F1Retain(parent);
                var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 80, 112);
                native.InitializeLinkGameplay(); native[0xd040 + slot * 256] = 1;
                native[0xd041 + slot * 256] = 0x71; native[0xd042 + slot * 256] = 0x0c;
                StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                    CompareFairyInteractionSlotsRom(native, "$71:$0c side-effect retirement");
                    FailIf(!parent.Finished || _saveData.ReadWramByte(AgesWramAddress.wDimitriState) != 0x60 ||
                        native[0xc647] != 0x60, "Placed $71:$0c must preserve bit$20, set bit$40 and delete in its first native visit.");
                });
            }
        }
    }

}
