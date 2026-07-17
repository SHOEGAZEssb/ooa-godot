using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace oracleofages;

/// <summary>
/// Loads importer-generated cutscene commands with source-aware schema
/// diagnostics. Event databases share this decoder instead of interpreting
/// untyped columns independently.
/// </summary>
internal static class CutsceneCommandCatalog
{
    private const int ColumnCount = 9;

    public static IReadOnlyList<CutsceneCommand> Load(string path)
    {
        var commands = new List<CutsceneCommand>();
        string source = FileAccess.GetFileAsString(path);
        string[] lines = source.Split('\n');
        for (int physicalLine = 1; physicalLine <= lines.Length; physicalLine++)
        {
            string line = lines[physicalLine - 1].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != ColumnCount)
            {
                throw Error(
                    path,
                    physicalLine,
                    $"expected {ColumnCount} columns, got {columns.Length}");
            }

            commands.Add(ParseCommand(path, physicalLine, columns));
        }

        if (commands.Count == 0)
            throw new InvalidOperationException($"Cutscene command stream is empty: {path}");
        return commands.AsReadOnly();
    }

    private static CutsceneCommand ParseCommand(
        string path,
        int physicalLine,
        IReadOnlyList<string> columns)
    {
        string script = Required(path, physicalLine, "script", columns[0]);
        string label = Required(path, physicalLine, "label", columns[1]);
        int commandIndex = Decimal(path, physicalLine, "index", columns[2]);
        int sourceLine = Decimal(path, physicalLine, "source-line", columns[3]);
        string opcode = Required(path, physicalLine, "opcode", columns[4]);
        if (commandIndex < 0)
            throw Error(path, physicalLine, "index cannot be negative");
        if (sourceLine <= 0)
            throw Error(path, physicalLine, "source-line must be positive");

        var source = new CutsceneCommandSource(
            script, label, commandIndex, sourceLine, opcode);
        string actor = columns[5];
        string arg0 = columns[6];
        string arg1 = columns[7];
        string payload = DecodePayload(path, physicalLine, columns[8]);

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

    private static string DecodePayload(
        string path,
        int physicalLine,
        string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException exception)
        {
            throw Error(path, physicalLine, "payload-base64 is malformed", exception);
        }
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
