using Godot;
using System;

namespace oracleofages;

/// <summary>MENU_SECRET input and validation; the shared lifecycle owns fades and pause.</summary>
internal sealed class SecretEntryController(
    Node interfaceLayer, OracleMenuLifecycle lifecycle,
    OracleSaveData save, Action<int> playSound) : IOracleMenuLifecycleClient
{
    private readonly LinkedGameNpcDatabase _secrets = new();
    private MainMenuScreen? _screen;
    private Action<bool>? _completion;
    private int _expectedIndex;
    private bool _result;
    private bool _error;
    private int _errorDelay;
    private int _repeatKeys;
    private int _repeatCounter;
    private static readonly string[] Actions =
        ["attack", "item", "map", "inventory", "move_right", "move_left", "move_up", "move_down"];

    public string MenuName => "MENU_SECRET";
    internal bool IsActive => lifecycle.IsOwnedBy(this);
    internal MainMenuScreen? Screen => _screen;

    internal bool Open(int expectedIndex, Action<bool> completion)
    {
        if ((uint)expectedIndex >= 64) throw new ArgumentOutOfRangeException(nameof(expectedIndex));
        if (!lifecycle.TryBeginOpening(this)) return false;
        _expectedIndex = expectedIndex;
        _completion = completion;
        _result = false;
        _error = false;
        _errorDelay = _repeatKeys = _repeatCounter = 0;
        return true;
    }

    internal void Update(double delta)
    {
        if (!IsActive) return;
        if (!lifecycle.IsOpenFor(this))
        {
            lifecycle.Update(this, delta);
            return;
        }
        int held = 0, pressed = 0;
        for (int bit = 0; bit < Actions.Length; bit++)
        {
            if (Input.IsActionPressed(Actions[bit])) held |= 1 << bit;
            if (Input.IsActionJustPressed(Actions[bit])) pressed |= 1 << bit;
        }
        if (_error)
        {
            if (_errorDelay > 0) { _errorDelay--; return; }
            if (held == 0) return;
            _error = false;
            _screen!.ShowSecretError(false);
            return;
        }
        // bank0.s:getInputWithAutofire: 40-update delay, then every fourth.
        int directionKeys = held & 0xf0;
        if ((_repeatKeys & directionKeys) == 0) _repeatCounter = 0;
        else if (++_repeatCounter >= 0x28)
        {
            _repeatCounter = (_repeatCounter & 0x1f) | 0x80;
            if ((_repeatCounter & 3) == 0) pressed = held;
        }
        _repeatKeys = directionKeys;
        if (pressed == 0) return;
        int selected = 7;
        while ((pressed & (1 << selected)) == 0) selected--;
        playSound(selected >= 4 ? OracleSoundEngine.SndMenuMove :
            selected == 1 ? OracleSoundEngine.SndClink : OracleSoundEngine.SndSelectItem);
        switch (selected)
        {
            case 7: _screen!.MoveNameCursor(Vector2I.Down); break;
            case 6: _screen!.MoveNameCursor(Vector2I.Up); break;
            case 5: _screen!.MoveNameCursor(Vector2I.Left); break;
            case 4: _screen!.MoveNameCursor(Vector2I.Right); break;
            case 3: if (_screen!.SelectSecretLowerOption(3)) Submit(_screen.EnteredSecret); break;
            case 2: break;
            case 1: _screen!.DeleteNameCharacter(); break;
            case 0: Accept(); break;
        }
    }

    private void Accept()
    {
        if (_screen!.TryGetSelectedNameCharacter(out char character))
        {
            _screen.AppendNameCharacter(character);
            return;
        }
        switch (_screen.NameLowerChoice)
        {
            case 0: _screen.MoveNameEntryPosition(-1); break;
            case 1: _screen.MoveNameEntryPosition(1); break;
            case 2: if (_screen.SelectSecretLowerOption(2)) lifecycle.BeginClosing(this); break;
            case 3: if (_screen.SelectSecretLowerOption(3)) Submit(_screen.EnteredSecret); break;
        }
    }

    internal void Submit(ReadOnlySpan<byte> symbols)
    {
        if (!lifecycle.IsOpenFor(this)) throw new InvalidOperationException("Secret input is not open.");
        if (!_secrets.ValidateSecret(symbols, _expectedIndex, save))
        {
            _error = true;
            _errorDelay = 0x10;
            _screen!.ShowSecretError(true);
            playSound(OracleSoundEngine.SndError);
            return;
        }
        _result = true;
        playSound(OracleSoundEngine.SndSolvePuzzle);
        lifecycle.BeginClosing(this);
    }

    public void OpenAtWhite()
    {
        _screen = new MainMenuScreen { Name = "SecretEntry", ZIndex = 60 };
        interfaceLayer.AddChild(_screen);
        _screen.ShowSecretEntry();
    }

    public void CloseAtWhite()
    {
        if (_screen is null) return;
        _screen.GetParent()?.RemoveChild(_screen);
        _screen.QueueFree();
        _screen = null;
    }

    public void LifecycleClosed()
    {
        Action<bool>? completion = _completion;
        _completion = null;
        completion?.Invoke(_result);
    }
}
