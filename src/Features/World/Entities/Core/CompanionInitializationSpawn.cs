using Godot;

namespace Oracles;

// A native function call on the retained slot, rather than a fresh companion birth.
internal sealed record CompanionInitializationSpawn(int Id,Vector2 Position):RoomEntitySpawn;
