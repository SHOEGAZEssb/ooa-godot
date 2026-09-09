using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Shared command host for one INTERAC_REMOTE_MAKU_CUTSCENE $8a placement.
/// Concrete events own their independent entry predicates and lifecycles; this
/// type implements the source-shared script, palette, HUD, music, era-selected
/// map state, and native $62 confetti behavior.
/// </summary>
internal abstract class RemoteMakuEvent :
    CutsceneCommandHost,
    IRoomEvent,
    ICutsceneCommandHost,
    IUpdatesDuringDialogueRoomEvent
{
    private readonly RemoteMakuEventDatabase _database;
    private readonly CutsceneCommandRunner _runner;
    private RemoteMakuEventStage _stage;
    private RemoteMakuConfettiEffect? _confetti;
    private int _textboxFlags;
    private int _dontUpdateStatusBar;

    protected RemoteMakuEvent(
        RoomEventContext context,
        RemoteMakuEventDatabase database)
    {
        Context = context;
        _database = database;
        _runner = new CutsceneCommandRunner(this);
    }

    protected RoomEventContext Context { get; }
    public bool HasState => _stage != RemoteMakuEventStage.Inactive;
    public bool BlocksGameplay => _stage == RemoteMakuEventStage.Running;
    internal RemoteMakuEventStage Stage => _stage;
    internal int CommandInstruction => _runner.Instruction;
    internal int CommandCounter => _runner.Counter;
    internal int TextboxFlags => _textboxFlags;
    internal int DontUpdateStatusBar => _dontUpdateStatusBar;
    internal RemoteMakuConfettiEffect? Confetti => _confetti;
    internal RemoteMakuEventRecord Record => _database.Record;

    protected bool MatchesEssenceRoom(int group, OracleRoomData room)
    {
        RemoteMakuEventRecord record = _database.Record;
        OracleSaveData save = Context.Rooms.SaveData;
        return group == record.Group &&
            room.Id == record.Room &&
            (save.ReadWramByte(0xc6bf) & record.EssenceMask) != 0 &&
            !save.HasRoomFlag(
                record.Group,
                record.Room,
                (byte)record.RoomFlag);
    }

    protected void Begin()
    {
        Cancel();
        _stage = RemoteMakuEventStage.Running;
        _runner.Start(_database.Commands);
    }

    public void UpdateFrame()
    {
        if (!HasState)
            return;

        if (_stage == RemoteMakuEventStage.Running)
            _runner.AdvanceFrame();
        _confetti?.UpdateFrame();

        if (_stage == RemoteMakuEventStage.Tail &&
            (_confetti is null || _confetti.Finished))
        {
            RemoveConfetti();
            _stage = RemoteMakuEventStage.Inactive;
        }
    }

    public void UpdateDuringDialogueFrame() => _confetti?.UpdateFrame();

    public virtual void Cancel()
    {
        _runner.Clear();
        Context.Player.EndCutsceneControl();
        Context.Hud.ShowStatusBar();
        Context.RoomView.ClearBackgroundFade();
        RestoreFadePresentation();
        RemoveConfetti();
        _textboxFlags = 0;
        _dontUpdateStatusBar = 0;
        _stage = RemoteMakuEventStage.Inactive;
    }

    private void SpawnConfetti(RemoteMakuConfettiKind kind)
    {
        if (_database.Record.ConfettiKind != kind)
            throw UnsupportedCommand($"spawn {kind} confetti");
        RemoveConfetti();
        Vector2 cameraOrigin = Context.RoomCamera.Position - new Vector2(
            OracleRoomData.ViewportWidth / 2.0f,
            OracleRoomData.ScreenHeight / 2.0f -
                OracleRoomData.GameplayScreenTop);
        _confetti = new RemoteMakuConfettiEffect
        {
            Name = kind == RemoteMakuConfettiKind.Past
                ? "RemoteMakuPastConfetti"
                : "RemoteMakuPresentConfetti"
        };
        _confetti.Initialize(_database, Context.Sound, cameraOrigin);
        Context.Player.GetParent().AddChild(_confetti);
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

    private void RestoreFadePresentation()
    {
        Context.Fade.Color = new Color(1, 1, 1, 0);
        ReleaseFullScreenFade(restoreColor: false);
    }

    private bool UpdatePaletteFade(
        string handler,
        int commandUpdate,
        int frames)
    {
        int steps = Math.Min(
            32,
            (commandUpdate + 1) / _database.Record.FadeDelay);
        float progress = steps / 32.0f;
        switch (handler)
        {
            case "FadeOutBlack":
                Context.RoomView.SetBackgroundFade(Colors.Black, progress);
                Context.Hud.SetHiddenStatusBarFade(Colors.Black, progress);
                break;
            case "FadeInWhite":
                CaptureFullScreenFade(Context.Hud.ZIndex + 1);
                Context.Fade.Color = new Color(1, 1, 1, 1.0f - progress);
                break;
            default:
                throw UnsupportedCommand($"update native handler '{handler}'");
        }

        if (commandUpdate + 1 < frames)
            return false;
        if (handler == "FadeInWhite")
            RestoreFadePresentation();
        return true;
    }

    RoomEventContext ICutsceneCommandHost.Context => Context;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) => false;
    bool ICutsceneCommandHost.GateOpen(string gate) =>
        throw UnsupportedCommand($"read gate '{gate}'");

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        RemoteMakuEventRecord record = _database.Record;
        int expectedText = Context.Rooms.SaveData.IsLinkedGame
            ? record.LinkedTextId
            : record.StandardTextId;
        if (textId != expectedText)
            throw UnsupportedCommand($"show text TX_{textId:x4}");
        int mapText = Context.Rooms.SaveData.IsLinkedGame
            ? record.LinkedMapText
            : record.StandardMapText;
        if (record.SubId == 1)
            Context.Rooms.SaveData.SetMakuMapTextPast(mapText);
        else
            Context.Rooms.SaveData.SetMakuMapTextPresent(mapText);
        Context.ShowDialogue(message, textboxFlags: _textboxFlags);
    }

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
                throw UnsupportedCommand($"write '{binding}'=${value:x2}");
        }
    }

    void ICutsceneCommandHost.SetMusic(int music)
    {
        RemoteMakuEventRecord record = _database.Record;
        if (music != record.Music)
            throw UnsupportedCommand($"set music ${music:x2}");
        Context.Sound.PlaySound(music);
    }

    void ICutsceneCommandHost.OrRoomFlag(int flag)
    {
        RemoteMakuEventRecord record = _database.Record;
        if (flag != record.RoomFlag)
            throw UnsupportedCommand($"set room flag ${flag:x2}");
        Context.Rooms.SaveData.SetRoomFlag(
            record.Group,
            record.Room,
            (byte)flag);
    }

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        RemoteMakuEventRecord record = _database.Record;
        switch (handler)
        {
            case "HideHud":
                Context.Hud.HideStatusBar();
                break;
            case "SpawnPresentConfetti":
                SpawnConfetti(RemoteMakuConfettiKind.Present);
                break;
            case "SpawnPastConfetti":
                SpawnConfetti(RemoteMakuConfettiKind.Past);
                break;
            case "ShowHud":
                Context.Hud.ShowStatusBar();
                break;
            case "ClearFadingPalettes":
                Context.RoomView.ClearBackgroundFade();
                break;
            case "ResetMusic":
                Context.Sound.PlayRoomMusic(record.Group, record.Room);
                break;
            case "IncMakuTreeState":
                Context.Rooms.SaveData.SetMakuTreeState(Math.Min(
                    0xff,
                    Context.Rooms.SaveData.MakuTreeState + 1));
                break;
            default:
                throw UnsupportedCommand($"run native handler '{handler}'");
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
            throw UnsupportedCommand($"update native handler '{handler}' payload");
        return UpdatePaletteFade(handler, commandUpdate, frames);
    }

    void ICutsceneCommandHost.ScriptEnded()
    {
        _stage = _confetti is { Finished: false }
            ? RemoteMakuEventStage.Tail
            : RemoteMakuEventStage.Inactive;
        if (_stage == RemoteMakuEventStage.Inactive)
            RemoveConfetti();
    }

}

internal enum RemoteMakuEventStage
{
    Inactive,
    Running,
    Tail
}
