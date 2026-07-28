using Godot;
using System;

namespace oracleofages;

internal interface ICutsceneCommandHost
{
    RoomEventContext Context { get; }
    bool DialogueOpen { get; }
    bool IsLinkedGame { get; }
    int FrameCounter { get; }
    ICutsceneCommandTraceSink? TraceSink { get; }

    void SetActiveCommandSource(CutsceneCommandSource? source);
    bool HasActorBinding(CutsceneActorId actor);
    void SetInputEnabled(bool enabled);
    void SetMenuEnabled(bool enabled);
    void SetDisabledObjects(int value);
    bool GateOpen(string gate);
    bool MemoryEquals(string binding, int value);
    int ReadMemory(string binding);
    bool RoomFlagSet(int flag);
    bool TradeItemEquals(int value);
    bool TextOptionEquals(int value);
    bool TryConsumeActorButton(CutsceneActorId actor);
    void ShowText(int textId, string message);
    void SetActorAnimation(string actor, int animation, string encodedAnimation);
    void SetActorMovementAnimation(string actor, int angle, string encodedAnimation);
    void SetActorCollisionRadii(string actor, int radiusY, int radiusX);
    void SetActorButtonSensitive(string actor);
    void MoveActorAtSpeed(string actor, int speed, int angle);
    void SetActorZ(string actor, int zFixed);
    void SetActorVisible(string actor, bool visible);
    void WriteObjectByte(string actor, int address, int value);
    Vector2 GetActorPosition(CutsceneActorId actor);
    void SetActorPosition(
        CutsceneActorId actor,
        Vector2 position,
        Vector2 facingDelta,
        Vector2 movement);
    void CompleteActorTranslation(CutsceneActorId actor);
    void DeleteActor(CutsceneActorId actor);
    void WriteMemory(string binding, int value);
    void GiveItem(int treasureId, int parameter);
    void PlaySound(int sound);
    void SetMusic(int music);
    void SetGlobalFlag(int flag);
    void OrRoomFlag(int flag);
    void RunNativeHandler(string handler);
    bool UpdateNativeHandler(
        string handler,
        CutsceneActorId? actor,
        int commandUpdate,
        int frames,
        string payload);
    void ScriptEnded();
}
