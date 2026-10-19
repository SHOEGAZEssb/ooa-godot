using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateShadowHagShadowPublicationRom()
    {
        var nudgeRom=new LinkCollisionRom();
        for(int current=0;current<32;current++)
        for(int target=0;target<32;target++)
        {
            nudgeRom[0xd109]=(byte)current;
            nudgeRom.Call(0x1fd4,bank:0,objectPage:0xd1,accumulator:target);
            FailIf(OracleObjectMath.NudgeAngleTowards(current,target)!=nudgeRom[0xd109],
                $"objectNudgeAngleTowards current${current:x2}/target${target:x2} differs from original ROM.");
        }
        foreach(int index in Enumerable.Range(0,4))
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);LoadValidationRoom(4,0x4a);
            var boss=_entities.Entities<ShadowHagBoss>().Single();
            foreach(var entity in _entities.EntityAdapters<IRoomEntity>().Where(e=>!ReferenceEquals(e.Node,boss)).ToArray())_entities.RetireEntity(entity);
            // Keep the parent present but freeze only ENEMY updates. PARTs
            // still consume the current global Link target in this phase.
            F1Set(boss,"_initialized",true);F1Set(boss,"_introActive",false);F1Set(boss,"_state",ShadowHagState.ShadowsChasing);
            F1Set(boss,"_counter1",150);F1Set(boss,"_counter2",4);boss.Position=new(120,88);
            _runtimeState.SetWramByte(0xcc8a,4);_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            var actor=_entities.Spawn<ShadowHagShadowEffect>(new ShadowHagShadowSpawn(boss,index));
            var adapter=_entities.EntityAdapters<ShadowHagShadowRoomEntity>().Single();_player.BeginCutsceneControl(owner:actor);
            int initialAngle=4+index*8;
            Vector2 target=new(120+(index is 0 or 1?-32:32),88+(index is 0 or 3?32:-32));
            var angleRom=new ObjectMotionRom();angleRom.Word(ObjectMotionRom.Object+10,88*256);angleRom.Word(ObjectMotionRom.Object+12,120*256);
            bool targetFound=false;
            foreach(var candidate in (from dy in Enumerable.Range(-40,81) from dx in Enumerable.Range(-40,81)
                where Math.Abs(dx)+Math.Abs(dy)>=32
                let point=new Vector2(120+dx,88+dy) where !_collision.Collides(point)
                orderby point.DistanceSquaredTo(target) select point))
            {
                angleRom.Call(ObjectMotionRom.RelativeAngle,targetY:(int)candidate.Y,targetX:(int)candidate.X);
                if(angleRom.Result!=(initialAngle^0x10))continue;
                target=candidate;targetFound=true;break;
            }
            FailIf(!targetFound,"PART$41 opposite steering fixture requires a source-derived reachable Link target.");
            _player.WarpTo(target);F1Set(boss,"_linkPosition",target);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,(int)target.X,(int)target.Y);
            const int parent=0xd080;native[parent]=1;native[parent+1]=0x7a;native[parent+6]=150;native[parent+7]=4;
            native[parent+11]=88;native[parent+13]=120;
            int at=0xd0c0+F1Parts[adapter]*256;native[at]=1;native[at+1]=0x41;native[at+9]=(byte)index;
            native[at+11]=88;native[at+13]=120;native.Word(at+0x16,parent);
            byte[] observed=new byte[4];int probes=0,updates=0;
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,15);_entities.AddEntity(before);
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);}));
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                native[0xffb0]=(byte)_player.Position.Y;native[0xffb1]=(byte)_player.Position.X;
                native.AdvanceParts(_entities.FrameCounter);updates++;
                FailIf(actor.Finished!=(native[at]==0)||!actor.Finished&&(
                    actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||actor.Angle!=native[at+9]||
                    (int)actor.State+1!=native[at+4]||SomariaPrivate<int>(actor,"_counter1")!=native[at+6])||
                    boss.Counter2!=native[parent+7]||boss.Visible!=((native[parent+0x1a]&128)!=0)||
                    probes!=1||Enumerable.Range(0,4).Any(i=>observed[i]!=native[0xcec0+i]),
                    $"PART$41 index${index:x2}/fraction{fractional}/update{updates}: original nudge/target/motion/return lifetime or scratch differs: angle${actor.Angle:x2}/${native[at+9]:x2}.");
                probes=0;
            });
            Step(1);
            if(fractional){actor.Position=new(120.25f,88.5f);native[at+10]=0x80;native[at+12]=0x40;}
            F1Set(actor,"_counter1",1);native[at+6]=1;
            Step(1);FailIf(actor.Angle!=((initialAngle+1)&31),"Exactly opposite PART$41 target must nudge clockwise.");
            // Change Link while the source parent remains frozen. The target
            // publication precedes ENEMY/PART dispatch and is not parent-owned.
            _player.WarpTo(new(120,40));Step(20);
            _dialogue.ShowMessage("Shadow PART hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,0x0c);native[0xcc8a]=0x0c;Step(3);_runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            F1Set(boss,"_state",ShadowHagState.ShadowsConverging);F1Set(boss,"_counter1",255);native[parent+6]=255;
            // The transition update retains chase movement; subsequent state2
            // homes on the parent's current position and moves on arrival.
            Step(1);boss.Position=new(72,56);native[parent+11]=56;native[parent+13]=72;
            while(!actor.Finished&&updates<250)Step(1);
            Step(2);FailIf(!actor.Finished||boss.Counter2!=3,"PART$41 must return once, move on arrival and delete on the following update.");
            _entities.Clear();_player.EndCutsceneControl(actor);
        }
    }
}
