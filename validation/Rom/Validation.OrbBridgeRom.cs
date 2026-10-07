using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareOrbBridgeRom()
    {
        static Vector2 Point(int packed) => new((packed&15)*16+8,(packed>>4)*16+8);
        foreach (bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(2,0x9e,OracleSaveData.RoomFlag40,false);
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,0xff);
            LoadValidationRoom(2,0x9e);
            var records = new DungeonMechanicDatabase().GetRoomRecords(2,0x9e);
            FailIf(records.Select(row => (row.Order,row.Id,row.SubId,row.PackedPosition,row.Parameter)).ToArray()
                is not [(0,0xdc,0x12,0x13,0x0c),(1,3,0,0x61,0)],
                "Original mainData.s room$2:$9e must allocate its bridge controller before the stationary orb.");
            var controller = _entities.Entities<OrbBridgeControllerRoomEntity>().Single();
            var orb = _entities.Entities<DungeonOrbRoomEntity>().Single();
            FailIf(_entities.InteractionSlot(controller) != 2 || _runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) != 0,
                "Room initialization must clear wToggleBlocksState before allocating INTERAC$dc:$12 in$d2.");
            _inventory.GiveTreasure(TreasureId.Shooter,1); _inventory.EquipA(TreasureId.Shooter);
            _inventory.GiveTreasure(TreasureId.EmberSeeds,5); _inventory.SelectShooterSeeds(0);
            _inventory.GiveTreasure(TreasureId.Feather,1); _inventory.EquipB(TreasureId.Feather);
            // The orb's island is surrounded by holes and the south wall.
            // Jump west from floor$27, then shoot southwest across the holes.
            Vector2 start = new(112.25f,36.5f); _player.WarpTo(start); _player.Face(Vector2I.Left);
            FailIf(_collision.Collides(start),"Orb contact must approach original room$2:$9e floor at tile$27.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,3,112,36) { HostilePartsEnabled = true };
            rom.Word(0xd00a,36*256+128); rom.Word(0xd00c,112*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xcc21] = 36; rom[0xcc22] = 112; rom[0xcc23] = 2;
            rom[0xd240] = 1; rom[0xd241] = 0xdc; rom[0xd242] = 0x12;
            rom[0xd24b] = 0x13;
            rom[0xd0c0] = 1; rom[0xd0c1] = 3; rom[0xd0c2] = 0;
            rom[0xd0cb] = 104; rom[0xd0cd] = 24;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,bool attack = false,int angle = 0xff,bool feather = false)
            {
                int tick = 0;
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int directions = (movement.X < 0 ? 0x20 : movement.X > 0 ? 0x10 : 0) | (movement.Y > 0 ? 0x80 : movement.Y < 0 ? 0x40 : 0);
                int held = (attack ? 1 : 0) | (feather ? 2 : 0) | directions;
                int edge = (attack ? 1 : 0) | (feather ? 2 : 0);
                StepGameplayUpdates(count,movement,MenuRomActions(held),MenuRomActions(edge),batch,() => {
                    rom.UpdateGameplay(tick++ == 0 ? edge : 0,held,angle,_entities.FrameCounter);
                    CompareSomariaMotionRom(rom,$"Orb bridge batch={batch}, update{++update}");
                    if (rom[0xd004] == 1) FailIf(_player.TopDownAirborne != (rom[0xcc5c] != 0) ||
                        (_player.ItemCreationZFixed&0xffff) != rom.Word(0xd00e) ||
                        (_player.TopDownAirSpeedZ&0xffff) != rom.Word(0xd014),
                        "Original Feather jump, landing and Shooter handoff must retain Link's fixed Z and gravity.");
                    rom.AdvanceTileGraphics();
                    var part = _entities.Entities<BridgeSpawnerRoomEntity>().SingleOrDefault();
                    bool nativeBridge = rom[0xd1c0] != 0 && rom[0xd1c1] == 0x0c;
                    FailIf(controller.Finished != (rom[0xd240] == 0) || (part != null) != nativeBridge ||
                        _runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) != rom[0xcdd2] ||
                        _saveData.GetRoomFlags(2,0x9e) != rom[0xc79e] ||
                        SomariaPrivate<bool>(orb,"_initialized") != (rom[0xd0c4] != 0) ||
                        orb.PendingHit != ((rom[0xd0ea]&0x80) != 0) || orb.HitLockout != -unchecked((sbyte)rom[0xd0eb]) ||
                        orb.Palette != rom[0xd0db] || !sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Orb bridge update{update}: controller/PART allocation, pending contact, palette, shared bytes or cues differ.");
                    if (part != null) FailIf(SomariaPrivate<bool>(part,"_initialized") != (rom[0xd1c4] != 0) ||
                        SomariaPrivate<int>(part,"_counter") != rom[0xd1c6] || SomariaPrivate<int>(part,"_remaining") != rom[0xd1c7] ||
                        SomariaPrivate<int>(part,"_position") != rom[0xd1cb],
                        $"Bridge PART$0c counter/remaining/packed position differs at update{update}.");
                    FailIf(_rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31),"Bridge changed-tile queue differs.");
                    var items = _entities.Entities<EmberSeedEffect>();
                    int[] nativeItems = Enumerable.Range(0xd7,5).Select(page => page<<8)
                        .Where(slot => rom[slot] != 0 && rom[slot+1] == 0x20).ToArray();
                    FailIf(items.Count != nativeItems.Length || _inventory.EmberSeeds != rom[0xc6b9],"Orb Shooter allocation/BCD debit differs.");
                    foreach (var item in items)
                    {
                        int slot = nativeItems.Single(slot => slot>>8 == _entities.DynamicItemSlotOf(
                            _entities.EntityAdapters<EmberSeedRoomEntity>().Single(adapter => ReferenceEquals(adapter.Node,item))));
                        int state = item.State == EmberState.Flying ? 1 : 3;
                        FailIf(state != rom[slot+4] || item.PrecisePosition != new Vector2(rom.Word(slot+0xc)/256f,rom.Word(slot+0xa)/256f) ||
                            (item.ZFixed&0xffff) != rom.Word(slot+0xe) || (item.SpeedZ&0xffff) != rom.Word(slot+0x14) ||
                            item.ShooterElevation != rom[slot+0x3e] || item.Angle*4 != rom[slot+9] ||
                            item.BouncesRemaining != rom[slot+0x34] || item.CollisionEnabled != ((rom[slot+0x24]&0x80) != 0) ||
                            SomariaPrivate<int>(item,"_frameCounter") != rom[slot+0x20] || state == 3 && item.FlameCounter != rom[slot+6],
                            "Orb ITEM$20 state/XY/Z/collision/animation differs.");
                    }
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                        "Orb and bridge handoff must preserve global RNG order.");
                });
            }
            _dialogue.ShowGameplayMessage("Pending orb initialization",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0; Step(3);
            Step(25,angle:24,feather:true); Step(5,attack:true,angle:20); Step();
            for (int wait = 0; !orb.PendingHit && wait < 64; wait++) Step();
            FailIf(!orb.PendingHit || controller.Finished,"Real item contact must publish a pending orb hit before the bridge handoff.");
            _dialogue.ShowGameplayMessage("Pending orb contact",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0; Step();
            FailIf(!controller.Finished || !orb.IsOn || sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1,
                "Next eligible PART pass must toggle the orb before INTERAC$dc:$12 allocates its pending bridge and latches flag$40.");
            Step(7);
            byte before = _currentRoom.GetMetatile(Point(0x13));
            byte filler = _currentRoom.GetMetatile(Point(0x11));
            for (int index = 0; index < 31; index++)
            {
                FailIf(!_rooms.TrySetTile(0x11,filler),"Bridge queue fixture must accept31 writes.");
                rom.SetTile(0x11,filler);
            }
            Step();
            FailIf(_currentRoom.GetMetatile(Point(0x13)) != before || _currentRoom.GetUnderlyingMetatile(Point(0x13)) != 0x6e,
                "Full queue must reject the first bridge half's live write while retaining its prior underlying-buffer write.");
            Step(7);
            FailIf(_currentRoom.GetMetatile(Point(0x13)) != before,"Draining graphics must not retry the rejected bridge half.");
            Step();
            FailIf(_currentRoom.GetMetatile(Point(0x13)) != 0x6d,"Next scheduled bridge half must publish the full tile.");
            for (int wait = 0; _entities.Entities<BridgeSpawnerRoomEntity>().Count != 0 && wait < 88; wait++) Step();
            FailIf(Enumerable.Range(0x13,6).Any(p => _currentRoom.GetMetatile(Point(p)) != 0x6d) ||
                sounds.Requests.Count(cue => cue == SoundId.SndDoorClose) != 12,
                "All twelve original half-tile updates must finish the six-tile bridge.");
            Step(8);
            _player.WarpTo(start); _player.Face(Vector2I.Left);
            rom.Word(0xd00a,36*256+128); rom.Word(0xd00c,112*256+64); rom[0xd008] = 3;
            Step(25,angle:24,feather:true); Step(5,attack:true,angle:20); Step();
            for (int wait = 0; orb.IsOn && wait < 64; wait++) Step();
            FailIf(orb.IsOn || _entities.Entities<BridgeSpawnerRoomEntity>().Count != 0 ||
                sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1,
                "Repeated real contact must turn the orb off without restarting its completed bridge.");
            FailIf(!OracleSaveData.TryDeserialize(_saveData.Serialize(),out var restored) ||
                !restored!.HasRoomFlag(2,0x9e,OracleSaveData.RoomFlag40),"Bridge flag$40 must survive the explicit save-image boundary.");
            LoadValidationRoom(2,0x9f); LoadValidationRoom(2,0x9e);
            FailIf(_runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) != 0 ||
                Enumerable.Range(0x13,6).Any(p => _currentRoom.GetMetatile(Point(p)) != 0x6d),
                "Re-entry must clear the toggle byte and reconstruct all six completed bridge tiles before actors run.");
            StepGameplayUpdates(2,Vector2.Zero,batched:batch);
            FailIf(_entities.Entities<OrbBridgeControllerRoomEntity>().Count != 0 || _entities.Entities<BridgeSpawnerRoomEntity>().Count != 0,
                "Flag$40 must retire the reloaded controller without replaying bridge construction.");
        }
    }
}
