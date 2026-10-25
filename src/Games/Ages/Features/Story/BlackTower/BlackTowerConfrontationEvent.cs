using Godot;
using System.Collections.Generic;

namespace Oracles;

internal sealed class BlackTowerConfrontationEvent(RoomEventContext context) : IRoomEntryEvent
{
    private readonly BlackTowerConfrontationDatabase _data = new();
    private readonly RoomEventResources _resources = new(context, new object());
    private readonly HashSet<IRoomEntity> _owned = [];
    private BlackTowerConfrontationActor? _ralph;
    private bool _ownsMasks;
    public bool HasState { get; private set; }
    public bool BlocksGameplay => false;
    internal IRoomEntity CreateRalph(NpcRecord record)
    {
        return new BlackTowerConfrontationActor(context, _data, this, 0x37, new(record.X, record.Y));
    }
    public bool Matches(int group, OracleRoomData room) => group == 4 && room.Id == 0xfe;
    public void Start(OracleRoomData room)
    {
        HasState = true;
        foreach (var actor in context.Entities.EntityAdapters<BlackTowerConfrontationActor>())
            if (actor.Id == 0x37) { _ralph = actor; _owned.Add(actor); }
    }
    public void UpdateFrame() { }
    internal void SpawnAmbi()
    {
        if (!context.Entities.InteractionSlotAvailable) return;
        var actor = new BlackTowerConfrontationActor(context, _data, this, 0x4d, new(0x78, 0x3c));
        _owned.Add(actor); context.Entities.AddEntity(actor);
    }
    internal void SpawnSword(BlackTowerConfrontationActor parent)
    {
        if (!context.Entities.InteractionSlotAvailable) return;
        var sword = new LinkedSwordRoomEntity(_data.Actor(0x5e, parent.Node.Position),
            () => parent.Finished ? null : (NpcCharacter)parent.Node, index => _data.Animation(0x5e, index));
        _owned.Add(sword); context.Entities.AddEntity(sword);
    }
    internal bool TryInteract(NpcCharacter npc)
    {
        if (_ralph is null || !ReferenceEquals(_ralph.Node, npc) || !npc.ScriptButtonSensitive) return false;
        _ralph.PressButton(); return true;
    }
    internal void Input(bool enabled)
    {
        _ownsMasks = !enabled;
        context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, enabled ? (byte)0 : (byte)0x81);
        context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wMenuDisabled, enabled ? (byte)0 : (byte)0x80);
        if (!enabled)
        {
            context.Player.ClearNativeItemParents(); context.Player.DropHeldItemsForScript();
            context.Player.ApplyInteractionInvincibility(0x80);
        }
    }
    internal void Fade(bool fadeOut, int delay = 1, int speed = 1)
    {
        _resources.CaptureFullScreenFade();
        context.Transitions.BeginNativeWhiteFade(fadeOut, delay, speed);
    }
    internal void ClearPalettes()
    {
        _resources.CaptureFullScreenFade();
        context.Fade.Color = Colors.White;
    }
    internal void AdvanceBeforeObjects()
    { if (HasState) context.Transitions.AdvanceNativeWhiteFade(); }
    public void Cancel()
    {
        if (!HasState) return;
        HasState = false;
        foreach (var entity in _owned) context.Entities.RetireEntity(entity);
        _owned.Clear(); _ralph = null;
        if (_ownsMasks) Input(true);
        context.Transitions.CancelNativeWhiteFade(); _resources.ReleaseFullScreenFade();
    }
}
