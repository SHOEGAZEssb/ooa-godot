using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePumpkinProjectileMotionRom()
    {
        int lowerBirths=0,deletions=0,moves=0;
        foreach(int form in new[]{0,1})
        foreach(int variant in form==0?Enumerable.Range(0,96):Enumerable.Range(0,24))
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x13);_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(120,144));_player.Face(Vector2I.Up);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            _entities.AddEntity(new ProjectileMotionContactSuppression());
            int angle=form==0?(variant/4%4)*8:(variant/3%4)*8;
            int slot=form==0&&variant/48!=0?5:0,capacity=form==0?variant/16%3:0,gate=form==0?variant%4:(variant/3+variant%3)%4;
            int sub=form==0?0:variant/12+1,entry=form==0?0:variant%3,z=form==0?-16:0;
            Vector2 initial=new(120,88);
            if(entry==1)initial=angle switch{0=>new(120,18),8=>new(_currentRoom.Width-18,88),16=>new(120,_currentRoom.Height-18),_=>new(18,88)};
            if(entry==2)_currentRoom.SetPositionTileAndCollision(initial,_currentRoom.GetMetatile(initial),0xff,0);
            var root=_entities.Spawn<PumpkinHeadProjectile>(new PumpkinHeadProjectileSpawn(initial,angle,sub,z));
            var rootAdapter=_entities.EntityAdapters<PumpkinHeadProjectileRoomEntity>().Single();F1Parts[rootAdapter]=slot;
            if(fractional)root.Position+=new Vector2(.25f,.5f); // Declared retained state0 words after fresh allocation.
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,120,144)
                {HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[WramAddress.wWarpsDisabled]=1;
            native[0xcc08]=0xa6; // Original partData[$42] graphics header.
            int rootAt=0xd0c0+slot*256;native[rootAt]=1;native[rootAt+1]=0x42;native[rootAt+2]=(byte)sub;native[rootAt+9]=(byte)angle;
            native.Word(rootAt+10,(int)(root.Position.Y*256));native.Word(rootAt+12,(int)(root.Position.X*256));native[rootAt+15]=unchecked((byte)z);
            var available=(slot==0?new[]{1,2}:new[]{0,1}).Take(capacity).ToHashSet();
            foreach(int occupied in Enumerable.Range(0,16).Where(p=>p!=slot&&!available.Contains(p)))
            {
                var filler=new ItemPhaseValidationEntity(()=>{});F1Parts.Add(filler,occupied);_entities.AddEntity(filler);
                int p=0xd0c0+occupied*256;native[p]=1;native[p+1]=0x0a;native[p+4]=1;native[p+0x29]=1;
                // Ages partCodeTable[$0a] is the actual partCodeNil RET.
            }
            var seen=new List<(PumpkinHeadProjectile Actor,int Slot)>{(root,slot)};
            byte[] observed=new byte[4];int probes=0,updates=0;
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {
                probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);
                foreach(var shot in _entities.Entities<PumpkinHeadProjectile>())
                    if(seen.All(row=>!ReferenceEquals(row.Actor,shot)))seen.Add((shot,F1Parts.Single(p=>p.Key.Node==shot).Value));
            }));
            var sounds=_sound.AttachPlayRequestAudit();
            void Seed(){for(int i=0;i<4;i++){_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));native[0xcec0+i]=(byte)(0xa5+i);}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                var before=seen.Select(row=>(row.Slot,State:native[0xd0c4+row.Slot*256],X:native.Word(0xd0cc+row.Slot*256),Y:native.Word(0xd0ca+row.Slot*256))).ToArray();
                byte[] expected=new byte[4];native.UpdateGameplay(0,0,255,_entities.FrameCounter,
                    beforeInteractions:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                foreach(var (shot,poolSlot) in seen)
                {
                    int p=0xd0c0+poolSlot*256;bool alive=native[p]!=0&&native[p+1]==0x42;
                    if(shot.Finished){if(!alive)deletions++;else FailIf(true,"Pumpkin projectile retired before the original PART.");continue;}
                    var animation=SomariaPrivate<EnemyAnimationPlayer>(shot,"_animation");
                    FailIf(!alive||shot.State!=native[p+4]||shot.SubId!=native[p+2]||shot.Delay!=native[p+6]||shot.Angle!=native[p+9]||
                        shot.Position!=new Vector2(native.Word(p+12)/256f,native.Word(p+10)/256f)||shot.ZHigh!=unchecked((sbyte)native[p+15])||
                        shot.Visible!=((native[p+0x1a]&128)!=0)||shot.Visible&&shot.ZIndex!=ObjectDrawPriority.FromVisible(native[p+0x1a])||
                        !shot.InitializationPending&&(animation.CurrentParameter!=native[p+0x21]||SomariaPrivate<int>(animation,"_frameCounter")!=native[p+0x20]),
                        $"Pumpkin PART$42/form{form}/variant{variant}/fraction{fractional}/slot${poolSlot:x2}/update{updates}: state{shot.State}/{native[p+4]}, C{shot.Delay}/{native[p+6]}, angle{shot.Angle}/{native[p+9]}, XYZ{shot.Position}/{shot.ZHigh}/{native.Word(p+12)/256f},{native.Word(p+10)/256f}/{unchecked((sbyte)native[p+15])}, visible{shot.Visible}/${native[p+0x1a]:x2}, clock{SomariaPrivate<int>(animation,"_frameCounter")}/{native[p+0x20]}.");
                    var prior=before.FirstOrDefault(row=>row.Slot==poolSlot);
                    if(prior.State is 1 or 2&&(prior.X!=native.Word(p+12)||prior.Y!=native.Word(p+10)))moves++;
                }
                int active=Enumerable.Range(0,16).Count(p=>native[0xd0c0+p*256]!=0&&native[0xd0c1+p*256]==0x42);
                FailIf(active!=_entities.Entities<PumpkinHeadProjectile>().Count||probes!=1||!observed.SequenceEqual(expected),
                    $"Pumpkin PART pool/CEC form{form}/variant{variant}/update{updates}: active{_entities.Entities<PumpkinHeadProjectile>().Count}/{active}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}].");
                if(form==0&&slot==5&&updates==1&&capacity!=0)
                {lowerBirths++;FailIf(seen.Where(row=>row.Slot<5).Any(row=>row.Actor.State!=0),"Earlier-slot Pumpkin siblings must wait for the next physical PART pass.");}
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls)||
                    !sounds.Requests.SequenceEqual(native.Sounds),"Pumpkin flight/allocation must preserve native RNG and cues.");probes=0;Seed();
            });
            int mask=gate==2?9:1;_runtimeState.SetWramByte(0xcc8a,(byte)mask);native[0xcc8a]=(byte)mask;
            if(gate==1){_dialogue.ShowMessage("Pumpkin PART state0 hold.",104);native[0xcba0]=1;}
            if(gate==3){_runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;}
            Seed();Step(2);
            if(gate!=0)Step(2);
            _dialogue.Close();native[0xcba0]=0;_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;Step(3);
            _dialogue.ShowMessage("Pumpkin PART flight hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,9);native[0xcc8a]=9;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(_entities.Entities<PumpkinHeadProjectile>().Count!=0&&updates<220)Step(1);
            FailIf(_entities.Entities<PumpkinHeadProjectile>().Count!=0,"Every Pumpkin fan must retire against original room terrain/boundaries.");
            Step(2);_entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(lowerBirths==0||moves==0||deletions==0,"Pumpkin projectile matrix must execute earlier-slot births, state1 motion and retirement.");
    }
}
