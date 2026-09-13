using Godot;

namespace oracleofages;

internal interface ISwitchHookHittableRoomEntity
{
    bool ApplySwitchHookHit(SwitchHookItem hook, Vector2 linkPosition);
}
