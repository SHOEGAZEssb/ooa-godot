using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal abstract class CutsceneCommandHost : ICutsceneCommandHost
{
    private CutsceneCommandSource? _activeSource;

    public virtual bool DialogueOpen => throw UnsupportedCommand("read dialogue state");
    public virtual bool ScriptExecutionBlocked => DialogueOpen;
    public virtual bool IsLinkedGame => throw UnsupportedCommand("read linked-game state");
    public virtual int FrameCounter => throw UnsupportedCommand("read the frame counter");
    public virtual ICutsceneCommandTraceSink? TraceSink => null;

    public void SetActiveCommandSource(CutsceneCommandSource? source) =>
        _activeSource = source;

    public virtual bool HasActorBinding(CutsceneActorId actor) => false;
    public virtual void SetInputEnabled(bool enabled) =>
        throw UnsupportedCommand($"set input enabled={enabled}");
    public virtual void SetMenuEnabled(bool enabled) =>
        throw UnsupportedCommand($"set menu enabled={enabled}");
    public virtual void SetDisabledObjects(int value) =>
        throw UnsupportedCommand($"set disabled objects ${value:x2}");
    public virtual bool GateOpen(string gate) =>
        throw UnsupportedCommand($"read gate '{gate}'");
    public virtual bool MemoryEquals(string binding, int value) =>
        throw UnsupportedCommand($"compare '{binding}' with ${value:x2}");
    public virtual int ReadMemory(string binding) =>
        throw UnsupportedCommand($"read '{binding}'");
    public virtual bool RoomFlagSet(int flag) =>
        throw UnsupportedCommand($"read room flag ${flag:x2}");
    public virtual bool TradeItemEquals(int value) =>
        throw UnsupportedCommand($"compare trade item ${value:x2}");
    public virtual bool TextOptionEquals(int value) =>
        throw UnsupportedCommand($"read text option ${value:x2}");
    public virtual bool TryConsumeActorButton(CutsceneActorId actor) =>
        throw UnsupportedCommand($"consume A for actor '{actor}'");
    public virtual void ShowText(int textId, string message) =>
        throw UnsupportedCommand($"show text ${textId:x4}");
    public virtual void ShowText(
        int textId,
        string message,
        int? textboxPosition)
    {
        if (textboxPosition is int position)
            throw UnsupportedCommand($"show text ${textId:x4} at position ${position:x2}");
        ((ICutsceneCommandHost)this).ShowText(textId, message);
    }
    public virtual void ShowLoadedText() =>
        throw UnsupportedCommand("show the loaded text");
    public virtual void SetActorAnimation(
        string actor, int animation, string encodedAnimation) =>
        throw UnsupportedCommand($"set actor '{actor}' animation ${animation:x2}");
    public virtual void SetActorMovementAnimation(
        string actor, int angle, string encodedAnimation) =>
        throw UnsupportedCommand($"set actor '{actor}' movement animation ${angle:x2}");
    public virtual void SetActorCollisionRadii(
        string actor, int radiusY, int radiusX) =>
        throw UnsupportedCommand($"set actor '{actor}' collision radii");
    public virtual void SetActorButtonSensitive(string actor) =>
        throw UnsupportedCommand($"set actor '{actor}' A-button sensitivity");
    public virtual void InitializeActorCollisionRadii(string actor) =>
        throw UnsupportedCommand($"initialize actor '{actor}' collision radii");
    public virtual void MoveActorAtSpeed(string actor, int speed, int angle) =>
        throw UnsupportedCommand($"move actor '{actor}'");
    public virtual void SetActorCoordinates(string actor, int y, int x) =>
        throw UnsupportedCommand($"set actor '{actor}' coordinates ${y:x2}/${x:x2}");
    public virtual void SetActorZ(string actor, int zFixed) =>
        throw UnsupportedCommand($"set actor '{actor}' Z");
    public virtual void SetActorVisible(string actor, bool visible) =>
        throw UnsupportedCommand($"set actor '{actor}' visible={visible}");
    public virtual void WriteObjectByte(string actor, int address, int value) =>
        throw UnsupportedCommand($"write actor '{actor}'.${address:x2}=${value:x2}");
    public virtual Vector2 GetActorPosition(CutsceneActorId actor) =>
        throw UnsupportedCommand($"read actor '{actor}' position");
    public virtual void SetActorPosition(
        CutsceneActorId actor,
        Vector2 position,
        Vector2 facingDelta,
        Vector2 movement) =>
        throw UnsupportedCommand($"set actor '{actor}' position");
    public virtual void CompleteActorTranslation(CutsceneActorId actor) =>
        throw UnsupportedCommand($"complete actor '{actor}' translation");
    public virtual void DeleteActor(CutsceneActorId actor) =>
        throw UnsupportedCommand($"delete actor '{actor}'");
    public virtual void WriteMemory(string binding, int value) =>
        throw UnsupportedCommand($"write '{binding}'=${value:x2}");
    public virtual void GiveItem(int treasureId, int parameter) =>
        throw UnsupportedCommand($"give treasure ${treasureId:x2}:${parameter:x2}");
    public virtual void PlaySound(int sound) =>
        throw UnsupportedCommand($"play sound ${sound:x2}");
    public virtual void SetMusic(int music) =>
        throw UnsupportedCommand($"set music ${music:x2}");
    public virtual void SetGlobalFlag(int flag) =>
        throw UnsupportedCommand($"set global flag ${flag:x2}");
    public virtual void OrRoomFlag(int flag) =>
        throw UnsupportedCommand($"OR room flag ${flag:x2}");
    public virtual void RunNativeHandler(string handler) =>
        throw UnsupportedCommand($"run native handler '{handler}'");
    public virtual bool UpdateNativeHandler(
        string handler,
        CutsceneActorId? actor,
        int commandUpdate,
        int frames,
        string payload) =>
        throw UnsupportedCommand($"update native handler '{handler}'");
    public virtual void ScriptEnded() => throw UnsupportedCommand("end the script");

    protected InvalidOperationException UnsupportedCommand(string operation)
    {
        CutsceneCommandSchemaEntry? schema = _activeSource is { } source
            ? CutsceneCommandSchema.FindOpcode(source.Opcode)
            : null;
        string capabilities = schema is null
            ? string.Empty
            : $" requiring [{string.Join(", ", schema.Capabilities)}]";
        return new InvalidOperationException(
            $"{GetType().Name} cannot {operation}{capabilities} at " +
            (_activeSource?.ToString() ?? "an unknown cutscene command"));
    }
}
