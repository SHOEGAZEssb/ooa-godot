using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMoblinBoomerangPublicationRom()
    {
        int catches=0,earlyReturns=0,moves=0;
        foreach(int scenario in new[]{0,1,2,3,4,5,6,7,8,9})
        foreach(int angle in scenario<2||scenario>=7?new[]{0,8,16,24}:new[]{8})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x17);_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(120,120));
            _runtimeState.SetWramByte(0xcc8a,1);_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);_entities.AddEntity(new ProjectileMotionContactSuppression());
            bool Clear(Vector2 p)=>_currentRoom.GetTerrainInfo(p) is {Collision:0,Hazard:HazardType.None}&&
                _currentRoom.GetTerrainInfo(p+new Vector2(-1,5)).Hazard==HazardType.None&&_currentRoom.GetTerrainInfo(p+new Vector2(1,5)).Hazard==HazardType.None;
            var cells=(from y in Enumerable.Range(1,_currentRoom.HeightInTiles-2) from x in Enumerable.Range(1,_currentRoom.WidthInTiles-2)
                select new Vector2(x*16+8,y*16+8)).ToArray();
            Vector2 parentPosition=cells.OrderBy(p=>p.DistanceSquaredTo(new(80,64))).First(Clear);
            if(scenario==5)parentPosition=new(2,parentPosition.Y); // Declared wrapping catch boundary, not a placed enemy.
            if(fractional)parentPosition+=new Vector2(.25f,.5f);
            var data=EnemyDatabase.Shared;var parent=new BoomerangMoblinCharacter();parent.Initialize(data.ImportedEnemy(0x0a),_currentRoom,parentPosition,_random);
            F1Set(parent,"_initialized",true);F1Set(parent,"_state",BoomerangMoblinCharacterMoblinState.WaitingForBoomerang);
            F1Set(parent,"_angle",angle);parent.Animation.SetAnimation(angle>>3);
            var source=data.EnemyHandlers.ResolveHandler(0x0a,0,"moblinBoomerang.s:owner handoff").CombatSource(0,0,"moblinBoomerang.s:owner handoff");
            var ownerAdapter=new BoomerangMoblinRoomEntity(parent,source,_sound.PlaySound);_entities.RegisterEnemySlot(ownerAdapter,1);_entities.AddEntity(ownerAdapter);
            Vector2 initial=scenario==1?cells.Where(p=>_currentRoom.IsSolid(p)&&p.DistanceTo(parentPosition)>12)
                .OrderBy(p=>p.DistanceSquaredTo(parentPosition)).First():parentPosition;
            if(scenario==1&&fractional)initial+=new Vector2(.5f,.75f);
            if(scenario>=7)
            {
                // Declared raw collision bytes exercise all four quarters;
                // objectCheckSimpleCollision does not use strip masks.
                Vector2 tile=(parentPosition/16).Floor()*16;
                initial=tile+(angle switch{0=>new Vector2(4,4),8=>new Vector2(12,4),16=>new Vector2(4,12),_=>new Vector2(12,12)});
                if(fractional)initial+=new Vector2(.25f,.5f);
                byte collision=(byte)(scenario==7?0x11:scenario==8?0x1e:0x1f);
                _currentRoom.SetPositionTileAndCollision(initial,_currentRoom.GetMetatile(initial),collision,0);
            }
            var actor=new MoblinBoomerangProjectile(parent,_currentRoom,initial,angle,data.MoblinBoomerang,_runtimeState);
            actor.Position=initial; // Declared retained words after allocation, separate from fresh birth.
            var adapter=new MoblinBoomerangRoomEntity(actor);F1Parts.Add(adapter,0);_entities.AddEntity(adapter);
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,120,120)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[0xcc8a]=1;native[WramAddress.wWarpsDisabled]=1;
            native[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x0a*4];native[0xcc0a]=0x8e;
            const int owner=0xd180,at=0xd0c0;native[owner]=1;native[owner+1]=0x0a;native[owner+4]=10;
            native[owner+9]=(byte)angle;native[owner+16]=0x14;native[owner+0x29]=(byte)parent.Health;native[owner+0x1a]=0xc2;
            native.Word(owner+10,(int)(parentPosition.Y*256));native.Word(owner+12,(int)(parentPosition.X*256));native.Word(owner+0x18,at);native.SetEnemyAnimation(1,angle>>3);
            native[at]=1;native[at+1]=0x21;native[at+9]=(byte)angle;native.Word(at+0x16,owner);
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
            byte[] observed=new byte[4],enemyObserved=new byte[4];int probes=0,enemyProbes=0,updates=0;
            var stage=new PartMovementScratchProbe(()=>{for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(stage,0);_entities.AddEntity(stage);
            var enemyProbe=new PartMovementScratchProbe(()=>{enemyProbes++;for(int i=0;i<4;i++)enemyObserved[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(enemyProbe,15);_entities.AddEntity(enemyProbe);
            _entities.AddEntity(new PartMovementScratchProbe(()=>{probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);}));
            var animation=SomariaPrivate<EnemyAnimationPlayer>(actor,"_animation");
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldState=native[at+4],oldX=native.Word(at+12),oldY=native.Word(at+10);bool alive=native[at]!=0;byte[] expectedEnemy=new byte[4];
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeParts:()=>{for(int i=0;i<4;i++)expectedEnemy[i]=native[0xcec0+i];});updates++;
                if(alive&&native[at]==0)catches++;
                if(oldState==1&&native[at+4]==2&&native[at+6]==45)earlyReturns++;
                if(alive&&native[at]!=0&&(oldX!=native.Word(at+12)||oldY!=native.Word(at+10)))moves++;
                FailIf(actor.Finished!=(native[at]==0)||!actor.Finished&&(
                    actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Returning!=(native[at+4]==2)||actor.Counter!=native[at+6]||actor.SpeedRaw!=native[at+16]||
                    SomariaPrivate<int>(actor,"_speedCounter")!=native[at+7]||SomariaPrivate<int>(actor,"_angle")!=native[at+9]||
                    SomariaPrivate<int>(animation,"_frameCounter")!=native[at+0x20]||animation.CurrentParameter!=native[at+0x21])||
                    probes!=1||enemyProbes!=1||!enemyObserved.SequenceEqual(expectedEnemy)||Enumerable.Range(0,4).Any(i=>observed[i]!=native[0xcec0+i]),
                    $"Moblin PART$21/scenario{scenario}/angle${angle:x2}/fraction{fractional}/update{updates}: done{actor.Finished}/{native[at]==0}, XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, return{actor.Returning}/{native[at+4]}, C{actor.Counter}/{native[at+6]}, speed${actor.SpeedRaw:x2}/${native[at+16]:x2}, clock{SomariaPrivate<int>(animation,"_frameCounter")}/{native[at+0x20]}, scratch[{string.Join(',',observed)}]/[{string.Join(',',Enumerable.Range(0,4).Select(i=>native[0xcec0+i]))}].");
                FailIf((int)parent.State!=native[owner+4]||parent.Position!=new Vector2(native.Word(owner+12)/256f,native.Word(owner+10)/256f)||
                    SomariaPrivate<int>(parent,"_counter")!=native[owner+6]||SomariaPrivate<int>(parent,"_angle")!=native[owner+9]||
                    parent.State==BoomerangMoblinCharacterMoblinState.WaitingForBoomerang&&SomariaPrivate<bool>(parent,"_boomerangReturned")!=(native.Word(owner+0x18)==0),
                    $"Moblin PART$21/scenario{scenario}/update{updates}: catch must publish to its owner for the next eligible ENEMY visit.");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls,
                    "Moblin boomerang catch/repeated owner route must retain original RNG order.");probes=enemyProbes=0;
            });
            if(scenario==0&&angle!=24)
            {
                if(angle==0){_dialogue.ShowMessage("Fresh boomerang hold.",104);native[0xcba0]=1;}
                if(angle==8){_runtimeState.SetWramByte(0xcc8a,9);native[0xcc8a]=9;}
                if(angle==16){_runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;}
                Step(3);_dialogue.Close();native[0xcba0]=0;
                _runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            }
            else Step(1);
            if(scenario==6){F1Set(actor,"_counter",0);native[at+6]=0;}
            else if(scenario is >=2 and <=5)
            {
                actor.Deflect();native[at+4]=2;
                Vector2 catchPoint=scenario switch{2=>new(Mathf.Floor(parentPosition.X)+4.5f,Mathf.Floor(parentPosition.Y)),
                    3=>new(Mathf.Floor(parentPosition.X),Mathf.Floor(parentPosition.Y)+4.5f),5=>new(254.5f,Mathf.Floor(parentPosition.Y)),
                    _=>parentPosition+new Vector2(24,0)};
                actor.Position=catchPoint;native.Word(at+10,(int)(catchPoint.Y*256));native.Word(at+12,(int)(catchPoint.X*256));
            }
            _dialogue.ShowMessage("Enemy boomerang hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,9);native[0xcc8a]=9;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(native[at]!=0&&updates<180)Step(1);
            FailIf(!actor.Finished,"Enemy boomerang must complete its original outbound/return/catch path.");
            // Hold the owner after catch, then allow it to consume the PART's
            // pointer-clear publication on its own subsequent ENEMY visit.
            _runtimeState.SetWramByte(0xcc8a,5);native[0xcc8a]=5;Step(2);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;Step(2);
            FailIf(parent.State==BoomerangMoblinCharacterMoblinState.WaitingForBoomerang,
                "The caught boomerang's owner must resume its source route after its freeze is released.");
            _entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(catches!=100||earlyReturns==0||moves==0,"All100 enemy boomerang flows must catch, including naturally blocked early returns, raw collision quarters and moving routes.");
    }
}
