using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateLeeverPublicationRom()
    {
        int sinks=0,respawns=0,moves=0,waterEntries=0;
        foreach(int subid in new[]{0,1})
        foreach(int angle in new[]{0,8,16,24})
        foreach(int scenario in new[]{4,0,1,2,3})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(1,scenario==1?0xca:0xab);_entities.Clear();
            bool Clear(Vector2 point)=>_currentRoom.GetTerrainInfo(point) is {Collision:0,Hazard:HazardType.None};
            var points=(from y in Enumerable.Range(1,14) from x in Enumerable.Range(1,18)
                let point=new Vector2(x*8,y*8) where Clear(point)&&Clear(point+new Vector2(-1,5))&&Clear(point+new Vector2(1,5)) select point).ToArray();
            Vector2 link=points.OrderBy(point=>point.DistanceSquaredTo(new(80,104))).First(point=>
                new[]{-0x30,-0x40,-0x50}.Any(offset=>
                {
                    int candidate=unchecked((byte)(_currentRoom.GetPackedPosition(point)+offset));
                    return candidate<0x80&&(candidate&15)<10&&Clear(new((candidate&15)*16+8,(candidate>>4)*16+8));
                }));
            bool Blocked(Vector2 point)=>EnemyAdjacentWallResolver.Shared.ProbeTopDown(point,angle,p=>
                p.X<0||p.X>=_currentRoom.Width||p.Y<0||p.Y>=_currentRoom.Height||
                _currentRoom.IsSolid(p)||_currentRoom.GetTerrainInfo(p).Hazard==HazardType.Hole).Bitset!=0;
            bool NativeBlocked(Vector2 point)=>EnemyAdjacentWallResolver.Shared.ProbeTopDown(point,angle,p=>
                p.X<0||p.X>=_currentRoom.Width||p.Y<0||p.Y>=_currentRoom.Height||
                _currentRoom.IsSolidForEnemyMovement(p,holesAreWalls:true)).Bitset!=0;
            Vector2 initial=points.OrderByDescending(point=>scenario==1&&NativeBlocked(point)&&!Blocked(point))
                .ThenBy(point=>point.DistanceSquaredTo(new(80,64))).First(point=>NativeBlocked(point)==(scenario==1));
            if(scenario==1&&NativeBlocked(initial)&&!Blocked(initial))waterEntries++;
            if(fractional)initial+=new Vector2(.25f,.75f);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(link);_player.Face(Vector2I.Up);
            _runtimeState.SetWramByte(0xcc8a,1);_runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(AgesWramAddress.wScrollMode,0);_entities.AddEntity(new ProjectileMotionContactSuppression());
            var data=EnemyDatabase.Shared;var actor=new LeeverCharacter();actor.Initialize(data.ImportedEnemy(0x0b,subid),_currentRoom,initial,_random);
            F1Set(actor,"_state",scenario==4?LeeverState.Uninitialized:scenario==3?LeeverState.Emerging:LeeverState.Chasing);
            F1Set(actor,"_counter",scenario==4?0:scenario==2?1:18);
            F1Set(actor,"_angle",scenario==4?0:angle);F1Set(actor,"_collisionEnabled",scenario<3);F1Set(actor,"_emergenceComplete",scenario==3);
            actor.Visible=scenario!=4;actor.Animation.SetAnimation(scenario==4?0:1);
            var source=data.EnemyHandlers.ResolveHandler(0x0b,subid,"leever.s:bounded chase")
                .CombatSource(0,0,"leever.s:bounded chase");
            var adapter=new LeeverRoomEntity(actor,source,_sound.PlaySound);_entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            // Keep red's first blocked chase update from retargeting away
            // from its declared wall before the native gate is exercised.
            if(subid==1&&scenario==1)
            {
                var current=_random.CaptureState();var trial=new OracleRandom();
                for(int value=0;value<256;value++)
                {var candidate=current with{Rng1=(byte)value,Rng2=(byte)value};trial.RestoreState(candidate);if(trial.Next().Value<0x14)continue;_random.RestoreState(candidate);break;}
            }
            if(scenario==4)_random.RestoreState(_random.CaptureState() with{Rng1=0x37,Rng2=0x0d});
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,(int)link.X,(int)link.Y)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[0xcc8a]=1;native[AgesWramAddress.wWarpsDisabled]=1;
            native[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x0b*4];
            const int at=0xd180;native[at]=1;native[at+1]=0x0b;native[at+2]=(byte)subid;
            native[at+4]=(byte)(scenario==4?0:scenario==3?9:10);native[at+6]=(byte)(scenario==4?0:scenario==2?1:18);native[at+9]=(byte)(scenario==4?0:angle);
            native[at+16]=0x14;native[at+0x24]=(byte)(scenario<3?0x8b:0x0b);native[at+0x29]=(byte)actor.Health;native[at+0x1a]=(byte)(scenario==4?0:0xc2);
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
            if(scenario!=4)native.SetEnemyAnimation(1,1);
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0;bool lateWritten=false;
            var stage=new PartMovementScratchProbe(()=>
            {
                for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
                if(scenario==3&&!lateWritten){lateWritten=true;_player.SetScriptedCoordinateHigh(horizontal:true,136);}
            });_entities.RegisterEnemySlot(stage,0);_entities.AddEntity(stage);
            var after=new PartMovementScratchProbe(observedCapture.Capture);
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldState=native[at+4],oldX=native.Word(at+12),oldY=native.Word(at+10);byte[] expected=new byte[4];
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeParts:()=>
                {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];},beforeEnemies:()=>
                {if(scenario==3&&updates==0)native[0xd00d]=136;});updates++;
                if(scenario==4&&updates==1)
                    FailIf(native[at+0x3d]!=0x5e||native[at+6]!=0x10||native.RandomCalls!=2,
                        "Original common+Leever initialization from RNG $0d37 must draw $5e/$d4 and select the $10 timer.");
                if(scenario==4&&subid==0&&updates<100&&oldState==9&&native[at+4]==10)
                    FailIf(native[at+6]!=0xa8||native.RandomCalls!=3,
                        "Original first blue chase from RNG $0d37 must consume its third draw ($38) and select counter $a8.");
                if(oldState==10&&native[at+4]==11)sinks++;
                if(oldState==8&&native[at+4]==9)respawns++;
                if(oldState==10&&(oldX!=native.Word(at+12)||oldY!=native.Word(at+10)))moves++;
                FailIf((int)actor.State!=native[at+4]||actor.Counter!=native[at+6]||actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    SomariaPrivate<int>(actor,"_angle")!=native[at+9]||actor.Visible!=((native[at+0x1a]&128)!=0)||
                    actor.CollisionEnabled!=((native[at+0x24]&128)!=0)||actor.AnimationParameter!=native[at+0x21]||
                    SomariaPrivate<int>(actor.Animation,"_frameCounter")!=native[at+0x20]||observedCapture.Samples!=1||!observedCapture.Bytes.SequenceEqual(expected),
                    $"Leever$0b:${subid:x2}/angle${angle:x2}/scenario{scenario}/fraction{fractional}/update{updates}: state{actor.State}/{native[at+4]}, C{actor.Counter}/{native[at+6]}, XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, angle{SomariaPrivate<int>(actor,"_angle")}/{native[at+9]}, param{actor.AnimationParameter}/{native[at+0x21]}, clock{SomariaPrivate<int>(actor.Animation,"_frameCounter")}/{native[at+0x20]}, scratch[{string.Join(',',observedCapture.Bytes)}]/[{string.Join(',',expected)}].");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    $"Leever/subid{subid}/update{updates}: source retarget/sink/spawn RNG differs: {_random.CaptureState().Calls-seed.Calls}/{native.RandomCalls}.");observedCapture.Samples=0;
            });
            if(scenario==4&&angle!=24)
            {
                if(angle==0){_dialogue.ShowMessage("Fresh Leever hold.",104);native[0xcba0]=1;}
                if(angle==8){_runtimeState.SetWramByte(0xcc8a,5);native[0xcc8a]=5;}
                if(angle==16){_runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;}
                Step(3);_dialogue.Close();native[0xcba0]=0;
                _runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            }
            Step(1);
            _dialogue.ShowMessage("Leever chase hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,5);native[0xcc8a]=5;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            if(scenario==4)
            {
                while(native[at+4]!=10&&updates<250)Step(1);
                FailIf(native[at+4]!=10,"A fresh Leever must complete common initialization and its first source emergence.");
            }
            while(native[at+4]!=8&&updates<500)Step(1);
            FailIf(native[at+4]!=8,"Leever chase must finish sinking before starting another source emergence.");
            while(native[at+4]!=10&&updates<750)Step(1);
            FailIf(native[at+4]!=10,"Leever must complete its next source spawn/emergence and repeat the chase.");
            Step(5);_entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(sinks<160||respawns<160||moves<160||waterEntries==0,
            "Leever comparisons must exercise sinking, repeat emergence, actual chase movement and the room's water/lava collision-$10 boundaries.");
    }
}
