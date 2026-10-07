using Godot;
using System;

namespace oracleofages;

internal sealed partial class OctogonPart : TransitionOffsetNode2D
{
    private readonly OctogonBehaviorProfile _data = OctogonBehaviorProfile.Shared;
    private readonly OracleRoomData _room;
    private readonly OracleRuntimeState _memory;
    private readonly OracleRandom _random;
    private readonly Func<int,bool> _parentVisible;
    private readonly Func<RoomEntitySpawn,bool> _part;
    private readonly Action<int> _sound;
    private int _z;
    private int _speedZ;
    private bool _collision = true;
    private bool _healthCleared;
    private int _health;
    internal int Id { get; }
    internal int SubId { get; }
    internal int ParentSlot { get; }
    internal int State { get; private set; }
    internal int Speed { get; private set; }
    internal int Angle { get; private set; }
    internal int Counter { get; private set; }
    internal int Gravity { get; private set; }
    internal int ZFixed => _z;
    internal int SpeedZ => _speedZ;
    internal int InvincibilityCounter { get; set; }
    internal int ContactFlags { get; private set; }
    internal bool PendingCollision => (ContactFlags&128) != 0;
    internal bool Finished { get; private set; }
    internal bool CollisionEnabled => _collision && !Finished;
    internal EnemyAnimationPlayer Animation { get; }
    internal Rect2 CollisionBounds => new(Position.Floor()-new Vector2(RadiusX,RadiusY),new(RadiusX*2,RadiusY*2));
    internal int RadiusX { get; private set; }
    internal int RadiusY { get; private set; }
    internal OctogonPart(OctogonPartSpawn spawn,OracleRoomData room,OracleRuntimeState memory,OracleRandom random,
        Func<int,bool> parentVisible,Func<RoomEntitySpawn,bool> part,Action<int> sound)
    {
        if (spawn.Id is not (0x48 or 0x55) || spawn.SubId is < 0 or > 1 || spawn.Id == 0x55 && spawn.SubId != 0)
            throw new NotSupportedException($"Octogon PART${spawn.Id:x2}:${spawn.SubId:x2}.");
        Id = spawn.Id; SubId = spawn.SubId; ParentSlot = spawn.ParentSlot; Angle = spawn.Angle;
        _room = room; _memory = memory; _random = random; _parentVisible = parentVisible; _part = part; _sound = sound;
        Position = spawn.Position.Floor(); _z = unchecked((short)(spawn.ZHigh<<8)); Visible = false;
        var properties = OctogonEffectsDatabase.Shared.Properties(Id); RadiusY = properties[2]>>4; RadiusX = properties[2]&15; _health = properties[4];
        var visual = OctogonEffectsDatabase.Shared.Visual(Id); Animation = new(this,visual.Animations.Length);
        Animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites),visual.Animations,visual.TileBase,visual.Palette,
            sourceGrayscaleInverted:visual.SourceGrayscaleInverted);
    }
    internal void PublishCollision(int type) => ContactFlags = 128|type;
    internal void ClearHealthAndCollision() { _healthCleared = true; _collision = false; }
    internal void ApplySomariaDamage(int rawDamage)
    {
        _health = Math.Clamp(_health+rawDamage,0,255);
        if (_health == 0) ClearHealthAndCollision();
        InvincibilityCounter = 21; PublishCollision(ItemCollisionType.SomariaBlock);
    }
    internal void UpdateFrame(RoomEntityFrame frame)
    {
        if (Finished) return;
        if (State != 0 && InvincibilityCounter != 0) InvincibilityCounter -= Math.Sign(InvincibilityCounter);
        bool hit = State != 0 && (PendingCollision || _healthCleared);
        if (hit && Id == 0x48 && SubId != 0) { Delete(); return; }
        if (hit && Id == 0x55)
        {
            if (ContactFlags != 128 || !frame.Player.NativeObjectVulnerable || frame.Player.NativeContactSignal)
            { if (ContactFlags != 128) Burst(); }
            else
            {
                frame.Player.RequestCollapse(1); State = 3; _z &= 255; _collision = false; Show(false);
            }
        }
        ContactFlags &= 0x7f;
        if (Id == 0x55) Bubble(frame); else Depth(frame.ScentSeedTarget ?? frame.Player.Position);
        QueueRedraw();
    }
    private void Depth(Vector2 target)
    {
        if (State == 0)
        {
            _healthCleared = false; _collision = true; State = 1;
            if (SubId != 0) { RadiusX = RadiusY = 2; Speed = _data.PartState[0]; Animation.SetAnimation(1); Show(true); return; }
            if (!_parentVisible(ParentSlot))
            {
                State = 2; Counter++; SetZHigh(_data.PartState[6]); Gravity = _data.PartState[5];
                int index = _random.Next().Value&6;
                CopyHigh(new(_data.DepthPositions[index+1],_data.DepthPositions[index])); _sound(SoundId.SndSplash);
            }
            else { Gravity = _data.PartState[4]; CopyHigh(Position.Floor()+Vector2.Up*16); _sound(SoundId.SndScentSeed); }
            Show(false); return;
        }
        if (SubId != 0)
        {
            Move();
            if (_room.IsSolidForEnemyMovement(Position.Floor(),holesAreWalls:false)) { Delete(); return; }
            Animation.Advance(); return;
        }
        switch (State)
        {
            case 1:
                SetZHigh(((_z>>8)-_data.PartState[7])&255);
                int high = (_z>>8)&255;
                if (high >= _data.PartState[8]) { Animation.Advance(); return; }
                if (high >= _data.PartState[6]) { Visible = !Visible; Animation.Advance(); return; }
                State = 2; Counter = _data.PartState[3]; _collision = false; CopyHigh(target.Floor()); Visible = false; return;
            case 2:
                if (Counter != 0) Counter--;
                if (Counter == 0) { State = 3; _collision = true; Show(false); } return;
            case 3:
                if (!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,Gravity)) { Animation.Advance(); return; }
                int angle = _random.Next().Value&_data.PartState[9];
                for (int count = 0; count < _data.PartState[9]; count++)
                    if (_part(new OctogonPartSpawn(Position.Floor(),0x48,1,-1,angle,_z>>8))) angle = (angle+_data.PartState[10])&31;
                _sound(SoundId.SndUnknown3); Delete(); return;
            default: throw new NotSupportedException($"octogonDepthCharge.s state${State:x2}.");
        }
    }
    private void Bubble(RoomEntityFrame frame)
    {
        switch (State)
        {
            case 0: _healthCleared = false; State = 1; Speed = _data.PartState[1]; Counter = _data.PartState[2]; Show(true); return;
            case 1:
                if (Counter != 0) Counter--;
                if (Counter == 0) { Burst(); return; }
                SetZHigh(_data.BubbleZ[(frame.Counter>>3)&3]); Move(); Animation.Advance(); return;
            case 2: Animation.Advance(); if (Animation.CurrentParameter == 0xff) Delete(); return;
            case 3:
                CopyHigh(frame.Player.EnemyContactPosition); SetZHigh(frame.Player.ItemCreationZFixed>>8);
                if (frame.Player.CollapsePending) return;
                if (frame.Player.CollapsedActive) Animation.Advance(); else Burst(); return;
            default: throw new NotSupportedException($"octogonBubble.s state${State:x2}.");
        }
    }
    private void Burst() { State = 2; _collision = false; Animation.SetAnimation(1); }
    private void Move()
    {
        var velocity = NativeObjectMovement.Velocity(_memory,Speed,Angle);
        Position = OracleObjectPosition.FromPixels(Position).Add(velocity.YFixed,velocity.XFixed).PrecisePosition;
    }
    private void CopyHigh(Vector2 point) => Position = new((byte)OracleObjectPosition.HighByte(point.X)+Position.X-Position.Floor().X,
        (byte)OracleObjectPosition.HighByte(point.Y)+Position.Y-Position.Floor().Y);
    private void SetZHigh(int high) => _z = unchecked((short)((high<<8)|(_z&255)));
    private void Show(bool priority2) { Visible = true; ZIndex = ObjectDrawPriority.FromVisible(priority2 ? 2 : 1); }
    private void Delete() { Finished = true; Visible = false; }
    public override void _Draw()
    { if (Visible && !Finished) DrawTexture(Animation.CurrentTexture,Animation.CurrentOffset+Vector2.Down*(_z>>8)+TransitionDrawOffset); }
}

internal sealed record OctogonPartSpawn(Vector2 Position,int Id,int SubId,int ParentSlot,int Angle = 0,int ZHigh = 0) : RoomEntitySpawn;
