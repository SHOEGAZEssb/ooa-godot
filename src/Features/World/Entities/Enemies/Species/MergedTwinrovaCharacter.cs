using Godot;
using System;

namespace Oracles;

// common/enemies/mergedTwinrova.s, ENEMY $01. objectCopyPosition copies
// only XYZ high bytes into a new zeroed slot before the original is deleted.
internal sealed partial class MergedTwinrovaCharacter : EnemyCharacter
{
    internal TwinrovaEnvironment World { get; }
    internal int Slot { get; set; }
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Subid { get; private set; }
    internal int Counter { get; private set; }
    internal int SwapCounter { get; private set; }
    internal int SwapDisabled { get; private set; }
    internal int MovementWait { get; private set; }
    internal int MinimumMovement {get;private set;}
    internal int AttackIndex {get;private set;}
    internal int Attack {get;private set;}
    internal int Counter2 {get;private set;}
    internal int IcePattern {get;private set;}
    internal int TargetIndex {get;private set;}
    internal Vector2I Target {get;private set;}
    internal int BossHealth { get; private set; }
    internal int Vulnerability { get; private set; }
    private int _pendingHit=-1;
    internal bool JustHit=>_pendingHit>=0;
    internal int Speed { get; private set; }
    internal int Angle { get; private set; }
    internal int ZFixed { get; private set; }
    internal int Palette { get; private set; }
    internal int CollisionType { get; private set; } = 1;
    internal override bool InitializationPending => State == 0;
    internal override bool CollisionEnabled => !IsDead && (CollisionType & 128) != 0;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (ZFixed >> 8);
    internal override Texture2D CurrentDrawTexture => DrawsDamagePalette ? base.CurrentDrawTexture : Animation.CurrentTextureForPalette(Palette);
    internal MergedTwinrovaCharacter(TwinrovaEnvironment world, Vector2 position, int z)
    {
        World = world;
        InitializeEnemy(position.Floor(), EnemyCharacterConfiguration.FromImported(world.Data.Actor(1)), positionedOam: true, paletteVariants: [0,1,2]);
        ZFixed = (z >> 8) << 8;
        Health = 0; Animation.SetFrameCounter(0); Visible = false;
    }
    internal void PublishHit(int item,int damage=0,int invincibility=0)
    {
        _pendingHit=item;
        if(damage!=0) Health=Math.Max(0,Health-damage);
        if(Health==0) CollisionType&=127;
        if(invincibility!=0) InvincibilityCounter=invincibility;
    }
    internal void UpdateFrame(RoomEntityFrame frame)
    {
        if (IsDead) return;
        try
        {
            BeginFrame(advanceInvincibility: false, continueDuringHitAndKnockback: true);
            if (State == 0)
            {
                Health = World.Data.Actor(1).Health; Animation.SetAnimation(0);
                World.Entities.Random.Next();
            }
            else if(_pendingHit>=0)
            {
                if(Vulnerability!=0 && _pendingHit is >=0x1a and <=0x1e)
                {
                    World.Entities.OnSoundRequested(SoundId.SndBossDamage);InvincibilityCounter=45;
                    BossHealth=(BossHealth-1)&255;
                    if(BossHealth==0)
                    {
                        Health=0;CollisionType&=127;Substate=0;Counter=216;
                        World.Entities.OnSoundRequested(SoundId.SndCtrlStopMusic);return;
                    }
                }
            }
            else if(Health==0)
            {
                if(BossHealth==0){Death(frame);return;}
                Vulnerability=120;Health=20;CollisionType=255;Palette=0;RestartAnimation(9);return;
            }
            if(Vulnerability!=0)
            {
                Vulnerability--;
                if(Vulnerability!=0)return;
                State=9;Palette=(Subid^1)+1;RestartAnimation(Subid+1);
            }
            // This helper precedes even state zero. Its byte decrement wraps
            // on zero, and state zero then installs the initial 210.
            if ((frame.Counter & 3) == 0)
            {
                SwapCounter = (SwapCounter - 1) & 255;
                if(SwapCounter==0)
                {
                    SwapCounter=1;
                    if(SwapDisabled==0){SwapCounter=210;Counter=30;State=9;}
                }
            }
            switch (State)
            {
                case 0:
                    World.Entities.RuntimeState.SetWramByte(0xcc1d, 1);
                    if (!World.Entities.TryCreatePart(new BossShadowSpawn(() => Position, () => ZFixed >> 8, () => !IsDead, 0, 0x12))) return;
                    State = 8; Speed = 0x3c; Counter = 8; SwapCounter = 210; BossHealth = 5;
                    ZFixed = (ZFixed & 255) | -256;
                    Visible = true; ZIndex = ObjectDrawPriority.FromVisible(0x83);
                    World.Entities.OnRoomEntityDialogueRequested(0x2f0b, World.Data.Text(0x2f0b), Position); return;
                case 8:
                    Counter = (Counter - 1) & 255; if (Counter != 0) return;
                    State = 9; CollisionType |= 128;
                    Subid = World.Entities.Random.Next().Value & 1;
                    Palette = (Subid ^ 1) + 1; RestartAnimation(Subid + 1);
                    World.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
                    World.Entities.RuntimeState.SetWramByte(0xcbca, 0);
                    World.Entities.SetNativeActiveMusic(SoundId.MusTwinrova);
                    World.Entities.OnSoundRequested(SoundId.MusTwinrova); return;
                case 9:
                    if ((((int)Position.X - 0x78 + 2) & 255) < 5 && (((int)Position.Y - 0x48 + 2) & 255) < 5)
                    { State = 0x0a; Substate=0; Counter = 60; CollisionType = 0x96; SwapDisabled=1; MovementWait=0; return; }
                    Angle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), new(0x78,0x48));
                    Position = ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position), Speed, Angle).PrecisePosition;
                    return;
                case 0x0a: SwapRoom(); return;
                case 0x0b:
                    if(Dec()==0) State=0x0c;
                    return;
                case >=0x0c and <=0x10: AttackFrame(frame);return;
                default:
                    throw new NotSupportedException($"mergedTwinrova.s: ENEMY $01:${Subid:x2} state${State:x2} arena/attack continuation is not implemented.");
            }
        }
        finally { _pendingHit=-1; AdvanceInvincibilityCounter(); QueueRedraw(); }
    }
    private int Dec()=>Counter=(Counter-1)&255;
    private void SwapRoom()
    {
        switch(Substate)
        {
            case 0:
                if(Dec()!=0){Palette^=3;return;}
                Substate=1;Counter=30;Subid=(Subid+1)&1;
                Palette=(Subid^1)+1;RestartAnimation(Subid+1);return;
            case 1:
                if(Dec()!=0){Palette^=3;return;}
                Substate=2;Palette=(Subid^1)+1;
                World.Entities.RuntimeState.SetWramByte(0xcbca,1);
                World.PaletteFade(true,1,3);
                World.Entities.OnSoundRequested(SoundId.SndEndless);return;
            case 2:
                Substate=3;World.ReloadArena(Subid+1);return;
            case 3:
                Substate=4;World.Fade(false,4);return;
            case 4:
                if(World.Entities.PaletteFadeActiveSource())return;
                State=0x0b;CollisionType=0x81;Counter=30;SwapCounter=210;SwapDisabled=0;
                World.Entities.RuntimeState.SetWramByte(0xcbca,0);
                World.Entities.OnSoundRequested(SoundId.SndCtrlStopSfx);return;
            default:throw new NotSupportedException($"mergedTwinrova_stateA: substate${Substate:x2}.");
        }
    }
}
