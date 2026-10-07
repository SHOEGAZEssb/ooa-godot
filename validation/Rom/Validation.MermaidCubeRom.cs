using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidCubeRom()
    {
        const BindingFlags flags = BindingFlags.Instance|BindingFlags.NonPublic;
        foreach (bool batch in new[] { false,true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5,0x21);
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(0x78,0x70)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Mermaid cube approach must start below the original cube on reachable room$5:$21 floor.");
            var cube = _entities.Entities<ColoredCubeRoomEntity>().Single();
            var blue = _entities.Entities<ColoredCubeFloorSensorRoomEntity>().Single(sensor => sensor.Position == new Vector2(0x78,0x38));
            var active = SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity",flags)!;
            foreach (var entity in active.Where(entity => entity is not (ColoredCubeRoomEntity or ColoredCubeFloorSensorRoomEntity or DungeonPuzzleChestRoomEntity)).ToArray())
            { active.Remove(entity); free.Invoke(_entities,[entity]); }
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x70);
            rom.InitializeLinkGameplay(); rom[0xd009] = 0xff;
            foreach (var entity in active)
            {
                int a = 0xd040+_entities.InteractionSlot(entity.Node)*256;
                rom[a] = 1; rom[a+1] = (byte)(entity == cube ? 0x19 : 0x90);
                rom[a+2] = (byte)(entity == cube ? 3 : entity == blue || entity is ColoredCubeFloorSensorRoomEntity ? 2 : 3);
                rom[a+11] = (byte)entity.Node.Position.Y; rom[a+13] = (byte)entity.Node.Position.X;
            }
            int p = 0xd040+_entities.InteractionSlot(cube)*256;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            _runtimeState.SetWramByte(WramAddress.wRotatingCubePos,rom[0xccae] = 0x99);
            _runtimeState.SetWramByte(WramAddress.wRotatingCubeColor,rom[0xccad] = 0x81);
            void Step(int count = 1,Vector2 movement = default) => StepGameplayUpdates(count,movement,batched:batch,afterUpdate:() =>
            {
                int angle = movement == Vector2.Up ? 0 : movement == Vector2.Left ? 24 : movement == Vector2.Right ? 8 : 0xff;
                int held = movement == Vector2.Up ? 0x40 : movement == Vector2.Left ? 0x20 : movement == Vector2.Right ? 0x10 : 0;
                rom.UpdateGameplay(0,held,angle,_entities.FrameCounter); update++;
                int pushCounter = SomariaPrivate<int>(cube,"_pushCounter");
                FailIf(cube.Finished != (rom[p] == 0), $"Mermaid cube update{update}: native hazard deletion differs.");
                FailIf(!cube.Finished && (cube.Moving != (rom[p+4] == 2) || cube.Orientation != rom[p+8] ||
                    cube.Position != new Vector2(rom[p+13],rom[p+11]) || cube.Visible != ((rom[p+26]&128) != 0)),
                    $"Mermaid cube update{update}: native initialization/roll differs, runtime={cube.Moving}/{cube.Orientation}/{cube.Position}/{cube.Visible}, ROM={rom[p+4]}/{rom[p+8]}/{rom[p+13]},{rom[p+11]}/${rom[p+26]:x2}, push={pushCounter}/${rom[p+6]:x2}, Link={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}.");
                FailIf(_runtimeState.ReadWramByte(WramAddress.wRotatingCubePos) != rom[0xccae] ||
                    _runtimeState.ReadWramByte(WramAddress.wRotatingCubeColor) != rom[0xccad],
                    $"Mermaid cube update{update}: shared position/color writers differ, runtime=${_runtimeState.ReadWramByte(WramAddress.wRotatingCubePos):x2}/${_runtimeState.ReadWramByte(WramAddress.wRotatingCubeColor):x2}, ROM=${rom[0xccae]:x2}/${rom[0xccad]:x2}.");
                CompareSomariaMotionRom(rom,$"Mermaid cube update{update}");
                foreach (int packed in new[] { 0x57,0x47,0x37,0x36 })
                {
                    Vector2 point = new((packed&15)*16+8,(packed>>4)*16+8);
                    FailIf(_currentRoom.GetMetatile(point) != rom[0xcf00+packed] ||
                        _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00+packed],
                        $"Mermaid cube update{update}: rolling cell${packed:x2} tile/solidity differs.");
                }
                FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),$"Mermaid cube update{update}: native roll/pad sound order differs.");
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls-seed.Calls != rom.RandomCalls,
                    $"Mermaid cube update{update}: global RNG differs.");
            });
            _dialogue.ShowGameplayMessage("Pending cube initialization",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            FailIf(rom[0xccae] != 0x57 || rom[0xccad] != 2,"Native cube state0 must publish initial position$57/color$02 under text before the floor sensors.");
            bool paused = false;
            for (int i = 0; i < 160 && (cube.Position != new Vector2(0x78,0x38) || cube.Moving); i++)
            {
                Step(1,Vector2.Up);
                if (!paused && cube.Moving)
                {
                    paused = true;
                    _dialogue.ShowGameplayMessage("Pending cube roll",120); rom[0xcba0] = 1;
                    Step(3); _dialogue.Close(); rom[0xcba0] = 0;
                }
            }
            FailIf(cube.Position != new Vector2(0x78,0x38) || cube.Moving || !blue.Finished || rom[0xcf37] != 0xa0 || rom[0xce37] != 0x0f,
                "Two actual rolls through original geometry must match the blue pad and preserve the occupying cube's solidity.");
            // Approach from the right around the original collision cell.
            Step(24,Vector2.Right); Step(12,Vector2.Up);
            for (int i = 0; i < 90 && (cube.Position != new Vector2(0x68,0x38) || cube.Moving); i++) Step(1,Vector2.Left);
            FailIf(cube.Position != new Vector2(0x68,0x38) || cube.Moving || rom[0xcf37] != 0xa0 || rom[0xce37] != 0 ||
                !blue.Finished || _entities.Entities<DungeonPuzzleChestRoomEntity>().Count != 1,
                "The cube's next roll must clear its old pad collision, retain completed floor and leave the other two pads/chest unresolved.");
            Step(4);
            Step(100,Vector2.Left);
            FailIf(cube.Finished || cube.Moving || cube.Position != new Vector2(0x58,0x38) || rom[0xccae] != 0x35 ||
                rom[0xcf34] != 0xf7 || rom[0xce34] == 0 || rom[0xce35] != 0x0f,
                "The cube must stop at$35: adjacent-tile solidity includes the original pit$34's hazard collision bits.");
        }
        GD.Print("Validated clean-US Mermaid cube state-zero text admission, actual geometric approach/rolling pause, blue pad handoff, repeat roll clearing prior solidity and full-byte pit blocking through split/batched gameplay updates.");
    }
}
