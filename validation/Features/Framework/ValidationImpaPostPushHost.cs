using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;
internal sealed class ValidationImpaPostPushHost(int linkAngle) :
    CutsceneCommandHost, ICutsceneCommandHost
{
    private bool _dialogueOpen;
    private int _frameCounter;

    public ValidationCutsceneTrace Trace { get; } = new();
    public override bool DialogueOpen => _dialogueOpen;
    public override bool IsLinkedGame => false;
    public override int FrameCounter => _frameCounter;
    public override ICutsceneCommandTraceSink TraceSink => Trace;
    public Vector2 Position { get; private set; }
    public Vector2I Facing { get; private set; } = Vector2I.Right;
    public List<int> TextIds { get; } = new();
    public int Signal { get; private set; } = 0x06;
    public bool Ended { get; private set; }

    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Impa";
    public void AdvanceValidationFrame() => _frameCounter++;
    public void CloseDialogue() => _dialogueOpen = false;
    public override void SetInputEnabled(bool enabled) => throw Unsupported(nameof(SetInputEnabled));
    public override void SetMenuEnabled(bool enabled) => throw Unsupported(nameof(SetMenuEnabled));
    public override void SetDisabledObjects(int value) => throw Unsupported(nameof(SetDisabledObjects));
    public override bool GateOpen(string gate) =>
        throw Unsupported(nameof(GateOpen));
    public override bool MemoryEquals(string binding, int value) => binding == "w1Link.angle" && linkAngle == value;
    public override void ShowText(int textId, string message)
    {
        TextIds.Add(textId);
        _dialogueOpen = true;
    }

    public override void SetActorAnimation(string actor, int animation, string encodedAnimation)
    {
    }

    public override void SetActorMovementAnimation(string actor, int angle, string encodedAnimation)
    {
        Vector2 direction = OracleObjectMath.StrictCardinalVector(angle);
        Facing = new Vector2I(Mathf.RoundToInt(direction.X), Mathf.RoundToInt(direction.Y));
    }

    public override void SetActorCollisionRadii(string actor, int radiusY, int radiusX) => throw Unsupported(nameof(SetActorCollisionRadii));
    public override void SetActorButtonSensitive(string actor) => throw Unsupported(nameof(SetActorButtonSensitive));
    public override void MoveActorAtSpeed(string actor, int speed, int angle) =>
        Position += OracleObjectMovement.Shared.Delta(speed, angle);
    public override void SetActorZ(string actor, int zFixed) => throw Unsupported(nameof(SetActorZ));
    public override void SetActorVisible(string actor, bool visible) => throw Unsupported(nameof(SetActorVisible));
    public override void WriteMemory(string binding, int value)
    {
        if (binding != "wTmpcfc0.genericCutscene.cfd0")
            throw Unsupported(nameof(WriteMemory));
        Signal = value;
    }

    public override void PlaySound(int sound) => throw Unsupported(nameof(PlaySound));
    public override void SetGlobalFlag(int flag) => throw Unsupported(nameof(SetGlobalFlag));
    public override void OrRoomFlag(int flag) => throw Unsupported(nameof(OrRoomFlag));
    public override void RunNativeHandler(string handler) => throw Unsupported(nameof(RunNativeHandler));
    public override void ScriptEnded() => Ended = true;
    private static InvalidOperationException Unsupported(string operation) => new($"Validation Impa post-push host does not support {operation}.");
}
