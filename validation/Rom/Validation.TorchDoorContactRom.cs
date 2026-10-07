using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareTorchDoorContactRom()
    {
        int fixture = 0;
        foreach (bool shooter in new[] { false,true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0xce); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.SeedSatchel,1);
            if (shooter) _inventory.GiveTreasure(TreasureId.Shooter,1);
            _inventory.GiveTreasure(TreasureId.EmberSeeds,5); _inventory.SelectSatchelSeeds(0);
            _inventory.SelectShooterSeeds(0);
            _inventory.EquipA(shooter ? TreasureId.Shooter : TreasureId.SeedSatchel); _inventory.EquipB(0);
            // Use the production factory's shared torch owner and callbacks.
            // Isolate only the source door and scanner from the room's NPCs,
            // diamond and treasure; this is a bounded PART/item/door handoff.
            var placed = SomariaPrivate<RoomEntityFactory>(_entities,"_factory")
                .CreateRoomEntities(4,_currentRoom,EnemyPlacementContext.Unrestricted).ToArray();
            foreach (var entity in placed)
                if (entity is DungeonDoorRoomEntity or LightableTorchScannerRoomEntity) _entities.AddEntity(entity);
                else entity.Node.Free();
            var door = _entities.Entities<DungeonDoorRoomEntity>().Single();
            var scanner = _entities.Entities<LightableTorchScannerRoomEntity>().Single();
            var state = SomariaPrivate<LightableTorchState>(scanner,"_state");
            FailIf(door.SubId != 0x14 || door.PackedPosition != 7 || _entities.InteractionSlot(door) != 2 ||
                _entities.InteractionSlot(scanner) != 3 || _currentRoom.Layout[0x16] != 8 || _currentRoom.Layout[0x18] != 8,
                "Original room$4:$ce must retain its two source torches, common door and scanner order.");
            _player.WarpTo(new(120.25f,24.5f)); _player.Face(Vector2I.Left);
            FailIf(_collision.Collides(_player.Position),"Ember use must start on the original floor between the two torches.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,3,120,24) { HostilePartsEnabled = true };
            rom.Word(0xd00a,24*256+128); rom.Word(0xd00c,120*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd240] = 1; rom[0xd241] = 0x1e; rom[0xd242] = 0x14; rom[0xd24b] = 7;
            rom[0xd340] = 1; rom[0xd341] = 0xc7; rom[0xd342] = 8; rom[0xd34b] = 6; rom[0xd34d] = 0x10;
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1,bool fire = false) => StepSomariaMotionRom(rom,count,batch,
                held:fire ? 1 : 0,pressed:fire ? 1 : 0,afterUpdate:() => {
                    rom.AdvanceTileGraphics();
                    var random = _random.CaptureState();
                    int nativeTorches = Enumerable.Range(0xd0,8).Count(page => rom[(page<<8)|0xc0] != 0 && rom[(page<<8)|0xc1] == 6);
                    var torches = _entities.Entities<LightableTorchRoomEntity>();
                    foreach (var torch in torches)
                    {
                        int slot = Enumerable.Range(0xd0,8).Select(page => (page<<8)|0xc0)
                            .Single(slot => rom[slot] != 0 && rom[slot+1] == 6 && rom[slot+0xd] == torch.Position.X);
                        FailIf(torch.Initialized != (rom[slot+4] != 0) || torch.HitPending != ((rom[slot+0x2a]&0x80) != 0),
                            "Actual Ember contact must retain native torch initialization and pending-hit phase.");
                    }
                    var seeds = _entities.Entities<EmberSeedEffect>();
                    int[] nativeSeeds = Enumerable.Range(0xd7,5).Select(page => page<<8)
                        .Where(slot => rom[slot] != 0 && rom[slot+1] == 0x20).ToArray();
                    FailIf(seeds.Count != nativeSeeds.Length || _saveData.ReadWramByte(WramAddress.wNumEmberSeeds) != rom[0xc6b9],
                        "Torch contact must preserve real seed allocation/retirement and BCD inventory debit.");
                    foreach (var item in seeds)
                    {
                        int slot = nativeSeeds.Single(slot => slot>>8 == _entities.DynamicItemSlotOf(
                            _entities.EntityAdapters<EmberSeedRoomEntity>().Single(adapter => ReferenceEquals(adapter.Node,item))));
                        int nativeState = item.State == EmberState.Flying ? 1 : 3;
                        FailIf(nativeState != rom[slot+4] || item.PrecisePosition != new Vector2(rom.Word(slot+0xc)/256f,rom.Word(slot+0xa)/256f) ||
                            (item.ZFixed&0xffff) != rom.Word(slot+0xe) || (item.SpeedZ&0xffff) != rom.Word(slot+0x14) ||
                            item.CollisionEnabled != ((rom[slot+0x24]&0x80) != 0) ||
                            SomariaPrivate<int>(item,"_frameCounter") != rom[slot+0x20] || nativeState == 3 && item.FlameCounter != rom[slot+6],
                            $"Torch seed state/full XY/Z/collision differs: runtime={nativeState}/{item.PrecisePosition}/{item.ZFixed}/{item.CollisionEnabled}; native={rom[slot+4]}/{rom.Word(slot+0xc)/256f},{rom.Word(slot+0xa)/256f}/{rom.Word(slot+0xe)}/${rom[slot+0x24]:x2}.");
                    }
                    FailIf(state.LitCount != rom[0xcc8f] || _entities.Entities<LightableTorchRoomEntity>().Count != nativeTorches ||
                        door.Finished != (rom[0xd240] == 0) || SomariaPrivate<int>(door,"_counter") != rom[0xd246] ||
                        _entities.BossEntrySignal != rom[0xcc93] || !sounds.Requests.SequenceEqual(rom.Sounds) ||
                        random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                        $"Actual torch contact batch={batch}: runtime torches={_entities.Entities<LightableTorchRoomEntity>().Count}/lit={state.LitCount}/door={SomariaPrivate<DoorState>(door,"_state")}/counter={SomariaPrivate<int>(door,"_counter")}; native torches={nativeTorches}/lit={rom[0xcc8f]}/door=${rom[0xd244]:x2}:${rom[0xd245]:x2}/counter={rom[0xd246]}.");
                });
            Step(12);
            FailIf(state.TotalTorches != 2 || state.LitCount != 0 || !scanner.Finished ||
                !_entities.Entities<LightableTorchRoomEntity>().Select(torch => torch.PackedPosition).Order().SequenceEqual(new[] { 0x16,0x18 }),
                "Actual source scanner must create exactly PART$06:$00 at$16/$18 before Ember use.");
            Step(fire:true); Step(24);
            FailIf(state.LitCount != 1 || _currentRoom.Layout[0x16] != 9 || _currentRoom.Layout[0x18] != 8 ||
                sounds.Requests.Contains(SoundId.SndSolvePuzzle),
                "First actual Ember must light only the left source torch and leave the door unsolved.");
            _player.Face(Vector2I.Right); rom[0xd008] = 1;
            Step(fire:true); Step(24);
            FailIf(state.LitCount != 2 || _currentRoom.Layout[0x18] != 9,
                "Repeated Ember use must light the second original torch through the ordered collision/next PART handoff.");
            Step(40); Step(3);
            FailIf(!door.Finished || _currentRoom.Layout[7] != 0xa0 || _currentRoom.IsSolid(door.Position) ||
                sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1 ||
                sounds.Requests.Count(cue => cue == SoundId.SndDoorClose) != 2,
                "The actual shared torch count must release the door after wait30 once, without repeated solve/open commands.");
        }
    }
}
