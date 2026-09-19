using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// ENEMY $7f. The two $56 slots and both bomb parts keep their own dispatches.
internal sealed partial class KingMoblinBoss : EnemyCharacter
{
    internal KingMoblinEnvironment World { get; private set; } = null!;
    internal KingMoblinDatabase Data => World.Data;
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Speed { get; private set; } = 0x1e;
    internal bool ControlsDisabled => State is 9 or 10 || State >= 0x12;
    internal KingMoblinMinion?[] Minions { get; } = new KingMoblinMinion?[2];
    internal KingMoblinBomb? Bomb { get; set; }
    internal bool EscapeSignal { get; set; }
    private int _angle, _targetX, _z, _speedZ;
    private bool _hit;
    internal override bool CollisionEnabled => State < 0x12 && base.CollisionEnabled;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + new Vector2(0,_z >> 8);
    internal void Initialize(KingMoblinEnvironment world, Vector2 position)
    {
        World=world;
        InitializeEnemy(position,EnemyCharacterConfiguration.FromImported(Data.Actor(0x7f)),
            paletteOverrides:Data.Palettes,positionedOam:true);
        Name="KingMoblin"; Visible=false;
    }
    internal void BombHit()
    {
        if(!CollisionEnabled || InvincibilityCounter!=0) return;
        Health--; InvincibilityCounter=30; _hit=true;
    }
    internal void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if(IsDead) return;
        if(State==0)
        {
            World.Random.Next(); // enemyStandardUpdate retries state zero, including its RNG draw.
            World.Sound(OracleSoundEngine.SndCtrlStopMusic); World.EnableLink();
            if(!World.EnemySlots(2)) return;
            spawns.Add(new KingMoblinMinionSpawn(this,0)); spawns.Add(new KingMoblinMinionSpawn(this,1));
            State=8; Speed=0x1e; Visible=true; RestartAnimation(2); return;
        }
        if(_hit)
        {
            _hit=false; World.Sound(OracleSoundEngine.SndBossDamage);
            // enemyStandardUpdate gives JUST_HIT priority over NO_HEALTH.
            if(Health>0) Speed=Data.Bytes("speeds")[Health-1];
        }
        else if(Health==0 && frame.Player.PatchCollisionsEnabled)
        {
            State=0x12; Health=1; _angle=0; Speed=0x78; InvincibilityCounter=0; RestartAnimation(6);
        }
        if(InvincibilityCounter!=0) { AdvanceInvincibilityCounter(); return; }
        switch(State)
        {
            case 8:
                if((byte)((int)frame.Player.Position.X-0x40)<0x20 && frame.Player.TopDownAirZ==0)
                {
                    frame.Player.Face(Vector2I.Up);
                    if(!frame.Player.BombFairyVulnerable) return;
                    frame.Player.ClearNativeItemParents();
                    Tile(new(0x18,0x68),0xa0);
                    if(World.InteractionSlot()) spawns.Add(new PuzzlePuffSpawn(new(0x18,0x68),OracleSoundEngine.SndPoof));
                    State=9; Counter=0x18;
                }
                AdvanceAnimation(); break;
            case 9:
                if(--Counter==0)
                {
                    State=10; int text=World.Save?.IsLinkedGame==true?0x2f1a:0x2f19;
                    World.Dialogue(text,Data.Text(text),Position);
                }
                else AdvanceAnimation();
                break;
            case 10:
                State=11; Counter=30;
                foreach(var minion in Minions) minion!.StartFight();
                World.EnableLink(); World.Sound(OracleSoundEngine.MusBoss); RestartAnimation(0); break;
            case 11:
                if(Counter>0) Counter--;
                if(Counter!=0) { AdvanceAnimation(); break; }
                if(!World.PartSlot()) break;
                spawns.Add(new KingMoblinBombSpawn(this,null)); State=12; Pickup(); break;
            case 12:
                if(CheckCentre() || --Counter!=0) break;
                State=13; Counter=Data.Bytes("raise")[Health-1];
                if(Bomb!.State!=5) Bomb.Position=Position+new Vector2(-14,-16);
                RestartAnimation(2); break;
            case 13:
                if(CheckCentre() || --Counter!=0) break;
                State=14; Counter=30;
                int angle=OracleObjectMovement.Shared.RelativeAngle(Position,frame.Player.Position);
                int relative=(byte)(angle-12);
                if(relative>=7) angle=(relative&128)!=0?12:19;
                if(Bomb!.State!=5) Bomb.Throw(angle,-0x180);
                RestartAnimation(5); break;
            case 14:
                if(--Counter==0) {State=15; RestartAnimation(2);} break;
            case 15:
                if(CheckCentre()) break;
                if(Bomb!.State==4 && (int)Bomb.Position.Y<0x36 && (byte)((int)Bomb.Position.X-0x30)<0x41)
                {
                    if(NearBomb()) { Grab(); break; }
                    _targetX=(int)Bomb.Position.X; Walk(_targetX<Position.X,17); break;
                }
                AdvanceAnimation(); break;
            case 16:
                if(Centred()) {State=11; Counter=30; RestartAnimation(0);}
                else {Move(); AdvanceAnimation();} break;
            case 17:
                if(CheckCentre()) break;
                Move();
                if((byte)(_targetX-(int)Position.X+8)<17) Grab(); else AdvanceAnimation(); break;
            case 18:
                Move();
                if((int)Position.Y<12) {State=19; _angle=16; Speed=0x14; _speedZ=-0x160; World.Shake(60);}
                break;
            case 19:
                if(Bounce()) {State=20; Counter=150; Position=new(Position.X,0x20);} else Move(); break;
            case 20:
                if(EscapeSignal) {State=21; Counter=98;}
                else if((frame.Counter&1)==0 && --Counter==0) {State=11; Counter=30; RestartAnimation(0);}
                break;
            case 21:
                if(--Counter==0)
                {
                    World.Save?.SetRoomFlag(0,0x09,1);
                    World.Save?.SetGlobalFlag(Data.Bytes("GLOBALFLAG_MOBLINS_KEEP_DESTROYED")[0]);
                    World.Save?.SetGlobalFlag(Data.Bytes("GLOBALFLAG_16")[0]);
                    World.Warp(new Warp(2,World.Room.Id,-1,0,0,0,0x09,0x45,0,3));
                    Finish();
                }
                else if(((Counter-1)&31)==0 && World.InteractionSlot())
                {
                    var position=new Vector2(Data.Bytes("explosions")[(Counter&0x60)>>5],8);
                    spawns.Add(new KingMoblinExplosionSpawn(position)); Tile(position,0xa1);
                }
                break;
            default: throw new InvalidOperationException($"ENEMY_KING_MOBLIN $7f: invalid state ${State:x2}.");
        }
        QueueRedraw();
    }
    private bool Bounce()
    {
        if(!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20)) return false;
        int rebound=(-_speedZ)>>1;
        if(rebound> -0x80) { _z=0; return true; }
        _speedZ=rebound; return false;
    }
    private void Tile(Vector2 position,byte tile) => World.Room.SetPositionTileAndCollision(position,tile,null,World.Tick());
    private void Pickup() {Counter=Data.Bytes("pickup")[Health-1]; RestartAnimation(4);}
    private void Grab() {if(Bomb!.State!=5) Bomb.GrabByBoss(Position+new Vector2(0,8)); State=12; Pickup();}
    private bool NearBomb() => (byte)((int)Position.X-(int)Bomb!.Position.X+8)<17;
    private bool Centred() => (byte)((int)Position.X-0x4e)<5;
    private bool CheckCentre()
    {
        if(Bomb is {IsDead:false}) return false;
        // cp $b0 sets carry for the right side of centre; the source picks
        // ANGLE_LEFT on carry and ANGLE_RIGHT for the wrapped left side.
        if(Centred()) {State=11; Counter=30; RestartAnimation(0);} else Walk((byte)((int)Position.X-0x4e)<0xb0,16);
        return true;
    }
    private void Walk(bool left,int state) {_angle=left?24:8; State=state; RestartAnimation(left?3:1);}
    private void Move() {var p=Position; OracleObjectMovement.Shared.ApplySpeed(ref p,Speed,_angle); Position=p;}
}

internal sealed record KingMoblinEnvironment(KingMoblinDatabase Data, OracleRoomData Room, OracleRandom Random,
    OracleSaveData? Save, Func<int,bool> EnemySlots, Func<bool> PartSlot, Func<bool> InteractionSlot,
    Action<int> Sound, Action<int> Shake, Func<bool> Shaking, Action EnableLink,
    Action<int,string,Vector2> Dialogue, Action<Warp> Warp, Func<long> Tick,
    BraceletDatabaseRecord Bracelet, BombRecord Throwing);
internal sealed record KingMoblinMinionSpawn(KingMoblinBoss Boss,int SubId) : RoomEntitySpawn;
internal sealed record KingMoblinBombSpawn(KingMoblinBoss Boss,KingMoblinMinion? Minion) : RoomEntitySpawn;
internal sealed record KingMoblinExplosionSpawn(Vector2 Position) : RoomEntitySpawn;
internal sealed record KingMoblinExclamationSpawn(Vector2 Position) : RoomEntitySpawn;
