using Godot;
using System;

namespace Oracles;

// common/enemies/twinrova.s:$03. The two native slots keep independent
// counters; shared signals are read through the live related enemy slot.
internal sealed partial class TwinrovaCharacter : EnemyCharacter
{
    internal TwinrovaEnvironment World { get; }
    internal TwinrovaDatabase Data => World.Data;
    internal int Slot { get; set; }
    internal int TwinSlot { get; private set; } = -1;
    internal int Subid { get; private set; }
    internal int Var03 { get; private set; }
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter { get; private set; }
    internal int Counter2 { get; private set; }
    internal int Flags { get; set; }
    internal int BobCounter { get; private set; }
    internal int Pattern { get; private set; }
    internal int Waypoint { get; private set; }
    internal Vector2I Target { get; private set; }
    internal int FacingDelay { get; private set; }
    internal int AttackIndex { get; private set; }
    internal int AttackCounter { get; private set; }
    internal int Direction { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int AngularVelocity { get; private set; }
    internal int ZFixed { get; private set; }
    private int _speedZ, _palette, _puff = -1;
    private bool _collision = true;
    internal TwinrovaCharacter Twin => World.Entities.ResolveEnemySlot(TwinSlot) is TwinrovaRoomEntity twin ? twin.Actor :
        throw new NotSupportedException($"twinrova.s: ENEMY $03 slot${Slot:x2} relatedObj1 ${TwinSlot:x2} has no live twin.");
    internal override bool InitializationPending => State == 0;
    internal override bool CollisionEnabled => !IsDead && _collision;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (ZFixed >> 8);
    internal override Texture2D CurrentDrawTexture => DrawsDamagePalette ? base.CurrentDrawTexture : Animation.CurrentTextureForPalette(_palette);
    internal TwinrovaCharacter(TwinrovaEnvironment world, Vector2 position, int subid = 0, int var03 = 0)
    {
        World = world; Subid = subid; Var03 = var03;
        InitializeEnemy(position.Floor(), EnemyCharacterConfiguration.FromImported(Data.Actor(3)), positionedOam: true, paletteVariants: [0, 1, 2]);
        Health = 0; _collision = false; Animation.SetFrameCounter(0);
        Visible = false;
    }
    internal void UpdateFrame(RoomEntityFrame frame)
    {
        if (IsDead) return;
        try
        {
            BeginFrame(advanceInvincibility: false, continueDuringHitAndKnockback: true);
            if (State == 0)
            {
                Health = Data.Actor(3).Health; _collision = true; Animation.SetAnimation(0);
                World.Entities.Random.Next();
            }
            if (Health == 0 && (Flags & 0x40) != 0)
            {
                Health = Twin.Health = 0x7f; _collision = Twin._collision = false;
                State = 0x0d; Twin.State = 0x0f; World.Entities.OnSoundRequested(SoundId.SndCtrlStopMusic);
            }
            if (FacingDelay != 0) FacingDelay--;
            if ((Flags & 8) != 0)
            {
                BobCounter = (BobCounter - 1) & 255;
                if ((BobCounter & 7) == 0) HighZ(Data.Bytes("levitation")[(BobCounter & 0x18) >> 3]);
            }
            Fire(frame);
            switch (State)
            {
                case 0:
                    World.Entities.RuntimeState.SetWramByte(0xcc1d, 3); // wEnemyIDToLoadExtraGfx.
                    World.Entities.RuntimeState.SetWramByte(AgesWramAddress.wLoadedTreeGfxIndex, 1);
                    if ((Var03 & 1) != 0) { Initialize(frame); break; }
                    State = 1; frame.Player.Face(Vector2I.Up); Input(false);
                    goto case 1;
                case 1:
                    var child = World.Entities.TryAllocateEnemy(_ => new TwinrovaRoomEntity(new(World, Vector2.Zero, 1, (Var03 + 1) & 255), counted: false)) as TwinrovaRoomEntity;
                    if (child is null) break;
                    TwinSlot = child.Slot; child.Actor.TwinSlot = Slot;
                    if (child.Slot < Slot)
                    {
                        child.Actor.Subid = 0; Subid = 1; Var03 = (Var03 + 1) & 255; State = 0;
                    }
                    else Initialize(frame);
                    break;
                case >= 2 and < 8: break;
                case 8:
                    State = 9; Flags |= 0x88; Counter = 106; HighY(8);
                    var entrance = Data.Bytes("entrance"); int offset = Subid * 4;
                    HighX(entrance[offset]); _palette = entrance[offset + 1]; Angle = entrance[offset + 2]; AngularVelocity = entrance[offset + 3];
                    if (Subid == 0) Text(0x2f04);
                    BobCounter = World.Entities.Random.Next().Value; UpdateAnimationFromAngle(); break;
                case 9: Entrance(frame); break;
                case 0x0a:
                    State = 0x0b;
                    if (Subid == 0) { World.Entities.SetNativeActiveMusic(SoundId.MusTwinrova); World.Entities.OnSoundRequested(SoundId.MusTwinrova); }
                    NextTarget(); break;
                case 0x0b:
                    if (Subid == 0 && (frame.Counter & 127) == 0) World.Entities.OnSoundRequested(SoundId.SndFairyCutscene);
                    if (!MoveTowardTarget()) break;
                    if (!NextTarget()) { State = 0x0c; Counter = 30; break; }
                    if (Subid != 0 || AttackInProgress()) break;
                    AttackIndex = (AttackIndex + 1) & 7;
                    if ((Data.Bytes("attack-pattern")[0] & (1 << AttackIndex)) != 0)
                    {
                        var attacker = (World.Entities.Random.Next().Value & 1) == 0 ? this : Twin;
                        attacker.Flags |= 5;
                    }
                    break;
                case 0x0c:
                    if (Dec() != 0) { AdvanceAnimation(); break; }
                    State = 0x0b; Pattern = World.Entities.Random.Next().Value & 3; Waypoint = 0; NextTarget(); break;
                case 0x0d:
                    State = 0x0e; HighZ(0); Flags &= ~8; RestartAnimation((Angle & 16) == 0 ? 10 : 11); break;
                case 0x0e:
                    if (AnimationParameter == 255)
                    {
                        RestartAnimation(Direction + 4); State = 0x0f; HighZ(((ZFixed >> 8) - 1) & 255); Text(0x2f09);
                    }
                    AdvanceAnimation(); break;
                case 0x0f:
                    if (Rise())
                    {
                        if ((Twin.Flags & 16) != 0) { State = 0x10; Substate = 0; Flags &= ~8; }
                        else { Toward(frame.EnemyTarget ?? frame.Player.Position); MovingAnimation(); }
                    }
                    AdvanceAnimation(); break;
                case 0x10: Merge(frame); break;
                default: throw Unsupported();
            }
        }
        finally
        {
            AdvanceInvincibilityCounter();
            if ((Flags & 128) != 0)
            {
                Visible = true;
                int z = (ZFixed >> 8) & 255;
                int priority = ((z - 1) & 255) < 16 ? 3 : (int)Position.Y <= (((int)frame.Player.Position.Y + 11) & 255) ? 2 : 1;
                ZIndex = ObjectDrawPriority.FromVisible(0xc0 | priority);
            }
        }
    }
    private void Initialize(RoomEntityFrame frame)
    {
        if ((Var03 & 128) == 0) { Speed = 0x28; State = 8; return; }
        Input(true); HighY(0x56); HighX(Subid == 0 ? 0x60 : 0x90); _palette = Subid == 0 ? 2 : 1;
        State = 0x0a; Direction = 255; Speed = 0x32; Flags |= 0x88;
        Toward(frame.EnemyTarget ?? frame.Player.Position); Direction = CalculateDirection(Angle).Direction + 4; RestartAnimation(Direction);
    }
    private void Entrance(RoomEntityFrame frame)
    {
        switch (Substate)
        {
            case 0:
                if (Dec() != 0) Move();
                else { Counter = 8; Counter2 = 18; Substate = 1; }
                AdvanceAnimation(); return;
            case 1:
                if (Dec() != 0) { Move(); AdvanceAnimation(); return; }
                Counter = 8; Counter2 = (Counter2 - 1) & 255;
                if (Counter2 == 0)
                {
                    Substate++; Counter = 30; Toward(frame.EnemyTarget ?? frame.Player.Position);
                    Direction = CalculateDirection(Angle).Direction; RestartAnimation(Direction); return;
                }
                Angle = (Angle + AngularVelocity) & 31; UpdateAnimationFromAngle(); Move(); AdvanceAnimation(); return;
            case 2:
                if (Dec() != 0) { AdvanceAnimation(); return; }
                Substate = 3; if (Subid == 0) Text(0x2f05); return;
            case 3:
                _puff = World.Entities.TryCreateVirePuff(Position, ZFixed >> 8, 2);
                if (_puff < 0) return;
                Substate = 4; Flags &= ~128; Visible = false; return;
            case 4:
                if ((World.Entities.InteractionAnimationParameter(_puff) & 128) == 0) return;
                Flags |= 128; Input(true); World.Cutscene(0x18);
                Toward(frame.EnemyTarget ?? frame.Player.Position); Direction = CalculateDirection(Angle).Direction + 4; RestartAnimation(Direction); return;
            default: throw Unsupported();
        }
    }
    private void Fire(RoomEntityFrame frame)
    {
        if ((Flags & 1) == 0) return;
        if ((Flags & 4) != 0)
        {
            Flags &= ~4; Direction = 255; AttackCounter = 240;
            if (World.Entities.PartSlotAvailable) World.Entities.AddEntity(new TwinrovaProjectileRoomEntity(this, Subid == 0 ? 0x4b : 0x4d));
            int angle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), (frame.EnemyTarget ?? frame.Player.Position).Floor());
            RestartAnimation(angle < 16 ? 8 : 9); return;
        }
        if (!CollisionEnabled) return;
        if (AttackCounter != 0)
        {
            AttackCounter--; if (AnimationParameter != 255) return;
        }
        Animation.SetParameter((AnimationParameter - 1) & 255);
        Direction = CalculateDirection(Angle).Direction; RestartAnimation(Direction + 4);
    }
    private bool NextTarget()
    {
        FacingDelay = 0;
        int index = Waypoint; Waypoint = (Waypoint + 1) & 255;
        var targets = Data.Bytes($"targets-{Subid}-{Pattern}");
        if (index * 2 >= targets.Length) throw new NotSupportedException($"twinrova.s: subid${Subid:x2} pattern${Pattern:x2} waypoint${index:x2} reads beyond its source table.");
        int y = targets[index * 2]; if (y == 0) return false;
        Target = new(targets[index * 2 + 1], y);
        MoveTowardTarget(); // The source target setter falls through into movement.
        return true;
    }
    private bool MoveTowardTarget()
    {
        if ((((int)Position.X - Target.X + 1) & 255) < 3 && (((int)Position.Y - Target.Y + 1) & 255) < 3) return true;
        Toward(Target); Move(); MovingAnimation(); AdvanceAnimation(); return false;
    }
    private bool AttackInProgress()
    {
        var actor = (Flags & 1) != 0 ? this : Twin;
        if ((actor.Flags & 1) == 0) return false;
        if ((actor.Direction & 128) == 0) actor.Flags &= ~1;
        return true;
    }
    private (int Direction, bool Same) CalculateDirection(int angle)
    {
        int direction = ((angle + 4) & 0x18) >> 3;
        return (direction, (angle & 7) == 4 || Direction == direction);
    }
    private void UpdateAnimationFromAngle()
    { var next = CalculateDirection(Angle); if (!next.Same) { Direction = next.Direction; RestartAnimation(Direction); } }
    private void MovingAnimation(int? angle = null)
    {
        var next = CalculateDirection(angle ?? Angle);
        if (next.Same || (Direction & 128) != 0 || FacingDelay != 0) return;
        FacingDelay = 30; Direction = next.Direction; RestartAnimation(Direction + 4);
    }
    private void Move() => Position = ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position), Speed, Angle).PrecisePosition;
    private void Toward(Vector2 target) => Angle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), target.Floor());
    private int Dec() => Counter = (Counter - 1) & 255;
    private void HighX(int x) => Position = new((x & 255) + Position.X % 1, Position.Y);
    private void HighY(int y) => Position = new(Position.X, (y & 255) + Position.Y % 1);
    private void HighZ(int z) => ZFixed = (unchecked((sbyte)z) << 8) | (ZFixed & 255);
    private void Text(int id) => World.Entities.OnRoomEntityDialogueRequested(id, Data.Text(id), Position);
    private void Input(bool enabled)
    {
        World.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, enabled ? (byte)0 : (byte)1);
        World.Entities.RuntimeState.SetWramByte(AgesWramAddress.wMenuDisabled, enabled ? (byte)0 : (byte)1);
    }
    private NotSupportedException Unsupported() => new($"twinrova.s: ENEMY $03:${Subid:x2}, state${State:x2}, substate${Substate:x2}.");
}
