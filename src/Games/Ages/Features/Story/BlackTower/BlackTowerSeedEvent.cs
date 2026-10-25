using Godot;
using System.Collections.Generic;

namespace Oracles;

// INTERAC_BLACK_TOWER_DOOR_HANDLER is the sole placed interaction in $4:ea.
// Its $d7/$84 children retain native slot allocation and same-pass updates.
internal sealed class BlackTowerSeedEvent(RoomEventContext context, System.Action beginLinkedCutscene) : IRoomEntryEvent
{
    private readonly BlackTowerSeedDatabase _data = new();
    private readonly RoomEventResources _resources = new(context, new object());
    private readonly HashSet<IRoomEntity> _actors = [];
    private bool _ownsMasks;
    public bool HasState { get; private set; }
    public bool BlocksGameplay => false;
    public bool Matches(int group, OracleRoomData room) => group == 4 && room.Id == 0xea;
    public void Start(OracleRoomData room)
    {
        Cancel();
        HasState = true;
        Spawn(0xc6, 0, Vector2.Zero);
    }
    internal bool Spawn(int id, int subid, Vector2 position, int angle = 0, int counter = 0, int zHigh = 0)
    {
        if (!context.Entities.InteractionSlotAvailable) return false;
        var entity = new BlackTowerSeedRoomEntity(context, _data, this, id, subid, position, angle, counter, zHigh);
        context.Entities.AddEntity(entity);
        _actors.Add(entity);
        return true;
    }
    internal void SetMasks(bool disabled)
    {
        _ownsMasks = disabled;
        context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, disabled ? (byte)0x81 : (byte)0);
        context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wMenuDisabled, disabled ? (byte)0x81 : (byte)0);
    }
    internal void Fade(bool fadeOut, int delay)
    {
        _resources.CaptureFullScreenFade();
        context.Transitions.BeginNativeWhiteFade(fadeOut, delay);
    }
    internal void BlackFadeOut()
    {_resources.CaptureFullScreenFade();context.Transitions.BeginNativeBlackFade(true);}
    internal void BeginLinkedCutscene()
    {
        // The seed deletes itself after publishing CUTSCENE $11. Transfer
        // the fade without cancelling it or clearing the native masks.
        HasState=false;_ownsMasks=false;_actors.Clear();
        _resources.ReleaseFullScreenFade(restoreColor:false);beginLinkedCutscene();
    }
    internal void AdvanceBeforeObjects()
    {
        if (HasState) context.Transitions.AdvanceNativeWhiteFade();
    }
    public void UpdateFrame() { }
    public void Cancel()
    {
        if (!HasState) return;
        HasState = false;
        foreach (var entity in _actors) context.Entities.RetireEntity(entity);
        _actors.Clear();
        context.Transitions.CancelNativeWhiteFade();
        _resources.ReleaseFullScreenFade();
        if (_ownsMasks)
        {
            SetMasks(false);
            context.Player.EndForcedRoomEntryMovement();
            context.Player.EndGetItemTwoHandPose();
        }
        context.Player.EndCutsceneControl(this);
        // loadRoom resets wDisableWarps; cancelling this owner must also
        // release its no-seed gate without saving room completion.
        context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisableWarps, 0);
    }
}
