using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePeahatTerrainRom()
    {
        int landings=0,bounces=0,moves=0;
        foreach(int form in Enumerable.Range(0,7))
        foreach(int variant in form==0?Enumerable.Range(0,8):form is 3 or 4?Enumerable.Range(0,32):new[]{0})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,form==2?0x91:0x7b);_entities.Clear();
            var points=(from y in Enumerable.Range(0,_currentRoom.Height/4) from x in Enumerable.Range(0,_currentRoom.Width/4)
                select new Vector2(x*4,y*4)).ToArray();
            Vector2 initial=form switch
            {
                0=>variant%2==0?new(200,72):new(56,88),
                1=>points.First(p=>_currentRoom.GetTerrainInfo(p).Collision==0x10),
                2=>points.First(p=>_currentRoom.GetTerrainInfo(p).Collision is >0 and <0x0f&&!_currentRoom.IsSolid(p)),
                _=>points.Where(p=>_currentRoom.GetTerrainInfo(p) is {Collision:0,Hazard:HazardType.None})
                    .OrderBy(p=>p.DistanceSquaredTo(new(120,80))).First()
            };
            if(form==4)
            {
                Vector2 delta=OracleObjectMovement.Shared.Delta(0x1e,variant);
                initial=Mathf.Abs(delta.X)>Mathf.Abs(delta.Y)
                    ?new(delta.X>0?_currentRoom.Width-2:2,_currentRoom.Height/2)
                    :new(_currentRoom.Width/2,delta.Y>0?_currentRoom.Height-2:2);
            }
            if(form==3)initial=(initial/16).Floor()*16+new Vector2(8,8);
            if(fractional)initial+=new Vector2(.25f,.5f);
            if(form==3)_currentRoom.SetPositionTileAndCollision(initial,_currentRoom.GetMetatile(initial),0xff,0);
            Vector2 link=points.Where(p=>_currentRoom.GetTerrainInfo(p) is {Collision:0,Hazard:HazardType.None})
                .OrderBy(p=>p.DistanceSquaredTo(new(120,128))).First();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(link);
            _runtimeState.SetWramByte(0xcc8a,1);_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            _entities.AddEntity(new ProjectileMotionContactSuppression());
            var data=new EnemyDatabase();var actor=new PeahatCharacter();actor.Initialize(data.ImportedEnemy(0x3e),_currentRoom,initial,_random);
            int state=form==0?0:form is 3 or 4?10:form==5?8:11;
            int counter=form==0||form==5?0:form is 1 or 2?127:form==6?255:63;
            int z=form is 3 or 4?-6:0,speed=form==0?0:form is 3 or 4?0x1e:5;
            F1Set(actor,"_state",(PeahatState)state);F1Set(actor,"_counter",counter);F1Set(actor,"_angle",form is 3 or 4?variant:0);
            F1Set(actor,"_zHigh",z);F1Set(actor,"_speedRaw",speed);
            actor.ZIndex=state is 0 or 8?ObjectDrawPriority.BehindLinkZIndex:ObjectDrawPriority.InFrontOfLinkZIndex;
            var source=data.EnemyHandlers.ResolveHandler(0x3e,0,"peahat.s:terrain/landing").CombatSource(0,0,"peahat.s:terrain/landing");
            var adapter=new PeahatRoomEntity(actor,source,_sound.PlaySound);_entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            if(form==0)
            {
                var current=_random.CaptureState();var trial=new OracleRandom();bool found=false;
                for(int value=0x100;value<65536;value++)
                {
                    var candidate=current with{Rng1=(byte)value,Rng2=(byte)(value>>8)};trial.RestoreState(candidate);trial.Next();
                    if((trial.Next().Value&7)!=variant)continue;_random.RestoreState(candidate);found=true;break;
                }
                FailIf(!found,"Peahat full-cycle fixture must select each of the eight original flight-duration rows.");
            }
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,(int)link.X,(int)link.Y)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[0xcc8a]=1;native[WramAddress.wWarpsDisabled]=1;
            native[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x3e*4];
            const int at=0xd180;native[at]=1;native[at+1]=0x3e;native[at+4]=(byte)state;native[at+6]=(byte)counter;
            native[at+9]=(byte)(form is 3 or 4?variant:0);native[at+16]=(byte)speed;native[at+15]=unchecked((byte)z);native[at+0x29]=(byte)actor.Health;native[at+0x1a]=0xc1;
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
            if(form!=0)native.SetEnemyAnimation(1,0);
            byte[] observed=new byte[4];int probes=0,updates=0;bool speedPublished=form!=0;
            var stage=new PartMovementScratchProbe(()=>{for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(stage,0);_entities.AddEntity(stage);
            var after=new PartMovementScratchProbe(()=>{probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldState=native[at+4],oldAngle=native[at+9],oldX=native.Word(at+12),oldY=native.Word(at+10);byte[] expected=new byte[4];
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeParts:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                // State0 passes the jump-table accumulator to the common
                // speed setter. No handler consumes it before state8 writes
                // SPEED_20; compare the first meaningful speed publication.
                if(oldState==8&&native[at+4]==9)speedPublished=true;
                if(oldState==11&&native[at+4]==8)landings++;
                if(oldAngle!=native[at+9]&&form is 3 or 4)bounces++;
                if(oldX!=native.Word(at+12)||oldY!=native.Word(at+10))moves++;
                if(form==0&&oldState==9&&native[at+4]==10)
                    FailIf(native[at+6]!=new[]{180,180,210,210,240,240,0,0}[variant]||native.RandomCalls!=3,
                        "Peahat flight rows and common/duration/angle RNG draws must match the original source table, including zero counters.");
                FailIf((int)actor.State!=native[at+4]||actor.Counter!=native[at+6]||actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.ZHigh!=unchecked((sbyte)native[at+15])||actor.CollisionMode!=native[at+0x25]||
                    SomariaPrivate<int>(actor,"_angle")!=native[at+9]||speedPublished&&SomariaPrivate<int>(actor,"_speedRaw")!=native[at+16]||
                    actor.AnimationParameter!=native[at+0x21]||SomariaPrivate<int>(actor.Animation,"_frameCounter")!=native[at+0x20]||
                    probes!=1||!observed.SequenceEqual(expected),
                    $"Peahat$3e/form{form}/variant${variant:x2}/fraction{fractional}/update{updates}: state{actor.State}/{native[at+4]}, C{actor.Counter}/{native[at+6]}, XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, angle{SomariaPrivate<int>(actor,"_angle")}/{native[at+9]}, Z{actor.ZHigh}/{unchecked((sbyte)native[at+15])}, mode${actor.CollisionMode:x2}/${native[at+0x25]:x2}, speed{SomariaPrivate<int>(actor,"_speedRaw")}/{native[at+16]}, clock{SomariaPrivate<int>(actor.Animation,"_frameCounter")}/{native[at+0x20]}, probes{probes}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}].");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls,
                    "Peahat timing/terrain changes must retain original RNG order.");probes=0;
            });
            Step(1);
            _dialogue.ShowMessage("Peahat terrain hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,5);native[0xcc8a]=5;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            if(form==0)
            {
                bool departed=false;
                while(updates<650)
                {Step(1);departed|=native[at+4]==9;if(departed&&native[at+4]==8)break;}
                FailIf(native[at+4]!=8,"Peahat must finish the complete source takeoff/flight/slowdown/ground route.");
            }
            Step(2);_entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(landings<40||bounces==0||moves==0,"Peahat must execute natural cycles, both grounded predicates and boundary bounces.");
    }
}
