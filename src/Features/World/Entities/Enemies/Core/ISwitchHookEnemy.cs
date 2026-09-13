using Godot;

namespace oracleofages;

internal interface ISwitchHookEnemy
{
    bool SwitchHookHeld { get; }
    Vector2 SwitchHookPosition { get; }
    void BeginSwitchHook(Vector2 linkPosition);
    void CopySwitchHookPosition(Vector2 position, int zHigh);
    void SwapSwitchHook();
    void ReleaseSwitchHook();
}
