using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class NpcRoomEntity(NpcCharacter npc, bool initializeStoryOnce = false,
    bool animateBeforeDialogue = false, bool deleteIfInactiveOnInitialization = false)
    : NpcCharacterRoomEntityAdapter(
        RequireOrdinaryGeneric(npc), npc.SetTransitionDrawOffset),
    IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private bool _nativeInitialized;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => deleteIfInactiveOnInitialization && !_nativeInitialized;
    public bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue;
    private Func<bool>? _pendingDialogue;
    private bool _storyInitialized;
    public bool RefreshStoryState => !initializeStoryOnce || !_storyInitialized;
    public void CompleteStoryInitialization() => _storyInitialized = true;
    public NpcCharacter Npc => Entity;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!InitializeNative()) return;
        // Link sets pressedAButton in its earlier update. Most ordinary
        // handlers run the generic script before facing and animation;
        // Ambi $4d:$0a and Ralph $37:$12 run npcFaceLinkAndAnimate
        // first. Text freezes the interaction on the next pass.
        if (animateBeforeDialogue) AdvanceNpc(frame);
        if (_pendingDialogue?.Invoke() == true) _pendingDialogue = null;
        if (!animateBeforeDialogue) AdvanceNpc(frame);
    }
    private bool InitializeNative()
    {
        if (!_nativeInitialized)
        {
            _nativeInitialized = true;
            Finished = deleteIfInactiveOnInitialization && !Entity.Active;
        }
        return !Finished;
    }
    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        InitializeNative() ? base.PrepareForScreenTransition(spawns) : ScreenTransitionPresentation.Hidden;
    private void AdvanceNpc(RoomEntityFrame frame)
    {
        if (initializeStoryOnce && !Entity.Record.CanFace)
            Entity.AnimateAsNpcOneUpdate(frame.Player);
        else Entity.FaceLinkAndAnimateOneUpdate(frame.Player, preventPassing: false);
    }
    public bool TryQueueDialogue(Func<bool> tryShowDialogue)
    { _pendingDialogue = tryShowDialogue; return true; }
    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);
    public NpcCharacter? FindTalkTarget(Player player) =>
        _pendingDialogue is null && Entity.CanTalkTo(player) ? Entity : null;
    private static NpcCharacter RequireOrdinaryGeneric(NpcCharacter npc)
    {
        if (npc.Record.Implementation !=
            NpcImplementationClassification.OrdinaryGeneric)
        {
            throw new InvalidOperationException(
                $"NPC {npc.Record.Group}:{npc.Record.Room:x2} " +
                $"${npc.Record.Id:x2}:${npc.Record.SubId:x2} " +
                $"var03=${npc.Record.Var03:x2} cannot use the ordinary NPC " +
                $"adapter with classification {npc.Record.Implementation}.");
        }
        return npc;
    }
}
