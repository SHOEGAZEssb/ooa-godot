using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePumpkinProjectileBirthRom()
    {
        foreach(int free in Enumerable.Range(0,4))
        foreach(int angle in new[]{0,8,16,24})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x13);
            var parent=_entities.EntityAdapters<PumpkinHeadBossRoomEntity>().Single();var boss=(PumpkinHeadBoss)parent.Node;
            F1Retain(parent);F1Set(parent,"_initialized",true);F1Set(boss,"_initialized",true);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(120,144));_player.Face(Vector2I.Up);
            _runtimeState.SetWramByte(0xcc8a,9);_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);_entities.AddEntity(new ProjectileMotionContactSuppression());
            Vector2 body=new(120,88),head=body+(fractional?new Vector2(.75f,.25f):Vector2.Zero);
            boss.Position=body;F1Set(boss,"_precisePosition",body);F1Set(boss,"_headPosition",head);F1Set(boss,"_headZ",-16);
            F1Set(boss,"_bodyZFixed",0);F1Set(boss,"_state",BossState.Firing);F1Set(boss,"_counter",37);F1Set(boss,"_angle",angle);
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,120,144)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[0xcc8a]=9;native[WramAddress.wWarpsDisabled]=1;
            native[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x78*4];
            foreach(int slot in new[]{0,1,2})
            {
                int p=0xd080+slot*256;native[p]=1;native[p+1]=0x78;native[p+2]=(byte)(slot+1);
                native[p+4]=(byte)(slot==1?0x0b:0x0c);native[p+9]=(byte)angle;native[p+0x29]=8;native[p+0x1a]=0x83;
                native[p+11]=88;native[p+13]=120;native.Word(p+0x16,0xd080);
                native.SetEnemyAnimation(slot,slot==0?0x0b+(angle>>3):slot==1?0x0a:((angle>>2)&6)+1);
            }
            native[0xd086]=37;native[0xd286]=1;native.Word(0xd096,0xd180);native.Word(0xd098,0xd280);
            native.Word(0xd28a,(int)(head.Y*256));native.Word(0xd28c,(int)(head.X*256));native.Word(0xd28e,-0x1000+(fractional?0x80:0));
            int probes=0,born=0;Vector2 before=Vector2.Zero;int beforeAngle=0,beforeCounter=0;bool beforeInitialized=false;
            var sounds=_sound.AttachPlayRequestAudit();
            var probe=new PartMovementScratchProbe(()=>
            {
                probes++;var root=_entities.Entities<PumpkinHeadProjectile>().SingleOrDefault();
                born=root is null?0:1;
                if(root is not null){before=root.Position;beforeAngle=root.Angle;beforeCounter=root.Delay;beforeInitialized=SomariaPrivate<bool>(root,"_initialized");}
            });
            F1Parts.Add(probe,0);_entities.AddEntity(probe);
            native[0xd0c0]=1;native[0xd0c1]=0x7f;native[0xd0c4]=1;
            // Initialized inert reservations are held by mask09. Fresh PART
            // state0 still runs; its ordered children occupy later free slots.
            for(int slot=free+1;slot<16;slot++)
            {
                var filler=new ItemPhaseValidationEntity(()=>{});F1Parts.Add(filler,slot);_entities.AddEntity(filler);
                int p=0xd0c0+slot*256;native[p]=1;native[p+1]=0x7f;native[p+4]=1;
            }
            int expectedBorn=0,expectedAngle=0,expectedCounter=0;Vector2 expectedBefore=Vector2.Zero;
            StepGameplayUpdates(1,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeParts:()=>
                {
                    expectedBorn=native[0xd1c0]!=0&&native[0xd1c1]==0x42?1:0;
                    if(expectedBorn!=0){expectedBefore=new(native.Word(0xd1cc)/256f,native.Word(0xd1ca)/256f);expectedAngle=native[0xd1c9];expectedCounter=native[0xd1c6];}
                });
                FailIf(probes!=1||born!=expectedBorn||born!=0&&(before!=expectedBefore||beforeAngle!=expectedAngle||beforeCounter!=expectedCounter||beforeInitialized)||
                    SomariaPrivate<int>(boss,"_counter")!=native[0xd086],
                    $"Pumpkin ENEMY->PART birth/free{free}/angle${angle:x2}/fraction{fractional}: born{born}/{expectedBorn}, beforePART XY{before}/{expectedBefore}, C{beforeCounter}/{expectedCounter}, initialized{beforeInitialized}, bodyC{SomariaPrivate<int>(boss,"_counter")}/{native[0xd086]}.");
                FailIf(_entities.Entities<PumpkinHeadProjectile>().Count!=free,"The checked primary must keep its slot and admit only the available ordered siblings.");
                foreach(var shot in _entities.Entities<PumpkinHeadProjectile>())
                {
                    int slot=F1Parts.Single(p=>p.Key.Node==shot).Value,p=0xd0c0+slot*256;
                    FailIf(shot.Position!=new Vector2(native.Word(p+12)/256f,native.Word(p+10)/256f)||shot.Angle!=native[p+9]||shot.Delay!=native[p+6],
                        "Pumpkin state0 must fold high Y/Z, apply the root mouth/child angle offsets, and initialize later slots in this same PART pass.");
                }
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls||
                    !sounds.Requests.SequenceEqual(native.Sounds),"Pumpkin birth must preserve source failure behavior, RNG and ENEMY attack cue order.");
            });
            _entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
    }
}
