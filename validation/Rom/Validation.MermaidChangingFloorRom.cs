using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidChangingFloorRom()
    {
        const BindingFlags flags = BindingFlags.Instance|BindingFlags.NonPublic;
        foreach (bool batch in new[] { false,true })
        {
            ReinitializeGameplayForValidation();
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,0);
            LoadValidationRoom(5,0x1a);
            var controller = _entities.Entities<MermaidChangingFloorRoomEntity>().Single();
            var orb = _entities.Entities<DungeonOrbRoomEntity>().Single();
            FailIf(orb.Position != new Vector2(0x78,0x58),"Mermaid room$5:$1a must place its orb at packed$57 after the floor controller.");
            var active = SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity",flags)!;
            foreach (var entity in active.Where(entity => entity != controller && entity != orb).ToArray())
            { active.Remove(entity); free.Invoke(_entities,[entity]); }
            _inventory.GiveTreasure(TreasureId.Shooter,1);
            _inventory.GiveTreasure(0x20,0x20); _inventory.SelectShooterSeeds(0);
            _inventory.EquipA(0); _inventory.EquipB(TreasureId.Shooter);
            _player.WarpTo(new(0xd8,0x58)); _player.Face(Vector2I.Left);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
                "Mermaid changing floor must be shot from the original reachable east floor$5d.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,3,0xd8,0x58) { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay(); rom[0xd009] = 0xff; rom[0xcc39] = 0x0c;
            int p = 0xd040+_entities.InteractionSlot(controller)*256;
            rom[p] = 1; rom[p+1] = 0x90; rom[p+2] = 4;
            var partSlots = SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots");
            int o = 0xd0c0+partSlots[orb]*256;
            rom[o] = 1; rom[o+1] = 3; rom[o+11] = 0x58; rom[o+13] = 0x78;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,bool held = false,bool press = false,int angle = 0xff)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle);
                int keys = angle == 24 ? 0x20 : angle == 8 ? 0x10 : 0;
                int edge = press ? 2 : 0;
                StepGameplayUpdates(count,movement,MenuRomActions(keys|(held?2:0)),MenuRomActions(edge),batch,() =>
                {
                    rom.UpdateGameplay(edge,keys|(held?2:0),angle,_entities.FrameCounter); edge = 0; update++;
                    rom.AdvanceTileGraphics();
                    string context = $"Mermaid changing floor update{update}, batch={batch}";
                    CompareSomariaMotionRom(rom,context);
                    FailIf(_runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
                        _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != rom[0xcc02] ||
                        _runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) != rom[0xcdd2],
                        context+$": source freeze/menu/orb bytes differ, runtime=${_runtimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}/${_runtimeState.ReadWramByte(WramAddress.wMenuDisabled):x2}/${_runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress):x2}, ROM=${rom[0xcc8a]:x2}/${rom[0xcc02]:x2}/${rom[0xcdd2]:x2}, controller=${rom[p+4]:x2}:${rom[p+6]:x2}/${rom[p+7]:x2}.");
                    var workers = _entities.Entities<MermaidChangingFloorWorkerRoomEntity>().Where(worker => !worker.Finished).ToArray();
                    var nativeWorkers = Enumerable.Range(0xd2,14).Select(page => (page<<8)+0x40)
                        .Where(a => rom[a] != 0 && rom[a+1] == 0x90 && rom[a+2] is 5 or 6).ToArray();
                    FailIf(workers.Length != nativeWorkers.Length,context+": checked worker allocations/deletion differ.");
                    foreach (var worker in workers)
                    {
                        int a = 0xd040+_entities.InteractionSlot(worker)*256;
                        FailIf(rom[a+2] != worker.SubId || worker.Initialized != (rom[a+5] == 1) ||
                            worker.PackedPosition != rom[a+0x30] || worker.YStep != rom[a+0x31] || worker.BufferOffset != rom[a+0x33],
                            context+$": INTERAC$90:${worker.SubId:x2} physical slot/substate/serpentine cursor differs.");
                    }
                    for (int index = 0; index < 256; index++)
                        FailIf(_runtimeState.ReadWramByte(WramAddress.wBigBuffer+index) != rom[0xc300+index],context+$": live shared floor buffer byte${index:x2} differs.");
                    for (int index = 0; index < 0xb0; index++)
                    {
                        Vector2 point = new((index&15)*16+8,(index>>4)*16+8);
                        FailIf(_currentRoom.GetPackedStorageMetatile((byte)index) != rom[0xcf00+index] ||
                            (index&15) < 15 && _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00+index],
                            context+$": cell${index:x2} floor/solidity differs.");
                    }
                    FailIf(orb.IsOn != (rom[0xcdd2] != 0) || orb.PendingHit != ((rom[o+0x2a]&0x80) != 0) ||
                        !sounds.Requests.SequenceEqual(rom.Sounds),context+": orb handoff or ordered cues differ.");
                    var rng = _random.CaptureState();
                    FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls-seed.Calls != rom.RandomCalls,
                        context+": global RNG differs.");
                });
            }
            Step(2);
            for (int activation = 0; activation < 2; activation++)
            {
                Step(1,true,true,24); Step(4,true); Step();
                for (int wait = 0; !orb.PendingHit && wait < 80; wait++) Step();
                FailIf(!orb.PendingHit,"Actual Mermaid Seed Shooter must reach the orb across its original pit geometry.");
                Step();
                FailIf(_entities.Entities<MermaidChangingFloorWorkerRoomEntity>().Count != 2 || rom[p+6] != 1-activation ||
                    rom[0xcc8a] != 0xff || rom[0xcc02] != 0xff,
                    "The part pass must toggle, allocate both workers and copy the selected source pattern before their same-pass initialization.");
                // Native workers remain state0: text freezes Link and the orb,
                // while the serpentine tile sweep continues one cell per pass.
                Vector2 heldPosition = _player.PrecisePosition;
                _dialogue.ShowGameplayMessage("Pending floor sweep",120); rom[0xcba0] = 1;
                Step(4,angle:8); _dialogue.Close(); rom[0xcba0] = 0;
                FailIf(_player.PrecisePosition != heldPosition,"Floor/text masks must retain Link during the workers' sweep.");
                Step(54,angle:8);
                FailIf(rom[0xcc8a] != 0xff || _player.PrecisePosition != heldPosition,
                    "All 58 source cells per worker must be written before the terminating pass releases the freeze.");
                Step(angle:8);
                FailIf(_entities.Entities<MermaidChangingFloorWorkerRoomEntity>().Count != 0 || rom[0xcc8a] != 0 || rom[0xcc02] != 0 ||
                    _player.PrecisePosition != heldPosition || rom[0xcf71] != (activation == 0 ? 0xf4 : 0xa0) ||
                    rom[0xcf3d] != (activation == 0 ? 0xf4 : 0xa0) || rom[0xcf57] != 0x0a,
                    "Worker termination must release shared masks after Link's pass, preserve the orb and select independent source state1/state0 endpoints.");
                Step(4,angle:8);
                FailIf(_player.PrecisePosition.X <= heldPosition.X,"Link must regain movement on the following gameplay update.");
                Step(4,angle:24); Step(20);
            }
        }
        GD.Print("Validated clean-US Mermaid actual Seed Shooter/orb floor handoff, both source patterns, physical worker order/shared buffer, 58-cell sweeps, text and mask release, restored movement and repeat through split/batched gameplay.");
    }
}
