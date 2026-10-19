using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMoblinBoomerangBirthRom()
    {
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        foreach(bool lateTarget in new[]{false,true})
        foreach(int gate in new[]{0,1,2,3})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x17);_entities.Clear();
            var data=new EnemyDatabase();
            var cells=(from y in Enumerable.Range(1,_currentRoom.HeightInTiles-2) from x in Enumerable.Range(1,_currentRoom.WidthInTiles-2)
                let p=new Vector2(x*16+8,y*16+8) where _currentRoom.GetTerrainInfo(p) is {Collision:0,Hazard:HazardType.None} select p).ToArray();
            Vector2 initial=cells.OrderBy(p=>p.DistanceSquaredTo(new(80,64))).First();
            Vector2 link=cells.OrderByDescending(p=>p.DistanceSquaredTo(initial)).First();
            if(fractional)initial+=new Vector2(.25f,.5f);
            int target=(OracleObjectMovement.Shared.RelativeAngle(initial,link)+4)&24;
            Vector2 lateLink=cells.First(p=>p.DistanceTo(initial)>16&&((OracleObjectMovement.Shared.RelativeAngle(initial,p)+4)&24)!=target);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(link);
            _runtimeState.SetWramByte(0xcc8a,1);_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            _entities.AddEntity(new ProjectileMotionContactSuppression());
            var parent=new BoomerangMoblinCharacter();parent.Initialize(data.ImportedEnemy(0x0a),_currentRoom,initial,_random);
            var source=data.EnemyHandlers.ResolveHandler(0x0a,0,"boomerangMoblin.s:birth").CombatSource(0,0,"boomerangMoblin.s:birth");
            var adapter=new BoomerangMoblinRoomEntity(parent,source,_sound.PlaySound);_entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,(int)link.X,(int)link.Y)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[0xcc8a]=1;native[WramAddress.wWarpsDisabled]=1;
            native[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x0a*4];native[0xcc0a]=0x8e;
            const int owner=0xd180,at=0xd0c0;native[owner]=1;native[owner+1]=0x0a;
            native.Word(owner+10,(int)(initial.Y*256));native.Word(owner+12,(int)(initial.X*256));
            bool birthVisit=false;int beforePart=-1,probes=0,updates=0;
            byte[] observed=new byte[4];
            var stage=new PartMovementScratchProbe(()=>
            {
                for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
                if(birthVisit&&lateTarget)
                {_player.SetScriptedCoordinateHigh(horizontal:true,(int)lateLink.X);_player.SetScriptedCoordinateHigh(horizontal:false,(int)lateLink.Y);}
            });
            _entities.RegisterEnemySlot(stage,0);_entities.AddEntity(stage);
            var enemyProbe=new PartMovementScratchProbe(()=>
            {
                beforePart=_entities.Entities<MoblinBoomerangProjectile>().SingleOrDefault() is { } child
                    ?(child.InitializationPending?0:child.Returning?2:1):-1;
                if(!birthVisit)return;
                if(gate==2)_dialogue.ShowMessage("Late boomerang birth hold.",104);
                if(gate==3)_runtimeState.SetWramByte(0xcd00,8);
            });_entities.RegisterEnemySlot(enemyProbe,15);_entities.AddEntity(enemyProbe);
            _entities.AddEntity(new PartMovementScratchProbe(()=>{probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);}));
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int expectedBefore=-1;
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeParts:()=>
                {
                    expectedBefore=native[at]==0?-1:native[at+4];
                    if(!birthVisit)return;
                    if(gate==2)native[0xcba0]=1;
                    if(gate==3)native[0xcd00]=8;
                },beforeEnemies:()=>
                {if(birthVisit&&lateTarget){native[0xd00d]=(byte)lateLink.X;native[0xd00b]=(byte)lateLink.Y;}});updates++;
                var rng=_random.CaptureState();
                FailIf((int)parent.State!=native[owner+4]||parent.Position!=new Vector2(native.Word(owner+12)/256f,native.Word(owner+10)/256f)||
                    SomariaPrivate<int>(parent,"_counter")!=native[owner+6]||SomariaPrivate<int>(parent,"_angle")!=native[owner+9]||
                    rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls,
                    $"Boomerang Moblin/fraction{fractional}/late{lateTarget}/gate{gate}/update{updates}: source initialization/route/target/RNG differs: state{parent.State}/{native[owner+4]}, C{SomariaPrivate<int>(parent,"_counter")}/{native[owner+6]}, angle{SomariaPrivate<int>(parent,"_angle")}/{native[owner+9]}, calls{rng.Calls-seed.Calls}/{native.RandomCalls}.");
                var actor=_entities.Entities<MoblinBoomerangProjectile>().SingleOrDefault();
                FailIf(actor is not null&&!F1Parts.Any(pair=>ReferenceEquals(pair.Key.Node,actor)&&pair.Value==0),
                    "Source-born Moblin boomerang must own the first free physical PART slot, $d0c0.");
                FailIf(beforePart!=expectedBefore||(actor is null)!=(native[at]==0)||actor is not null&&(
                    actor.InitializationPending!=(native[at+4]==0)||actor.Returning!=(native[at+4]==2)||actor.Counter!=native[at+6]||actor.SpeedRaw!=native[at+16]||
                    actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    SomariaPrivate<int>(SomariaPrivate<EnemyAnimationPlayer>(actor,"_animation"),"_frameCounter")!=native[at+0x20])||
                    probes!=1||Enumerable.Range(0,4).Any(i=>observed[i]!=native[0xcec0+i]),
                    $"Moblin boomerang birth/fraction{fractional}/gate{gate}/update{updates}: beforePART{beforePart}/{expectedBefore}, childXY{actor?.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}; source birth must wait for its physical PART visit.");probes=0;
            });
            Step(1);
            // Declare the legitimate state9 decision entry, then choose each
            // route draw independently so the source accepts this throw.
            F1Set(parent,"_state",BoomerangMoblinCharacterMoblinState.Deciding);native[owner+4]=9;
            var current=_random.CaptureState();var trial=new OracleRandom();bool found=false;
            for(int value=0;value<256;value++)
            {
                var candidate=current with{Rng1=(byte)value,Rng2=(byte)value};trial.RestoreState(candidate);trial.Next();
                if((trial.Next().Value&24)!=target)continue;
                _random.RestoreState(candidate);native[0xff94]=candidate.Rng1;native[0xff95]=candidate.Rng2;found=true;break;
            }
            FailIf(!found,"Source Moblin decision fixture must find the actual accepted route draw.");
            if(gate==1){_runtimeState.SetWramByte(0xcc8a,9);native[0xcc8a]=9;}
            birthVisit=true;Step(1);birthVisit=false;Step(3);
            FailIf(_entities.Entities<MoblinBoomerangProjectile>().Count!=1||native[at]==0,
                "The accepted source decision must create its independent physical PART$21.");
            _dialogue.Close();native[0xcba0]=0;_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;Step(3);
            _entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
    }
}
