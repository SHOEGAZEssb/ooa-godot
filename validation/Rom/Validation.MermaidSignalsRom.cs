using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidBridgeRom()
    {
        foreach (bool batch in new[] { false,true })
        foreach (bool full in new[] { false,true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5,0x38);
            var script=_entities.Entities<DungeonSignalScriptRoomEntity>().Single();
            var orb=_entities.Entities<DungeonOrbRoomEntity>().Single();
            var active=SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free=typeof(RoomEntityManager).GetMethod("FreeEntity",BindingFlags.Instance|BindingFlags.NonPublic)!;
            // Isolate the actual orb/script/bridge handoff; room enemies and
            // arrow shooters have separate combat/placement regressions.
            foreach (var actor in active.Where(actor => actor != script && actor != orb).ToArray())
            { active.Remove(actor); free.Invoke(_entities,[actor]); }
            _inventory.GiveTreasure(TreasureId.Sword,1); _inventory.EquipA(TreasureId.Sword); _inventory.EquipB(0);
            _inventory.GiveTreasure(TreasureId.Bracelet,1); _inventory.EquipB(TreasureId.Bracelet);
            _player.WarpTo(new(120,40)); _player.Face(Vector2I.Left);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetPackedStorageMetatile(0x26) != 0x11,
                "Mermaid orb approach must begin on original floor east of the source pot$26.");
            // Use the existing Bracelet through gameplay to clear the two
            // pots on the east approach. Tile-lift parity is covered by the Bracelet regressions;
            // the native bridge fixture begins after this prerequisite.
            _runtimeState.SetWramByte(0xcfc1,0x91); _runtimeState.SetWramByte(0xcfc2,0xd2);
            StepGameplayUpdates(8,Vector2.Left,batched:batch);
            StepGameplayUpdates(1,Vector2.Zero,["item"],["item"],batch);
            StepGameplayUpdates(11,Vector2.Right,["item"],[],batch);
            StepGameplayUpdates(13,Vector2.Zero,batched:batch);
            FailIf(!_player.IsCarryingObject || _currentRoom.GetPackedStorageMetatile(0x26) != 0xa0,
                "Actual Bracelet pickup must remove pot$26 using its source floor replacement.");
            StepGameplayUpdates(1,Vector2.Up,batched:batch);
            StepGameplayUpdates(1,Vector2.Zero,["item"],["item"],batch);
            StepGameplayUpdates(40,Vector2.Zero,batched:batch);
            for (int walk=0;_player.Position.X>104 && walk<20;walk++) StepGameplayUpdates(1,Vector2.Left,batched:batch);
            for (int walk=0;_player.Position.Y<40 && walk<20;walk++) StepGameplayUpdates(1,Vector2.Down,batched:batch);
            StepGameplayUpdates(1,Vector2.Down,batched:batch);
            StepGameplayUpdates(1,Vector2.Zero,["item"],["item"],batch);
            StepGameplayUpdates(11,Vector2.Up,["item"],[],batch);
            StepGameplayUpdates(13,Vector2.Zero,batched:batch);
            FailIf(!_player.IsCarryingObject || _currentRoom.GetPackedStorageMetatile(0x36) != 0xa0,
                $"Actual Bracelet pickup must also clear source pot$36 east of the orb: Link={_player.PrecisePosition}, carry={_player.IsCarryingObject}, tile=${_currentRoom.GetPackedStorageMetatile(0x36):x2}.");
            StepGameplayUpdates(1,Vector2.Up,batched:batch);
            StepGameplayUpdates(1,Vector2.Zero,["item"],["item"],batch);
            StepGameplayUpdates(40,Vector2.Zero,batched:batch);
            for (int walk=0;_player.Position.Y<56 && walk<30;walk++) StepGameplayUpdates(1,Vector2.Down,batched:batch);
            _player.Face(Vector2I.Left);
            FailIf(_player.Position != new Vector2(104,56) || _collision.Collides(_player.Position),
                "The cleared source pots must admit Link on original floor east of the solid orb.");
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,3,104,56) { HostilePartsEnabled=true };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39]=0x0c;
            int s=0xd040+_entities.InteractionSlot(script)*256;
            FailIf(s != 0xd240,"Source room$5:$38 must reserve INTERAC$20:$01 first.");
            rom[s]=1; rom[s+1]=0x20; rom[s+2]=1;
            var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots");
            int o=0xd0c0+slots[orb]*256;
            rom[o]=1; rom[o+1]=3; rom[o+11]=56; rom[o+13]=88;
            var reservations=new List<EnemyAiSlotReservation>();
            if (full) for (int slot=0;slot<16;slot++) if (slot != slots[orb])
            {
                var reserve=new EnemyAiSlotReservation(); reservations.Add(reserve); slots.Add(reserve,slot);
                int a=0xd0c0+slot*256; rom[a]=1; rom[a+1]=0x0a; rom[a+4]=1;
            }
            rom[0xcfc1]=0x91; rom[0xcfc2]=0xd2;
            rom.AdvanceInteractions(_entities.FrameCounter);
            FailIf(rom[0xcfc1] != 0 || rom[0xcfc2] != 0 || _runtimeState.ReadWramByte(0xcfc1) != 0 ||
                _runtimeState.ReadWramByte(0xcfc2) != 0,"Native and actual gameplay state0 must clear both shared script signals.");
            var sounds=_sound.AttachPlayRequestAudit(); int update=0;
            void Step(int count=1,bool attack=false,int angle=0xff)
            {
                Vector2 movement=angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int directions=(movement.X<0 ? 0x20 : movement.X>0 ? 0x10 : 0)|(movement.Y<0 ? 0x40 : movement.Y>0 ? 0x80 : 0);
                int held=(attack ? 1 : 0)|directions, tick=0;
                StepGameplayUpdates(count,movement,MenuRomActions(held),attack ? ["attack"] : [],batch,() => {
                rom.UpdateGameplay(attack && tick++ == 0 ? 1 : 0,held,angle,_entities.FrameCounter);
                CompareSomariaMotionRom(rom,$"Mermaid bridge full={full}, batch={batch}, update{update+1}");
                rom.AdvanceTileGraphics(); string context=$"Mermaid bridge full={full}, batch={batch}, update{++update}";
                FailIf(script.Finished != (rom[s] == 0) || script.Initialized != (rom[s+4] != 0) && rom[s] != 0 ||
                    _runtimeState.ReadWramByte(0xcfc1) != rom[0xcfc1] || _runtimeState.ReadWramByte(0xcfc2) != rom[0xcfc2] ||
                    _runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) != rom[0xcdd2] ||
                    _saveData.GetRoomFlags(5,0x38) != rom[0xca38] ||
                    orb.PendingHit != ((rom[o+0x2a]&0x80) != 0) || orb.HitLockout != -unchecked((sbyte)rom[o+0x2b]),
                    context+": script lifecycle/shared writes/orb contact differs.");
                var parts=_entities.Entities<BridgeSpawnerRoomEntity>();
                int[] native=Enumerable.Range(0,16).Select(slot => 0xd0c0+slot*256)
                    .Where(a => rom[a] != 0 && rom[a+1] == 0x0c).ToArray();
                FailIf(parts.Count != native.Length,context+": source bridge allocation differs.");
                foreach (var part in parts)
                {
                    int a=0xd0c0+slots[part]*256;
                    FailIf(!native.Contains(a) || SomariaPrivate<bool>(part,"_initialized") != (rom[a+4] != 0) ||
                        SomariaPrivate<int>(part,"_counter") != rom[a+6] || SomariaPrivate<int>(part,"_remaining") != rom[a+7] ||
                        SomariaPrivate<int>(part,"_position") != rom[a+11],context+": ordered bridge half-step/clock differs.");
                }
                var rng=_random.CaptureState();
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls-seed.Calls != rom.RandomCalls ||
                    !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),context+": RNG/ordered cues differ.");
                });
            }
            try
            {
                // Other toggle bits must not satisfy checkflagset bit0.
                _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,0x80); rom[0xcdd2]=0x80;
                Step(3);
                FailIf(script.Finished || _saveData.HasRoomFlag(5,0x38,0x40),"Mermaid bridge must wait for bit0 specifically.");
                Step(attack:true);
                for (int wait=0;!script.Finished && wait<60;wait++) Step();
                FailIf(!script.Finished || !_saveData.HasRoomFlag(5,0x38,0x40) ||
                    sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1,
                    "Actual Sword/orb contact must publish flag$40 and Solve before its one allocation attempt.");
                if (!full)
                {
                    Step(8);
                    _dialogue.ShowGameplayMessage("Bridge pause",120); rom[0xcba0]=1; Step(3);
                    _dialogue.Close(); rom[0xcba0]=0; Step(58);
                    FailIf(_entities.Entities<BridgeSpawnerRoomEntity>().Count != 0 ||
                        new[] { 0x39,0x49,0x59,0x69 }.Any(p => _currentRoom.GetPackedStorageMetatile((byte)p) != 0x6a),
                        "Eight northward half-steps must complete the four original vertical bridge tiles.");
                }
                else
                {
                    foreach (var reserve in reservations) { int slot=slots[reserve]; slots.Remove(reserve); reserve.Node.Free(); rom[0xd0c0+slot*256]=0; }
                    reservations.Clear(); Step(10);
                    FailIf(_entities.Entities<BridgeSpawnerRoomEntity>().Count != 0,
                        "Unlike DC:$12, the completed $20 bridge script must not retry a full-pool failure.");
                }
                // Execute the original flag-conditioned restoration separately
                // from runtime re-entry, starting from literal source holes.
                foreach (int p in new[] { 0x39,0x49,0x59,0x69 }) rom[0xcf00+p]=0xf4;
                rom.ApplyRoomTileSubstitutions();
                LoadValidationRoom(5,0x39); LoadValidationRoom(5,0x38);
                foreach (int p in new[] { 0x39,0x49,0x59,0x69 })
                    FailIf(rom[0xcf00+p] != 0x6a || _currentRoom.GetPackedStorageMetatile((byte)p) != rom[0xcf00+p],
                        "Flag$40 must independently reconstruct the full source bridge on re-entry, including interrupted/failed construction.");
                StepGameplayUpdates(2,Vector2.Zero,batched:batch);
                FailIf(_entities.Entities<DungeonSignalScriptRoomEntity>().Count != 0 || _entities.Entities<BridgeSpawnerRoomEntity>().Count != 0,
                    "The source bridge script must stop on its saved flag without another builder.");
            }
            finally { foreach (var reserve in reservations) { slots.Remove(reserve); reserve.Node.Free(); } _dialogue.Close(); }
        }
        GD.Print("Validated clean-US Mermaid orb/bridge actual Sword contact, script carry/order, full-pool failure, timed northward half-steps, text pause and flag-backed re-entry in split/batched gameplay.");
    }

    private void ValidateMermaidConjunctionRom()
    {
        foreach (bool batch in new[] { false,true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5,0x3d);
            var script=_entities.Entities<DungeonSignalScriptRoomEntity>().Single();
            var door=_entities.Entities<DungeonDoorRoomEntity>().Single();
            var buttons=_entities.Entities<GroundButtonRoomEntity>().OrderBy(actor => actor.SubId).ToArray();
            var active=SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free=typeof(RoomEntityManager).GetMethod("FreeEntity",BindingFlags.Instance|BindingFlags.NonPublic)!;
            foreach (var actor in active.Where(actor => actor != script && actor != door && actor is not GroundButtonRoomEntity).ToArray())
            { active.Remove(actor); free.Invoke(_entities,[actor]); }
            FailIf(buttons.Length != 2 || _entities.InteractionSlot(door) != 2 || _entities.InteractionSlot(script) != 3,
                "Room$5:$3d must preserve door-before-conjunction source order and both pressure buttons.");
            _inventory.GiveTreasure(TreasureId.CaneOfSomaria,1); _inventory.EquipA(TreasureId.CaneOfSomaria); _inventory.EquipB(0);
            _player.WarpTo(new(72,72)); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(_player.Position),"Conjunction fixture must begin on the source floor west of button$45.");
            _entities.SetTrigger(7,true);
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,1,72,72) { HostilePartsEnabled=true };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39]=0x0c; rom[0xcca0]=0x80;
            rom[0xd240]=1; rom[0xd241]=0x1e; rom[0xd242]=4; rom[0xd24b]=7; rom[0xd24d]=2;
            rom[0xd340]=1; rom[0xd341]=0x20; rom[0xd342]=2;
            var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots");
            foreach (var button in buttons)
            {
                int a=0xd0c0+slots[button]*256;
                rom[a]=1; rom[a+1]=9; rom[a+2]=(byte)button.SubId;
                rom[a+11]=(byte)((button.PackedPosition>>4)*16+8); rom[a+13]=(byte)((button.PackedPosition&15)*16+8);
            }
            var sounds=_sound.AttachPlayRequestAudit(); int update=0;
            void Step(int count=1,int angle=0xff,bool cane=false) => StepSomariaMotionRom(rom,count,batch,angle,
                cane ? 1 : 0,cane ? 1 : 0,afterUpdate:() => {
                rom.AdvanceTileGraphics(); string context=$"Mermaid conjunction batch={batch}, update{++update}";
                FailIf(_entities.ActiveTriggers != rom[0xcca0] || script.Counter != rom[0xd346] ||
                    script.Initialized != (rom[0xd344] != 0) || script.Finished != (rom[0xd340] == 0) ||
                    door.Finished != (rom[0xd240] == 0) || !door.Finished &&
                        (SomariaPrivate<int>(door,"_counter") != rom[0xd246] || door.Counter2Alias != rom[0xd247]),
                    context+$": script/door clock or shared trigger handoff differs (${_entities.ActiveTriggers:x2}/${rom[0xcca0]:x2}).");
                foreach (var button in buttons)
                {
                    int a=0xd0c0+slots[button]*256;
                    FailIf(button.Pressed != (rom[a+0x30] != 0) || button.ReleaseCounter != rom[a+6],context+": pressure/button release differs.");
                }
                var rng=_random.CaptureState();
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls-seed.Calls != rom.RandomCalls ||
                    !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),context+": shared RNG/ordered cues differ.");
            });
            Step(); Step(cane:true); Step(26);
            FailIf(_entities.ActiveTriggers != 0x81 || _currentRoom.GetPackedStorageMetatile(0x07) != 0x78,
                "The actual Cane must cover only button$45; one input must leave trigger2 and the shutter closed.");
            Step(80,16);
            for (int walk=0;_player.Position.X<168 && walk<110;walk++) Step(angle:8);
            for (int walk=0;_player.Position.Y>72 && walk<90;walk++) Step(angle:0);
            for (int walk=0;(_entities.ActiveTriggers&2) == 0 && walk<40;walk++) Step(angle:24);
            FailIf(_entities.ActiveTriggers != 0x87 || _currentRoom.GetPackedStorageMetatile(0x07) != 0x78,
                "Link's actual approach to button$49 must set trigger2 after the earlier door dispatch, preserving bit7.");
            Step();
            _dialogue.ShowGameplayMessage("Conjunction pause",120); rom[0xcba0]=1; Step(3,16);
            _dialogue.Close(); rom[0xcba0]=0;
            Step(18);
            FailIf(door.Finished || _currentRoom.GetPackedStorageMetatile(0x07) != 0xa0 || _entities.ActiveTriggers != 0x87,
                "Both held buttons must open the reusable source shutter without retiring its controller.");
            Step(24,8); Step(16);
            FailIf(door.Finished || _currentRoom.GetPackedStorageMetatile(0x07) != 0x78 || _entities.ActiveTriggers != 0x81,
                "Releasing right pressure must clear only trigger2 and close the reusable source shutter.");
            Step(angle:16); Step(cane:true);
            FailIf(!_entities.Somaria!.Active,"The repeated real Cane action must start.");
            // Object pressure arms a $1c-update release delay. Allow the
            // replacement swing and that existing shared button timer.
            Step(46);
            FailIf(_entities.ActiveTriggers != 0x80,"Replacing the actual Cane block must release button$45 after its source delay and retain unrelated trigger bits.");
            for (int walk=0;(_entities.ActiveTriggers&2) == 0 && walk<40;walk++) Step(angle:24);
            FailIf(_entities.ActiveTriggers != 0x82 || _currentRoom.GetPackedStorageMetatile(0x07) != 0x78,
                "Repeating the right-button action with left pressure canceled must leave trigger2 clear and the shutter closed.");
            LoadValidationRoom(5,0x3e); LoadValidationRoom(5,0x3d);
            FailIf(_entities.ActiveTriggers != 0 || _entities.EntityAdapters<SomariaBlockRoomEntity>().Count() != 0 ||
                _currentRoom.GetPackedStorageMetatile(0x07) != 0x78,
                "Re-entry must clear temporary buttons/Cane state and restore the original unpersisted shutter.");
        }
        GD.Print("Validated clean-US Mermaid two-button conjunction with actual Cane/Link pressure, ordered shutter handoff, pause, cancellation/repeat and re-entry through split/batched gameplay.");
    }
}
