using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterCoverage()
    {
        // Independent mainData.s INTERAC$1e placement transcription.
        var placements = new (int Room, int SubId, int Position)[]
        {
            (0x9d, 6, 0xa7), (0xa6, 7, 0x50),
            (0xb4, 0x0b, 0x80), (0xb4, 8, 0x07),
            (0xb6, 8, 0x07), (0xbf, 8, 0x07), (0xbf, 0x0b, 0x50)
        };
        foreach (int room in placements.Select(row => row.Room).Distinct())
        {
            LoadValidationRoom(4, room);
            var actual = _entities.Entities<DungeonDoorRoomEntity>();
            var expected = placements.Where(row => row.Room == room).ToArray();
            FailIf(actual.Count != expected.Length || actual.Any(door =>
                !expected.Any(row => row.SubId == door.SubId && row.Position == door.PackedPosition) ||
                !door.EnemyCompletionSupported),
                $"Crown4:{room:x2} must instantiate every source shutter with complete enemy-count handling.");
            // harpFluteParent @harp rejects flags$7e before any tune branch.
            // Isolate the tune-completion gate; input/music timing is covered
            // by Harp validations and is not part of this shutter audit.
            FailIf((_currentRoom.TilesetFlags & 0x7e) == 0,
                "Crown shutter rooms must retain the source dungeon Harp restriction.");
            for (int song = 1; song <= 3; song++)
            {
                _harp.TryStart(_player);
                _harp.Complete(_player, song);
                FailIf(_transitions.IsTransitioning || _transitions.PaletteFadeActive || _dialogue.IsOpen || _harp.IsPlaying,
                    $"Harp tune{song} must end without a time-warp palette thread in Crown4:{room:x2}.");
            }
        }
        LoadValidationRoom(0, 0x60);
    }
}
