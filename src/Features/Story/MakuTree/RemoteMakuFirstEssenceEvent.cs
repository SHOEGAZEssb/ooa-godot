using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Room 0:8d's first INTERAC_REMOTE_MAKU_CUTSCENE $8a:$00. The imported
/// interaction-script lane coordinates the HUD, palette threads, dialogue,
/// music, room flag, and Maku state around the native $62 confetti object.
/// </summary>
internal sealed class RemoteMakuFirstEssenceEvent :
    IRoomEntryEvent,
    ICutsceneCommandHost
{
    private readonly RoomEventContext _context;
    private readonly RemoteMakuFirstEssenceDatabase _database = new();
    private readonly CutsceneCommandRunner _runner;
    private RemoteMakuFirstEssenceEventStage _stage;
    private RemoteMakuConfettiEffect? _confetti;
    private int _textboxFlags;
    private int _dontUpdateStatusBar;
    private Vector2 _fadeOriginalPosition;
    private Vector2 _fadeOriginalSize;
    private int _fadeOriginalZIndex;
    private bool _fadePresentationOwned;

    internal RemoteMakuFirstEssenceEvent(RoomEventContext context)
    {
        _context = context;
        _runner = new CutsceneCommandRunner(this);
    }

    public bool HasState => _stage != RemoteMakuFirstEssenceEventStage.Inactive;
    public bool BlocksGameplay =>
        _stage == RemoteMakuFirstEssenceEventStage.Running;
    internal RemoteMakuFirstEssenceEventStage Stage => _stage;
    internal int CommandInstruction => _runner.Instruction;
    internal int CommandCounter => _runner.Counter;
    internal int TextboxFlags => _textboxFlags;
    internal int DontUpdateStatusBar => _dontUpdateStatusBar;
    internal RemoteMakuConfettiEffect? Confetti => _confetti;
    internal RemoteMakuFirstEssenceDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room)
    {
        RemoteMakuFirstEssenceRecord record = _database.Record;
        OracleSaveData save = _context.Rooms.SaveData;
        return group == record.Group &&
            room.Id == record.Room &&
            (save.ReadWramByte(0xc6bf) & record.EssenceMask) != 0 &&
            !save.HasRoomFlag(record.Group, record.Room, (byte)record.RoomFlag);
    }

    public void Start(OracleRoomData _)
    {
        Cancel();
        _stage = RemoteMakuFirstEssenceEventStage.Running;
        _runner.Start(_database.Commands);
    }

    public void UpdateFrame()
    {
        if (!HasState)
            return;

        if (_stage == RemoteMakuFirstEssenceEventStage.Running)
            _runner.AdvanceFrame();
        _confetti?.UpdateFrame();

        if (_stage == RemoteMakuFirstEssenceEventStage.Tail &&
            (_confetti is null || _confetti.Finished))
        {
            RemoveConfetti();
            _stage = RemoteMakuFirstEssenceEventStage.Inactive;
        }
    }

    public void Cancel()
    {
        _runner.Clear();
        _context.Player.EndCutsceneControl();
        _context.Hud.ShowStatusBar();
        _context.RoomView.ClearBackgroundFade();
        RestoreFadePresentation();
        RemoveConfetti();
        _textboxFlags = 0;
        _dontUpdateStatusBar = 0;
        _stage = RemoteMakuFirstEssenceEventStage.Inactive;
    }

    private void SpawnPresentConfetti()
    {
        RemoveConfetti();
        Vector2 cameraOrigin = _context.RoomCamera.Position - new Vector2(
            OracleRoomData.ViewportWidth / 2.0f,
            OracleRoomData.ScreenHeight / 2.0f -
                OracleRoomData.GameplayScreenTop);
        _confetti = new RemoteMakuConfettiEffect
        {
            Name = "RemoteMakuPresentConfetti"
        };
        _confetti.Initialize(_database, _context.Sound, cameraOrigin);
        _context.Player.GetParent().AddChild(_confetti);
    }

    private void RemoveConfetti()
    {
        if (_confetti is null)
            return;
        Node? parent = _confetti.GetParent();
        parent?.RemoveChild(_confetti);
        _confetti.QueueFree();
        _confetti = null;
    }

    private void OwnFullScreenFade()
    {
        if (_fadePresentationOwned)
            return;
        _fadePresentationOwned = true;
        _fadeOriginalPosition = _context.Fade.Position;
        _fadeOriginalSize = _context.Fade.Size;
        _fadeOriginalZIndex = _context.Fade.ZIndex;
        _context.Fade.Position = Vector2.Zero;
        _context.Fade.Size = new Vector2(
            OracleRoomData.ViewportWidth,
            OracleRoomData.ScreenHeight);
        _context.Fade.ZIndex = _context.Hud.ZIndex + 1;
    }

    private void RestoreFadePresentation()
    {
        _context.Fade.Color = new Color(1, 1, 1, 0);
        if (!_fadePresentationOwned)
            return;
        _context.Fade.Position = _fadeOriginalPosition;
        _context.Fade.Size = _fadeOriginalSize;
        _context.Fade.ZIndex = _fadeOriginalZIndex;
        _fadePresentationOwned = false;
    }

    private bool UpdatePaletteFade(
        string handler,
        int commandUpdate,
        int frames)
    {
        int steps = Math.Min(32, (commandUpdate + 1) / 2);
        float progress = steps / 32.0f;
        switch (handler)
        {
            case "FadeOutBlack":
                _context.RoomView.SetBackgroundFade(Colors.Black, progress);
                break;
            case "FadeInWhite":
                OwnFullScreenFade();
                _context.Fade.Color = new Color(1, 1, 1, 1.0f - progress);
                break;
            default:
                throw Unsupported($"update native handler '{handler}'");
        }

        if (commandUpdate + 1 < frames)
            return false;
        if (handler == "FadeInWhite")
            RestoreFadePresentation();
        return true;
    }

    RoomEventContext ICutsceneCommandHost.Context => _context;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) => false;
    void ICutsceneCommandHost.SetMenuEnabled(bool enabled) =>
        throw Unsupported($"set menu enabled={enabled}");
    void ICutsceneCommandHost.SetDisabledObjects(int value) =>
        throw Unsupported($"set disabled objects=${value:x2}");
    bool ICutsceneCommandHost.GateOpen(string gate) =>
        throw Unsupported($"read gate '{gate}'");
    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        throw Unsupported($"read '{binding}'=${value:x2}");

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        RemoteMakuFirstEssenceRecord record = _database.Record;
        int expectedText = _context.Rooms.SaveData.IsLinkedGame
            ? record.LinkedTextId
            : record.StandardTextId;
        if (textId != expectedText)
            throw Unsupported($"show text TX_{textId:x4}");
        _context.Rooms.SaveData.SetMakuMapTextPresent(
            _context.Rooms.SaveData.IsLinkedGame
                ? record.LinkedMapText
                : record.StandardMapText);
        _context.ShowDialogue(message, textboxFlags: _textboxFlags);
    }

    void ICutsceneCommandHost.SetActorAnimation(
        string actor, int animation, string encodedAnimation) =>
        throw Unsupported($"set actor '{actor}' animation ${animation:x2}");
    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor, int angle, string encodedAnimation) =>
        throw Unsupported($"set actor '{actor}' movement angle ${angle:x2}");
    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor, int radiusY, int radiusX) =>
        throw Unsupported($"set actor '{actor}' collision");
    void ICutsceneCommandHost.SetActorButtonSensitive(string actor) =>
        throw Unsupported($"set actor '{actor}' A-button sensitivity");
    void ICutsceneCommandHost.MoveActorAtSpeed(
        string actor, int speed, int angle) =>
        throw Unsupported($"move actor '{actor}'");
    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        throw Unsupported($"set actor '{actor}' Z");
    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        throw Unsupported($"set actor '{actor}' visibility={visible}");

    void ICutsceneCommandHost.WriteMemory(string binding, int value)
    {
        switch (binding)
        {
            case "TextboxFlags":
                _textboxFlags = value;
                break;
            case "DontUpdateStatusBar":
                _dontUpdateStatusBar = value;
                break;
            default:
                throw Unsupported($"write '{binding}'=${value:x2}");
        }
    }

    void ICutsceneCommandHost.SetMusic(int music)
    {
        if (music != _database.Record.Music)
            throw Unsupported($"set music ${music:x2}");
        _context.Sound.PlaySound(music);
    }

    void ICutsceneCommandHost.OrRoomFlag(int flag)
    {
        if (flag != _database.Record.RoomFlag)
            throw Unsupported($"set room flag ${flag:x2}");
        _context.Rooms.SaveData.SetRoomFlag(
            _database.Record.Group,
            _database.Record.Room,
            (byte)flag);
    }

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "HideHud":
                _context.Hud.HideStatusBar();
                break;
            case "SpawnPresentConfetti":
                SpawnPresentConfetti();
                break;
            case "ShowHud":
                _context.Hud.ShowStatusBar();
                break;
            case "ClearFadingPalettes":
                _context.RoomView.ClearBackgroundFade();
                break;
            case "ResetMusic":
                _context.Sound.PlayRoomMusic(
                    _database.Record.Group,
                    _database.Record.Room);
                break;
            case "IncMakuTreeState":
                _context.Rooms.SaveData.SetMakuTreeState(Math.Min(
                    0xff,
                    _context.Rooms.SaveData.MakuTreeState + 1));
                break;
            default:
                throw Unsupported($"run native handler '{handler}'");
        }
    }

    bool ICutsceneCommandHost.UpdateNativeHandler(
        string handler,
        CutsceneActorId? actor,
        int commandUpdate,
        int frames,
        string payload)
    {
        if (actor is not null || !string.IsNullOrEmpty(payload))
            throw Unsupported($"update native handler '{handler}' payload");
        return UpdatePaletteFade(handler, commandUpdate, frames);
    }

    void ICutsceneCommandHost.ScriptEnded()
    {
        _stage = _confetti is { Finished: false }
            ? RemoteMakuFirstEssenceEventStage.Tail
            : RemoteMakuFirstEssenceEventStage.Inactive;
        if (_stage == RemoteMakuFirstEssenceEventStage.Inactive)
            RemoveConfetti();
    }

    private static InvalidOperationException Unsupported(string operation) =>
        new($"Room 0:8d remote Maku event cannot {operation}.");
}

internal enum RemoteMakuFirstEssenceEventStage
{
    Inactive,
    Running,
    Tail
}
