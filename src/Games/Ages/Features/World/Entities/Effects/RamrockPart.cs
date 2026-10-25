using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed partial class RamrockPart : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, INativePartHealthRoomEntity,
    IPostObjectLinkContactRoomEntity, IObjectCollisionHeightRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    INativeBraceletRoomEntity, IBraceletPullInteractableRoomEntity
{
    private readonly RamrockPartDatabase _data = RamrockPartDatabase.Shared;
    private readonly RamrockEnvironment _world;
    private readonly Func<int, RamrockPart?> _resolvePart;
    private readonly Func<RamrockPartSpawn, RamrockPart?> _spawn;
    private bool _collision;
    private int _speedZ, _palette;
    internal int Id { get; }
    internal RamrockEnvironment World => _world;
    internal int Slot { get; set; } = -1;
    internal int Subid { get; private set; }
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Var03 { get; }
    internal int ParentSlot { get; private set; }
    internal bool ParentIsPart { get; private set; }
    internal int BossSlot { get; private set; }
    internal int Counter1 { get; private set; }
    internal int Counter2 { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int ZFixed { get; private set; }
    internal int NativeVisible { get; private set; }
    internal int Invincibility { get; private set; }
    internal EnemyAnimationPlayer Animation { get; }
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public int CollisionZ => ZFixed >> 8;
    private Vector2 _radii;
    internal Rect2 Bounds => new(Position.Floor() - _radii, _radii * 2);
    internal RamrockPart(RamrockPartSpawn record, RamrockEnvironment world,
        Func<int, RamrockPart?> resolvePart, Func<RamrockPartSpawn, RamrockPart?> spawn)
    {
        Id = record.Id; Subid = record.SubId; Position = record.Position.Floor(); ZFixed = (record.ZFixed >> 8) << 8;
        ParentSlot = record.ParentSlot; ParentIsPart = record.ParentIsPart; Var03 = record.Var03;
        BossSlot = record.BossSlot < 0 && !ParentIsPart ? ParentSlot : record.BossSlot;
        _world = world; _resolvePart = resolvePart; _spawn = spawn;
        var properties = _data.Properties(Id); _radii = new(properties[2] & 15, properties[2] >> 4);
        _collision = (properties[1] & 0x80) != 0; _palette = properties[6];
        var visual = _data.Visual(Id); Animation = new(this, visual.Animations.Length);
        Animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites), visual.Animations, visual.TileBase, visual.Palette,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted, positionedOam: true);
        Visible = false;
    }
    private int ParentSubid => ParentIsPart ? (_resolvePart(ParentSlot)?.Subid ?? 0) : (_world.ResolveEnemy(ParentSlot)?.Subid ?? 0);
    private float ParentX => ParentIsPart ? (_resolvePart(ParentSlot)?.Position.X ?? 0) : (_world.ResolveEnemy(ParentSlot)?.Position.X ?? 0);
    private bool Dec1() { if (Counter1 != 0) Counter1--; return Counter1 == 0; }
    private void Show(int value) { NativeVisible = value; Visible = (value & 0x80) != 0; ZIndex = ObjectDrawPriority.FromVisible(value); }
    internal void Delete() { Finished = true; Show(NativeVisible & 0x7f); _collision = false; }
    public void ClearHealthAndCollision() => _collision = false;
    private void Move()
    {
        var velocity = NativeObjectMovement.Velocity(_world.Memory, Speed, Angle);
        Position = OracleObjectPosition.FromPixels(Position).Add(velocity.YFixed, velocity.XFixed).PrecisePosition;
    }
    private void CopyHigh(Vector2 point) => Position = point.Floor() + Position - Position.Floor();
    private bool UpdateZ(int acceleration)
    { int z = ZFixed; bool landed = OracleObjectMath.UpdateSpeedZ(ref z, ref _speedZ, acceleration); ZFixed = z; return landed; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (Invincibility < 0) Invincibility++;
        if (Id == 0x34) UpdateLaser();
        else if (Id == 0x4f) UpdateOrb(frame.Player.EnemyContactPosition);
        else if (Id == 0x35) UpdateGlovePart(frame.Player, frame.EnemyTarget ?? frame.Player.EnemyContactPosition);
        else throw new NotSupportedException($"Ramrock PART${Id:x2}.");
        QueueRedraw();
    }
    private void UpdateOrb(Vector2 link)
    {
        switch (State)
        {
            case 0: State = 1; Animation.SetAnimation(2); Counter1 = 40; Show(0x80); return;
            case 1:
                Animation.Advance(); if (ParentSubid != 15) { Delete(); return; }
                if (!Dec1()) return;
                Angle = OracleObjectMovement.Shared.RelativeAngle(Position, link); Speed = 0x50; State = 2;
                goto case 2;
            case 2:
                Animation.Advance();
                if (Dec1())
                {
                    Counter1 = 10; int toward = OracleObjectMovement.Shared.RelativeAngle(Position, link), delta = (Angle - toward) & 31;
                    if (delta != 0) Angle = (Angle + (delta < 16 ? -1 : 1)) & 31; return;
                }
                Move();
                Vector2 screen = _world.ToScreen(Position);
                if ((((int)Mathf.Floor(screen.Y) + 7) & 255) >= 0x8f ||
                    (((int)Mathf.Floor(screen.X) + 7) & 255) >= 0xaf) Delete(); return;
            default: throw new NotSupportedException($"ramrockSeedFormOrb.s state${State:x2}.");
        }
    }
    private void UpdateLaser()
    {
        if (State < 3) CopyHigh(new(ParentX, Position.Y));
        Counter2 = (Counter2 - 1) & 255; if ((Counter2 & 3) == 0) _palette ^= 1;
        switch (State)
        {
            case 0:
                State = 1; Speed = 0x6e; Angle = 16; Counter1 = 7; Counter2 = 3; Show(0x80); goto case 1;
            case 1:
                if (Var03 != 0 && !Dec1()) { Move(); return; }
                if (Var03 != 6)
                {
                    var next = _spawn(new(0x34, 14, Position, ZFixed, Slot, true, Var03 + 1, BossSlot));
                    if (next is null) return;
                }
                State = 2; goto case 2;
            case 2:
                if (ParentSubid == 14) return;
                State = 3; Counter1 = 7; goto case 3;
            case 3:
                if (Var03 == 6) { Delete(); return; }
                if (!Dec1()) { Move(); return; }
                // decCounter1 preserves A=$01 on its zero update. The source
                // copies that retained accumulator into subid before state4.
                Subid = 1; State = 4; return;
            case 4: Delete(); return;
            default: throw new NotSupportedException($"ramrockSeedFormLaser.s state${State:x2}.");
        }
    }
    public void HandleLinkContact(Player player)
    {
        if (!_collision || Finished || Invincibility != 0 || !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        if (player.IsUsingShield && player.CanAcceptShieldCollision &&
            RoomEntityManager.ObjectCollisionXYOverlaps(Bounds, player.ShieldCollisionBounds))
        {
            var response = _data.Collision(Id, player.Inventory.ShieldLevel);
            if (response.Active && response.Effect == CollisionEffect.BumpWithClinkLowKnockback) { Invincibility = -28; _world.Sound(SoundId.SndBombLand); return; }
            if (response.Active && response.Effect != 0) throw new NotSupportedException($"Ramrock PART${Id:x2} shield effect${response.Effect:x2}.");
        }
        if (player.NativeObjectVulnerable && player.OverlapsEnemyCollision(Bounds, CollisionZ) &&
            player.ApplyEnemyContactDamage(Position, (256 - _data.Properties(Id)[3]) % 256 / 2, RingDamageSource.Generic, 34, 15))
            Invincibility = -28;
    }
    public override void _Draw()
    {
        if (Visible && !Finished) DrawTexture(Animation.CurrentTextureForPalette(_palette),
            Animation.CurrentOffset + Vector2.Down * (ZFixed >> 8) + TransitionDrawOffset);
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}
