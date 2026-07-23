using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;
internal sealed class ValidationImpaPostPushHost(int linkAngle) : ICutsceneCommandHost
{
    public ValidationCutsceneTrace Trace { get; } = new();
    public bool DialogueOpen { get; private set; }
    public bool IsLinkedGame => false;
    public int FrameCounter { get; private set; }
    public ICutsceneCommandTraceSink TraceSink => Trace;
    public Vector2 Position { get; private set; }
    public Vector2I Facing { get; private set; } = Vector2I.Right;
    public List<int> TextIds { get; } = new();
    public int Signal { get; private set; } = 0x06;
    public bool Ended { get; private set; }

    public bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Impa";
    public void AdvanceValidationFrame() => FrameCounter++;
    public void CloseDialogue() => DialogueOpen = false;
    public void SetInputEnabled(bool enabled) => throw Unsupported(nameof(SetInputEnabled));
    public void SetMenuEnabled(bool enabled) => throw Unsupported(nameof(SetMenuEnabled));
    public void SetDisabledObjects(int value) => throw Unsupported(nameof(SetDisabledObjects));
    public bool GateOpen(string gate) => throw Unsupported(nameof(GateOpen));
    public bool MemoryEquals(string binding, int value) => binding == "w1Link.angle" && linkAngle == value;
    public void ShowText(int textId, string message)
    {
        TextIds.Add(textId);
        DialogueOpen = true;
    }

    public void SetActorAnimation(string actor, int animation, string encodedAnimation)
    {
    }

    public void SetActorMovementAnimation(string actor, int angle, string encodedAnimation)
    {
        Vector2 direction = OracleObjectMath.StrictCardinalVector(angle);
        Facing = new Vector2I(Mathf.RoundToInt(direction.X), Mathf.RoundToInt(direction.Y));
    }

    public void SetActorCollisionRadii(string actor, int radiusY, int radiusX) => throw Unsupported(nameof(SetActorCollisionRadii));
    public void SetActorButtonSensitive(string actor) => throw Unsupported(nameof(SetActorButtonSensitive));
    public void MoveActorAtSpeed(string actor, int speed, int angle) => Position += OracleObjectMath.StrictCardinalVector(angle) * (speed / 40.0f);
    public void SetActorZ(string actor, int zFixed) => throw Unsupported(nameof(SetActorZ));
    public void SetActorVisible(string actor, bool visible) => throw Unsupported(nameof(SetActorVisible));
    public void WriteMemory(string binding, int value)
    {
        if (binding != "wTmpcfc0.genericCutscene.cfd0")
            throw Unsupported(nameof(WriteMemory));
        Signal = value;
    }

    public void PlaySound(int sound) => throw Unsupported(nameof(PlaySound));
    public void SetGlobalFlag(int flag) => throw Unsupported(nameof(SetGlobalFlag));
    public void OrRoomFlag(int flag) => throw Unsupported(nameof(OrRoomFlag));
    public void RunNativeHandler(string handler) => throw Unsupported(nameof(RunNativeHandler));
    public void ScriptEnded() => Ended = true;
    private static InvalidOperationException Unsupported(string operation) => new($"Validation Impa post-push host does not support {operation}.");
}
