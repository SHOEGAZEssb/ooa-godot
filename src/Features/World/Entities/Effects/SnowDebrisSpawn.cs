using Godot;

namespace Oracles;

internal sealed record SnowDebrisSpawn(Vector2 Position, bool Flickers) : RoomEntitySpawn(UpdateThisFrame: true);
