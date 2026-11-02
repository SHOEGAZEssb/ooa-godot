
namespace Oracles;

// forestFairy.s:$05-$0d. State zero selects the table and
// visibility once; later updates never repeat the progression checks.
internal sealed class ForestNpcRoomEntity
    : NpcCharacterRoomEntityAdapter,
        IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity,
        INativeInteractionRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    internal ForestNpcRoomEntity(NpcCharacter npc, System.Action<ForestNpcRoomEntity> initializeStory)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _initializeStory = initializeStory;
        npc.SetScriptVisible(false);
    }
    private readonly System.Action<ForestNpcRoomEntity> _initializeStory;
    private bool _resolvingInitialization;
    private bool _nativeInitialized;
    public bool NativeInitializationPending => !_nativeInitialized;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => NativeInitializationPending;
    public bool UpdatesDuringRoomEntityFreeze => NativeInitializationPending;
    internal ForestFairySecretEvent? SecretScript { get; set; }
    private System.Func<bool>? _pendingDialogue;
    public bool TryQueueDialogue(System.Func<bool> tryShowDialogue)
    { _pendingDialogue = tryShowDialogue; return true; }
    public NpcCharacter Npc => Entity;
    public bool RefreshStoryState => _resolvingInitialization;
    public void CompleteStoryInitialization()
    {
        Entity.SetCollisionRadii(4, 4);
        Entity.SetScriptDrawOffset(new Vector2(0, -4));
        if (Entity.Record.SubId == 0x0b)
            Entity.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex);
        Entity.ShowInitializedPresentation();
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_nativeInitialized) { InitializeNative(); return; }
        if (!Entity.Active) return;
        if (_pendingDialogue?.Invoke() == true) _pendingDialogue = null;
        SecretScript?.AdvanceObjectFrame();
        Entity.AdvanceAnimationUpdates(1);
        Entity.PreventPlayerPassing(frame.Player);
    }
    private void InitializeNative()
    {
        _resolvingInitialization = true;
        try { _initializeStory(this); }
        finally { _resolvingInitialization = false; }
        _nativeInitialized = true;
        Finished = !Entity.Active;
        if (Finished) SecretScript?.Cancel();
        else Entity.SetScriptVisible(true);
    }
    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_nativeInitialized) InitializeNative();
        return Finished ? ScreenTransitionPresentation.Hidden : base.PrepareForScreenTransition(spawns);
    }
    public bool BlocksLink(Vector2 center) => _nativeInitialized && Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => _nativeInitialized && Entity.CanTalkTo(player) ? Entity : null;
}
