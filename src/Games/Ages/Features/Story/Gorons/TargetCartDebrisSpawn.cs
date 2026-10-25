using Godot;
namespace Oracles;
internal sealed record TargetCartDebrisSpawn(NpcRecord Record,Vector2 Position,int Direction):RoomEntitySpawn;
