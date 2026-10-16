using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class PossessedNayruCharacter : EnemyCharacter, ISwitchHookEnemy
{
    private RoomEntityManager _owner=null!;
    private PalacePossessionDatabase _data=null!;
    private PossessedNayruCharacter? _parent;
    private int _lastCollision, _z, _speedZ, _pillar=0xff, _palette=6, _puff=-1;
    private bool _justHit, _collision, _taunted, _spiders;
    private Vector2I _target;
    internal int SubId { get; private set; }
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter { get; private set; }
    internal int Counter2 { get; private set; }
    internal int RemainingHits { get; private set; }=3;
    internal int Speed { get; private set; }
    internal int Angle { get; private set; }
    internal int CollisionMode { get; private set; }=0x43;
    internal int ZFixed => _z;
    internal Vector2I Target => _target;
    internal bool JustHit => _justHit;
    internal PalacePossessionDatabase Data => _data;
    internal override bool InitializationPending => State==0;
    internal override bool CollisionEnabled => !IsDead && _collision;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset+Vector2.Down*(_z>>8);
    internal override Texture2D CurrentDrawTexture => DrawsDamagePalette ? base.CurrentDrawTexture : Animation.CurrentTextureForPalette(_palette);
    internal void Initialize(RoomEntityManager owner,PalacePossessionDatabase data,int subid,Vector2 position,PossessedNayruCharacter? parent)
    {
        _owner=owner; _data=data; SubId=subid; _parent=parent;
        if (parent is not null) RemainingHits=parent.RemainingHits;
        InitializeEnemy(position,EnemyCharacterConfiguration.FromImported(data.Enemy(subid)),paletteOverrides:data.Palettes,
            positionedOam:true,paletteVariants:[1,6]); Visible=false;
    }
    internal void MarkContact(int item,Vector2 origin,int invincibility=0,bool knockback=false,int damage=0)
    {
        _justHit=true; _lastCollision=item;
        if (invincibility!=0) InvincibilityCounter=invincibility;
        if (knockback) KnockbackAngle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),origin.Floor())^0x10;
        Health=System.Math.Max(0,Health-damage);
    }
    internal void RequestSound(int sound) => _owner.OnSoundRequested(sound);
    internal void InitializeStateZero()
    {
        if (State!=0 || IsDead) return;
        // enemyStandardUpdate consumes one RNG value before enemyCode61's
        // state-zero handler. updateEnemies admits this during scrolling;
        // state$08's shadow, movement and music wait for normal dispatch.
        _owner.Random.Next(); State=8; Speed=0x50; SetAnimation(SubId*3); Visible=true;
        if (SubId>=2) { Counter=12; Speed=0x14; _palette=1; }
    }
    internal void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if (State==0)
        {
            InitializeStateZero();
            return;
        }
        if (_justHit) WasHit(frame.Player);
        if (State==3) UpdateHook();
        else if (SubId<=1) UpdateNayru(frame,spawns);
        else if (SubId==2) UpdateVeran(frame,spawns);
        else UpdateCollapsedAmbi();
        _justHit=false; AdvanceInvincibilityCounter(); QueueRedraw();
    }
    private void WasHit(Player player)
    {
        KnockbackCounter=0;
        if (SubId<=1)
        {
            if (_lastCollision==0x18) { State=0x0e; Counter2=30; _collision=false; SetAnimation(SubId*3+2); }
            else if (_lastCollision is >=4 and <=0x12)
            { InvincibilityCounter=-24; player.ApplyPossessionRetaliation(KnockbackAngle^0x10); }
        }
        else if (_lastCollision is >=4 and <=0x0b)
        {
            if (CollisionMode==0x5b) InvincibilityCounter=-8;
            else
            {
                Counter=8; RemainingHits=(RemainingHits-1)&255;
                if (RemainingHits==0)
                { Health=0x80; _collision=false; State=0x0e; _owner.RuntimeState.SetWramByte(0xcfd0,1); RequestSound(SoundId.SndCtrlStopMusic); }
            }
        }
    }
    private void UpdateNayru(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        switch (State)
        {
            case 8:
                if (!_owner.PartSlotAvailable) return;
                spawns.Add(new BossShadowSpawn(()=>Position,()=>_z>>8,()=>GodotObject.IsInstanceValid(this)&&!IsDead,0,6));
                BeginMoving(); RemainingHits=3; _pillar=0xff; _owner.RuntimeState.SetWramByte(0xcfd0,0);
                _owner.SetNativeActiveMusic(SoundId.MusBoss); RequestSound(SoundId.MusBoss); break;
            case 9:
                if (--Counter!=0) { Visible=!Visible; break; }
                State=10; _z=(_z&255)|-0x200; Visible=false; ChoosePillar(frame.Player.Position); RequestSound(SoundId.SndCircling); break;
            case 10:
                if (!MoveTowardTarget()) break;
                State=11; _z&=255; Counter=30; break;
            case 11:
                if (--Counter!=0) { Visible=!Visible; break; }
                int random=_owner.Random.Next().Value&15;
                if (random>=_data.Table("attack-probabilities")[SubId*5+RemainingHits-1]) { BeginMoving(); Counter=30; Visible=!Visible; }
                else { State=12; Counter=30; _collision=true; SetAnimation(SubId*3+1); Visible=true; }
                break;
            case 12:
                if (--Counter==0)
                {
                    Counter=142; State=13;
                    _spiders=SubId==1 && (_owner.Random.Next().Value&15)>=_data.Table("spider-probabilities")[RemainingHits-1];
                    if (!_spiders) _owner.TryCreateVeranProjectile(this,0,Position,_z,0);
                }
                break;
            case 13:
                if (--Counter==0) { BeginMoving(); _collision=false; SetAnimation(SubId*3); }
                else if (_spiders && (Counter&31)==0 && _owner.RoomEnemyCount<6) _owner.TryCreateVeranSpider(Position);
                break;
            case 14:
                Counter2=(Counter2-1)&255;
                if (Counter2!=0) break;
                if (_owner.TryCreatePalacePossessionEnemy(Position.Floor()+new Vector2(4,-4),2,this) is null) break;
                State=15; _collision=false; _palette=1; Visible=true; break;
            case 15: break;
            case 16: State=17; _palette=6; Counter=15; Visible=true; break;
            case 17: if (--Counter==0) { SetAnimation(SubId*3); BeginMoving(); } break;
            case 18:
                if (_owner.IsScriptTextActive()) break;
                State=19; _owner.BeginPalacePossessionFade(true,2); break;
            case 19:
                if (_owner.PalacePossessionPaletteActive()) break;
                State=20; frame.Player.ClearNativeItemParents(); frame.Player.PutOnGroundForScript(); break;
            case 20:
                if (SubId==0) _owner.CompletePalacePossessionFight(frame.Player);
                else if (!_owner.PalacePossessionPaletteActive()) _owner.CompleteBlackTowerPossessionFight(frame.Player);
                break;
            default: throw new NotSupportedException($"veranPossessionBoss.s: Nayru state${State:x2}.");
        }
    }
    private void ChoosePillar(Vector2 link)
    {
        int index;
        do { index=_owner.Random.Next().Value&14; } while (index>=11 || index==_pillar);
        _pillar=index; var pillars=_data.Table("pillars"); var offsets=_data.Table("pillar-offsets");
        var pillar=new Vector2(pillars[index+1],pillars[index]);
        // BC is the pillar; the temporary origin bytes are Link's target.
        int side=((OracleObjectMovement.Shared.RelativeAngle(link.Floor(),pillar)+4)&24)>>2;
        _target=new((pillars[index+1]+offsets[side+1])&255,(pillars[index]+offsets[side])&255);
    }
    private bool MoveTowardTarget()
    {
        int x=OracleObjectPosition.HighByte(Position.X), y=OracleObjectPosition.HighByte(Position.Y);
        if (((x-_target.X+2)&255)<5 && ((y-_target.Y+2)&255)<5)
        { Position=new(_target.X+Position.X%1,_target.Y+Position.Y%1); return true; }
        Angle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),_target); Move(); return false;
    }
    private void BeginMoving() { State=9; Counter=60; }
    private void Move() => Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),Speed,Angle).PrecisePosition;
    private void ReturnToHost()
    { _parent!.State++; _parent.RemainingHits=RemainingHits; Finish(); }
    private void UpdateVeran(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        switch (State)
        {
            case 8:
                Move();
                if (--Counter==0)
                {
                    Counter=120; State=9; _collision=true; CollisionMode=0x5b;
                    if (_parent!.SubId==0 && !_parent._taunted) { _parent._taunted=true; Show(0x2f2a); }
                }
                break;
            case 9: if (--Counter==0) { Counter=12; State=10; Angle=0x10; _collision=false; } break;
            case 10: Move(); if (--Counter==0) { ReturnToHost(); return; } break;
            case 11:
                bool terminal=--Counter==0;
                if (terminal) { Counter=120; CollisionMode=0x5c; State=12; _collision=true; }
                SetAnimation(terminal || (Counter&1)!=0?9:6); return;
            case 12:
                if (--Counter==0)
                { State=13; _collision=false; Speed=0x64; _speedZ=-0x280; _target=(Vector2I)_parent!.Position.Floor(); }
                break;
            case 13:
                OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20);
                if (MoveTowardTarget() && (_z>>8)==0) ReturnToHost(); return;
            case 14:
                if (InvincibilityCounter!=0 || !frame.Player.NativeInteractionCollisionsEnabled) return;
                _puff=_owner.TryCreateTransformationPuff(Position);
                if (_puff<0) return;
                State=15; _owner.RuntimeState.SetWramByte(WramAddress.wDisabledObjects,1);
                _owner.RuntimeState.SetWramByte(WramAddress.wMenuDisabled,1); Visible=false; return;
            case 15: if ((_owner.InteractionAnimationParameter(_puff)&0x80)!=0) State=16; return;
            case 16: _parent!.State=18; Show(_parent.SubId==0?0x560b:0x5611); Finish(); return;
            default: throw new NotSupportedException($"veranPossessionBoss.s: Veran state${State:x2}.");
        }
        AdvanceAnimation();
    }
    private void UpdateCollapsedAmbi()
    {
        if (_owner.PalacePossessionPaletteActive()) return;
        if (State==8) { State=9; Counter2=60; SetAnimation(5); _owner.BeginPalacePossessionFade(false,1); }
        else if (State==9)
        {
            Counter2=(Counter2-1)&255;
            if (Counter2==0 && _owner.TryResumeBlackTowerAmbi(Position)) Finish();
        }
        else throw new NotSupportedException($"veranPossessionBoss.s: collapsed Ambi state ${State:x2}.");
    }
    private void Show(int id) => _owner.OnRoomEntityDialogueRequested(id,_data.Text(id),Position);
    public bool SwitchHookHeld => GodotObject.IsInstanceValid(this)&&!IsDead&&State==3&&Substate<3;
    public Vector2 SwitchHookPosition => Position;
    public void BeginSwitchHook(Vector2 link) { State=3; Substate=0; _collision=false; }
    public void CopySwitchHookPosition(Vector2 position,int zHigh)
    { Position=position.Floor()+new Vector2(Position.X%1,Position.Y%1); _z=(zHigh<<8)|(_z&255); QueueRedraw(); }
    public void SwapSwitchHook() => Substate=2;
    public void ReleaseSwitchHook() { if (SwitchHookHeld) Substate=3; }
    private void UpdateHook()
    {
        if (Substate==0) Substate=1;
        else if (Substate<3) AdvanceAnimation();
        else if (OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20)) { State=11; Counter=40; }
    }
}
