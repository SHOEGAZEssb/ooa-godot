
namespace Oracles;

internal sealed class SeasonsOpeningWorld(SeasonsOpeningSession session)
    : PlayerWorld(session.Rooms, session.Transitions, session.Entities, session.Collision, session.Sound,
        () => session.Menus?.Collision.CollisionsDisabled == true, session.Combat, session), IPlayerWorld
{
    private SubrosiaKeyDoorController? _subrosiaKeys;
    private GhiniTileSpawner? _ghiniSpawner;
    protected override bool UpdateNativeTileContact(byte handler,Vector2 position,Vector2I facing,Vector2 input) =>
        handler switch
        {
            6 => session.Engine!.Keyholes.UpdatePushAttempt(position,facing,input),
            7 => (_subrosiaKeys ??= new(session.Rooms,session.Entities,session.Sound)).UpdatePushAttempt(position,facing,input),
            8 => (_ghiniSpawner ??= new(session.Rooms,session.Entities)).UpdatePushAttempt(position,facing,input),
            _ => base.UpdateNativeTileContact(handler,position,facing,input)
        };
    public bool DialogueOpen => session.Interactions.DialogueOpen;
    public bool NativeTextActive => session.Entities.TextActiveSource();
    public bool ItemUsageDisabled => session.Entities.PlayerItemUsageDisabled;
    public byte NativeObjectDisableMask => session.DisabledObjects;
    public bool PlayerUpdatesFrozen => session.Entities.PlayerUpdatesFrozen;
    public bool NativePaletteChanging => session.Fading;
    public bool TryInteract(Player player) => session.TryTalk();
    public bool TrySecondaryInteract(Player player) => session.Interactions.TrySecondaryInteract(player);
    public void DeactivateWarpAtPlayerPosition(Player player) => session.Transitions.DeactivateWarpAtPlayerPosition(player);
    public bool CheckTileWarp(Player player)
    {
        if (!player.ApplicationUpdateOwned) return session.Transitions.CheckTileWarp(player);
        session.Transitions.RequestTileWarpCheck();
        return false;
    }
    public void CheckRoomExit(Player player)
    {
        if (!player.ApplicationUpdateOwned) session.CheckRoomExit();
    }
}
