using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterTiming()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var data = new DungeonMechanicDatabase();
        var source = data.GetRoomRecords(4, 0x9d).Single(r => r.Id == 0x1e);
        foreach (bool batch in new[] { false, true })
        foreach (int scenario in new[] { 0, 1, 2 })
        {
            LoadValidationRoom(4, 0x7c);
            _player.WarpTo(new(184, 88));
            int enemies = scenario == 2 ? 1 : 0;
            var sounds = new List<int>();
            var door = new DungeonDoorRoomEntity(source with { SubId = scenario == 0 ? 6 : 10 },
                _currentRoom, data, () => enemies, _ => true, p => p - new Vector2(0, 64), () => 0,
                sounds.Add, default, true);
            Vector2 position = door.Position;
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [door]);
            // commonScripts + scripting.s: radii1, angle2, branch/call3.
            // Trigger: contact4, respawn+ret5, decide6, sound7, state2 at8.
            // Empty enemy: branch3, state2 at4. Enemy cleared at20:
            // check20, sound21, wait8 at22, incstate30, interleave31.
            int start = scenario switch { 0 => 9, 1 => 5, _ => 31 };
            int finish = start + 6;
            for (int tick = 1; tick <= finish; tick++)
            {
                if (tick == 20) enemies = 0;
                sounds.Clear();
                StepGameplayUpdates(1, Vector2.Zero, [], [], batch);
                int[] expected = tick == start || tick == finish ? [SoundId.SndDoorClose]
                    : scenario == 0 && tick == 7 || scenario == 2 && tick == 21 ? [SoundId.SndSolvePuzzle] : [];
                FailIf(!sounds.SequenceEqual(expected) ||
                    _currentRoom.GetMetatile(position) != (tick < start ? 0x7a : 0xa0) ||
                    _currentRoom.GetTerrainInfo(position).Collision != (tick < finish ? 0x0f : 0) ||
                    door.Finished != (scenario != 0 && tick == finish),
                    $"Shutter script scenario{scenario} update{tick} differs from source command yields: sounds=[{string.Join(',', sounds)}], tile=${_currentRoom.GetMetatile(position):x2}.");
            }
        }
        LoadValidationRoom(0, 0x60);
    }
}
