using System;
using System.Collections.Generic;
using Godot;

namespace oracleofages;

/// <summary>
/// Fixed-update interpreter for importer-generated interaction-script records.
/// Each command explicitly preserves whether the original handler yielded or
/// continued dispatching commands in the same update.
/// </summary>
internal sealed class CutsceneCommandRunner(ICutsceneCommandHost host)
{
    private IReadOnlyList<CutsceneCommand> _commands = Array.Empty<CutsceneCommand>();
    private readonly Dictionary<(CutsceneActorId Actor, int Address), int> _objectBytes = new();
    private readonly Dictionary<CutsceneActorId, int> _speeds = new();
    private readonly Dictionary<CutsceneActorId, int> _angles = new();
    private readonly Stack<int> _returns = new();
    private int _instruction;
    private int _commandUpdates;
    private int _scriptUpdates;
    private int _nextInstruction = -1;
    private CutsceneActorId? _jumpActor;
    private Vector2 _startPosition;
    private Vector2 _startPosition2;
    private int _zFixed;
    private int _speedZ;

    public bool Active { get; private set; }
    public int Counter { get; private set; }
    public int ZFixed => _zFixed;
    public int CurrentCommandUpdates => _commandUpdates;
    public int Instruction => _instruction;
    public CutsceneCommand? CurrentCommand =>
        Active && _instruction < _commands.Count ? _commands[_instruction] : null;

    public int ActorSpeed(CutsceneActorId actor) =>
        _speeds.TryGetValue(actor, out int speed) ? speed : 0;

    /// <summary>
    /// Seeds motion bytes written by an interaction initializer before its
    /// script starts. Call only immediately after <see cref="Start"/>.
    /// </summary>
    public void SetInitialMotionRegisters(CutsceneActorId actor, int speed, int angle)
    {
        if (!Active || _instruction != 0 || _scriptUpdates != 0)
        {
            throw new InvalidOperationException(
                "Initial cutscene motion registers must be set before the first script update.");
        }
        _speeds[actor] = speed;
        _angles[actor] = angle;
    }

    public void Start(IReadOnlyList<CutsceneCommand> commands)
    {
        if (commands.Count == 0)
            throw new InvalidOperationException("A cutscene command stream cannot be empty.");
        for (int index = 0; index < commands.Count; index++)
        {
            if (commands[index].Source.CommandIndex != index)
            {
                throw new InvalidOperationException(
                    $"Cutscene command index mismatch at row {index}: " +
                    $"{commands[index].Source}.");
            }
            int target = commands[index] switch
            {
                CutsceneMemoryBranchCommand branch => branch.TargetCommand,
                CutsceneBranchCommand branch => branch.TargetCommand,
                CutsceneCallCommand call => call.TargetCommand,
                _ => -1
            };
            if (commands[index] is CutsceneMemoryBranchCommand or
                    CutsceneBranchCommand or CutsceneCallCommand &&
                (target < 0 || target >= commands.Count))
            {
                throw new InvalidOperationException(
                    $"Cutscene branch target {target} is outside the command stream at " +
                    $"{commands[index].Source}.");
            }

            foreach (CutsceneActorId actor in Actors(commands[index]))
            {
                if (!host.HasActorBinding(actor))
                {
                    throw new InvalidOperationException(
                        $"Cutscene actor binding '{actor}' is not registered at " +
                        $"{commands[index].Source}.");
                }
            }
        }

        _commands = commands;
        _objectBytes.Clear();
        _speeds.Clear();
        _angles.Clear();
        _returns.Clear();
        _instruction = 0;
        _commandUpdates = 0;
        _scriptUpdates = 0;
        _nextInstruction = -1;
        _jumpActor = null;
        _zFixed = 0;
        _speedZ = 0;
        _startPosition = Vector2.Zero;
        _startPosition2 = Vector2.Zero;
        Counter = 0;
        Active = true;
    }

    public void AdvanceFrame()
    {
        if (!Active || host.DialogueOpen)
            return;

        _scriptUpdates++;
        int dispatches = 0;
        while (Active)
        {
            if (_instruction >= _commands.Count)
            {
                throw new InvalidOperationException(
                    "Cutscene command stream reached EOF without scriptend.");
            }
            if (++dispatches > _commands.Count + 1)
            {
                throw new InvalidOperationException(
                    $"Cutscene command dispatch did not yield near {_commands[_instruction].Source}.");
            }

            CutsceneCommand command = _commands[_instruction];
            if (_commandUpdates == 0)
                Trace(command, CutsceneCommandTracePhase.Started);
            _nextInstruction = -1;
            CommandResult result = Execute(command);
            Trace(command, result == CommandResult.Block
                ? CutsceneCommandTracePhase.Updated
                : CutsceneCommandTracePhase.Completed);

            if (result == CommandResult.Block)
            {
                _commandUpdates++;
                return;
            }

            _instruction = _nextInstruction >= 0
                ? _nextInstruction
                : _instruction + 1;
            _commandUpdates = 0;
            if (result == CommandResult.Yield || result == CommandResult.End)
                return;
        }
    }

    public void Clear()
    {
        _commands = Array.Empty<CutsceneCommand>();
        _objectBytes.Clear();
        _speeds.Clear();
        _angles.Clear();
        _returns.Clear();
        _instruction = 0;
        _commandUpdates = 0;
        _scriptUpdates = 0;
        _nextInstruction = -1;
        _jumpActor = null;
        _zFixed = 0;
        _speedZ = 0;
        _startPosition = Vector2.Zero;
        _startPosition2 = Vector2.Zero;
        Counter = 0;
        Active = false;
    }

    private CommandResult Execute(CutsceneCommand command)
    {
        switch (command)
        {
            case CutsceneDisableInputCommand:
                host.SetInputEnabled(enabled: false);
                return CommandResult.Continue;

            case CutsceneDisableMenuCommand:
                host.SetMenuEnabled(enabled: false);
                return CommandResult.Continue;

            case CutsceneSetDisabledObjectsCommand disabledObjects:
                host.SetDisabledObjects(disabledObjects.Value);
                return CommandResult.Yield;

            case CutsceneSetDisabledObjectsContinueCommand disabledObjects:
                host.SetDisabledObjects(disabledObjects.Value);
                return CommandResult.Continue;

            case CutsceneSetCounterCommand counter:
                SetCounter(counter.Frames);
                return CommandResult.Continue;

            case CutsceneWaitPreloadedCounterCommand preloaded:
                if (Counter <= 0)
                {
                    throw new InvalidOperationException(
                        $"{preloaded.Source} cannot decrement an empty preloaded counter.");
                }
                SetCounter(Counter - 1);
                return Counter == 0 ? CommandResult.Continue : CommandResult.Block;

            case CutsceneWaitCommand wait:
                if (_commandUpdates == 0)
                {
                    SetCounter(wait.Frames);
                    return CommandResult.Block;
                }
                SetCounter(Counter - 1);
                return Counter == 0 ? CommandResult.Continue : CommandResult.Block;

            case CutsceneWaitFramesCommand wait:
                if (_commandUpdates == 0)
                    SetCounter(wait.Frames);
                SetCounter(Counter - 1);
                return Counter == 0 ? CommandResult.Yield : CommandResult.Block;

            case CutsceneShowTextCommand text:
                host.ShowText(text.TextId, text.Message);
                Observe(command, "Dialogue", value: text.TextId);
                return CommandResult.Yield;

            case CutsceneDialogueCommand text:
                if (_commandUpdates == 0)
                {
                    host.ShowText(text.TextId, text.Message);
                    Observe(command, "Dialogue", value: text.TextId);
                    return CommandResult.Block;
                }
                return CommandResult.Yield;

            case CutsceneShowTextVariantsCommand text:
                host.ShowText(
                    host.IsLinkedGame ? text.LinkedTextId : text.StandardTextId,
                    host.IsLinkedGame ? text.LinkedMessage : text.StandardMessage);
                return CommandResult.Yield;

            case CutsceneGateCommand gate:
                return host.GateOpen(gate.Gate)
                    ? CommandResult.Yield
                    : CommandResult.Block;

            case CutsceneMemoryGateCommand gate:
                return host.MemoryEquals(gate.Binding, gate.Value)
                    ? CommandResult.Yield
                    : CommandResult.Block;

            case CutsceneMemoryBranchCommand branch:
                if (host.MemoryEquals(branch.Binding, branch.Value))
                    _nextInstruction = branch.TargetCommand;
                return CommandResult.Continue;

            case CutsceneBranchCommand branch:
                _nextInstruction = branch.TargetCommand;
                return CommandResult.Continue;

            case CutsceneCallCommand call:
                _returns.Push(_instruction + 1);
                _nextInstruction = call.TargetCommand;
                return CommandResult.Yield;

            case CutsceneReturnCommand returned:
                if (!_returns.TryPop(out int returnInstruction))
                {
                    throw new InvalidOperationException(
                        $"Cutscene return stack is empty at {returned.Source}.");
                }
                _nextInstruction = returnInstruction;
                return CommandResult.Yield;

            case CutsceneSetAnimationCommand animation:
                host.SetActorAnimation(
                    animation.Actor, animation.Animation, animation.EncodedAnimation);
                Observe(command, "ActorAnimation", animation.ActorId, animation.Animation);
                return CommandResult.Yield;

            case CutsceneSetAnimationContinueCommand animation:
                host.SetActorAnimation(
                    animation.Actor, animation.Animation, animation.EncodedAnimation);
                Observe(command, "ActorAnimation", animation.ActorId, animation.Animation);
                return CommandResult.Continue;

            case CutsceneSetCollisionRadiiCommand collision:
                host.SetActorCollisionRadii(
                    collision.Actor, collision.RadiusY, collision.RadiusX);
                return CommandResult.Yield;

            case CutsceneMakeAButtonSensitiveCommand button:
                host.SetActorButtonSensitive(button.Actor);
                return CommandResult.Continue;

            case CutsceneSetSpeedCommand speed:
                _speeds[speed.Actor] = speed.Speed;
                return CommandResult.Yield;

            case CutsceneSetAngleCommand angle:
                _angles[angle.Actor] = angle.Angle;
                return CommandResult.Yield;

            case CutsceneApplySpeedCommand movement:
                if (_commandUpdates == 0)
                {
                    RequireRegister(_speeds, movement.Actor, "speed", movement.Source);
                    RequireRegister(_angles, movement.Actor, "angle", movement.Source);
                    SetCounter(movement.Counter);
                    return CommandResult.Block;
                }
                SetCounter(Counter - 1);
                if (Counter != 0)
                {
                    host.MoveActorAtSpeed(
                        movement.Actor,
                        _speeds[movement.Actor],
                        _angles[movement.Actor]);
                    return CommandResult.Block;
                }
                return CommandResult.Yield;

            case CutsceneMoveCommand movement:
                if (_commandUpdates == 0)
                {
                    RequireRegister(_speeds, movement.Actor, "speed", movement.Source);
                    _angles[movement.Actor] = movement.Angle;
                    host.SetActorMovementAnimation(
                        movement.Actor, movement.Angle, movement.EncodedAnimation);
                    SetCounter(movement.Counter);
                    return CommandResult.Block;
                }
                SetCounter(Counter - 1);
                if (Counter != 0)
                {
                    host.MoveActorAtSpeed(
                        movement.Actor,
                        _speeds[movement.Actor],
                        movement.Angle);
                    return CommandResult.Block;
                }
                return CommandResult.Yield;

            case CutsceneJumpCommand jump:
                // callscript itself returns without running the destination.
                if (_commandUpdates == 0)
                    return CommandResult.Block;
                if (_commandUpdates == 1)
                {
                    _jumpActor = jump.Actor;
                    _zFixed = 0;
                    _speedZ = jump.InitialSpeedZ;
                    host.PlaySound(jump.Sound);
                }
                else if (_jumpActor != jump.Actor)
                {
                    throw new InvalidOperationException(
                        $"{jump.Source} changed jump actors while airborne.");
                }

                bool landed = OracleObjectMath.UpdateSpeedZ(
                    ref _zFixed, ref _speedZ, jump.Gravity);
                host.SetActorZ(jump.Actor, _zFixed);
                if (!landed)
                    return CommandResult.Block;

                _jumpActor = null;
                return CommandResult.Yield;

            case CutsceneWriteObjectByteCommand write:
                _objectBytes[(write.Actor, write.Address)] = write.Value;
                host.WriteObjectByte(write.Actor, write.Address, write.Value);
                SetCounter(write.Value);
                return CommandResult.Yield;

            case CutsceneWriteMemoryCommand write:
                host.WriteMemory(write.Binding, write.Value);
                Observe(command, $"Memory:{write.Binding}", value: write.Value);
                return CommandResult.Continue;

            case CutscenePlaySoundCommand sound:
                host.PlaySound(sound.Sound);
                Observe(command, "Sound", value: sound.Sound);
                return CommandResult.Yield;

            case CutsceneFlickerCommand flicker:
                (CutsceneActorId Actor, int Address) key =
                    (flicker.Actor, flicker.CounterAddress);
                if (!_objectBytes.TryGetValue(key, out int value))
                {
                    throw new InvalidOperationException(
                        $"{flicker.Source} reads unset object byte ${flicker.CounterAddress:x2} " +
                        $"for actor '{flicker.Actor}'.");
                }
                host.SetActorVisible(
                    flicker.Actor,
                    (host.FrameCounter & flicker.FrameMask) != 0);
                value--;
                _objectBytes[key] = value;
                SetCounter(value);
                return value == 0 ? CommandResult.Continue : CommandResult.Block;

            case CutsceneTranslateCommand movement:
                if (_commandUpdates == 0)
                {
                    _startPosition = host.GetActorPosition(movement.Actor);
                    if (movement.Animation >= 0 && movement.SetAnimationOnStart)
                    {
                        host.SetActorAnimation(
                            movement.Actor, movement.Animation, string.Empty);
                    }
                }
                int elapsed = _commandUpdates + 1;
                host.SetActorPosition(
                    movement.Actor,
                    _startPosition + movement.Delta * elapsed / movement.Frames,
                    movement.Delta,
                    movement.Delta / movement.Frames);
                Observe(
                    command,
                    "ActorPosition",
                    movement.ActorId,
                    position: host.GetActorPosition(movement.ActorId));
                if (elapsed < movement.Frames)
                    return CommandResult.Block;
                host.CompleteActorTranslation(movement.ActorId);
                Observe(command, "ActorTranslationCompleted", movement.ActorId);
                return CommandResult.Yield;

            case CutsceneParallelTranslateCommand movement:
                if (_commandUpdates == 0)
                {
                    _startPosition = host.GetActorPosition(movement.Actor);
                    _startPosition2 = host.GetActorPosition(movement.Actor2);
                }
                int parallelElapsed = _commandUpdates + 1;
                UpdateTranslatedActor(
                    movement.Actor,
                    _startPosition,
                    movement.Delta,
                    movement.Frames,
                    parallelElapsed);
                UpdateTranslatedActor(
                    movement.Actor2,
                    _startPosition2,
                    movement.Delta2,
                    movement.Frames2,
                    parallelElapsed);
                Observe(
                    command,
                    "ActorPosition",
                    movement.ActorId,
                    position: host.GetActorPosition(movement.ActorId));
                Observe(
                    command,
                    "ActorPosition",
                    movement.Actor2Id,
                    position: host.GetActorPosition(movement.Actor2Id));
                return parallelElapsed >= Math.Max(movement.Frames, movement.Frames2)
                    ? CommandResult.Yield
                    : CommandResult.Block;

            case CutsceneDeleteActorCommand deleted:
                host.DeleteActor(deleted.Actor);
                Active = false;
                return CommandResult.End;

            case CutsceneSetGlobalFlagCommand flag:
                host.SetGlobalFlag(flag.Flag);
                Observe(command, "GlobalFlag", value: flag.Flag);
                return CommandResult.Continue;

            case CutsceneOrRoomFlagCommand flag:
                host.OrRoomFlag(flag.Flag);
                Observe(command, "RoomFlag", value: flag.Flag);
                // scriptCmd_orRoomFlags returns with carry clear, so
                // interactionRunScript saves the pointer and yields here.
                return CommandResult.Yield;

            case CutsceneOrRoomFlagContinueCommand flag:
                host.OrRoomFlag(flag.Flag);
                Observe(command, "RoomFlag", value: flag.Flag);
                return CommandResult.Continue;

            case CutsceneNativeCommand native:
                host.RunNativeHandler(native.Handler);
                Observe(command, $"Native:{native.Handler}");
                return CommandResult.Continue;

            case CutsceneNativeYieldCommand native:
                host.RunNativeHandler(native.Handler);
                Observe(command, $"Native:{native.Handler}");
                return CommandResult.Yield;

            case CutsceneNativeBlockingCommand native:
                bool nativeComplete = host.UpdateNativeHandler(
                    native.Handler,
                    native.Actor,
                    _commandUpdates,
                    native.Frames,
                    native.Payload);
                Observe(
                    command,
                    $"Native:{native.Handler}",
                    native.Actor,
                    _commandUpdates + 1);
                return nativeComplete ? CommandResult.Yield : CommandResult.Block;

            case CutsceneEnableInputCommand:
                host.SetInputEnabled(enabled: true);
                return CommandResult.Continue;

            case CutsceneEndCommand:
                host.ScriptEnded();
                Active = false;
                return CommandResult.End;

            default:
                throw new InvalidOperationException(
                    $"Unsupported cutscene command {command.GetType().Name} at {command.Source}.");
        }
    }

    private void SetCounter(int value)
    {
        if (value < 0)
            throw new InvalidOperationException("Cutscene command counter underflowed.");
        Counter = value;
    }

    private static void RequireRegister(
        IReadOnlyDictionary<CutsceneActorId, int> registers,
        CutsceneActorId actor,
        string register,
        CutsceneCommandSource source)
    {
        if (!registers.ContainsKey(actor))
        {
            throw new InvalidOperationException(
                $"{source} uses actor '{actor}' before setting its {register}.");
        }
    }

    private void UpdateTranslatedActor(
        CutsceneActorId actor,
        Vector2 start,
        Vector2 delta,
        int frames,
        int elapsed)
    {
        int clamped = Math.Min(elapsed, frames);
        host.SetActorPosition(
            actor,
            start + delta * clamped / frames,
            delta,
            elapsed <= frames ? delta / frames : Vector2.Zero);
        if (elapsed == frames)
            host.CompleteActorTranslation(actor);
    }

    private static IEnumerable<CutsceneActorId> Actors(CutsceneCommand command)
    {
        switch (command)
        {
            case CutsceneSetAnimationCommand value: yield return value.Actor; break;
            case CutsceneSetAnimationContinueCommand value: yield return value.Actor; break;
            case CutsceneSetCollisionRadiiCommand value: yield return value.Actor; break;
            case CutsceneMakeAButtonSensitiveCommand value: yield return value.Actor; break;
            case CutsceneSetSpeedCommand value: yield return value.Actor; break;
            case CutsceneSetAngleCommand value: yield return value.Actor; break;
            case CutsceneApplySpeedCommand value: yield return value.Actor; break;
            case CutsceneMoveCommand value: yield return value.Actor; break;
            case CutsceneJumpCommand value: yield return value.Actor; break;
            case CutsceneWriteObjectByteCommand value: yield return value.Actor; break;
            case CutsceneFlickerCommand value: yield return value.Actor; break;
            case CutsceneTranslateCommand value: yield return value.Actor; break;
            case CutsceneParallelTranslateCommand value:
                yield return value.Actor;
                yield return value.Actor2;
                break;
            case CutsceneDeleteActorCommand value: yield return value.Actor; break;
            case CutsceneNativeBlockingCommand { Actor: { } actor }:
                yield return actor;
                break;
        }
    }

    private void Trace(CutsceneCommand command, CutsceneCommandTracePhase phase) =>
        host.TraceSink?.Record(new CutsceneCommandTraceEntry(
            _scriptUpdates,
            command.Source,
            phase,
            Counter,
            phase == CutsceneCommandTracePhase.Completed &&
                command is CutsceneMemoryBranchCommand or CutsceneBranchCommand or
                    CutsceneCallCommand or CutsceneReturnCommand
                ? _nextInstruction >= 0 ? _nextInstruction : _instruction + 1
                : -1));

    private void Observe(
        CutsceneCommand command,
        string observation,
        CutsceneActorId? actor = null,
        int value = 0,
        Vector2 position = default) =>
        host.TraceSink?.RecordObservation(new CutsceneObservationTraceEntry(
            host.FrameCounter,
            command.Source.Script,
            observation,
            actor,
            value,
            position));

    private enum CommandResult
    {
        Block,
        Yield,
        Continue,
        End
    }
}
