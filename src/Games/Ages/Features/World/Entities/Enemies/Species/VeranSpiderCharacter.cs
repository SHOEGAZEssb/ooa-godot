using Godot;

namespace Oracles;

// ENEMY_VERAN_SPIDER $0f. The Ambi battle owns allocation, not this species.
internal sealed partial class VeranSpiderCharacter : EnemyCharacter, ISwitchHookEnemy, ITerrainShadowSource
{
    private readonly RoomEntityManager _owner;
    private readonly OracleRoomData _room;
    private readonly EnemyTerrainMovement _movement;
    private readonly ScentSeedAttraction _scent = new();
    private bool _collision, _scentEnabled, _burnCollisionDisabled;
    private int _z, _speedZ, _stun;
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter { get; private set; }
    internal int Cooldown { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int ZFixed => _z;
    internal int SpeedZ => _speedZ;
    internal int ScentCounter => _scent.Counter;
    internal int StunCounter=>_stun;
    internal override void ApplyBoomerangStun(int updates)=>_stun=updates;
    internal bool NativeDeath {get;private set;}
    internal bool CutsceneDeath {get;private set;}
    internal bool CutsceneTriggered=>_owner.RuntimeState.ReadWramByte(0xcfd0)!=0;
    internal override bool InitializationPending => State == 0;
    internal override bool CollisionEnabled => base.CollisionEnabled && _collision && !_burnCollisionDisabled;
    int? ITerrainShadowSource.TerrainShadowZHigh => _z >> 8;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (_z >> 8);
    internal VeranSpiderCharacter(RoomEntityManager owner, PalacePossessionDatabase data, OracleRoomData room, Vector2 position)
    {
        _owner=owner; _room=room; _movement=new(this,room);
        InitializeEnemy(position,EnemyCharacterConfiguration.FromImported(data.Spider),paletteOverrides:data.SpiderPalettes,positionedOam:true);
        ConfigureSwordKnockback(room,EnemyKnockbackMotion.Terrain,nativeSpeed:()=>Speed);
        Visible=false;
    }
    internal void UpdateFrame(RoomEntityFrame frame)
    {
        // State8 changes draw height without enemyAnimate. Refresh Godot's
        // cached drawing on eligible updates even when the OAM frame stays put.
        QueueRedraw();
        // enemyStandardUpdate initializes var3d again when state0 retries a
        // solid random spawn. cfd0 overrides JUST_HIT/recoil/death dispatch.
        if (State==0) _scent.Initialize(_owner.Random.Next().Value);
        bool stunned=State!=0&&!NativeHitPending&&!HasActiveKnockback&&Health>0&&_stun!=0;
        if(stunned)Position=EnemyStunMotion.Update(Position,State,frame.Counter,ref _stun,ref _z,ref _speedZ);
        if (CheckCutsceneDeath()) return;
        if (BeginFrame()||stunned) return;
        if (Health==0) { NativeDeath=true; Finish(); return; }
        if (_scentEnabled && State>=8 && frame.ScentSeedTarget is not null) { State=4; Speed=0x32; }
        switch (State)
        {
            case 0:
                int packed;
                do { packed=_owner.Random.Next().Value & 0x7f; } while (packed>=0x7a);
                Vector2 camera=-_owner.ToScreen(Vector2.Zero);
                Position=new(((packed&15)*16+8+(int)camera.X)&255,((packed&0x70)+8+(int)camera.Y)&255);
                if (_room.GetTerrainInfo(Position).Collision!=0) return;
                int offset=(-(int)Position.Y-8)&255, height=((int)camera.Y&255)+offset;
                if (height>255) height=offset;
                if ((height&0x80)==0) height=0x80;
                _z=(height-256)<<8; State=8; Speed=0x0f; _collision=true;
                _owner.OnSoundRequested(SoundId.SndFallInHole); Visible=true; ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex;
                return;
            case 3:
                if (Substate==0) Substate=1;
                else if (Substate==3 && OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20)) { State=9; _collision=true; }
                return;
            case 4:
                if (frame.ScentSeedTarget is not { } scent) { Wander(); return; }
                Angle=(_scent.UpdateAngle(Position,scent,Angle,cardinal:false)&0x18)+4;
                Move(); AdvanceAnimation(3); return;
            case 8:
                if (!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x0e)) return;
                _speedZ=0; State=9; _scentEnabled=true; ZIndex=ObjectDrawPriority.BehindLinkZIndex;
                _owner.OnSoundRequested(SoundId.SndBombLand); RandomDirection(); AdvanceAnimation(); return;
            case 9:
                int target=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),(frame.EnemyTarget??frame.Player.Position).Floor());
                if ((((target&7)-3)&255)<3 && Cooldown==0)
                { Angle=(target&0x18)+4; State=10; Speed=0x32; Counter=120; return; }
                if (Cooldown!=0) Cooldown--;
                Counter=(Counter-1)&255;
                if (Counter==0 || !Move()) { RandomDirection(); return; }
                AdvanceAnimation(); return;
            case 10:
                Counter=(Counter-1)&255;
                if (Counter!=0 && Move()) { AdvanceAnimation(3); return; }
                Wander(); Cooldown=0x40; RandomDirection(); return;
            default: throw new System.NotSupportedException($"veranSpider.s: ENEMY $0f state ${State:x2}.");
        }
    }
    internal bool CheckCutsceneDeath()
    {
        if (!CutsceneTriggered) return false;
        AdvanceInvincibilityCounter(); CutsceneDeath=true; NativeDeath=true; Finish(); return true;
    }
    internal void BeginEmberHit(int damage)
    {
        Health=System.Math.Max(0,Health-damage);_burnCollisionDisabled=Health==0;
        _stun=90;InvincibilityCounter=-90;DeferNativeHitStatus();
    }
    internal void ReleaseBurn() {InvincibilityCounter=0;_stun=1;}
    internal void BeginPegasusHit() {_stun=240;InvincibilityCounter=-16;}
    internal void BeginGaleHit() {_stun=0;State=5;Speed=5;}
    internal int BurnZFixed {get=>_z;set=>_z=value;}
    private bool Move() => _movement.MoveAtAngle(Angle,Speed,allowHoles:false);
    private void Wander() { State=9; Speed=0x0f; }
    private void RandomDirection()
    {
        var random=_owner.Random.Next(); Angle=(random.High&0x18)+4; Counter=(random.Low&0x70)+0x70;
    }
    public bool SwitchHookHeld => !IsDead && State==3 && Substate<3;
    public Vector2 SwitchHookPosition => Position;
    public void BeginSwitchHook(Vector2 link) { State=3; Substate=0; _collision=false; KnockbackCounter=0; }
    public void CopySwitchHookPosition(Vector2 position,int zHigh) { Position=position.Floor()+new Vector2(Position.X%1,Position.Y%1); _z=(zHigh<<8)|(_z&255); }
    public void SwapSwitchHook() => Substate=2;
    public void ReleaseSwitchHook() { if (SwitchHookHeld) Substate=3; }
}
