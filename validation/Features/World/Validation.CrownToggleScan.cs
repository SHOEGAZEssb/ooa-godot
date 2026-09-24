using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownToggleScan()
    {
        // cutscenes2.s @func_7ced scans $af down to $01. Its literal
        // replacement/collision pairs are $28/00, $29/00, $0e/1e, $0f/1e.
        (int Position, byte Original, byte Expected, byte Collision)[] cells =
        [
            (0x00, 0x0e, 0x0e, 0x1e),
            (0x01, 0x0e, 0x28, 0x00),
            (0x02, 0x0f, 0x29, 0x00),
            (0xae, 0x28, 0x0e, 0x1e),
            (0xaf, 0x29, 0x0f, 0x1e),
            (0x2f, 0x28, 0x0e, 0x1e)
        ];
        foreach (bool batched in new[] { false, true })
        {
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
            LoadValidationRoom(4, 0xa1);
            _entities.Clear();
            _player.WarpTo(new(120, 56));
            foreach (var cell in cells)
            {
                _currentRoom.SetUnderlyingStorageMetatile(cell.Position, cell.Original);
                _currentRoom.SetStorageTileAndCollision(cell.Position, cell.Original,
                    cell.Original < 0x28 ? (byte)0x1e : (byte)0, (long)_animationTicks);
            }
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 1);
            StepGameplayUpdates(8, Vector2.Zero, batched: batched);
            foreach (var cell in cells)
                FailIf(_currentRoom.Layout[cell.Position] != cell.Original,
                    "Floor scan must not run before the final delay expires.");
            StepGameplayUpdates(1, Vector2.Zero);
            foreach (var cell in cells)
            {
                FailIf(_currentRoom.Layout[cell.Position] != cell.Expected ||
                    _currentRoom.GetUnderlyingStorageMetatile(cell.Position) != cell.Expected,
                    $"Floor scan must preserve $00 and rewrite both buffers through padding ${cell.Position:x2}.");
                if ((cell.Position & 15) < 15)
                    FailIf(_currentRoom.GetTerrainInfo(new((cell.Position & 15) * 16 + 8,
                        (cell.Position >> 4) * 16 + 8)).Collision != cell.Collision,
                        $"Floor scan collision mismatch at ${cell.Position:x2}.");
            }
            FailIf(_entities.Entities<RockDebrisEffect>().Count != 0,
                "Bare toggling floors must not allocate block debris.");
        }
        _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
        LoadValidationRoom(0, 0x60);
    }
}
