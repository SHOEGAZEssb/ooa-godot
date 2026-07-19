using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Stable importer-owned actor identifier. Keeping this distinct from arbitrary
/// strings lets a host validate every binding before a command stream starts.
/// </summary>
internal readonly record struct CutsceneActorId
{
    public string Value { get; }

    public CutsceneActorId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A cutscene actor identifier cannot be empty.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;

    public static implicit operator CutsceneActorId(string value) => new(value);
    public static implicit operator string(CutsceneActorId actor) => actor.Value;
}

internal readonly record struct CutsceneCommandSource(
    string Script,
    string Label,
    int CommandIndex,
    int SourceLine,
    string Opcode)
{
    public override string ToString() =>
        $"{Script}:{Label}[{CommandIndex}] line {SourceLine} ({Opcode})";
}

internal abstract record CutsceneCommand(CutsceneCommandSource Source);

internal sealed record CutsceneDisableInputCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);

internal sealed record CutsceneDisableMenuCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);

internal sealed record CutsceneSetDisabledObjectsCommand(
    CutsceneCommandSource Source,
    int Value)
    : CutsceneCommand(Source);

/// <summary>A native WRAM write that carries into the next operation.</summary>
internal sealed record CutsceneSetDisabledObjectsContinueCommand(
    CutsceneCommandSource Source,
    int Value)
    : CutsceneCommand(Source);

internal sealed record CutsceneSetCounterCommand(
    CutsceneCommandSource Source,
    int Frames)
    : CutsceneCommand(Source);

/// <summary>
/// Decrements a counter installed by an earlier native operation. Unlike
/// <see cref="CutsceneWaitCommand"/>, the first runner update consumes a frame.
/// </summary>
internal sealed record CutsceneWaitPreloadedCounterCommand(
    CutsceneCommandSource Source)
    : CutsceneCommand(Source);

internal sealed record CutsceneWaitCommand(CutsceneCommandSource Source, int Frames)
    : CutsceneCommand(Source);

/// <summary>
/// Fixed-update orchestration wait whose first dispatch consumes frame one and
/// whose completion yields. This matches RoomEventTimeline's established
/// duration without changing the interaction-script wait opcode above.
/// </summary>
internal sealed record CutsceneWaitFramesCommand(
    CutsceneCommandSource Source,
    int Frames)
    : CutsceneCommand(Source);

internal sealed record CutsceneShowTextCommand(
    CutsceneCommandSource Source,
    int TextId,
    string Message)
    : CutsceneCommand(Source);

/// <summary>
/// Opens dialogue, blocks until it closes, then yields its completion update.
/// Used by imported multi-object orchestration whose controller owned that
/// extra command boundary.
/// </summary>
internal sealed record CutsceneDialogueCommand(
    CutsceneCommandSource Source,
    int TextId,
    string Message)
    : CutsceneCommand(Source);

internal sealed record CutsceneShowTextVariantsCommand(
    CutsceneCommandSource Source,
    int StandardTextId,
    string StandardMessage,
    int LinkedTextId,
    string LinkedMessage)
    : CutsceneCommand(Source);

internal sealed record CutsceneSetAnimationCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Animation,
    string EncodedAnimation)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

/// <summary>An asm helper animation change that carries into the next command.</summary>
internal sealed record CutsceneSetAnimationContinueCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Animation,
    string EncodedAnimation)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

internal sealed record CutsceneSetCollisionRadiiCommand(
    CutsceneCommandSource Source,
    string Actor,
    int RadiusY,
    int RadiusX)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

internal sealed record CutsceneMakeAButtonSensitiveCommand(
    CutsceneCommandSource Source,
    string Actor)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

internal sealed record CutsceneGateCommand(
    CutsceneCommandSource Source,
    string Gate)
    : CutsceneCommand(Source);

internal sealed record CutsceneMemoryGateCommand(
    CutsceneCommandSource Source,
    string Binding,
    int Value)
    : CutsceneCommand(Source);

internal sealed record CutsceneMemoryBranchCommand(
    CutsceneCommandSource Source,
    string Binding,
    int Value,
    int TargetCommand)
    : CutsceneCommand(Source);

internal sealed record CutsceneBranchCommand(
    CutsceneCommandSource Source,
    int TargetCommand)
    : CutsceneCommand(Source);

internal sealed record CutsceneCallCommand(
    CutsceneCommandSource Source,
    int TargetCommand)
    : CutsceneCommand(Source);

internal sealed record CutsceneReturnCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);

internal sealed record CutsceneSetSpeedCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Speed)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

internal sealed record CutsceneSetAngleCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Angle)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

internal sealed record CutsceneApplySpeedCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Counter)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

/// <summary>
/// Cardinal NPC movement opcodes set angle, select the corresponding
/// animation, and install counter2 in one script update.
/// </summary>
internal sealed record CutsceneMoveCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Angle,
    int Counter,
    string EncodedAnimation)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

/// <summary>
/// Typed expansion of the shared jumpAndWaitUntilLanded subscript. The first
/// update retains the callscript boundary; gravity begins on the next update.
/// </summary>
internal sealed record CutsceneJumpCommand(
    CutsceneCommandSource Source,
    string Actor,
    int InitialSpeedZ,
    int Gravity,
    int Sound)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

internal sealed record CutsceneWriteObjectByteCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Address,
    int Value)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

internal sealed record CutsceneWriteMemoryCommand(
    CutsceneCommandSource Source,
    string Binding,
    int Value)
    : CutsceneCommand(Source);

internal sealed record CutscenePlaySoundCommand(
    CutsceneCommandSource Source,
    int Sound)
    : CutsceneCommand(Source);

/// <summary>
/// Recognized native objectFlickerVisibility/dec-counter script loop. The
/// counter byte and frame mask remain explicit imported operands.
/// </summary>
internal sealed record CutsceneFlickerCommand(
    CutsceneCommandSource Source,
    string Actor,
    int CounterAddress,
    int FrameMask)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

/// <summary>Exact fixed-update displacement used by imported controller lanes.</summary>
internal sealed record CutsceneTranslateCommand(
    CutsceneCommandSource Source,
    string Actor,
    Vector2 Delta,
    int Frames,
    int Animation,
    bool SetAnimationOnStart)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

/// <summary>
/// Two independently timed actor displacements dispatched in stable actor
/// order. The command completes after the longer lane has completed.
/// </summary>
internal sealed record CutsceneParallelTranslateCommand(
    CutsceneCommandSource Source,
    string Actor,
    Vector2 Delta,
    int Frames,
    string Actor2,
    Vector2 Delta2,
    int Frames2)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
    public CutsceneActorId Actor2Id { get; } = new(Actor2);
}

internal sealed record CutsceneDeleteActorCommand(
    CutsceneCommandSource Source,
    string Actor)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

internal sealed record CutsceneSetGlobalFlagCommand(
    CutsceneCommandSource Source,
    int Flag)
    : CutsceneCommand(Source);

internal sealed record CutsceneOrRoomFlagCommand(
    CutsceneCommandSource Source,
    int Flag)
    : CutsceneCommand(Source);

/// <summary>
/// Native object-code room-flag mutation that remains in the same object
/// update. This is deliberately distinct from scriptCmd_orRoomFlags, whose
/// no-carry return yields the interaction script.
/// </summary>
internal sealed record CutsceneOrRoomFlagContinueCommand(
    CutsceneCommandSource Source,
    int Flag)
    : CutsceneCommand(Source);

internal sealed record CutsceneNativeCommand(
    CutsceneCommandSource Source,
    string Handler)
    : CutsceneCommand(Source);

/// <summary>A native controller mutation that owns one fixed update.</summary>
internal sealed record CutsceneNativeYieldCommand(
    CutsceneCommandSource Source,
    string Handler)
    : CutsceneCommand(Source);

/// <summary>
/// A bespoke object-code handler retained outside the script interpreter. The
/// shared runner owns its command boundary while the event host owns its state.
/// </summary>
internal sealed record CutsceneNativeBlockingCommand(
    CutsceneCommandSource Source,
    string Handler,
    CutsceneActorId? Actor,
    int Frames,
    string Payload)
    : CutsceneCommand(Source);

internal sealed record CutsceneEnableInputCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);

internal sealed record CutsceneEndCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);

internal enum CutsceneCommandTracePhase
{
    Started,
    Updated,
    Completed
}

internal readonly record struct CutsceneCommandTraceEntry(
    int ScriptUpdate,
    CutsceneCommandSource Source,
    CutsceneCommandTracePhase Phase,
    int Counter,
    int NextCommandIndex);

internal readonly record struct CutsceneObservationTraceEntry(
    int Frame,
    string Event,
    string Observation,
    CutsceneActorId? Actor,
    int Value,
    Vector2 Position);

internal interface ICutsceneCommandTraceSink
{
    void Record(CutsceneCommandTraceEntry entry);

    void RecordObservation(CutsceneObservationTraceEntry entry)
    {
    }
}

internal interface ICutsceneCommandHost
{
    bool DialogueOpen { get; }
    bool IsLinkedGame { get; }
    int FrameCounter { get; }
    ICutsceneCommandTraceSink? TraceSink { get; }

    bool HasActorBinding(CutsceneActorId actor) => true;

    void SetInputEnabled(bool enabled);
    void SetMenuEnabled(bool enabled);
    void SetDisabledObjects(int value);
    bool GateOpen(string gate);
    bool MemoryEquals(string binding, int value);
    void ShowText(int textId, string message);
    void SetActorAnimation(string actor, int animation, string encodedAnimation);
    void SetActorMovementAnimation(string actor, int angle, string encodedAnimation);
    void SetActorCollisionRadii(string actor, int radiusY, int radiusX);
    void SetActorButtonSensitive(string actor);
    void MoveActorAtSpeed(string actor, int speed, int angle);
    void SetActorZ(string actor, int zFixed);
    void SetActorVisible(string actor, bool visible);
    void WriteObjectByte(string actor, int address, int value)
    {
    }
    Vector2 GetActorPosition(CutsceneActorId actor) =>
        throw new InvalidOperationException($"Actor '{actor}' does not support translated movement.");
    void SetActorPosition(
        CutsceneActorId actor,
        Vector2 position,
        Vector2 facingDelta,
        Vector2 movement) =>
        throw new InvalidOperationException($"Actor '{actor}' does not support translated movement.");
    void CompleteActorTranslation(CutsceneActorId actor)
    {
    }
    void DeleteActor(CutsceneActorId actor) =>
        throw new InvalidOperationException($"Actor '{actor}' does not support deletion.");
    void WriteMemory(string binding, int value);
    void PlaySound(int sound);
    void SetGlobalFlag(int flag);
    void OrRoomFlag(int flag);
    void RunNativeHandler(string handler);
    bool UpdateNativeHandler(
        string handler,
        CutsceneActorId? actor,
        int commandUpdate,
        int frames,
        string payload) =>
        throw new InvalidOperationException(
            $"Native cutscene handler '{handler}' does not support blocking updates.");
    void ScriptEnded();
}
