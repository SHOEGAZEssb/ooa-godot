using Godot;
using System;

namespace oracleofages;

internal interface ICutsceneCommandHost
{
    RoomEventContext Context => throw new InvalidOperationException(
        "This cutscene command host has no room-event context.");
    bool DialogueOpen => Context.DialogueOpen;
    bool IsLinkedGame => Context.Rooms.SaveData.IsLinkedGame;
    int FrameCounter => Context.Entities.FrameCounter;
    ICutsceneCommandTraceSink? TraceSink => Context.CommandTraceSink;

    bool HasActorBinding(CutsceneActorId actor) => true;

    void SetInputEnabled(bool enabled)
    {
        if (enabled)
            Context.Player.EndCutsceneControl();
        else
            Context.Player.BeginCutsceneControl();
    }
    void SetMenuEnabled(bool enabled);
    void SetDisabledObjects(int value);
    bool GateOpen(string gate);
    bool MemoryEquals(string binding, int value);
    bool RoomFlagSet(int flag) =>
        throw new InvalidOperationException(
            $"This cutscene host cannot read room flag ${flag:x2}.");
    bool TextOptionEquals(int value) =>
        throw new InvalidOperationException(
            $"This cutscene host cannot read text option ${value:x2}.");
    bool TryConsumeActorButton(CutsceneActorId actor) =>
        throw new InvalidOperationException(
            $"This cutscene host cannot consume A for actor '{actor}'.");
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
    void PlaySound(int sound) => Context.Sound.PlaySound(sound);
    void SetMusic(int music) =>
        throw new InvalidOperationException(
            $"This cutscene host cannot set music ${music:x2}.");
    void SetGlobalFlag(int flag) => Context.Rooms.SaveData.SetGlobalFlag(flag);
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
