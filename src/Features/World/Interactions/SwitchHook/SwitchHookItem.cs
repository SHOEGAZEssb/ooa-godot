using Godot;
using System;
using System.Linq;

namespace oracleofages;

/// <summary>The $d6 weapon item; its parent and post-object pass run separately.</summary>
internal partial class SwitchHookItem : TransitionOffsetNode2D
{
    private readonly SwitchHookDatabase _database;
    private readonly SwitchHookLevel _level;
    private readonly ItemTilePassage _tilePassage = new();
    private readonly BreakableTileDatabase _breakables = new();
    private readonly Action<int> _sound;
    private readonly EnemyAnimationPlayer _animation;
    private readonly EnemyAnimationPlayer _chainAnimation;
    private readonly int _direction;
    private readonly SwitchHookController? _controller;
    private BreakableTileBreak _tile;
    private Texture2D? _tileTexture;
    private ISwitchHookEnemy? _enemy;
    private bool _enemyHitPending;
    private bool _objectCollisionPending;
    internal int HitDamage => 256 - _database.Graphic(_direction + 2).Damage;
    private bool _hideHookGraphic;
    internal bool CollisionEnabled => !Finished && State == 1 && !_enemyHitPending;
    internal int ZHigh { get; private set; }
    private Vector2 _precisePosition;
    private OracleRuntimeState? _movementMemory;
    internal void BindMovementMemory(OracleRuntimeState memory) => _movementMemory = memory;
    private bool _cancelRequested;
    private bool _chainCreated;
    internal bool ChainAllocated => _chainCreated;
    private int _chainCounter = 3;
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter { get; private set; }
    internal bool Finished { get; private set; }
    internal bool ChainVisible { get; private set; }
    internal Vector2 ChainPosition { get; private set; }
    internal int ChainZHigh { get; private set; }
    internal int Angle { get; private set; }
    internal Vector2 PrecisePosition => _precisePosition;
    internal Rect2 CollisionBounds => new(Position - Vector2.One * 6, Vector2.One * 12);

    internal SwitchHookItem(SwitchHookDatabase database, int level, Vector2 position, int direction, Action<int> sound,
        SwitchHookController? controller = null)
    {
        _database = database;
        _level = database.Level(level);
        _direction = direction;
        _sound = sound;
        _controller = controller;
        _precisePosition = position.Floor(); // itemCreateChild/objectCopyPosition copies high bytes.
        Position = _precisePosition;
        _animation = new EnemyAnimationPlayer(this, 6);
        _animation.Load(OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/spr_switch_hook.png"),
            database.HookGraphics.Select(g => g.Animation).ToArray(), 0, database.Graphic(0).OamFlags & 7);
        var chain = database.Chain;
        _chainAnimation = new EnemyAnimationPlayer(this, 1);
        _chainAnimation.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{chain.Sprite}.png"),
            [chain.Animation], chain.TileBase, chain.OamFlags & 7);
        Visible = false;
    }

    internal void RequestCancellation() => _cancelRequested = true;
    internal void NotifyObjectCollision(Vector2? clinkPosition = null)
    {
        _objectCollisionPending = true;
        // Collision runs after interactions. INTERAC_CLINK's state0 must
        // wait for the next interaction pass before becoming visible/audible.
        if (clinkPosition is { } point)
            _controller!.CreateClink(point, initializeOnUpdate: true);
    }
    internal void LatchEnemy(ISwitchHookEnemy enemy)
    {
        _enemy = enemy;
        _enemyHitPending = true; // collisionEffect2e: var2a bit5, collision bit7 cleared immediately.
    }
    internal void Delete()
    {
        _enemy?.ReleaseSwitchHook();
        Finished = true;
        Visible = false;
        ChainVisible = false;
        QueueRedraw();
    }

    internal void UpdateItem(OracleRoomData room, Vector2 linkPosition, bool frozen, Func<bool> chainSlotAvailable)
    {
        if (Finished || frozen && State != 0) return;
        if (State == 0)
        {
            var offset = _database.Offset(_direction);
            _precisePosition = new(((int)_precisePosition.X + offset.Position.X) & 0xff,
                ((int)_precisePosition.Y + offset.Position.Y) & 0xff);
            Position = _precisePosition;
            State = 1;
            Counter = _level.ExtensionFrames;
            Angle = _direction * 8;
            _animation.SetAnimation(_direction + 2);
            Visible = true;
            return;
        }
        if (State == 1)
        {
            if (_enemyHitPending)
            {
                _enemyHitPending = false;
                State = 3;
                Substate = 0;
                _controller!.Player.ClearInteractionKnockback(clearInvincibility: true);
                return;
            }
            if (_objectCollisionPending) { Retract(); return; }
            if (_cancelRequested) { Delete(); return; }
            Counter = (Counter - 1) & 0xff;
            if (Counter == 0 || Position.X >= room.Width || Position.Y >= room.Height)
            { Retract(); return; }
            if (room.IsSolid(Position) && !_tilePassage.CanPass(room, Position, Angle))
            {
                if (_controller is not null) _controller.CreateClink(Position);
                else _sound(SoundId.SndClink);
                if (_breakables.TryGet(room.ActiveCollisions, room.GetMetatile(Position), out var tile) &&
                    tile.AllowsSource(_level.BreakSource))
                {
                    if (_controller is null) throw new InvalidOperationException("Switch Hook tile exchange requires its world controller.");
                    State = 3;
                    Substate = 0;
                    _controller.Player.ClearInteractionKnockback(clearInvincibility: true);
                }
                else Retract();
                return;
            }
            if (!_chainCreated && chainSlotAvailable()) _chainCreated = true;
            PlayFlightSound();
            Move();
        }
        else if (State == 2)
        {
            if (Substate == 0)
            {
                Counter = (Counter - 1) & 0xff;
                PlayFlightSound();
                Angle = OracleObjectMovement.Shared.RelativeAngle(Position, linkPosition);
                Move();
                // boomerang.s:itemCheckWithinRangeOfLink, bc=$1008: [-8,+7].
                if (WithinLinkRange(Position.Y, linkPosition.Y) && WithinLinkRange(Position.X, linkPosition.X))
                {
                    Substate = 1;
                    Counter = _level.RetractedFrames;
                    Visible = false;
                    ChainVisible = false;
                }
            }
            else
            {
                _precisePosition = linkPosition.Floor() + _precisePosition - _precisePosition.Floor();
                Position = _precisePosition;
                if (--Counter == 0) Delete();
            }
        }
        else if (State == 3) UpdateExchange(room);
        QueueRedraw();
    }

    private void UpdateExchange(OracleRoomData room)
    {
        var controller = _controller ?? throw new InvalidOperationException("Missing Switch Hook exchange owner.");
        if (Substate == 0)
        {
            if (_cancelRequested) { Delete(); return; }
            if ((_animation.CurrentParameter & 0x80) == 0) { _animation.Advance(); return; }
            if (_enemy is not null)
            {
                if (!_enemy.SwitchHookHeld || !controller.CanLiftEnemy(_enemy.SwitchHookPosition))
                { _enemy.ReleaseSwitchHook(); Retract(); return; }
                _precisePosition = _enemy.SwitchHookPosition.Floor() + _precisePosition - _precisePosition.Floor();
                _hideHookGraphic = true;
            }
            else
            {
                if (!controller.TryLiftTile(Position, out _tile, out var texture)) { Retract(); return; }
                _tileTexture = texture;
                _precisePosition = _tile.TileCenter + _precisePosition - _precisePosition.Floor();
            }
            Position = _precisePosition.Floor();
            ZHigh = 0;
            Substate = 1;
            _sound(_level.ExchangeSound);
            controller.BeginHelper(Position);
            return;
        }
        var helper = controller.Helper ?? throw new InvalidOperationException("Switch Hook state3 lost reserved item $de.");
        if (!helper.Initialized) throw new InvalidOperationException("Switch Hook helper did not initialize in its later item slot.");
        if (Substate == 1)
        {
            ZHigh--;
            if (ZHigh < -(_level.LiftFrames - 1)) Substate = 2;
        }
        else if (Substate == 2)
        {
            helper.Swap();
            Vector2I facing = controller.Player.FacingVector;
            if (_enemy is null)
            {
                int direction = facing == Vector2I.Up ? 0 : facing == Vector2I.Right ? 1 : facing == Vector2I.Down ? 2 : 3;
                var offset = _database.Offset(direction);
                Vector2 corrected = new(((int)helper.HookPosition.X + offset.Position.X) & 0xff,
                    ((int)helper.HookPosition.Y + offset.Position.Y) & 0xff);
                int packed = room.GetPackedPosition(corrected) & 0xff;
                Vector2 center = TileCenter(packed);
                if (!controller.CanPlaceDiamond(center))
                {
                    Vector2 alternate = TileCenter((packed + offset.PlacementOffset) & 0xff);
                    if (controller.CanPlaceDiamond(alternate)) center = alternate;
                }
                helper.HookPosition = center + helper.HookPosition - helper.HookPosition.Floor();
            }
            else if (_enemy.SwitchHookHeld) _enemy.SwapSwitchHook();
            _precisePosition = helper.HookPosition;
            Position = _precisePosition.Floor();
            controller.Player.Face(-facing);
            Substate = 3;
        }
        else ZHigh++;
        if (_enemy?.SwitchHookHeld == true) _enemy.CopySwitchHookPosition(Position, ZHigh);
        controller.Player.SetSwitchHookPosition(helper.LinkPosition, (ZHigh << 8) | helper.LinkZLow);
        if (Substate == 3 && ZHigh == 0)
        {
            if (_enemy is null) controller.CompleteTile(_tile, Position);
            Delete();
        }
    }

    private static Vector2 TileCenter(int packed) => new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);

    private static bool WithinLinkRange(float item, float link) =>
        (((int)item - (int)link + 8) & 0xff) < 16;
    private void Retract() { State = 2; Substate = 0; }
    private void Move() => Position = NativeObjectMovement.ApplySpeed(_movementMemory, ref _precisePosition, _level.SpeedRaw, Angle);
    private void PlayFlightSound()
    {
        if ((Counter & _level.SoundMask) != 0) _sound(_level.FlightSound);
    }

    internal void UpdatePost(Vector2 linkPosition, bool parentActive)
    {
        if (!parentActive) Delete();
        if (Finished || State == 2 && Substate == 1) _chainCreated = false;
        if (Finished || !_chainCreated || State == 2 && Substate == 1)
        { ChainVisible = false; return; }
        ChainZHigh = ZHigh;
        if (--_chainCounter == 0) _chainCounter = 3;
        ChainPosition = new(ChainComponent(Position.X, linkPosition.X, 0), ChainComponent(Position.Y, linkPosition.Y, 3));
        ChainVisible = true;
        QueueRedraw();
    }
    private int ChainComponent(float hook, float link, int offset)
    {
        int position = (int)hook >= 0xf8 ? 0 : (int)hook;
        // Source uses the borrow bit as a signed ninth bit before two shifts.
        return (((position - (int)link) >> 2) * _chainCounter + (int)link + offset) & 0xff;
    }
    public override void _Draw()
    {
        if (!Finished && Visible && !_hideHookGraphic)
        {
            if (_tileTexture is not null)
                DrawTexture(_tileTexture, new Vector2(-8, -8 + ZHigh) + TransitionDrawOffset);
            else DrawTexture(_animation.CurrentTexture, _animation.CurrentOffset + TransitionDrawOffset);
        }
        if (ChainVisible)
            DrawTexture(_chainAnimation.CurrentTexture,
                _chainAnimation.CurrentOffset + ChainPosition - Position + Vector2.Down * ChainZHigh + TransitionDrawOffset);
    }
}
