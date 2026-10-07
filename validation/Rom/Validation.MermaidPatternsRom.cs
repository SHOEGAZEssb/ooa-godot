using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidPatternsRom()
    {
        const BindingFlags flags = BindingFlags.Instance|BindingFlags.NonPublic;
        foreach (bool batch in new[] { false,true })
        foreach (bool cubePads in new[] { false,true })
        {
            ReinitializeGameplayForValidation();
            int room = cubePads ? 0x21 : 0x2c;
            LoadValidationRoom(5,room);
            _inventory.GiveTreasure(TreasureId.Flippers,0);
            _inventory.GiveTreasure(TreasureId.MermaidSuit,0);
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(0x78,0x98));
            FailIf(_collision.Collides(_player.Position),"Mermaid pattern fixture must retain a reachable original room floor.");
            var chest = _entities.Entities<DungeonPuzzleChestRoomEntity>().Single();
            var sensors = _entities.Entities<ColoredCubeFloorSensorRoomEntity>().OrderBy(sensor => _entities.InteractionSlot(sensor)).ToArray();
            var data = new MermaidDungeonDatabase();
            var records = data.GetRoomRecords(5,room);
            FailIf(cubePads ? records.Count != 5 || sensors.Length != 3 || _entities.Entities<ColoredCubeRoomEntity>().Count != 1 : records.Count != 1,
                "Mermaid patterns must import the original cube/three sensors/chest or single diamond chest, in source order.");
            var patterns = new DungeonChestPatternDatabase();
            int[] positions = cubePads ? [0x37,0x65,0x69] : [0x16,0x17,0x18,0x26,0x27,0x28];
            int subid = cubePads ? 3 : 1, wantedTile = cubePads ? 0xa0 : 0xdb;
            var cells = patterns.Cells(0x90,subid);
            FailIf(!cells.Select(cell => cell.Position).SequenceEqual(positions) ||
                cells.Any(cell => cell.MinimumTile != wantedTile || cell.MaximumTile != wantedTile),
                "miscPuzzles source pattern cells/terminator boundaries differ from the independent $90:$01/$03 tables.");
            var active = SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity",flags)!;
            // Declare the puzzle publishers' inputs. This bounded controller
            // walk does not replay the cube's rolling route or hook exchanges.
            foreach (var entity in active.Where(entity => entity != chest && entity is not ColoredCubeFloorSensorRoomEntity).ToArray())
            { active.Remove(entity); free.Invoke(_entities,[entity]); }
            var nativeSlots = active.Select(entity => (Entity:entity,Address:0xd040+_entities.InteractionSlot(entity.Node)*256)).ToArray();
            var seed = _random.CaptureState();
            var rom = new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            rom[0xca00+room] = _saveData.ReadWramByte(0xca00+room);
            foreach (var pair in nativeSlots)
            {
                rom[pair.Address] = 1; rom[pair.Address+1] = 0x90;
                rom[pair.Address+2] = (byte)(pair.Entity == chest ? subid : 2);
                rom[pair.Address+11] = (byte)pair.Entity.Node.Position.Y;
                rom[pair.Address+13] = (byte)pair.Entity.Node.Position.X;
            }
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            void SetTile(int packed,byte tile)
            {
                _currentRoom.SetPositionTileAndCollision(new((packed&15)*16+8,(packed>>4)*16+8),tile,null,0);
                rom.CopyRoom(_currentRoom);
            }
            void DeclareCube(int position,int color)
            {
                _runtimeState.SetWramByte(WramAddress.wRotatingCubePos,rom[0xccae] = (byte)position);
                _runtimeState.SetWramByte(WramAddress.wRotatingCubeColor,rom[0xccad] = (byte)color);
            }
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() =>
            {
                rom.Update(_entities.FrameCounter,_player.Position); update++;
                foreach (var pair in nativeSlots)
                    FailIf(((IRoomEntityLifetime)pair.Entity).Finished != (rom[pair.Address] == 0),
                        $"Mermaid pattern room$5:${room:x2}, update{update}: native controller deletion differs.");
                for (int y = 0; y < _currentRoom.HeightInTiles; y++)
                for (int x = 0; x < _currentRoom.WidthInTiles; x++)
                {
                    int packed = y*16+x; Vector2 point = new(x*16+8,y*16+8);
                    FailIf(_currentRoom.GetMetatile(point) != rom[0xcf00+packed] ||
                        _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00+packed],
                        $"Mermaid pattern room$5:${room:x2}, update{update}: cell${packed:x2} tile/collision differs: runtime=${_currentRoom.GetMetatile(point):x2}/${_currentRoom.GetTerrainInfo(point).Collision:x2}, ROM=${rom[0xcf00+packed]:x2}/${rom[0xce00+packed]:x2}.");
                }
                FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Mermaid pattern room$5:${room:x2}, update{update}: solve/puff sound ordering differs, runtime={string.Join(',',sounds.Requests)}, ROM={string.Join(',',rom.Sounds)}.");
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls-seed.Calls != rom.RandomCalls,
                    $"Mermaid pattern room$5:${room:x2}, update{update}: global RNG differs.");
            });
            if (cubePads)
            {
                DeclareCube(0x38,2); Step();
                DeclareCube(0x37,1); Step();
                FailIf(sensors.Any(sensor => sensor.Finished) || chest.Finished,
                    "Cube pads must reject a different packed position or mismatched color.");
                _dialogue.ShowGameplayMessage("Cube pad state-zero dispatch",120); rom[0xcba0] = 1;
                DeclareCube(0x37,0x82); Step();
                FailIf(!sensors[0].Finished || chest.Finished,
                    "Blue pad must mask the cube's high color bits and latch during text without completing the other pads.");
                _dialogue.Close(); rom[0xcba0] = 0;
                DeclareCube(0x65,0); Step(); DeclareCube(0x69,1); Step();
                FailIf(sensors.Any(sensor => !sensor.Finished) || !chest.Finished,
                    "The last cube pad must publish its standard floor before the later chest checks all three cells in that same update.");
                foreach (int packed in positions)
                    FailIf(rom[0xcf00+packed] != 0xa0 || rom[0xce00+packed] != 0x0f,
                        "Completed cube pads must retain solid collision$0f after becoming floor$a0.");
            }
            else
            {
                foreach (int packed in positions.Take(5)) SetTile(packed,0xdb);
                SetTile(0x28,0xa0); Step(2);
                FailIf(chest.Finished,"Five diamonds must not satisfy the six-cell native condition.");
                _dialogue.ShowGameplayMessage("Diamond state-zero dispatch",120); rom[0xcba0] = 1;
                SetTile(0x28,0xdb); Step(); _dialogue.Close(); rom[0xcba0] = 0;
            }
            int chestPosition = cubePads ? 0x67 : 0x57;
            FailIf(!chest.Finished || rom[0xcf00+chestPosition] != 0xf1 ||
                _entities.Entities<PuzzlePuffEffect>().Count != 1,
                "Completed Mermaid pattern must write chest$f1 at its original position and allocate exactly one checked puff before deletion.");
            Step(40);
            LoadValidationRoom(5,room);
            _player.WarpTo(new(0x78,0x98));
            _saveData.SetRoomFlag(5,room,0x20);
            chest = _entities.Entities<DungeonPuzzleChestRoomEntity>().Single();
            active = SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            foreach (var entity in active.Where(entity => entity != chest).ToArray())
            { active.Remove(entity); free.Invoke(_entities,[entity]); }
            nativeSlots = [(chest,0xd040+_entities.InteractionSlot(chest)*256)];
            seed = _random.CaptureState(); rom = new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2; rom[0xca00+room] = 0x20;
            int a = nativeSlots[0].Address; rom[a] = 1; rom[a+1] = 0x90; rom[a+2] = (byte)subid;
            rom[a+11] = (byte)chest.Position.Y; rom[a+13] = (byte)chest.Position.X;
            sounds = _sound.AttachPlayRequestAudit(); Step(2);
            FailIf(!chest.Finished || _entities.Entities<PuzzlePuffEffect>().Count != 0,
                "Collected-item re-entry must delete the pattern controller before checking tiles or spawning another chest puff.");
        }
        GD.Print("Validated clean-US Mermaid diamond/pad pattern boundaries, shared cube inputs, same-pass solid floor/chest completion, state-zero text eligibility, checked puff and collected re-entry through split/batched gameplay updates.");
    }
}
