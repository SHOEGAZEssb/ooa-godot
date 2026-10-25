using Godot;

namespace Oracles;

internal sealed record RamrockPartSpawn(int Id, int SubId, Vector2 Position, int ZFixed,
    int ParentSlot, bool ParentIsPart = false, int Var03 = 0, int BossSlot = -1) : RoomEntitySpawn;
