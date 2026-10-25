using Godot;

namespace Oracles;

internal interface ISwitchHookHittableRoomEntity
{
    bool ApplySwitchHookHit(SwitchHookItem hook, Vector2 linkPosition);
}
