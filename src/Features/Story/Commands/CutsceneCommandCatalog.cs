using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace oracleofages;

/// <summary>
/// Loads importer-generated cutscene commands with source-aware schema
/// diagnostics. Event databases share this decoder instead of interpreting
/// untyped columns independently.
/// </summary>
internal static class CutsceneCommandCatalog
{
    public static IReadOnlyList<CutsceneCommand> Load(string path)
    {
        GeneratedTable table = GeneratedTable.Load(
            path,
            new GeneratedTableSchema(
                "cutscene command stream",
                GeneratedTableKeySemantics.Ordered,
                [
                    "script", "label", "index", "source-line", "opcode", "actor",
                    "arg0", "arg1", "payload-base64"
                ],
                headerRequired: true));
        var commands = new List<CutsceneCommand>();
        foreach (GeneratedTableRow row in table.Rows)
            commands.Add(ParseCommand(row));

        if (commands.Count == 0)
            throw new InvalidOperationException($"Cutscene command stream is empty: {path}");
        return commands.AsReadOnly();
    }

    private static CutsceneCommand ParseCommand(GeneratedTableRow row)
    {
        string path = row.Path;
        int physicalLine = row.LineNumber;
        string script = row.RequiredString(0);
        string label = row.RequiredString(1);
        int commandIndex = row.UnsignedDecimal(2);
        int sourceLine = row.UnsignedDecimal(3);
        string opcode = row.RequiredString(4);
        if (commandIndex < 0)
            throw Error(path, physicalLine, "index cannot be negative");
        if (sourceLine <= 0)
            throw Error(path, physicalLine, "source-line must be positive");

        var source = new CutsceneCommandSource(
            script, label, commandIndex, sourceLine, opcode);
        string actor = row.String(5);
        string arg0 = row.String(6);
        string arg1 = row.String(7);
        string payload = row.Base64Utf8(8);

        return opcode switch
        {
            "disableinput" => new CutsceneDisableInputCommand(source),
            "disablemenu" => new CutsceneDisableMenuCommand(source),
            "setdisabledobjects" => new CutsceneSetDisabledObjectsCommand(
                source, Hex(path, physicalLine, "arg0", arg0)),
            "setdisabledobjectscontinue" =>
                new CutsceneSetDisabledObjectsContinueCommand(
                    source, Hex(path, physicalLine, "arg0", arg0)),
            "setcounter" => new CutsceneSetCounterCommand(
                source, Decimal(path, physicalLine, "arg0", arg0)),
            "waitpreloadedcounter" => new CutsceneWaitPreloadedCounterCommand(source),
            "wait" => new CutsceneWaitCommand(
                source, Decimal(path, physicalLine, "arg0", arg0)),
            "waitframes" => new CutsceneWaitFramesCommand(
                source, PositiveDecimal(path, physicalLine, "arg0", arg0)),
            "showtext" => new CutsceneShowTextCommand(
                source, Hex(path, physicalLine, "arg0", arg0), payload),
            "dialogue" => new CutsceneDialogueCommand(
                source, Hex(path, physicalLine, "arg0", arg0), payload),
            "showtextdifferentforlinked" => ParseTextVariants(
                path, physicalLine, source, arg0, arg1, payload),
            "setanimation" => new CutsceneSetAnimationCommand(
                source,
                Required(path, physicalLine, "actor", actor),
                Hex(path, physicalLine, "arg0", arg0),
                payload),
            "setanimationcontinue" => new CutsceneSetAnimationContinueCommand(
                source,
                Required(path, physicalLine, "actor", actor),
                Hex(path, physicalLine, "arg0", arg0),
                Required(path, physicalLine, "payload", payload)),
            "setcollisionradii" => new CutsceneSetCollisionRadiiCommand(
                source,
                Required(path, physicalLine, "actor", actor),
                Hex(path, physicalLine, "arg0", arg0),
                Hex(path, physicalLine, "arg1", arg1)),
            "makeabuttonsensitive" => new CutsceneMakeAButtonSensitiveCommand(
                source, Required(path, physicalLine, "actor", actor)),
            "checkabutton" => new CutsceneCheckAButtonCommand(
                source, Required(path, physicalLine, "actor", actor)),
            "gate" => new CutsceneGateCommand(
                source, Required(path, physicalLine, "payload", payload)),
            "checkmemoryeq" => new CutsceneMemoryGateCommand(
                source,
                Required(path, physicalLine, "payload", payload),
                Hex(path, physicalLine, "arg0", arg0)),
            "jumpifmemoryeq" => new CutsceneMemoryBranchCommand(
                source,
                Required(path, physicalLine, "payload", payload),
                Hex(path, physicalLine, "arg0", arg0),
                Decimal(path, physicalLine, "arg1", arg1)),
            "jumpifroomflagset" => new CutsceneRoomFlagBranchCommand(
                source,
                Hex(path, physicalLine, "arg0", arg0),
                Decimal(path, physicalLine, "arg1", arg1)),
            "jumpiftextoptioneq" => new CutsceneTextOptionBranchCommand(
                source,
                Hex(path, physicalLine, "arg0", arg0),
                Decimal(path, physicalLine, "arg1", arg1)),
            "scriptjump" => new CutsceneBranchCommand(
                source, Decimal(path, physicalLine, "arg0", arg0)),
            "callscript" => new CutsceneCallCommand(
                source, Decimal(path, physicalLine, "arg0", arg0)),
            "return" => new CutsceneReturnCommand(source),
            "setspeed" => new CutsceneSetSpeedCommand(
                source,
                Required(path, physicalLine, "actor", actor),
                Hex(path, physicalLine, "arg0", arg0)),
            "setangle" => new CutsceneSetAngleCommand(
                source,
                Required(path, physicalLine, "actor", actor),
                Hex(path, physicalLine, "arg0", arg0)),
            "applyspeed" => new CutsceneApplySpeedCommand(
                source,
                Required(path, physicalLine, "actor", actor),
                Hex(path, physicalLine, "arg0", arg0)),
            "move" => new CutsceneMoveCommand(
                source,
                Required(path, physicalLine, "actor", actor),
                Hex(path, physicalLine, "arg0", arg0),
                Hex(path, physicalLine, "arg1", arg1),
                Required(path, physicalLine, "payload", payload)),
            "jump" => new CutsceneJumpCommand(
                source,
                Required(path, physicalLine, "actor", actor),
                Decimal(path, physicalLine, "arg0", arg0),
                Hex(path, physicalLine, "arg1", arg1),
                Hex(path, physicalLine, "payload", payload)),
            "writeobjectbyte" => new CutsceneWriteObjectByteCommand(
                source,
                Required(path, physicalLine, "actor", actor),
                Hex(path, physicalLine, "arg0", arg0),
                Hex(path, physicalLine, "arg1", arg1)),
            "writememory" => new CutsceneWriteMemoryCommand(
                source,
                Required(path, physicalLine, "payload", payload),
                Hex(path, physicalLine, "arg0", arg0)),
            "playsound" => new CutscenePlaySoundCommand(
                source, Hex(path, physicalLine, "arg0", arg0)),
            "setmusic" => new CutsceneSetMusicCommand(
                source, Hex(path, physicalLine, "arg0", arg0)),
            "flicker" => new CutsceneFlickerCommand(
                source,
                Required(path, physicalLine, "actor", actor),
                Hex(path, physicalLine, "arg0", arg0),
                Hex(path, physicalLine, "arg1", arg1)),
            "translate" => ParseTranslate(
                path, physicalLine, source, actor, arg0, arg1, payload),
            "paralleltranslate" => ParseParallelTranslate(
                path, physicalLine, source, actor, arg0, arg1, payload),
            "deleteactor" => new CutsceneDeleteActorCommand(
                source, Actor(path, physicalLine, actor)),
            "setglobalflag" => new CutsceneSetGlobalFlagCommand(
                source, Hex(path, physicalLine, "arg0", arg0)),
            "orroomflag" => new CutsceneOrRoomFlagCommand(
                source, Hex(path, physicalLine, "arg0", arg0)),
            "orroomflagcontinue" => new CutsceneOrRoomFlagContinueCommand(
                source, Hex(path, physicalLine, "arg0", arg0)),
            "native" => new CutsceneNativeCommand(
                source, Required(path, physicalLine, "payload", payload)),
            "nativeyield" => new CutsceneNativeYieldCommand(
                source, Required(path, physicalLine, "payload", payload)),
            "nativeblock" => ParseNativeBlock(
                path, physicalLine, source, actor, arg0, payload),
            "enableinput" => new CutsceneEnableInputCommand(source),
            "scriptend" => new CutsceneEndCommand(source),
            _ => throw Error(
                path,
                physicalLine,
                $"unsupported opcode '{opcode}' at {source}")
        };
    }

    private static CutsceneTranslateCommand ParseTranslate(
        string path,
        int physicalLine,
        CutsceneCommandSource source,
        string actor,
        string frames,
        string animation,
        string payload)
    {
        string[] values = payload.Split(',');
        if (values.Length != 3)
        {
            throw Error(
                path,
                physicalLine,
                "translate payload must be 'delta-x,delta-y,set-animation'");
        }
        return new CutsceneTranslateCommand(
            source,
            Actor(path, physicalLine, actor),
            new Vector2(
                Float(path, physicalLine, "delta-x", values[0]),
                Float(path, physicalLine, "delta-y", values[1])),
            PositiveDecimal(path, physicalLine, "arg0", frames),
            Decimal(path, physicalLine, "arg1", animation),
            Bool(path, physicalLine, "set-animation", values[2]));
    }

    private static CutsceneParallelTranslateCommand ParseParallelTranslate(
        string path,
        int physicalLine,
        CutsceneCommandSource source,
        string actor,
        string frames,
        string frames2,
        string payload)
    {
        string[] lanes = payload.Split('|');
        if (lanes.Length != 3)
        {
            throw Error(
                path,
                physicalLine,
                "paralleltranslate payload must be 'delta-x,delta-y|actor2|delta-x2,delta-y2'");
        }
        Vector2 delta = Vector(path, physicalLine, "first delta", lanes[0]);
        Vector2 delta2 = Vector(path, physicalLine, "second delta", lanes[2]);
        return new CutsceneParallelTranslateCommand(
            source,
            Actor(path, physicalLine, actor),
            delta,
            PositiveDecimal(path, physicalLine, "arg0", frames),
            Actor(path, physicalLine, lanes[1]),
            delta2,
            PositiveDecimal(path, physicalLine, "arg1", frames2));
    }

    private static CutsceneNativeBlockingCommand ParseNativeBlock(
        string path,
        int physicalLine,
        CutsceneCommandSource source,
        string actor,
        string frames,
        string payload)
    {
        int separator = payload.IndexOf('\0');
        string handler = separator < 0 ? payload : payload[..separator];
        string arguments = separator < 0 ? string.Empty : payload[(separator + 1)..];
        return new CutsceneNativeBlockingCommand(
            source,
            Required(path, physicalLine, "handler", handler),
            string.IsNullOrWhiteSpace(actor)
                ? (CutsceneActorId?)null
                : Actor(path, physicalLine, actor),
            PositiveDecimal(path, physicalLine, "arg0", frames),
            arguments);
    }

    private static CutsceneShowTextVariantsCommand ParseTextVariants(
        string path,
        int physicalLine,
        CutsceneCommandSource source,
        string standardTextId,
        string linkedTextId,
        string payload)
    {
        int separator = payload.IndexOf('\0');
        if (separator < 0 || payload.IndexOf('\0', separator + 1) >= 0)
        {
            throw Error(
                path,
                physicalLine,
                "showtextdifferentforlinked payload must contain exactly two NUL-separated messages");
        }
        return new CutsceneShowTextVariantsCommand(
            source,
            Hex(path, physicalLine, "arg0", standardTextId),
            payload[..separator],
            Hex(path, physicalLine, "arg1", linkedTextId),
            payload[(separator + 1)..]);
    }

    private static int Decimal(
        string path,
        int physicalLine,
        string field,
        string value)
    {
        if (!int.TryParse(
                value,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out int result))
        {
            throw Error(path, physicalLine, $"{field} is not a decimal integer: '{value}'");
        }
        return result;
    }

    private static int PositiveDecimal(
        string path,
        int physicalLine,
        string field,
        string value)
    {
        int result = Decimal(path, physicalLine, field, value);
        if (result <= 0)
            throw Error(path, physicalLine, $"{field} must be positive, got {result}");
        return result;
    }

    private static float Float(
        string path,
        int physicalLine,
        string field,
        string value)
    {
        if (!float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float result) || !float.IsFinite(result))
        {
            throw Error(path, physicalLine, $"{field} is not a finite number: '{value}'");
        }
        return result;
    }

    private static bool Bool(
        string path,
        int physicalLine,
        string field,
        string value) => value switch
        {
            "0" => false,
            "1" => true,
            _ => throw Error(path, physicalLine, $"{field} must be 0 or 1, got '{value}'")
        };

    private static Vector2 Vector(
        string path,
        int physicalLine,
        string field,
        string value)
    {
        string[] components = value.Split(',');
        if (components.Length != 2)
            throw Error(path, physicalLine, $"{field} must contain two comma-separated numbers");
        return new Vector2(
            Float(path, physicalLine, $"{field} x", components[0]),
            Float(path, physicalLine, $"{field} y", components[1]));
    }

    private static int Hex(
        string path,
        int physicalLine,
        string field,
        string value)
    {
        if (!int.TryParse(
                value,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out int result))
        {
            throw Error(path, physicalLine, $"{field} is not hexadecimal: '{value}'");
        }
        return result;
    }

    private static string Required(
        string path,
        int physicalLine,
        string field,
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw Error(path, physicalLine, $"{field} cannot be empty");
        return value;
    }

    private static CutsceneActorId Actor(
        string path,
        int physicalLine,
        string value) =>
        new(Required(path, physicalLine, "actor", value));

    private static InvalidOperationException Error(
        string path,
        int physicalLine,
        string message,
        Exception? innerException = null) =>
        new($"{path}:{physicalLine}: {message}.", innerException);
}

internal sealed record CutsceneApplySpeedCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Counter)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

internal sealed record CutsceneMemoryBranchCommand(
    CutsceneCommandSource Source,
    string Binding,
    int Value,
    int TargetCommand)
    : CutsceneCommand(Source);

internal sealed record CutsceneMakeAButtonSensitiveCommand(
    CutsceneCommandSource Source,
    string Actor)
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

internal sealed record CutsceneGateCommand(
    CutsceneCommandSource Source,
    string Gate)
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

internal sealed record CutsceneEndCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);

internal sealed record CutsceneEnableInputCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);

internal sealed record CutsceneDisableMenuCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);

internal sealed record CutsceneDisableInputCommand(CutsceneCommandSource Source)
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

internal sealed record CutsceneDeleteActorCommand(
    CutsceneCommandSource Source,
    string Actor)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

internal sealed record CutsceneCheckAButtonCommand(
    CutsceneCommandSource Source,
    string Actor)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

internal sealed record CutsceneCallCommand(
    CutsceneCommandSource Source,
    int TargetCommand)
    : CutsceneCommand(Source);

internal sealed record CutsceneBranchCommand(
    CutsceneCommandSource Source,
    int TargetCommand)
    : CutsceneCommand(Source);

internal sealed record CutscenePlaySoundCommand(
    CutsceneCommandSource Source,
    int Sound)
    : CutsceneCommand(Source);

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

/// <summary>
/// Native object-code room-flag mutation that remains in the same object
/// update. This is deliberately distinct from scriptCmd_orRoomFlags, whose
/// no-carry return yields the interaction script.
/// </summary>
internal sealed record CutsceneOrRoomFlagContinueCommand(
    CutsceneCommandSource Source,
    int Flag)
    : CutsceneCommand(Source);

internal sealed record CutsceneOrRoomFlagCommand(
    CutsceneCommandSource Source,
    int Flag)
    : CutsceneCommand(Source);

/// <summary>A native controller mutation that owns one fixed update.</summary>
internal sealed record CutsceneNativeYieldCommand(
    CutsceneCommandSource Source,
    string Handler)
    : CutsceneCommand(Source);

internal sealed record CutsceneNativeCommand(
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

internal sealed record CutsceneMemoryGateCommand(
    CutsceneCommandSource Source,
    string Binding,
    int Value)
    : CutsceneCommand(Source);

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

internal sealed record CutsceneSetAnimationCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Animation,
    string EncodedAnimation)
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

internal sealed record CutsceneRoomFlagBranchCommand(
    CutsceneCommandSource Source,
    int Flag,
    int TargetCommand)
    : CutsceneCommand(Source);

internal sealed record CutsceneReturnCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);

internal sealed record CutsceneSetDisabledObjectsCommand(
    CutsceneCommandSource Source,
    int Value)
    : CutsceneCommand(Source);

internal sealed record CutsceneSetCounterCommand(
    CutsceneCommandSource Source,
    int Frames)
    : CutsceneCommand(Source);

internal sealed record CutsceneSetCollisionRadiiCommand(
    CutsceneCommandSource Source,
    string Actor,
    int RadiusY,
    int RadiusX)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

internal sealed record CutsceneSetGlobalFlagCommand(
    CutsceneCommandSource Source,
    int Flag)
    : CutsceneCommand(Source);

/// <summary>A native WRAM write that carries into the next operation.</summary>
internal sealed record CutsceneSetDisabledObjectsContinueCommand(
    CutsceneCommandSource Source,
    int Value)
    : CutsceneCommand(Source);

internal sealed record CutsceneSetMusicCommand(
    CutsceneCommandSource Source,
    int Music)
    : CutsceneCommand(Source);

internal sealed record CutsceneSetSpeedCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Speed)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}

internal sealed record CutsceneShowTextVariantsCommand(
    CutsceneCommandSource Source,
    int StandardTextId,
    string StandardMessage,
    int LinkedTextId,
    string LinkedMessage)
    : CutsceneCommand(Source);

internal sealed record CutsceneShowTextCommand(
    CutsceneCommandSource Source,
    int TextId,
    string Message)
    : CutsceneCommand(Source);

internal sealed record CutsceneTextOptionBranchCommand(
    CutsceneCommandSource Source,
    int Value,
    int TargetCommand)
    : CutsceneCommand(Source);

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

/// <summary>
/// Decrements a counter installed by an earlier native operation. Unlike
/// <see cref="CutsceneWaitCommand"/>, the first runner update consumes a frame.
/// </summary>
internal sealed record CutsceneWaitPreloadedCounterCommand(
    CutsceneCommandSource Source)
    : CutsceneCommand(Source);

internal sealed record CutsceneWriteMemoryCommand(
    CutsceneCommandSource Source,
    string Binding,
    int Value)
    : CutsceneCommand(Source);

internal sealed record CutsceneWriteObjectByteCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Address,
    int Value)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}
