using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownToggleTiles()
    {
        // replaceTiles consumes NEW,OLD pairs. State0: $28->$0e/$0f->$29;
        // nonzero: $29->$0f/$0e->$28. The state check is OR A, not bit0.
        Image source = OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/gfx_animations_2.png");
        foreach (byte state in new byte[] { 0, 1, 2, 0, 1 })
        {
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, state);
            LoadValidationRoom(4, 0x9f);
            Vector2 point = new(120, 104);
            FailIf(_currentRoom.GetMetatile(point) != (state == 0 ? 0x0e : 0x28) ||
                _currentRoom.GetTerrainInfo(point).Collision != (state == 0 ? 0x1e : 0),
                "Crown cached reload must select raised/lowered floor from the complete toggle byte.");
            using Image uploaded = _currentRoom.CaptureLiveGraphics();
            int sourceTile = (state == 0 ? 0x740 : 0x7c0) / 16;
            for (int tile = 0; tile < 4; tile++)
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int columns = source.GetWidth() / 8;
                FailIf(uploaded.GetPixel((0x4c + tile) % 16 * 8 + x, (0x4c + tile) / 16 * 8 + y) !=
                    source.GetPixel((sourceTile + tile) % columns * 8 + x, (sourceTile + tile) / columns * 8 + y),
                    "Toggle room load must upload the four source tiles from graphics header $3d/$3f.");
            }
        }
        var database = new DungeonToggleTileDatabase();
        byte[] originals = [0x0e, 0x0f, 0x28, 0x29];
        foreach (int group in new[] { 0, 4, 6 })
        foreach (int dungeon in new[] { 4, 5, 8, 11 })
        foreach (byte state in new byte[] { 0, 1 })
        {
            for (int i = 0; i < originals.Length; i++)
                _currentRoom.SetPositionTileAndCollision(new(24 + i * 16, 24), originals[i], null, (long)_animationTicks);
            database.Apply(group, dungeon, state, _currentRoom, (long)_animationTicks);
            byte[] expected = group == 4 && dungeon is 5 or 8 or 11
                ? state == 0 ? [0x0e, 0x29, 0x0e, 0x29] : [0x28, 0x0f, 0x28, 0x0f]
                : originals;
            for (int i = 0; i < expected.Length; i++)
                FailIf(_currentRoom.GetMetatile(new(24 + i * 16, 24)) != expected[i],
                    "Toggle substitutions must honor the source dungeon bitset and exclude small/side-view groups.");
        }
        _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
        LoadValidationRoom(0, 0x60);
    }
}
