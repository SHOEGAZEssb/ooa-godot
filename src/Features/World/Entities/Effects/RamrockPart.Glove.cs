using Godot;
using System;

namespace oracleofages;

internal sealed partial class RamrockPart
{
    private readonly BraceletGrabGeometry _grabGeometry = new();
    private Action<INativeBraceletRoomEntity>? _publishGrab;
    private Player? _puller;
    private bool _pullRequested, _grabbable;
    private int _gloveChildSignal, _charged, _pullSound;
    private bool IsGloveChild => (Subid & 0x80) != 0;
    private int GloveSide => Subid >> 4 & 1;
    private RamrockCharacter? Boss => _world.ResolveEnemy(BossSlot);
    private RamrockPart? RelatedPart => ParentIsPart ? _resolvePart(ParentSlot) : null;
    private Vector2 GloveHome => Boss is { } boss ? new((byte)((int)boss.Position.X + (GloveSide == 0 ? 12 : -12)),
        (byte)((int)boss.Position.Y + 12)) : Vector2.Zero;
    public void BindGrabbablePublisher(Action<INativeBraceletRoomEntity> publish) => _publishGrab = publish;
    public void UpdateHeldPosition(Player player) { }
    public bool TryUseBracelet(Player player, Vector2I releaseDirection) => false;
    public bool TryBeginBraceletPull(Player player)
    {
        if (Id != 0x35 || !_grabbable || Finished || State != 4 || IsGloveChild || _puller is not null ||
            !player.IsOnGroundForBracelet || player.IsCarryingObject ||
            !_grabGeometry.Overlaps(new(player.EnemyContactPosition - new Vector2(6,6), new(12,12)),
                player.EnemyContactZ, CarriedObjectMotion.DirectionIndex(player.FacingVector), Bounds, CollisionZ, false)) return false;
        _puller = player; State = 2; Substate = 0; _grabbable = false;
        player.SetBraceletActionPose(BraceletActionPose.Pull); return true;
    }
    public bool UpdateBraceletPull(Player player, Vector2 movementInput, bool assignedButtonHeld)
    {
        if (_puller != player || Finished || State != 2 || !assignedButtonHeld)
        { CancelBraceletPull(player); return false; }
        _pullRequested = movementInput.Dot(Vector2.Down) > 0.5f;
        player.SetBraceletActionPose(_pullRequested ? BraceletActionPose.PullStrain : BraceletActionPose.Pull);
        return true;
    }
    public void CancelBraceletPull(Player player)
    {
        if (_puller != player) return;
        _puller = null; _pullRequested = false; player.ClearBraceletActionPose();
        if (State == 2) Substate = 3;
    }
    private void BeginGloveReturn(int speed)
    {
        Speed = speed;
        if (RelatedPart is { } parent) parent._gloveChildSignal = (parent._gloveChildSignal + 1) & 255;
        State = 3; Angle = OracleObjectMovement.Shared.RelativeAngle(Position, GloveHome);
    }
    private void InitializeGlove()
    {
        State = 1; ZFixed = (-127 << 8) | (ZFixed & 255);
        bool child = IsGloveChild; int original = Subid;
        if ((Subid & 15) != 2)
        {
            var next = _spawn(new(0x35, original + 1, Position, ZFixed, Slot, true, BossSlot: BossSlot));
            if (next is null) throw new NotSupportedException("ramrockGloveFormArm.s: unchecked child allocation failed.");
            if (!child)
            {
                Subid = (Subid << 4) & 255; next.Subid = Subid | 0x80;
            }
            int offset = (next.Subid & 0x10) == 0 ? -8 : 8;
            next.CopyHigh(new((byte)((int)Position.X + offset), (byte)((int)Position.Y - 4)));
        }
        if (!child)
        { Angle = GloveSide == 0 ? 24 : 8; _world.Sound(SoundId.SndThrow); Show(0xc0); }
        else
        {
            Animation.SetAnimation(1); Counter1 = (Subid & 15) + 10; _collision = false; Show(0xc1);
        }
    }
    private void UpdateGlovePart(Player player, Vector2 target)
    {
        var boss = Boss;
        if (boss is null || boss.Health == 0) { if (_puller is not null) CancelBraceletPull(_puller); Delete(); return; }
        bool child = IsGloveChild;
        if (child && Counter1 == 0 && State != 4) Show(NativeVisible ^ 0x80);
        if (!child && !player.GrabStateActive && Player.EnemyCollisionOverlaps(player.EnemyContactPosition, Bounds))
            player.AdvanceInteractionVelocity(0x28, OracleObjectMovement.Shared.RelativeAngle(Position, target));
        _grabbable = false;
        switch (State)
        {
            case 0: InitializeGlove(); return;
            case 1:
                if (child && !Dec1()) return;
                if (!UpdateZ(0x10)) return;
                if (!child) { if (_gloveChildSignal != 0) BeginGloveReturn(0x78); return; }
                if (Counter2 == 0) { Counter2++; _speedZ = -0x180; return; }
                BeginGloveReturn(0x78); return;
            case 2:
                if (child)
                {
                    var parent = RelatedPart;
                    int delta = (byte)((int)(parent?.Position.Y ?? 0) - (int)Position.Y);
                    if (delta >= 16) CopyHigh(new(Position.X, (byte)((int)Position.Y + 1)));
                    if (parent?.State != 2) BeginGloveReturn(0x78); return;
                }
                switch (Substate)
                {
                    case 0:
                        Substate = 1; Angle = 16; Speed = 0x14; Counter2 = 96;
                        Position = new(Position.X, Mathf.Floor(Position.Y));
                        if (_puller is not null) _puller.SetScriptedPosition(new(_puller.PrecisePosition.X, Mathf.Floor(_puller.PrecisePosition.Y)));
                        boss.GloveSignal &= ~(1 << GloveSide); return;
                    case 1:
                        if (boss.Subid == 17)
                        { if (_puller is not null) CancelBraceletPull(_puller); Substate = 2; goto case 2; }
                        if (!_pullRequested) { _pullSound = 0; return; }
                        _pullRequested = false;
                        if (Counter2 == 0) return;
                        Counter2--; if (Counter2 == 0) { Counter1 = 20; _charged = (_charged + 1) & 255; }
                        if (_pullSound == 0) { _pullSound = SoundId.SndMoveBlock; _world.Sound(_pullSound); }
                        _puller?.AdvanceInteractionVelocity(Speed, Angle); Move(); return;
                    case 2:
                    case 3:
                        _world.Sound(SoundId.SndBigSword); BeginGloveReturn(0x78); return;
                    default: throw new NotSupportedException($"PART$35 pull substate${Substate:x2}.");
                }
            case 3:
                if (Counter1 != 0) { Counter1--; Move(); return; }
                Angle = OracleObjectMovement.Shared.RelativeAngle(Position, GloveHome); Move();
                if (!ReachedGloveHome()) return;
                if (child) Show(NativeVisible & 0x7f);
                else
                {
                    _collision = true;
                    if (_charged != 0) { _charged = 0; boss.ApplyPhaseHit(); }
                    Counter1 = 60; boss.GloveSignal |= 1 << GloveSide;
                }
                State = 4; return;
            case 4:
                if (child)
                {
                    var parent = RelatedPart;
                    if (parent?.State != 4) { State = parent?.State ?? 0; Angle = parent?.Angle ?? 0; Show(NativeVisible | 0x80); }
                    else CopyHigh(GloveHome); return;
                }
                _collision = true; CopyHigh(GloveHome);
                if (!Dec1() || boss.Subid != 18) return;
                if ((boss.AttackParameter & 32) != 0 || Subid == boss.AttackParameter)
                {
                    _world.Sound(SoundId.SndExplosion); Angle = OracleObjectMovement.Shared.RelativeAngle(Position, player.EnemyContactPosition);
                    State = 5; Counter1 = 2; Speed = 0x78; boss.GloveSignal &= ~(1 << GloveSide); return;
                }
                if (OracleObjectMovement.Shared.RelativeAngle(Position, player.EnemyContactPosition) == 16 && player.FacingVector == Vector2I.Up)
                { _collision = false; _grabbable = true; _publishGrab?.Invoke(this); } return;
            case 5:
                if (child)
                {
                    var parent = RelatedPart;
                    Speed = Math.Max(0, (parent?.Speed ?? 0) - 25);
                    if (parent?.State == 3) BeginGloveReturn(0x14); else Move(); return;
                }
                if (WallInFront()) { GloveWait(); return; }
                Move(); if (!Dec1()) return;
                Counter1 = 3; if (Speed == 0) { GloveWait(); return; }
                Speed = Math.Max(0, Speed - 10); return;
            case 6:
                if (!Dec1()) return;
                _collision = false;
                if ((boss.AttackParameter & 32) != 0) boss.AttackParameter = 0x80;
                BeginGloveReturn(0x14); return;
            default: throw new NotSupportedException($"ramrockGloveFormArm.s state${State:x2}.");
        }
    }
    private void GloveWait() { State = 6; Counter1 = 60; Speed = 0; }
    private bool ReachedGloveHome()
    {
        Vector2 target = GloveHome;
        return ((int)Position.Y - (int)target.Y + 3 & 255) is > 0 and <= 6 &&
            ((int)Position.X - (int)target.X + 3 & 255) is > 0 and <= 6;
    }
    private bool WallInFront()
    {
        Vector2I offset = RamrockProfile.Shared.PartFrontOffset(Angle);
        Vector2 probe = new((byte)((int)Position.X + offset.X), (byte)((int)Position.Y + offset.Y));
        return probe.X >= _world.Room.Width || probe.Y >= _world.Room.Height || _world.Room.GetTerrainInfo(probe).Collision != 0;
    }
}
