using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

// ITEM$0d locomotion dispatch; ITEM$03/$0d share BombEffect's explosion owner.
internal partial class BombchuItem : BombEffect
{
    private readonly BombchuDatabase _data = new();
    private readonly BombRecord _common = new BombDatabase().Data;
    private OracleRoomData _room = null!;
    private Player _player = null!;
    private RoomEntityManager _entities = null!;
    private EnemyAnimationPlayer _walking = null!;
    private BombchuSteering? _steering;
    private int _locomotion, _counter1, _counter2, _radius, _scanSlot, _targetSlot = -1, _speedTmp, _verticalFlags;
    private int _group;

    internal int ItemState => State == BombState.Exploding ? 0xff : _locomotion;
    internal int Counter1 => State == BombState.Exploding ? (byte)BreakProbe : _counter1;
    internal int Counter2 => _counter2;
    internal int Radius => State == BombState.Exploding ? ExplosionRadius : _radius;
    internal int ScanSlot => _scanSlot;
    internal int TargetSlot => _targetSlot;
    internal int SpeedTmp => _speedTmp;
    internal BombchuSteering? Steering => _steering;
    internal int WalkingFrame => _walking.FrameIndex;
    internal int WalkingParameter => _walking.CurrentParameter;
    internal int VerticalFlags => _verticalFlags;
    private bool Sideview => (_room.TilesetFlags & (int)TilesetFlags.Sidescroll) != 0;

    internal BombRecord ExplosionRecord
    {
        get
        {
            var graphic = _data.Graphic(0);
            return _common with { Item = 0x0d, TreasureId = TreasureId.Bombchus, Sprite = graphic.Sprite,
                TileBase = graphic.TileBase, Palette = graphic.OamFlags & 7, Collision = graphic.Collision,
                RadiusY = 0x18, RadiusX = 0x18, BaseDamage = -unchecked((sbyte)graphic.RawDamage),
                FuseAnimation = graphic.Animation, ExplosionAnimation = _data.Graphic(6).Animation,
                Source = graphic.Source };
        }
    }

    internal void Configure(OracleRoomData room, Player player, RoomEntityManager entities, int group,
        Vector2 creationPosition, int creationZ)
    {
        _room = room; _player = player; _entities = entities; _group = group;
        _precisePosition = creationPosition; _zFixed = unchecked((short)creationZ); SyncPosition();
        _walking = new(this, 6);
        var graphic = _data.Graphic(0);
        _walking.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{graphic.Sprite}.png"),
            Enumerable.Range(0, 6).Select(index => _data.Graphic(index).Animation).ToArray(),
            graphic.TileBase, graphic.OamFlags & 7, positionedOam: true);
    }

    internal void UpdateBombchu(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        _counter2 = (byte)(_counter2 - 1);
        if (_counter2 == 0) BeginExplosion();
        if (State == BombState.Exploding) { UpdateExplosion(frame.Player, spawns); return; }
        if (Position.X >= _room.Width || Position.Y >= _room.Height) { Discard(); return; }

        if (Sideview && _steering?.Clinging != true)
        {
            bool landed = ItemVerticalMotion.AdvanceSideview(_room, ref _precisePosition, ref _zFixed,
                ref _speedZ, ref _verticalFlags, _data.Constant("sidescroll-gravity"), out int effect);
            SyncPosition();
            if (!landed && effect != 0)
            {
                Hazard(effect);
                // The wrapper CALLs a water splash, then XOR A clears carry.
                // Lava tail-jumps to the splash and retains carry for deletion.
                if (effect == 4) { Discard(); return; }
            }
        }
        else if (!Sideview)
        {
            bool grounded = (_zFixed >> 8) >= 0;
            if (!grounded) grounded = OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, _data.Constant("topdown-gravity"));
            if (grounded)
            {
                int hazard = ItemHazardDatabase.Shared.Hazard(_room.ActiveCollisions,
                    _room.GetMetatile(new(Position.X, (byte)((int)Position.Y + 5))));
                if (hazard != 0) { Hazard(hazard); Discard(); return; }
            }
        }
        if (_locomotion == 0)
        {
            _player.Inventory.TryConsumeBombchu(); // decNumBombchus also permits an externally emptied count.
            _locomotion = 1; _counter1 = _data.Constant("initial-wait"); _counter2 = _data.Constant("initial-fuse");
            _radius = _data.Constant("initial-vision"); _speedTmp = _data.Constant("search-speed"); _scanSlot = 0;
            _steering = new(_data, CarriedObjectMotion.DirectionIndex(_player.FacingVector), Sideview);
            _walking.SetAnimation(_steering.Animation);
            Vector2I offset = _data.PlacementOffset(CarriedObjectMotion.DirectionIndex(_player.FacingVector), _group >= 6);
            Vector2 point = new((byte)((int)_player.Position.X + offset.X), (byte)((int)_player.Position.Y + offset.Y));
            if (Collision(point) == 0x0f) point = _player.Position;
            _precisePosition = point + new Vector2(_precisePosition.X - Mathf.Floor(_precisePosition.X),
                _precisePosition.Y - Mathf.Floor(_precisePosition.Y));
            ActivateIndependentItem();
            return;
        }
        if (Sideview)
        {
            if (_locomotion == 1)
            {
                _speedRaw = _data.Constant("search-speed");
                if (Scan()) return;
            }
            else if (_locomotion == 2)
            {
                _counter1 = (byte)(_counter1 - 1);
                if (_counter1 != 0) return;
                _counter1 = _data.Constant("chase-counter"); _locomotion = 3;
            }
            if (_locomotion == 3)
            {
                if (TargetContact()) { BeginExplosion(); return; }
                if ((frame.Counter & _data.Constant("homing-frame-mask")) == 0) Home();
            }
            var climb = _steering!.CheckWalls(angle => CollisionFront(angle));
            if (climb.ResetSpeedZ) _speedZ = 0;
            if (climb.AnimationChanged) _walking.SetAnimation(_steering.Animation);
            Move(); _walking.Advance(); return;
        }
        if (_locomotion == 1 && (_zFixed >> 8) >= 0) _locomotion = 2;
        if (_locomotion == 2 && Scan()) return;
        if (_locomotion == 3)
        {
            _counter1 = (byte)(_counter1 - 1);
            if (_counter1 == 0) { _counter1 = _data.Constant("chase-counter"); _locomotion = 4; }
            else { Conveyor(); return; }
        }
        if (_locomotion == 4)
        {
            if (TargetContact()) { BeginExplosion(); return; }
            if ((frame.Counter & _data.Constant("homing-frame-mask")) == 0) Home();
        }
        int collision = CollisionFront(_steering!.Angle);
        // specialCollisionValues.s: holes/water/lava$10, unused$15, boundary$ff.
        if (collision is 0x10 or 0x15 or 0xff)
        {
            if (_steering.TurnFromImpassableTile()) _walking.SetAnimation(_steering.Animation);
        }
        else
        {
            int speed = collision == 0 ? _speedTmp : _common.ReducedBounceSpeed(_speedTmp);
            if (speed < _speedRaw) _speedZ = _data.Constant("wall-jump-speed-z");
            _speedRaw = speed;
        }
        OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, _data.Constant("motion-gravity"));
        Move(); Conveyor(); _walking.Advance();
    }

    private bool Scan()
    {
        var target = _entities.BombchuTargetAt(_scanSlot, requireVisible: true);
        if (target is { } enemy && enemy.Visible && _data.CanTarget(enemy.Id) &&
            RoomEntityManager.ObjectCollisionXYOverlaps(Bounds(_radius), enemy.Bounds))
        {
            _targetSlot = _scanSlot; _radius = _data.Constant("target-radius");
            _counter1 = _data.Constant("target-wait"); _speedTmp = _data.Constant("target-speed");
            _locomotion++; Home(); return true;
        }
        if (++_scanSlot == 16)
        {
            _scanSlot = 0; _radius += _data.Constant("vision-step");
            if (_radius >= _data.Constant("vision-limit")) _radius = _data.Constant("vision-reset");
        }
        return false;
    }
    private bool TargetContact() => _entities.BombchuTargetAt(_targetSlot) is not { Health: > 0 } target ||
        RoomEntityManager.ObjectCollisionXYOverlaps(Bounds(_radius), target.Bounds);
    private void Home()
    {
        Vector2 target = _entities.BombchuTargetAt(_targetSlot)?.Position ?? Vector2.Zero;
        if (_steering!.Home(OracleObjectMovement.Shared.RelativeAngle(Position, target), Sideview))
            _walking.SetAnimation(_steering.Animation);
    }
    private Rect2 Bounds(int radius) => new(Position - Vector2.One * radius, Vector2.One * radius * 2);
    private int Collision(Vector2 point) => point.X >= _room.Width || point.Y >= _room.Height ? 0xff :
        _room.GetTerrainInfo(point).Collision;
    private int CollisionFront(int angle)
    {
        Vector2I offset = _data.FrontOffset(angle);
        return Collision(new((byte)((int)Position.X + offset.X), (byte)((int)Position.Y + offset.Y)));
    }
    private void Move() => Position = NativeObjectMovement.ApplySpeed(_movementMemory, ref _precisePosition, _speedRaw, _steering!.Angle);
    private void Conveyor()
    {
        if ((_zFixed >> 8) < 0) return;
        TerrainType type = _room.GetTerrainInfo(new(Position.X, (byte)((int)Position.Y + 5))).Type;
        int direction = type switch { TerrainType.UpConveyor => 0, TerrainType.RightConveyor => 1,
            TerrainType.DownConveyor => 2, TerrainType.LeftConveyor => 3, _ => -1 };
        if (direction < 0) return;
        Vector2I offset = _common.EdgeOffsets[direction];
        Vector2 point = new((byte)((int)Position.X + offset.X), (byte)((int)Position.Y + offset.Y));
        if (Collision(point) != 0xff && !_room.IsSolid(point))
            Position = NativeObjectMovement.ApplySpeed(_movementMemory, ref _precisePosition, _data.Constant("search-speed"), direction * 8);
    }
    private void BeginExplosion() { _counter2 = 0; InitializeExplosion(_radius); }
    private void Hazard(int kind) => _entities.CreateBombchuHazard(kind, Position, _zFixed >> 8);
    public override void _Draw()
    {
        if (State == BombState.Exploding) { base._Draw(); return; }
        if (!Finished && Visible) DrawTexture(_walking.CurrentTexture, SourceOamDrawOffset + _walking.CurrentOffset + Vector2.Down * (_zFixed >> 8));
    }
}
