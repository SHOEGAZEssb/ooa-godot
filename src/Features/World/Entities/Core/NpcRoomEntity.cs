
namespace Oracles;

internal class NpcRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly bool _initializeStoryOnce, _animateBeforeDialogue, _deleteIfInactiveOnInitialization, _scripted;
    protected virtual bool Initialized { get; set; }
    public bool Finished { get; protected set; }
    public virtual bool UpdatesDuringDialogue => _deleteIfInactiveOnInitialization && !Initialized;
    public virtual bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue;
    internal NpcInteractionCommandHost? Script { get; set; }
    private Func<bool>? _pendingDialogue;
    private bool _storyInitialized;
    public virtual bool RefreshStoryState => !_initializeStoryOnce || !_storyInitialized;
    public virtual void CompleteStoryInitialization() => _storyInitialized = true;
    public NpcCharacter Npc => Entity;
    internal NpcRoomEntity(NpcCharacter npc, bool initializeStoryOnce = false,
        bool animateBeforeDialogue = false, bool deleteIfInactiveOnInitialization = false)
        : base(RequireOrdinaryGeneric(npc), npc.SetTransitionDrawOffset)
    {
        _initializeStoryOnce=initializeStoryOnce; _animateBeforeDialogue=animateBeforeDialogue;
        _deleteIfInactiveOnInitialization=deleteIfInactiveOnInitialization;
    }
    protected NpcRoomEntity(NpcCharacter npc) : base(npc,npc.SetTransitionDrawOffset)
    { _initializeStoryOnce=true; _scripted=true; }
    protected virtual bool CanUpdateNpc => true;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!CanUpdateNpc || Finished) return;
        if (!Initialized)
        {
            Initialized=true;
            if (!InitializeNpc(frame,spawns)) return;
        }
        if (Finished || UpdateNativeNpc(frame,spawns)) return;
        // Link sets pressedAButton in its earlier update. Most ordinary
        // handlers run the generic script before facing and animation;
        // Ambi $4d:$0a and Ralph $37:$12 run npcFaceLinkAndAnimate
        // first. Text freezes the interaction on the next pass.
        if (_animateBeforeDialogue) AdvanceNpc(frame);
        Script?.AdvanceObjectUpdate(frame.Player);
        if (Finished) return;
        if (_pendingDialogue?.Invoke() == true) _pendingDialogue = null;
        if (!_animateBeforeDialogue) AdvanceNpc(frame);
        Entity.ProjectNativePosition();
    }
    protected virtual bool InitializeNpc(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        Finished = _deleteIfInactiveOnInitialization && !Entity.Active;
        return !Finished;
    }
    protected virtual bool UpdateNativeNpc(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => false;
    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_scripted && !Initialized)
        { Initialized=true; Finished=_deleteIfInactiveOnInitialization && !Entity.Active; }
        return Finished ? ScreenTransitionPresentation.Hidden : base.PrepareForScreenTransition(spawns);
    }
    protected virtual void AdvanceNpc(RoomEntityFrame frame)
    {
        if (_initializeStoryOnce && !Entity.Record.CanFace)
            Entity.AnimateAsNpcOneUpdate(frame.Player);
        else Entity.FaceLinkAndAnimateOneUpdate(frame.Player, preventPassing: _scripted);
    }
    public bool TryQueueDialogue(Func<bool> tryShowDialogue)
    { if (_scripted) return false; _pendingDialogue = tryShowDialogue; return true; }
    public virtual bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);
    public virtual NpcCharacter? FindTalkTarget(Player player) =>
        _pendingDialogue is null && (Script is null || Script is {HasState:true,InputDisabled:false,CanSelect:true} && Entity.ScriptButtonSensitive) &&
        Entity.CanTalkTo(player) ? Entity : null;
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
