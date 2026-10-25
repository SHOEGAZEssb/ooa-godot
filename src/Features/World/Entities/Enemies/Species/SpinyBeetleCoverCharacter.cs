using Godot;
using System.Collections.Generic;

namespace Oracles;

// bushOrRock.s: ENEMY $58 is an uncounted, independently dispatched child.
// Its carried lifetime survives detachment and the original parent's death.
internal sealed partial class SpinyBeetleCoverCharacter : EnemyCharacter, ISwitchHookEnemy
{
    internal SpinyBeetleCharacter Parent { get; }
    private readonly OracleRandom _random;
    private readonly Texture2D _texture;
    private OracleRoomData _room;
    private readonly BraceletDatabaseRecord _bracelet;
    private readonly BombRecord _throwing;
    private readonly System.Action<Rect2,int,int,int> _applyThrownHit;
    private CarriedObjectMotion _motion;
    private bool _detached, _held, _thrown;
    private bool _initialized;
    private bool _burnCollisionDisabled;
    private int _stunCounter, _speedZ;
    internal int ZFixed { get; set; }
    private int _hookSubstate, _hookZ, _hookSpeedZ;
    internal override bool InitializationPending => !_initialized;
    internal bool Rock => Parent.Record.SubId >= 2;
    internal bool Grabbable => _initialized && !_detached && !IsDead;
    internal bool Held => _held;
    internal void EnterRoom(OracleRoomData room) => _room = room;
    internal int ZHigh => SwitchHookHeld ? _hookZ >> 8 : _held || _thrown ? _motion.ZFixed >> 8 : ZFixed >> 8;
    internal override bool CollisionEnabled => _initialized && !IsDead && Health > 0 &&
        !_detached && !_burnCollisionDisabled && !SwitchHookHeld && !NativeHitPending;

    internal SpinyBeetleCoverCharacter(SpinyBeetleCharacter parent, OracleRandom random,
        OracleRoomData room, BraceletDatabaseRecord bracelet, BombRecord throwing,
        System.Action<Rect2,int,int,int> applyThrownHit)
    {
        Parent = parent; _random = random;
        _texture = parent.CoverTexture; _room = room; _bracelet = bracelet; _throwing = throwing;
        _applyThrownHit = applyThrownHit; _motion = new(parent.Position.Floor());
        var data = EnemyBehaviorTables.Shared.SpinyCoverAttributes;
        InitializeEnemy(parent.Position.Floor(), EnemyCharacterConfiguration.FromImported(parent.Record with
        { Id = data[0].Value, Health = data[1].Value, RadiusY = data[2].Value, RadiusX = data[3].Value }));
        Visible = false;
    }

    internal void UpdateFrame(Player player, int frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) { _random.Next(); _initialized = true; Visible = true; }
        if (!_detached && Parent.IsDead) { Finish(); return; }
        bool justHit = NativeHitPending;
        bool stunned = !justHit && Health > 0 && _stunCounter != 0;
        if (stunned)
        {
            int z = ZFixed;
            Position = EnemyStunMotion.Update(Position,8,frame,ref _stunCounter,ref z,ref _speedZ);
            ZFixed = z;
        }
        BeginFrame();
        // enemyCode58 continues state8 on JUST_HIT even at zero health;
        // its destruction branch runs on the following normal status visit.
        if (Health == 0 && !justHit) { Destroy(spawns); return; }
        if (stunned) return;
        if (SwitchHookHeld)
        {
            if (_hookSubstate == 0) { Detach(); _hookSubstate = 1; }
            else if (_hookSubstate == 3 && OracleObjectMath.UpdateSpeedZ(ref _hookZ, ref _hookSpeedZ, 0x20)) Destroy(spawns);
            QueueRedraw(); return;
        }
        if (_held) UpdateHeldPosition(player);
        else if (_thrown)
        {
            if (Position.X < 0 || Position.X >= _room.Width || Position.Y < 0 || Position.Y >= _room.Height)
            { Finish(); return; }
            if (_motion.AdvanceHorizontal(_throwing,_room,ApplyMovementSpeed,breakOnCollision:true) || _motion.AdvanceVertical(_bracelet))
            {
                Position = _motion.GroundPosition.Floor();
                var hazard = _room.GetTerrainInfo(Position).Hazard;
                if (hazard is HazardType.Water or HazardType.Lava) { spawns.Add(new EnemySplashSpawn(Position,hazard)); Finish(); }
                else if (hazard == HazardType.Hole) { spawns.Add(new FallingDownHoleSpawn(Position)); Finish(); }
                else Destroy(spawns);
                return;
            }
            Position = _motion.GroundPosition;
            _applyThrownHit(CarriedObjectMotion.CollisionBounds(Position.Floor(),_bracelet),ZHigh,_bracelet.CollisionZRadius,_bracelet.Damage);
        }
        else { Position = Parent.Position.Floor(); ZFixed = (Parent.CoverZ << 8) | (ZFixed & 255); }
        ZIndex = _held ? ObjectDrawPriority.InFrontOfLinkZIndex : ObjectDrawPriority.RelativeToLink(Position.Y,player.Position.Y,ZHigh);
        QueueRedraw();
    }

    internal bool BreakCover(Vector2 _, int damage)
    {
        if (!CollisionEnabled || Rock || InvincibilityCounter != 0) return false;
        Health = System.Math.Max(0, Health - damage);
        InvincibilityCounter = EnemyBehaviorTables.Shared.EnemySwordDamageProfiles[3].First;
        DeferNativeHitStatus(); return true;
    }
    internal void BeginBurn(int damage)
    {
        var status = EnemyBehaviorTables.Shared.TargetGhini.EmberStatus;
        Health = System.Math.Max(0,Health-damage); _burnCollisionDisabled = Health == 0;
        InvincibilityCounter = unchecked((sbyte)status[1].Value); _stunCounter = status[3].Value;
        DeferNativeHitStatus();
    }
    internal void ReleaseBurn() { InvincibilityCounter = 0; _stunCounter = 1; }
    private void Destroy(ICollection<RoomEntitySpawn> spawns)
    {
        if (!Parent.IsDead && GodotObject.IsInstanceValid(Parent)) Parent.DeleteNativeCover();
        if (Rock) spawns.Add(new RockDebrisSpawn(Position)); else spawns.Add(new GrassDebrisSpawn(Position));
        Finish();
    }
    private void Detach() { if (!_detached) { _detached = true; Parent.DetachNativeCover(); } }
    internal void UpdateHeldPosition(Player player)
    {
        if (!_held) return;
        _motion.Hold(player); Position = _motion.GroundPosition; QueueRedraw();
    }
    internal bool UseBracelet(Player player, Vector2I direction)
    {
        if (_held)
        {
            _held = false; _thrown = true; _motion.Release(player,direction,_bracelet); return true;
        }
        if (!_initialized || _detached || IsDead || player.IsCarryingObject || player.CutsceneControlled) return false;
        Vector2 delta = Position - (player.Position + (Vector2)player.FacingVector * 6);
        if (Mathf.Abs(delta.X) >= 13 || Mathf.Abs(delta.Y) >= 13) return false;
        Detach(); _held = true; player.BeginCarriedObjectPose(); _motion.Hold(player); Position = _motion.GroundPosition;
        return true;
    }
    public override void _Draw()
    {
        if (!Visible || IsDead) return;
        DrawTexture(_texture, new Vector2(-8,-8 + ZHigh) + TransitionDrawOffset);
    }
    public bool SwitchHookHeld { get; private set; }
    public Vector2 SwitchHookPosition => Position;
    public void BeginSwitchHook(Vector2 linkPosition) { SwitchHookHeld = true; _hookSubstate = 0; }
    public void CopySwitchHookPosition(Vector2 position, int zHigh)
    { Position = position.Floor() + Position - Position.Floor(); _hookZ = (zHigh << 8) | (_hookZ & 255); }
    public void SwapSwitchHook() => _hookSubstate = 2;
    public void ReleaseSwitchHook() { if (SwitchHookHeld) _hookSubstate = 3; }
}
