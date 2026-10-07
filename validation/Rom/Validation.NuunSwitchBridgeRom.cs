using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareNuunSwitchBridgeRom()
    {
        static Vector2 Point(int packed) => new((packed&15)*16+8,(packed>>4)*16+8);
        foreach (bool batch in new[] { false,true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(0,0x54,OracleSaveData.RoomFlag40,false);
            LoadValidationRoom(0,0x54);
            _inventory.GiveTreasure(TreasureId.Shooter,1); _inventory.GiveTreasure(TreasureId.EmberSeeds,5);
            _inventory.SelectShooterSeeds(0); _inventory.EquipA(TreasureId.Shooter); _inventory.EquipB(0);
            Vector2 start = new(24.25f,104.5f); _player.WarpTo(start); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(start),"Nuun Shooter approach must start on original room$0:$54 floor.");
            var source = new DungeonMechanicDatabase().GetRoomRecords(0,0x54).Single();
            var bridge = _entities.Entities<NuunBridgeRoomEntity>().Single();
            FailIf(source is not { Id:0x6b,SubId:0x0f,PackedPosition:0x68,Parameter:1 } ||
                _entities.InteractionSlot(bridge) != 2 || _currentRoom.GetMetatile(Point(0x67)) != 0xce ||
                _runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0,
                "Original $6b:$0f placement must retain switch mask1, burnable tree$67:$ce and the cleared overworld switch byte.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,1,24,104) { HostilePartsEnabled = true };
            rom.Word(0xd00a,104*256+128); rom.Word(0xd00c,24*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd240] = 1; rom[0xd241] = 0x6b; rom[0xd242] = 0x0f;
            rom[0xd24b] = 104; rom[0xd24d] = 136;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,bool fire = false)
            {
                int tick = 0;
                StepGameplayUpdates(count,Vector2.Zero,fire ? ["attack"] : [],fire ? ["attack"] : [],batch,() => {
                    rom.UpdateGameplay(fire && tick++ == 0 ? 1 : 0,fire ? 1 : 0,0xff,_entities.FrameCounter);
                    CompareSomariaMotionRom(rom,$"Nuun switch/bridge update{++update}, batch={batch}");
                    rom.AdvanceTileGraphics();
                    bool switchAlive = rom[0xd0c0] != 0 && rom[0xd0c1] == 5;
                    var part = _entities.Entities<DungeonSwitchRoomEntity>().SingleOrDefault();
                    FailIf((part != null) != switchAlive || bridge.Finished != (rom[0xd240] == 0) ||
                        !bridge.Finished && (SomariaPrivate<int>(bridge,"_state") != rom[0xd244] ||
                            SomariaPrivate<int>(bridge,"_counter") != rom[0xd246]) ||
                        _runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != rom[0xcdd3] ||
                        _saveData.GetRoomFlags(0,0x54) != rom[0xc754] || _inventory.EmberSeeds != rom[0xc6b9] ||
                        !sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Nuun update{update}, batch={batch}: bridge state/counter={SomariaPrivate<int>(bridge,"_state")}/{SomariaPrivate<int>(bridge,"_counter")}, native={rom[0xd244]}/{rom[0xd246]}, switch={part != null}/{switchAlive}, flags=${_saveData.GetRoomFlags(0,0x54):x2}/${rom[0xc754]:x2}, cues=[{string.Join(',',sounds.Requests)}]/[{string.Join(',',rom.Sounds)}].");
                    if (part != null) FailIf(SomariaPrivate<bool>(part,"_initialized") != (rom[0xd0c4] != 0) ||
                        SomariaPrivate<bool>(part,"_pendingHit") != ((rom[0xd0ea]&0x80) != 0) ||
                        part.HitLockout != -unchecked((sbyte)rom[0xd0eb]),"Nuun switch initialization/pending collision/lockout differs.");
                    var items = _entities.Entities<EmberSeedEffect>();
                    int[] nativeItems = Enumerable.Range(0xd7,5).Select(page => page<<8)
                        .Where(slot => rom[slot] != 0 && rom[slot+1] == 0x20).ToArray();
                    FailIf(items.Count != nativeItems.Length,"Nuun Shooter must retain native ITEM$20 allocation and retirement.");
                    foreach (var item in items)
                    {
                        int slot = nativeItems.Single(slot => slot>>8 == _entities.DynamicItemSlotOf(
                            _entities.EntityAdapters<EmberSeedRoomEntity>().Single(adapter => ReferenceEquals(adapter.Node,item))));
                        int state = item.State == EmberState.Flying ? 1 : 3;
                        FailIf(state != rom[slot+4] || item.PrecisePosition != new Vector2(rom.Word(slot+0xc)/256f,rom.Word(slot+0xa)/256f) ||
                            (item.ZFixed&0xffff) != rom.Word(slot+0xe) || (item.SpeedZ&0xffff) != rom.Word(slot+0x14) ||
                            item.ShooterElevation != rom[slot+0x3e] || item.CollisionEnabled != ((rom[slot+0x24]&0x80) != 0) ||
                            SomariaPrivate<int>(item,"_frameCounter") != rom[slot+0x20] || state == 3 && item.FlameCounter != rom[slot+6],
                            $"Nuun ITEM$20 state/full XY/Z/elevation/collision/animation differs at update{update}.");
                    }
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                        "Nuun Shooter/switch/bridge must retain global RNG order.");
                });
            }
            _dialogue.ShowGameplayMessage("Pending Nuun switch",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0; Step(3);
            Step(fire:true);
            for (int wait = 0; _currentRoom.GetMetatile(Point(0x67)) == 0xce && wait < 96; wait++) Step();
            FailIf(_currentRoom.GetMetatile(Point(0x67)) == 0xce || _saveData.HasRoomFlag(0,0x54,OracleSaveData.RoomFlag40),
                "First real Shooter use must cross both original cliff levels and burn only the tree before the overworld switch.");
            Step(24); Step(fire:true);
            for (int wait = 0; !_saveData.HasRoomFlag(0,0x54,OracleSaveData.RoomFlag40) && wait < 96; wait++) Step();
            FailIf(!_saveData.HasRoomFlag(0,0x54,OracleSaveData.RoomFlag40) || !bridge.DisablesMovement,
                "Repeated real Shooter use must delete the one-shot switch, latch room flag$40 and hand off to the bridge controller.");
            for (int wait = 0; !bridge.Finished && wait < 176; wait++) Step();
            FailIf(!bridge.Finished || bridge.DisablesMovement ||
                sounds.Requests.Count(cue => cue == SoundId.SndDoorClose) != 3 ||
                sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1,
                "The bounded native simple script must finish all three bridge stages and release Link once.");
            Step(3); Step(fire:true); Step(32);
            FailIf(sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1,
                "A completed one-shot switch/bridge must not restart after repeated item input.");
            LoadValidationRoom(0,0x55); LoadValidationRoom(0,0x54);
            // Room loading reconstructs tiles before the retained placed
            // controller tests flag$40 on its first native dispatch.
            var reload = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,1,
                (int)_player.Position.X,(int)_player.Position.Y);
            reload[0xd240] = 1; reload[0xd241] = 0x6b; reload[0xd242] = 0x0f;
            reload[0xd24b] = 104; reload[0xd24d] = 136;
            StepGameplayUpdates(2,Vector2.Zero,batched:batch,afterUpdate:() => {
                reload.AdvanceInteractions(_entities.FrameCounter);
                FailIf(_entities.Entities<NuunBridgeRoomEntity>().Count != (reload[0xd240] != 0 ? 1 : 0),
                    "Flag$40 must delete the original placed controller on its first eligible re-entry update.");
            });
            FailIf(_entities.Entities<NuunBridgeRoomEntity>().Count != 0 || _entities.Entities<DungeonSwitchRoomEntity>().Count != 0 ||
                _currentRoom.GetMetatile(Point(0x68)) != 0x9e || new[] { 0x43,0x44,0x45 }.Any(p => _currentRoom.GetMetatile(Point(p)) != 0x1d) ||
                new[] { 0x53,0x54,0x55 }.Any(p => _currentRoom.GetMetatile(Point(p)) != 0x1e),
                "Flag$40 re-entry must reconstruct the completed original bridge and suppress its one-shot owners.");
        }
    }
}
