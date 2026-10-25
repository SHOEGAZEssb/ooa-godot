using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateGalleryDebrisRom()
    {
        foreach(int free in new[]{4,0,1,2,3,8})
        foreach(int type in new[]{0,1,2,3})
        foreach(bool doubleHit in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(2,0xe9);
            _roomEvents.Get<ShootingGalleryEvent>().Cancel();_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(24,104));_player.BeginCutsceneControl();
            _runtimeState.SetWramByte(0xcc8a,1);_runtimeState.SetWramByte(0xcd00,0);
            // shootingGallery_targetPositions_lynna begins$31; ball.s table_6bab
            // maps types0..3 to$d9/$d7/$dc/$d8. Declare the original state3 target probe;
            // real reachable sword contacts are covered by GalleryReflectionRom.
            Vector2 center=new(24,56);byte[] tiles=[0xd9,0xd7,0xdc,0xd8];byte tile=tiles[type];
            _currentRoom.SetPositionTileAndCollision(center,tile,null,_entities.AnimationTick());
            if(doubleHit)_currentRoom.SetPositionTileAndCollision(new(40,56),tiles[(type+1)&3],null,_entities.AnimationTick());
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,24,104)
                {CompanionDispatchEnabled=true,HostilePartsEnabled=true};
            native.InitializeLinkGameplay();native[0xcc8a]=1;native[0xcd00]=0;
            for(int i=0;i<14-free;i++)
            {
                var puff=new PuzzlePuffEffect();puff.Initialize(new(24,24),SoundId.MusNone);
                _entities.AddEntity(new InteractionSlotValidationEntity(puff,static _=>{}));
                int filler=0xd040+_entities.InteractionSlot(puff)*256;
                native[filler]=1;native[filler+1]=0;native[filler+4]=1;native[filler+0x20]=0xff;
            }
            var session=new ShootingGallerySession();session.BeginBall();
            var ball=_entities.Spawn<ShootingGalleryBall>(new ShootingGalleryBallSpawn(session,new(doubleHit?32:24,56)));
            F1Set(ball,"_state",ShootingGalleryBallState.Reflected);F1Set(ball,"_speed",0x78);F1Set(ball,"_angle",0);
            int at=0xd0c0+F1Parts[_entities.EntityAdapters<ShootingGalleryBallRoomEntity>().Single()]*256;
            native[at]=1;native[at+1]=0x38;native[at+4]=3;native[at+16]=0x78;
            native.Word(at+10,56*256);native.Word(at+12,(doubleHit?32:24)*256);
            native[0xccd4]=native[0xccd6]=native[0xcfd6]=0;native[0xcfd5]=0xff;
            var sounds=_sound.AttachPlayRequestAudit();sounds.Clear();
            byte[] observed=new byte[4],expected=new byte[4];int updates=0;
            var seed=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(seed,15);_entities.AddEntity(seed);
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);}));
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.UpdateGameplay(0,0,0xff,_entities.FrameCounter,
                    beforeParts:()=>{for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);},
                    afterInteractions:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                var children=_entities.EntityAdapters<ShootingGalleryTargetDebrisRoomEntity>()
                    .OrderBy(d=>_entities.InteractionSlot(d.Node)).ToArray();
                int[] slots=Enumerable.Range(2,14).Where(s=>native[0xd040+s*256]!=0&&native[0xd041+s*256]==0x92).ToArray();
                FailIf(children.Length!=slots.Length||!observed.SequenceEqual(expected),
                    $"Gallery debris free{free}/type{type}/double{doubleHit}/update{updates}: original checked allocation or last-writer scratch differs (children{children.Length}/{slots.Length}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}]).");
                for(int i=0;i<children.Length;i++)
                {
                    var debris=(ShootingGalleryTargetDebris)children[i].Node;int child=0xd040+slots[i]*256;
                    FailIf(_entities.InteractionSlot(debris)!=slots[i]||
                        SomariaPrivate<Vector2>(debris,"_precisePosition")!=new Vector2(native.Word(child+12)/256f,native.Word(child+10)/256f)||
                        SomariaPrivate<int>(debris,"_angle")!=native[child+9]||debris.Counter!=native[child+6]||
                        debris.Palette!=(native[child+0x1c]&7),
                        $"Gallery debris free{free}/type{type}/update{updates}/slot${slots[i]:x2}: native order, complete XY, counter or palette differs (angle{SomariaPrivate<int>(debris,"_angle")}/{native[child+9]}, XY{SomariaPrivate<Vector2>(debris,"_precisePosition")}/{native.Word(child+12)/256f},{native.Word(child+10)/256f}, counter{debris.Counter}/{native[child+6]}, palette{debris.Palette}/{native[child+0x1c]&7}).");
                }
                FailIf(session.HitCount!=(native[0xccd6]&0x7f)||session.HitTargets!=native[0xccd4]||
                    _currentRoom.GetMetatile(center)!=native[0xcf31],
                    "Gallery target replacement and first-hit side effects must survive allocation failure.");
                var rng=_random.CaptureState();
                FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||!sounds.Requests.SequenceEqual(native.Sounds),
                    "Gallery fragment allocation and initialization must retain source RNG and sound order.");
            });
            Step(1);FailIf(session.HitCount!=(doubleHit?2:1)||
                doubleHit&&_currentRoom.GetMetatile(new(40,56))!=native[0xcf32],
                "Both ordered source side probes must replace targets independently of available child slots.");
            _dialogue.ShowMessage("Gallery fragments continue.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,3);native[0xcc8a]=3;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            Step(3);FailIf(_entities.EntityAdapters<ShootingGalleryTargetDebrisRoomEntity>().Any(),
                "Gallery fragments must release physical interaction slots on their twelfth state1 update.");
            _entities.Clear();_player.EndCutsceneControl();
        }
    }
}
