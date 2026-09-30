using Godot;

namespace oracleofages;

// Clean-US roomInitialization.loadRememberedCompanion and interactionCode67.
// The interaction executes its real search, presets, text requests and deletion.
internal sealed class CompanionSpawnRom
{
    internal readonly LinkCollisionRom Memory = new(bankedStack: true);
    internal CompanionSpawnRom(OracleRoomData room, int group, Vector2 link)
    {
        Memory[0xcc2d] = (byte)group; Memory[0xcc30] = (byte)room.Id;
        Memory[0xcc34] = room.TilesetFlags;
        Memory[0xd00b] = (byte)link.Y; Memory[0xd00d] = (byte)link.X;
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
            Memory[0xce00 + y * 16 + x] = room.GetTerrainInfo(new(x * 16 + 8, y * 16 + 8)).Collision;
    }
    internal void Remembered() => Memory.Call(0x768a, bank: 2);
    internal void Spawn(int subid)
    {
        Memory[0xd040] = 1; Memory[0xd041] = 0x67; Memory[0xd042] = (byte)subid;
        Memory[0xffae] = 0x40;
        Memory.Call(0x4a5d, bank: 0x0a, objectPage: 0xd0);
        Memory[0xffae] = 0;
    }
}
