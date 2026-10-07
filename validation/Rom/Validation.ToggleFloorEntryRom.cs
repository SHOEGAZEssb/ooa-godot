using Godot;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareToggleFloorEntryRom()
    {
        ReinitializeGameplayForValidation();
        foreach (byte state in new byte[] { 0,1,2,0,1 })
        {
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,state);
            LoadValidationRoom(4,0x9f);
            var rom = new FrontendRom { VramDmaTransfersEnabled = true };
            for (int address = 0xc5b0; address < 0xcb00; address++) rom[address] = _saveData.ReadWramByte(address);
            rom[0xcc2d] = 4; rom[0xcc30] = 0x9f; rom[0xcc05] = 0xff; rom[0xcd00] = 1; rom[0xcdd2] = state;
            rom.LoadRoomTileset();
            for (int packed = 0; packed < 0xb0; packed++)
                rom[0xcf00+packed] = _currentRoom.GetOriginalMetatile(new((packed&15)*16+8,(packed>>4)*16+8));
            rom.ApplyRoomTileSubstitutions();
            Vector2 point = new(120,104);
            FailIf(_currentRoom.GetMetatile(point) != rom[0xcf67] || rom[0xcf67] != (state == 0 ? 0x0e : 0x28) ||
                _currentRoom.GetTerrainInfo(point).Collision != (state == 0 ? 0x1e : 0),
                "Cached Crown$4:$9f entry must use the whole toggle byte, including bit1 without bit0, and reset previous substitutions.");
            using Image uploaded = _currentRoom.CaptureLiveGraphics();
            for (int tile = 0; tile < 4; tile++)
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int address = 0x8cc0+tile*16+y*2;
                int shade = ((rom.VramByte(1,address)>>(7-x))&1)|(((rom.VramByte(1,address+1)>>(7-x))&1)<<1);
                int pixel = Mathf.RoundToInt((1-uploaded.GetPixel((0x4c+tile)%16*8+x,(0x4c+tile)/16*8+y).R)*3);
                FailIf(pixel != shade,$"Room-entry state${state:x2} native header pixel differs at tile${0x4c+tile:x2},({x},{y}).");
            }
        }
        var data = new DungeonToggleTileDatabase();
        byte[] originals = [0x0e,0x0f,0x28,0x29];
        foreach (int group in new[] { 0,4,6 })
        foreach (int dungeon in new[] { 4,5,8,11 })
        foreach (byte state in new byte[] { 0,1 })
        {
            var rom = new FrontendRom { VramDmaTransfersEnabled = true };
            rom[0xcc2d] = (byte)group; rom[0xcc30] = 0x9f; rom[0xcc39] = (byte)dungeon;
            rom[0xcc05] = 0xff; rom[0xcd00] = 1; rom[0xcdd2] = state;
            // Explicit source-handler inputs: four floor types, a supported
            // or unsupported dungeon, and the original caller's group gate.
            for (int index = 0; index < originals.Length; index++)
            {
                _currentRoom.SetPositionTileAndCollision(new(24+index*16,24),originals[index],null,(long)_animationTicks);
                rom[0xcf11+index] = originals[index];
            }
            data.Apply(group,dungeon,state,_currentRoom,(long)_animationTicks);
            rom.ApplyRoomTileSubstitutions();
            byte[] expected = group == 4 && dungeon is 5 or 8 or 11
                ? state == 0 ? [0x0e,0x29,0x0e,0x29] : [0x28,0x0f,0x28,0x0f] : originals;
            for (int index = 0; index < expected.Length; index++)
                FailIf(_currentRoom.GetMetatile(new(24+index*16,24)) != rom[0xcf11+index] || rom[0xcf11+index] != expected[index],
                    $"Native toggle substitutions dungeon${dungeon:x2}, group${group:x1}, state${state:x2}, type${originals[index]:x2} must honor the original dungeon bitset/group gate and literal pairs.");
        }
    }
}
