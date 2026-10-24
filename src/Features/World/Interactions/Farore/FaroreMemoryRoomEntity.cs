using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class FaroreMemoryRoomEntity(NpcCharacter actor, OracleSaveData save)
    : NpcCharacterRoomEntityAdapter(actor, actor.SetTransitionDrawOffset), IFixedRoomEntity,
      ITalkTarget, IOrdinaryNpcEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
      IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private bool _initialized;
    internal FaroreMemoryScriptHost? Script { get; set; }
    public NpcCharacter Npc => Entity;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    private void Initialize()
    {
        _initialized = true;
        Finished = !save.IsLinkedGame && !save.HasGlobalFlag(GlobalFlag.FinishedGame);
        if (Finished) return;
        Entity.ShowInitializedPresentation(); Entity.SetFixedDrawPriority(ObjectDrawPriority.FromVisible(0x83));
    }
    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) Initialize();
        return Finished ? ScreenTransitionPresentation.Hidden : ScreenTransitionPresentation.Visible;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (!_initialized) { Initialize(); return; }
        Script?.Advance(frame.Player);
    }
    public NpcCharacter? FindTalkTarget(Player player) => !Finished && Entity.CanTalkTo(player) ? Entity : null;
}
