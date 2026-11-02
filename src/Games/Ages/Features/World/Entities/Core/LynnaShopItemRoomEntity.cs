
namespace Oracles;

internal sealed class LynnaShopItemRoomEntity(LynnaShopItem item, OracleRuntimeState? memory = null, Func<bool>? initialize = null)
    : RoomEntityAdapter<LynnaShopItem>(item, item.SetTransitionDrawOffset),
        INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private bool _initialized = initialize is null;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    private void Initialize()
    {
        if (_initialized || Finished || (memory!.ReadField("wInShop") & 2) == 0) return;
        _initialized = true;
        Finished = !initialize!();
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Initialize();
        return !Finished && Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) { Initialize(); return; }
        if (!Finished) Entity.UpdateFrame(frame.Player);
    }
}
