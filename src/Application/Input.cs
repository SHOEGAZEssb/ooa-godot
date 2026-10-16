using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Fixed-update input facade. Movement is translated to digital directions
/// after each action's deadzone. During an update every reader observes the
/// same immutable sample; other input delegates directly to Godot.
/// </summary>
internal static class Input
{
    private static ApplicationInputSnapshot? _current;
    private static ulong _originalUpdate;
    private static int? _nativeGameKeys;
    private static int _precedingGameKeys,_gameKeys,_nativeGameKeysPressed;
    private static readonly string[] GameActions=["attack","item","map","inventory","move_right","move_left","move_up","move_down"];
    private static readonly StringName MoveLeft = "move_left";
    private static readonly StringName MoveRight = "move_right";
    private static readonly StringName MoveUp = "move_up";
    private static readonly StringName MoveDown = "move_down";

    internal static ulong TimingFrame =>
        _current is null ? Engine.GetProcessFrames() : _originalUpdate;
    internal static bool OriginalUpdateActive => _current is not null;

    internal static void BeginOriginalUpdate(ApplicationInputSnapshot snapshot)
    {
        if (_current is not null)
        {
            throw new InvalidOperationException(
                "An application input snapshot is already active.");
        }
        _originalUpdate++;
        _current = snapshot;
        _precedingGameKeys=_gameKeys;_gameKeys=ReadSnapshotGameKeys();_nativeGameKeysPressed=ReadSnapshotGameKeys(justPressed:true);
    }

    internal static void EndOriginalUpdate()
    {
        if (_current is null)
        {
            throw new InvalidOperationException(
                "No application input snapshot is active.");
        }
        _current = null;
        _nativeGameKeys=null;
    }

    // updateGameKeysPressed follows menus and precedes all special objects.
    // Keep the physical snapshot intact; a native stream replaces that later
    // gameplay sample, including fresh-key edges, for the rest of this update.
    internal static void OverrideGameKeys(int keys)
    {
        if(_current is null)throw new InvalidOperationException("Native game keys require an original application update.");
        _nativeGameKeys=keys&255;_gameKeys=keys&255;_nativeGameKeysPressed=_gameKeys&~_precedingGameKeys;
    }
    private static int KeyBit(string action)=>action switch
    {"attack"=>1,"item"=>2,"map"=>4,"inventory"=>8,"move_right"=>16,"move_left"=>32,"move_up"=>64,"move_down"=>128,_=>0};
    private static int ReadSnapshotGameKeys(bool justPressed=false)
    {
        int keys=0;foreach(string action in GameActions)
            if(justPressed?_current!.Value.IsJustPressed(action):_current!.Value.IsPressed(action))keys|=KeyBit(action);
        return keys;
    }

    internal static bool IsActionPressed(
        string action,
        bool exactMatch = false) =>
        _nativeGameKeys is int keys&&KeyBit(action) is >0 ? (keys&KeyBit(action))!=0 : _current?.IsPressed(action) ??
        Godot.Input.IsActionPressed(action, exactMatch);

    // The text thread reads wKeysPressed/wKeysJustPressed, independently of
    // simulated wGameKeys* used by Link and the ordinary object pass.
    internal static bool IsPhysicalActionPressed(string action)=>
        _current?.IsPressed(action)??Godot.Input.IsActionPressed(action);
    internal static bool IsPhysicalActionJustPressed(string action)=>
        _current?.IsJustPressed(action)??Godot.Input.IsActionJustPressed(action);

    internal static bool IsActionJustPressed(
        string action,
        bool exactMatch = false) =>
        _nativeGameKeys is not null&&KeyBit(action) is >0 ? (_nativeGameKeysPressed&KeyBit(action))!=0 : _current?.IsJustPressed(action) ??
        Godot.Input.IsActionJustPressed(action, exactMatch);

    internal static Vector2 GetVector(
        string negativeX,
        string positiveX,
        string negativeY,
        string positiveY,
        float deadzone = -1.0f)
    {
        if (negativeX == "move_left" &&
            positiveX == "move_right" &&
            negativeY == "move_up" &&
            positiveY == "move_down")
        {
            if(_nativeGameKeys is int keys)return ((keys>>4) switch
                {1=>Vector2.Right,2=>Vector2.Left,4=>Vector2.Up,5=>new Vector2(1,-1),6=>new Vector2(-1,-1),8=>Vector2.Down,
                    9=>new Vector2(1,1),10=>new Vector2(-1,1),_=>Vector2.Zero}).LimitLength();
            return _current?.Movement ?? ReadMovement();
        }
        return Godot.Input.GetVector(
            negativeX, positiveX, negativeY, positiveY, deadzone);
    }

    internal static Vector2 ReadMovement()
    {
        // specialObjects.s:updateGameKeysPressed derives wLinkAngle from D-pad bits.
        // Godot.GetVector uses a circular deadzone and retains tiny off-axis
        // values once the other axis is held. Quantize the per-action pressed
        // state so player, collision, companions and menus agree.
        return new Vector2(
            (Godot.Input.IsActionPressed(MoveRight) ? 1 : 0) -
            (Godot.Input.IsActionPressed(MoveLeft) ? 1 : 0),
            (Godot.Input.IsActionPressed(MoveDown) ? 1 : 0) -
            (Godot.Input.IsActionPressed(MoveUp) ? 1 : 0)).LimitLength();
    }

    internal static void ActionPress(StringName action, float strength = 1.0f) =>
        Godot.Input.ActionPress(action, strength);

    internal static void ActionRelease(StringName action) =>
        Godot.Input.ActionRelease(action);
}
