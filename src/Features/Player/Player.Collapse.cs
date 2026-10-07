using Godot;
using System;

namespace oracleofages;

public partial class Player
{
    private bool _collapseRequested;
    private int _collapseSubstate = -1;
    private int _collapseParameter;
    private int _collapseCounter;
    private bool _collapseUpdated;
    internal bool CollapsePending => _collapseRequested;
    internal bool CollapsedActive => _collapseSubstate >= 0;
    internal int CollapseCounter => _collapseCounter;
    internal int CollapseSubstate => _collapseSubstate;
    internal void RequestCollapse(int parameter)
    {
        if (parameter != 1) throw new NotSupportedException($"linkState14: collapse parameter${parameter:x2} is not represented.");
        _collapseRequested = true; _collapseParameter = parameter;
    }
    private bool AdvanceCollapse()
    {
        if (_collapseRequested)
        {
            // Normal Link consumes wLinkForceState and returns. State14's
            // substate0 runs on the following special-object update.
            _collapseRequested = false; _collapseSubstate = 0; _collapseUpdated = true;
            return true;
        }
        if (!CollapsedActive) return false;
        _collapseUpdated = true;
        if (_collapseSubstate == 0)
        {
            _collapseSubstate = 1; _collapseCounter = 0xf0;
            ClearNativeItemParents(); ClearShieldParent(); CancelSwordAttack(); _startedParentItemAnimations = 0;
            _walking = false; _linkWalkAnimationFrame = 0; _linkWalkAnimationCounter = 2;
            QueueRedraw(); return true;
        }
        OracleObjectMath.UpdateSpeedZ(ref _topDownAirZFixed,ref _topDownAirSpeedZ,0x40);
        _topDownAirborne = _topDownAirZFixed < 0;
        if (_collapseParameter != 0)
        {
            Vector2 input = Input.GetVector("move_left","move_right","move_up","move_down");
            if (input != Vector2.Zero)
            {
                var facing = FacingForInput(_facing,input);
                if (facing != _facing) { _facing = facing; _linkWalkAnimationFrame = 0; _linkWalkAnimationCounter = 2; }
            }
        }
        bool pressed = Input.IsActionJustPressed("attack") || Input.IsActionJustPressed("item") ||
            Input.IsActionJustPressed("move_up") || Input.IsActionJustPressed("move_right") ||
            Input.IsActionJustPressed("move_down") || Input.IsActionJustPressed("move_left") ||
            Input.IsActionJustPressed("inventory") || Input.IsActionJustPressed("map");
        bool contact = _collapseParameter != 0 && NativeContactSignal;
        int next = contact ? _collapseCounter : _collapseCounter-(pressed ? 4 : 1);
        if (!contact) _collapseCounter = next&255;
        if (contact || next < 0)
        {
            // linkState13@restoreToNormal does not consume damageToApply.
            // Its retained damage is applied by the next normal-state update.
            _enemyInvincibilityFrames = 1; _enemyKnockbackFrames = 0;
            _collapseSubstate = -1;
            _walking = false; _linkWalkAnimationFrame = 0; _linkWalkAnimationCounter = 2;
        }
        QueueRedraw(); return true;
    }
    private void CancelCollapse() { _collapseRequested = false; _collapseSubstate = -1; _collapseCounter = 0; _collapseUpdated = false; }
}
