using Godot;
using System;

namespace Oracles;

/// <summary>
/// Fixed-update input facade. Movement is translated to digital directions
/// after each action's deadzone. During an update every reader observes the
/// same immutable sample; other input delegates directly to Godot.
/// </summary>
internal static class Input
{
    private static ApplicationInputSnapshot? _current;
    private static ulong _originalUpdate;
    private static OracleRuntimeState? _gameplayMemory;
    // The final chord reads the following func_410d "xor a" opcode in both ROMs.
    private static readonly byte[] GameKeyAngles =
        [0xff, 0x08, 0x18, 0xff, 0x00, 0x04, 0x1c, 0xff, 0x10, 0x0c, 0x14, 0xff, 0xff, 0xff, 0xff, 0xaf];
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
    }

    internal static void EndOriginalUpdate()
    {
        if (_current is null)
        {
            throw new InvalidOperationException(
                "No application input snapshot is active.");
        }
        _current = null;
        _gameplayMemory = null;
    }

    // specialObjects.s:updateGameKeysPressed follows menus. Only this pass
    // advances the session's predecessor; the physical snapshot stays intact.
    internal static void PublishGameKeys(OracleRuntimeState memory, int? simulatedKeys, bool dying)
    {
        UseGameKeys(memory);
        int keys = ReadSnapshotGameKeys();
        int mode = memory.ReadField("wUseSimulatedInput");
        if (mode == 2)
        {
            memory.SetField("wUseSimulatedInput", 0);
            keys = (keys & 15) | ((keys & 0xa0) >> 1) | ((keys & 0x50) << 1);
        }
        else if (mode != 0)
        {
            int paletteMode = memory.ReadField("wPaletteThread_mode");
            if (paletteMode != 0) keys = paletteMode;
            else if ((mode & 0x80) != 0) keys = memory.ReadField("wSimulatedInputValue");
            else keys = simulatedKeys ?? throw new NotSupportedException(
                    $"{memory.Game}: specialObjects.s:updateGameKeysPressed requires an imported simulated-input stream; wUseSimulatedInput=${mode:x2}.");
        }
        int preceding = memory.ReadField("wGameKeysPressed");
        keys = dying ? 0 : keys & 255;
        memory.SetField("wGameKeysPressed", (byte)keys);
        memory.SetField("wGameKeysJustPressed", (byte)(keys & ~preceding));
        memory.SetField("wLinkAngle", dying ? (byte)0xff : GameKeyAngles[keys >> 4]);
    }

    internal static void UseGameKeys(OracleRuntimeState memory)
    {
        if (_current is null || _gameplayMemory is not null)
            throw new InvalidOperationException("Native game keys require one gameplay owner in the original application update.");
        _gameplayMemory = memory;
    }
    private static int KeyBit(string action)=>action switch
    {"attack"=>1,"item"=>2,"map"=>4,"inventory"=>8,"move_right"=>16,"move_left"=>32,"move_up"=>64,"move_down"=>128,_=>0};
    private static int ReadSnapshotGameKeys()
    {
        int keys=0;foreach(string action in GameActions)
            if(_current!.Value.IsPressed(action))keys|=KeyBit(action);
        return keys;
    }

    internal static bool IsActionPressed(
        string action,
        bool exactMatch = false) =>
        _gameplayMemory is not null && KeyBit(action) is >0 ? (_gameplayMemory.ReadField("wGameKeysPressed")&KeyBit(action))!=0 : _current?.IsPressed(action) ??
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
        _gameplayMemory is not null && KeyBit(action) is >0 ? (_gameplayMemory.ReadField("wGameKeysJustPressed")&KeyBit(action))!=0 : _current?.IsJustPressed(action) ??
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
            if (_gameplayMemory is not null) return DirectionForButtons(_gameplayMemory.ReadField("wGameKeysPressed") >> 4);
            return _current?.Movement ?? ReadMovement();
        }
        return Godot.Input.GetVector(
            negativeX, positiveX, negativeY, positiveY, deadzone);
    }

    internal static Vector2 ReadMovement()
    {
        int buttons = (Godot.Input.IsActionPressed(MoveRight) ? 1 : 0) |
            (Godot.Input.IsActionPressed(MoveLeft) ? 2 : 0) |
            (Godot.Input.IsActionPressed(MoveUp) ? 4 : 0) |
            (Godot.Input.IsActionPressed(MoveDown) ? 8 : 0);
        return DirectionForButtons(buttons);
    }

    // specialObjects.s:updateGameKeysPressed @directionButtonToAngle rejects
    // opposing D-pad buttons, including chords with a third direction held.
    private static Vector2 DirectionForButtons(int buttons) => (buttons switch
    {
        1 => Vector2.Right,
        2 => Vector2.Left,
        4 => Vector2.Up,
        5 => new Vector2(1, -1),
        6 => new Vector2(-1, -1),
        8 => Vector2.Down,
        9 => new Vector2(1, 1),
        10 => new Vector2(-1, 1),
        _ => Vector2.Zero
    }).LimitLength();

    internal static void ActionPress(StringName action, float strength = 1.0f) =>
        Godot.Input.ActionPress(action, strength);

    internal static void ActionRelease(StringName action) =>
        Godot.Input.ActionRelease(action);
}
