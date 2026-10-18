using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// PART_FALLING_FIRE $23: invisible emitters and their falling subid$02 child.
internal sealed partial class FallingFireRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IPostObjectLinkContactRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity,
    INativePartHealthRoomEntity
{
    private readonly RoomEntityManager _owner;
    private readonly PlacedPartDatabase _data;
    private ushort _yFixed;
    private ushort _speedYFixed;
    private bool _collision;
    internal int Slot { get; set; } = -1;
    internal int SubId { get; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int YFixed => _yFixed;
    internal int SpeedYFixed => _speedYFixed;
    internal int CurrentFrame => AnimationFrame;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;

    internal FallingFireRoomEntity(RoomEntityManager owner, PlacedPartDatabase data, int subid, Vector2 position)
    {
        if (subid is < 0 or > 2) throw new NotSupportedException($"fallingFire.s: PART$23:${subid:x2} is unsupported.");
        _owner = owner; _data = data; SubId = subid;
        InitializeVisual(data.Definition(PartId.FallingFire).Visual, position.Floor(), positionedOam: true);
        _yFixed = unchecked((ushort)((int)position.Y * 256));
        Visible = false;
        ZIndex = ObjectDrawPriority.BehindLinkZIndex;
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    private void Initialize()
    {
        State = 1;
        if (SubId == 2) { _collision = true; Visible = true; }
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0) { Initialize(); return; }
        if (SubId != 2)
        {
            if (Counter != 0) Counter--;
            if (Counter != 0) return;
            // func_553f deliberately accesses ENEMY.counter2 ($87) in
            // this PART page, not PART.counter2 ($c7).
            int cycle = SubId == 1 ? _owner.AdvanceFallingFireCycle(Slot) : 0;
            Counter = _data.Table("fire-intervals")[cycle];
            _owner.TryCreateFallingFire(Position);
            return;
        }
        if ((_yFixed >> 8) >= 0xb0) { Finished = true; Visible = false; return; }
        _yFixed = unchecked((ushort)(_yFixed + _speedYFixed));
        _speedYFixed = unchecked((ushort)(_speedYFixed + 0x10));
        Position = new(Position.X, _yFixed >> 8);
        AdvanceAnimation(); QueueRedraw();
    }
    public void ClearHealthAndCollision() => _collision = false;
    public void HandleLinkContact(Player player)
    {
        if (!_collision || Finished || !player.EnemyContactHeightOverlaps(0)) return;
        var definition = _data.Definition(PartId.FallingFire);
        var bounds = new Rect2(Position - new Vector2(definition.RadiusX, definition.RadiusY),
            new(definition.RadiusX * 2, definition.RadiusY * 2));
        if (player.IsUsingShield && player.CanAcceptShieldCollision && _data.Active(PartId.FallingFire, player.Inventory.ShieldLevel) &&
            RoomEntityManager.ObjectCollisionXYOverlaps(bounds, player.ShieldCollisionBounds))
        {
            // mode$72 + shield$03 selects LINKDMG_14 / ENEMYDMG_1c;
            // the latter leaves the fire alive with no recoil or hit flag.
            player.ApplyShieldCollisionRecoil(Position, 15, 19);
            return;
        }
        if (player.NativeObjectVulnerable && player.OverlapsEnemyCollision(bounds, 0))
            player.ApplyEnemyContactDamage(Position, definition.Damage, RingDamageSource.Generic, 42, 21);
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0) Initialize();
        return Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
}
