using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// ages/enemies/ambiGuard.s. Detection writes var3b in the later PART pass;
// the next ENEMY pass consumes it before advancing the movement script.
internal sealed partial class AmbiGuardCharacter : EnemyCharacter, ITerrainShadowSource
{
    private RoomEntityManager _owner = null!;
    private AmbiGuardPatrol _patrol = null!;
    private EnemyTerrainMovement _movement = null!;
    private DetectionHelperRoomEntity? _detector;
    private int _command, _destination, _substate, _speedZ, _scentCounter, _hitCollision;
    private bool _fleeing;
    internal int SubId { get; private set; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Counter2 { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int Seen { get; private set; }
    internal int Alert { get; private set; }
    internal int Delay { get; private set; }
    internal int DetectionSignal { get; set; }
    internal int ZFixed { get; private set; }
    internal bool Attacking => (SubId & 0x80) != 0;
    internal int CollisionMode => State == 0x11 && Attacking ? 0x3d : 0x5a;
    internal override bool InitializationPending => State == 0;
    internal override bool CollisionEnabled => Health != 0 && base.CollisionEnabled;
    int? ITerrainShadowSource.TerrainShadowZHigh => ZFixed >> 8;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (ZFixed >> 8);

    internal void Initialize(ImportedEnemyDefinition record, AmbiGuardPatrol patrol,
        OracleRoomData room, Vector2 position, RoomEntityManager owner, int subid)
    {
        _owner = owner; _patrol = patrol; SubId = subid;
        _movement = new(this, room);
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        ConfigureSwordKnockback(room, EnemyKnockbackMotion.Terrain, nativeSpeed: () => Speed);
        Visible = false;
    }
    internal void AlertFromOther(bool sighting)
    {
        if (sighting) { if (Seen == 0) Seen = 1; }
        else if (Alert == 0) { Alert = 1; Delay = 60; }
    }
    internal void RecordCollision(int collision, Vector2 origin)
    {
        _hitCollision = collision | 0x80;
        KnockbackAngle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), origin.Floor()) ^ 0x10;
        DeferNativeHitStatus();
    }
    internal void RecordDeflection(int collision)
    {
        _hitCollision = collision | 0x80;
        InvincibilityCounter = BoomerangCollisionDatabase.Shared.DeflectionInvincibility;
        // ENEMYDMG $28 writes var2a and invincibility but preserves recoil.
        DeferNativeHitStatus();
    }
    internal override bool TakeSwordHit(Vector2 origin, int damage)
    {
        if (CollisionMode == 0x5a) { RecordCollision(4, origin); return true; }
        bool accepted = base.TakeSwordHit(origin, damage);
        if (accepted) RecordCollision(4, origin);
        return accepted;
    }
    internal override bool TakeBurnHit(int damage) => false;
    protected override void CompleteKnockbackDeath()
    {
        _fleeing = true;
        AdvanceFlee();
    }
    internal void InitializeStateZero()
    {
        if (State != 0 || IsDead) return;
        _owner.Random.Next(); // enemyStandardUpdate reloads var3d on every failed allocation.
        Speed = _patrol.Speed; _command = 0; RunScript();
        if (_owner.SaveData?.HasRoomFlag(4, 0xfc, 0x80) == true) { Finish(); return; }
        _detector = _owner.TryCreateGuardDetector(this);
        if (_detector is null) { State = 0; return; }
        Angle = _patrol.InitialDirection << 3;
        SetAnimation(_patrol.InitialDirection); Visible = true;
    }
    internal void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0)
        {
            InitializeStateZero();
            return;
        }
        bool hit = NativeHitPending;
        if (BeginFrame(continueDuringHitAndKnockback: hit)) return;
        if (_fleeing || Health == 0 && !hit) { _fleeing = true; AdvanceFlee(); return; }
        if (hit && Seen == 0)
        {
            if (_hitCollision < 0x92)
            { Seen = 1; if (Attacking) { Counter2 = 90; Delay = 0; } }
            else if (_hitCollision != 0x9e && Alert == 0)
            { Alert = 1; Delay = 90; FaceAngle(KnockbackAngle ^ 0x10); }
        }
        if (frame.ScentSeedTarget is { } scent)
        {
            if (_scentCounter == 0) { _scentCounter = 150; Alert = (Alert + 1) & 255; Delay = 60; Face(scent); }
            else if ((frame.Counter & 1) == 0) _scentCounter--;
        }
        if (_owner.ReadPlayingInstrument() != 0)
        { Alert = (Alert + 1) & 255; Delay = 60; Face(frame.ScentSeedTarget ?? frame.Player.Position); }
        if (DetectionSignal == 0xff && Seen == 0)
        { Seen = 1; if (Attacking) { Counter2 = 90; Delay = 0; } }
        DetectionSignal = 0;
        if ((Seen & 1) != 0 && (Seen & 2) == 0 &&
            (Attacking || frame.Player.NativeInteractionCollisionsEnabled && frame.Player.TopDownAirZ >= 0))
        {
            if (!Attacking)
            {
                _owner.RuntimeState.SetWramByte(AgesWramAddress.wMenuDisabled, 0x80);
                // ambiGuard.s: DISABLE_COMPANION | DISABLE_LINK ($21).
                // INTERACTION $02 must remain clear for the capture handoff.
                _owner.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0x21);
                _owner.RuntimeState.SetWramByte(AgesWramAddress.wDisableScreenTransitions, 0x21);
                Delay = 60; Exclaim(spawns);
            }
            Seen |= 2; State = 0x0f;
            if (_detector is not null) _detector.Health = 0;
        }
        else if (Seen == 0 && (Alert & 1) != 0)
        { Alert = 0; Speed = 0x32; Exclaim(spawns); }
        if (Delay != 0)
        {
            Delay--;
            if (Delay == 1 && State is >= 8 and <= 11) { Angle = (State - 8) << 3; SetAnimation(State - 8); }
            else if (Delay == 60) { _owner.OnSoundRequested(SoundId.SndMakuTreePast); _owner.AlertPalaceGuards(this, false); }
            return;
        }
        switch (State)
        {
            case 8: case 9: case 10: case 11:
                bool y = State is 8 or 10;
                int coordinate = OracleObjectPosition.HighByte(y ? Position.Y : Position.X);
                bool reached = State is 8 or 11 ? coordinate <= _destination : coordinate >= _destination;
                if (reached)
                {
                    Position = y ? new(Position.X, _destination + Position.Y % 1) : new(_destination + Position.X % 1, Position.Y);
                    RunScript();
                }
                else { Position = ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position), Speed, Angle).PrecisePosition; AdvanceAnimation(); }
                break;
            case 12: case 14:
                Counter = (Counter - 1) & 255;
                if (Counter == 0) RunScript(); else AdvanceAnimation();
                break;
            case 13:
                Counter = (Counter - 1) & 255;
                if (Counter == 0) { State = 14; Counter = 30; Angle ^= 0x10; SetAnimation(Angle >> 3); }
                break;
            case 15:
                State = 16;
                if (Attacking) { if (Counter2 == 0) Counter2 = 60; Exclaim(spawns); }
                else { Counter = 90; _owner.OnSoundRequested(SoundId.SndWhistle); }
                Face(frame.Player.Position); break;
            case 16 when Attacking:
                Counter2 = (Counter2 - 1) & 255;
                if (Counter2 == 0) { State = 17; Counter = 20; Speed = 0x3c; Face(frame.Player.Position); }
                else if (Counter2 == 60) { _owner.OnSoundRequested(SoundId.SndWhistle); _owner.AlertPalaceGuards(this, true); }
                break;
            case 16:
                AdvanceAnimation(); Counter = (Counter - 1) & 255;
                if (Counter == 0 || Within(frame.Player.Position, 24)) _owner.RequestPalaceBoot();
                else _movement.MoveAtAngle(Angle, Speed, allowHoles: false);
                break;
            case 17 when Attacking:
                Counter = (Counter - 1) & 255;
                if (Counter == 0) { Counter = 20; Face(frame.Player.Position); }
                _movement.MoveAtAngle(Angle, Speed, allowHoles: false); AdvanceAnimation(); break;
        }
        QueueRedraw();
    }
    private bool Within(Vector2 link, int radius)
    {
        // objectCheckLinkWithinDistance compares the Manhattan distance.
        int y = Math.Abs((int)link.Y - (int)Position.Y);
        int x = Math.Abs((int)link.X - (int)Position.X);
        return y <= radius && x < radius - y;
    }
    private void Face(Vector2 target) => FaceAngle(OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), target.Floor()));
    private void FaceAngle(int angle) { Angle = (angle + 4) & 0x18; SetAnimation(Angle >> 3); }
    private void Exclaim(ICollection<RoomEntitySpawn> spawns)
    { if (_owner.InteractionSlotAvailable) spawns.Add(new ExclamationMarkSpawn(Position.Floor() + new Vector2(8, -12), Duration: 45)); }
    private void RunScript()
    {
        for (int dispatch = 0; dispatch <= _patrol.Commands.Length; dispatch++)
        {
            var command = _patrol.Commands[_command++];
            if (command.Opcode == "loop") { _command = command.Arg0; continue; }
            if (command.Opcode is "wait" or "state")
            { State = command.Opcode == "wait" ? 12 : command.Arg1; Counter = command.Arg0; }
            else
            {
                int direction = command.Opcode switch { "up" => 0, "right" => 1, "down" => 2, "left" => 3, _ => throw new InvalidOperationException(command.Source) };
                Angle = direction << 3; State = direction + 8; _destination = command.Arg0;
            }
            SetAnimation((Angle & 0x18) >> 3); return;
        }
        throw new InvalidOperationException($"{_patrol.Source}: movement script loops without yielding.");
    }
    private void AdvanceFlee()
    {
        if (_substate == 0) { _substate = 1; _speedZ = -0x100; }
        if (_substate == 2)
        {
            if (OracleObjectPosition.HighByte(Position.Y) >= 0xb0 || OracleObjectPosition.HighByte(Position.X) >= 0xf0) { Finish(); return; }
            Position = ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position), Speed, Angle).PrecisePosition;
        }
        int z = ZFixed;
        bool landed = OracleObjectMath.UpdateSpeedZ(ref z, ref _speedZ, 0x20); ZFixed = z;
        if (landed)
        {
            _speedZ = -0x1c0;
            if (_substate == 1) { _substate = 2; Speed = 0x32; Angle = KnockbackAngle; SetAnimation(((Angle + 4) & 0x18) >> 3); }
        }
        else if (_substate == 2) AdvanceAnimation();
        QueueRedraw();
    }
}
