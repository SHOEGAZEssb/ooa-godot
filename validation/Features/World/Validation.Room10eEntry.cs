using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom10eBottomEntry()
    {
        byte[][] sourceWarpTiles =
        [
            [0xdc, 0xdd, 0xde, 0xdf, 0xed, 0xee, 0xef],
            [0x34, 0x36, 0x44, 0x45, 0x46, 0x47, 0xaf],
            [0x44, 0x45, 0x46, 0x47, 0x4f],
            [],
            [0xdc, 0xdd, 0xde, 0xdf, 0xed, 0xee, 0xef],
            [0x44, 0x45, 0x46, 0x47, 0x4f]
        ];
        for (int collisions = 0; collisions < sourceWarpTiles.Length; collisions++)
        for (int tile = 0; tile <= 0xff; tile++)
            FailIf(WarpDatabase.IsWarpTile(collisions, (byte)tile) !=
                (System.Array.IndexOf(sourceWarpTiles[collisions], (byte)tile) >= 0),
                $"warpTileTable collision set ${collisions:x2} misclassified tile ${tile:x2}.");

        // warpTileTable is indexed by wActiveCollisions, not wActiveGroup.
        // The indoor set accepts $34/$36/$44-$47/$af; $ef is ordinary
        // floor in tileset $66, despite room 1:0e's overworld group.
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            ResetValidationInput();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            LoadValidationRoom(0, 0x3c);
            _player.WarpTo(new Vector2(0x48, 0x48));
            for (int repeat = 0; repeat < 2; repeat++)
            {
                FailIf(_collision.Collides(_player.Position),
                    "Room 1:0e entry must approach 0:3c's entrance through clear ground.");
                for (int update = 0; update < 60 && !IsTransitioning; update++)
                    StepGameplayUpdates(1, Vector2.Up);
                FailIf(!IsTransitioning || _activeGroup != 1 || _currentRoom.Id != 0x0e,
                    $"Bottom entry did not reach 1:0e: {_activeGroup}:{_currentRoom.Id:x2}, {_player.Position}.");
                FailIf(_currentRoom.ActiveCollisions != 1 || _player.Position != new Vector2(0x50, 0x80),
                    "Room 1:0e must enter at $50,$80 using indoor collision set $01.");
                StepGameplayUpdates(120, Vector2.Zero, batched: batched);
                FailIf(IsTransitioning || _activeGroup != 1 || _currentRoom.Id != 0x0e ||
                    _player.Position != new Vector2(0x50, 0x64) ||
                    _currentRoom.GetMetatile(_player.Position) != 0xef,
                    "Room 1:0e's $ef arrival floor incorrectly triggered dungeon 6.");
                var warps = new WarpDatabase();
                FailIf(warps.TryGetTileWarp(1, _currentRoom, 0x65, 0xef, out _) ||
                    !warps.TryGetTileWarp(1, _currentRoom, 0x16, 0xaf, out Warp dungeon) ||
                    dungeon.DestinationGroup != 5 || dungeon.DestinationRoom != 0x26,
                    "Room 1:0e must reject floor $ef and retain its indoor $af warp to 5:26.");
                StepGameplayUpdates(8, Vector2.Up, batched: batched);
                FailIf(IsTransitioning || _player.Position.Y >= 0x64,
                    "Room 1:0e did not release movement across its ordinary floor after entry.");
                for (int update = 0; update < 60 && !IsTransitioning; update++)
                    StepGameplayUpdates(1, Vector2.Down);
                FailIf(!IsTransitioning, "Room 1:0e's bottom exit did not activate.");
                StepGameplayUpdates(120, Vector2.Zero, batched: batched);
                FailIf(IsTransitioning || _activeGroup != 0 || _currentRoom.Id != 0x3c,
                    "Room 1:0e's bottom exit did not return to 0:3c.");
                StepGameplayUpdates(12, Vector2.Down);
            }
        }
    }
}
