using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidEraWallsRom()
    {
        const BindingFlags flags = BindingFlags.Instance|BindingFlags.NonPublic;
        foreach (bool batch in new[] { false,true })
        foreach (bool east in new[] { false,true })
        {
            ReinitializeGameplayForValidation();
            int room = east ? 0x44 : 0x3a, target = east ? 0x26 : 0x19, subid = east ? 9 : 8;
            byte mask = east ? (byte)2 : (byte)1, opposite = east ? (byte)8 : (byte)4;
            int angle = east ? 8 : 0; Vector2I direction = east ? Vector2I.Right : Vector2I.Up;
            LoadValidationRoom(5,room);
            var mirror = _entities.Entities<DungeonRoomFlagMirrorRoomEntity>().Single();
            var placement = new MermaidDungeonDatabase().GetRoomRecords(5,room).Single();
            var profile = new DungeonRoomFlagMirrorDatabase().Profile(placement);
            FailIf(profile.Flag != mask || profile.TargetRoom != target || placement.Order != (east ? 2 : 0),
                "D6 source mirrors require $08:north->519 and $09:east->526 in original placement order.");
            var active = SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity",flags)!;
            foreach (var actor in active.Where(actor => actor != mirror).ToArray())
            { active.Remove(actor); free.Invoke(_entities,[actor]); }
            byte closed = east ? (byte)0x31 : (byte)0x38, opened = east ? (byte)0x35 : (byte)0x34;
            int packed = Enumerable.Range(0,0xb0).Single(index => _currentRoom.GetPackedStorageMetatile((byte)index) == closed);
            Vector2 center = new((packed&15)*16+8,(packed>>4)*16+8);
            Vector2 start = center-(Vector2)direction*48;
            _player.WarpTo(start); _player.Face(direction);
            FailIf(_collision.Collides(start) || _currentRoom.GetTerrainInfo(start).Hazard != HazardType.None,
                $"D6 wall$5:${room:x2} must have an original safe floor approach to${packed:x2}.");
            FailIf(!_rooms.TryGetNeighbor(direction,out int neighbor),"Bomb wall requires an imported dungeon-map neighbor.");
            // Prime the destination cache before the flag is published.
            OracleRoomData cachedTarget = _rooms.GetRoom(5,target);
            byte[] targetLayout = cachedTarget.Layout.ToArray();
            // Independent singleTileChangeGroup5Data rows, rather than a
            // search through the imported changes consumed by RoomSession.
            int targetPacked = east ? 0x5e : 0x08;
            FailIf(cachedTarget.GetPackedStorageMetatile((byte)targetPacked) != (east ? 0xb1 : 0xb0),
                "Unopened present-era passage must retain its original solid wall until its single-tile flag write.");
            _saveData.SetRoomFlag(5,target,0x10); // Preserve an unrelated visited bit.
            _saveData.SetRoomFlag(4,target,mask); // Another flags page cannot satisfy it.
            _inventory.GiveTreasure(TreasureId.Bombs,0x10); _inventory.EquipA(TreasureId.Bombs); _inventory.EquipB(0);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,angle/8,(int)start.X,(int)start.Y);
            rom.InitializeLinkGameplay(); rom.CreateMenuView().LoadDungeon(_rooms.CurrentDungeonIndex);
            rom[0xd009] = 0xff;
            int p = 0xd040+_entities.InteractionSlot(mirror)*256;
            rom[p] = 1; rom[p+1] = 0x90; rom[p+2] = (byte)subid;
            var cues = _sound.AttachPlayRequestAudit(); int previous = 0,update = 0;
            void Step(int count = 1,int movementAngle = 0xff,bool held = false)
            {
                int keys = (held ? 1 : 0) | (movementAngle switch { 0 => 0x40,8 => 0x10,16 => 0x80,24 => 0x20,_ => 0 });
                int edge = keys&~previous; previous = keys;
                Vector2 movement = movementAngle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(movementAngle);
                StepGameplayUpdates(count,movement,MenuRomActions(keys),MenuRomActions(edge),batch,() =>
                {
                    rom.UpdateGameplay(edge,keys,movementAngle,_entities.FrameCounter); edge = 0; update++;
                    rom.CheckTileWarps(); rom.AdvanceTileGraphics();
                    string context = $"Mermaid era wall$5:${room:x2} batch={batch} update{update}";
                    CompareSomariaMotionRom(rom,context);
                    FailIf(mirror.Finished != (rom[p] == 0),context+": mirror lifetime differs.");
                    for (int index = 0; index < 256; index++)
                        FailIf(_saveData.GetRoomFlags(5,index) != rom[0xca00+index],context+$": flags$5:${index:x2} differ.");
                    for (int index = 0; index < 0xb0; index++)
                    {
                        Vector2 point = new((index&15)*16+8,(index>>4)*16+8);
                        FailIf(_currentRoom.GetPackedStorageMetatile((byte)index) != rom[0xcf00+index] ||
                            (index&15) < 15 && _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00+index],
                            context+$": wall tile/collision${index:x2} differs.");
                    }
                    var rng = _random.CaptureState();
                    FailIf(!cues.Requests.SequenceEqual(rom.Sounds) || rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] ||
                        rng.Calls-seed.Calls != rom.RandomCalls || _inventory.Bombs != rom[0xc6b0],
                        context+": bomb debit, sounds or global RNG differs.");
                });
            }
            Step(2);
            FailIf(mirror.Finished || _saveData.GetRoomFlags(5,target) != 0x10,
                "Mirror must ignore another page and unrelated bits until the matching active-room flag is set.");
            Step(34,angle); Step(); Step(1,held:true); Step(19); Step(1,held:true); Step(8);
            FailIf(_entities.Entities<BombEffect>().Count != 1,"Actual A-button planting/drop must leave one native bomb beside the wall.");
            Step(32,angle^16); Step(100);
            FailIf(!mirror.Finished || _currentRoom.GetPackedStorageMetatile((byte)packed) != opened ||
                !_saveData.HasRoomFlag(5,room,mask) || !_saveData.HasRoomFlag(5,neighbor,opposite) ||
                _saveData.GetRoomFlags(5,target) != (0x10|mask),
                "Bomb break must publish original local/neighbor bits and mirror the era flag in the same object update.");
            Step(34,angle); Step(); Step(1,held:true); Step(19); Step(1,held:true); Step(8); Step(32,angle^16); Step(100);
            FailIf(_saveData.GetRoomFlags(5,target) != (0x10|mask),"Repeated bomb use must preserve the completed era link.");
            LoadValidationRoom(0,0x60); LoadValidationRoom(5,target);
            rom[0xcc30] = (byte)target; rom.CreateMenuView().LoadRoomTileset();
            rom.CreateMenuView().LoadDungeon(_rooms.CurrentDungeonIndex);
            for (int index = 0; index < targetLayout.Length; index++) rom[0xcf00+index] = targetLayout[index];
            rom.ApplyRoomTileSubstitutions();
            for (int index = 0; index < targetLayout.Length; index++)
                FailIf(_currentRoom.GetPackedStorageMetatile((byte)index) != rom[0xcf00+index],
                    $"Present D6 room$5:${target:x2} cached re-entry tile${index:x2} differs from original room substitutions.");
            FailIf(_currentRoom.GetPackedStorageMetatile((byte)targetPacked) != opened ||
                _currentRoom.GetCollision(opened) != _currentRoom.GetTerrainInfo(new((targetPacked&15)*16+8,(targetPacked>>4)*16+8)).Collision,
                "Present room re-entry must restore the source open passage even when its room was cached before bombing.");
            LoadValidationRoom(5,room); StepGameplayUpdates(1,Vector2.Zero,batched:batch);
            FailIf(_entities.Entities<DungeonRoomFlagMirrorRoomEntity>().Any(actor => !actor.Finished) ||
                _currentRoom.GetPackedStorageMetatile((byte)packed) != opened,
                "Past re-entry must retire the satisfied mirror and restore its bombed wall.");
        }
        // Declare only the object's immediate control inputs. Real bomb and
        // room-load publishers are exercised above; these cases isolate the
        // state-zero text/mask admission and enabled02 cancellation priority.
        foreach (bool batch in new[] { false,true })
        foreach (bool east in new[] { false,true })
        foreach (bool outgoing in new[] { false,true })
        {
            ReinitializeGameplayForValidation(); int room = east ? 0x44 : 0x3a,target = east ? 0x26 : 0x19;
            byte mask = east ? (byte)2 : (byte)1;
            LoadValidationRoom(5,room);
            var mirror = _entities.Entities<DungeonRoomFlagMirrorRoomEntity>().Single();
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity",flags)!;
            void KeepMirror()
            {
                var active = SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
                foreach (var actor in active.Where(actor => actor != mirror).ToArray())
                { active.Remove(actor); free.Invoke(_entities,[actor]); }
            }
            KeepMirror(); _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(0x78,0x78));
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x78); rom.InitializeLinkGameplay();
            int p = 0xd040+_entities.InteractionSlot(mirror)*256;
            rom[p] = 1; rom[p+1] = 0x90; rom[p+2] = east ? (byte)9 : (byte)8;
            StepGameplayUpdates(1,Vector2.Zero,batched:batch,afterUpdate:() => rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter));
            _dialogue.ShowGameplayMessage("Room-flag mirror admission",120); rom[0xcba0] = 1;
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,0xff); rom[0xcc8a] = 0xff;
            if (outgoing)
            {
                // The incoming room carries the right bit; the outgoing
                // owner must delete before following that active-room byte.
                _saveData.SetRoomFlag(5,0x1d,mask); rom[0xca1d] = mask;
                var destination = _rooms.GetRoom(5,0x1d);
                _entities.BeginScreenTransition(5,destination,Vector2.Zero,_player);
                _rooms.SetLoadedRoom(5,destination); KeepMirror();
                rom.SetOutgoingInteractions(); rom[0xcc30] = 0x1d; rom[0xcd00] = 4;
            }
            else { _saveData.SetRoomFlag(5,room,mask); rom[0xca00+room] |= mask; }
            var cues = _sound.AttachPlayRequestAudit();
            StepGameplayUpdates(4,Vector2.Zero,batched:batch,afterUpdate:() =>
            {
                rom.AdvanceInteractions(_entities.FrameCounter);
                FailIf(mirror.Finished != (rom[p] == 0) ||
                    _saveData.GetRoomFlags(5,target) != rom[0xca00+target],
                    "Native mirror state-zero modal admission/outgoing cancellation must agree on lifetime and target flags.");
            });
            FailIf(!mirror.Finished || (_saveData.GetRoomFlags(5,target)&mask) != (outgoing ? 0 : mask) || cues.Requests.Count != 0,
                "Native outgoing deletion must precede incoming flag sampling; active state-zero mirrors still run under text/masks without audio.");
            _dialogue.Close();
        }
        GD.Print("Validated clean-US D6 actual bomb/era-wall handoff, imported dungeon neighbors, both flag pages and cached present/past re-entry, repeated bombs, debit/cues/RNG with split/batched gameplay.");
    }
}
