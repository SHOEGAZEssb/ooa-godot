using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateGalleryReflectionRom()
    {
        foreach(int attackY in new[]{68,70})
        foreach(int linkX in new[]{76,84})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(2,0xe9);
            _roomEvents.Get<ShootingGalleryEvent>().Cancel();_entities.Clear();
            _inventory.GiveTreasure(TreasureId.Sword,1);_inventory.EquipA(TreasureId.Sword);_inventory.EquipB(0);
            _inventory.ApplyDamage(1); // Retain an ordinary swing without a full-health beam.
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(0xcd00,0);
            _player.WarpTo(new(linkX,104));
            FailIf(_collision.Collides(_player.Position),"Gallery sword fixture must start on actual doorway-side floor.");
            StepGameplayUpdates(8,Vector2.Up,batched:batched);
            FailIf(_player.Position!=new Vector2(linkX,96)||_collision.Collides(_player.Position),
                "Gallery sword fixture must approach through the room's actual collision geometry.");
            var seed=_random.CaptureState() with{Rng1=0};_random.RestoreState(seed);
            var session=new ShootingGallerySession();session.BeginBall();
            var ball=_entities.Spawn<ShootingGalleryBall>(new ShootingGalleryBallSpawn(session,new(80,8)));
            var adapter=_entities.EntityAdapters<ShootingGalleryBallRoomEntity>().Single();int at=0xd0c0+F1Parts[adapter]*256;
            var native=new SomariaRom(_saveData,seed,_currentRoom,0,linkX,96)
                {CompanionDispatchEnabled=true,HostilePartsEnabled=true};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[0xcc6e]=1;native[0xd009]=0xff;
            native[at]=1;native[at+1]=0x38;native[at+11]=8;native[at+13]=80;
            byte[] observed=new byte[4],expected=new byte[4];int probes=0;
            void Seed(OracleRuntimeState memory){for(int i=0;i<4;i++)memory.SetWramByte(0xcec0+i,(byte)(0xa5+i));}
            var beforeParts=new PartMovementScratchProbe(()=>Seed(_runtimeState));
            _entities.RegisterEnemySlot(beforeParts,15);_entities.AddEntity(beforeParts);
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);}));
            int updates=0;bool contacted=false,reflected=false;
            void Step(int count,bool attack=false,bool pressed=false)
            {
                int edge=pressed?1:0;
                StepGameplayUpdates(count,Vector2.Zero,attack?["attack"]:[],pressed?["attack"]:[],batched,()=>
                {
                    native.UpdateGameplay(edge,attack?1:0,0xff,_entities.FrameCounter,
                        beforeParts:()=>{for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);},
                        beforeInteractions:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});edge=0;updates++;
                    contacted|=(native[at+0x2a]&128)!=0;reflected|=native[at+4]==3;
                    int state=native[at+4];
                    bool pending=state<2&&(native[at+0x2a]&128)!=0&&native[at+0x2a]!=0x80;
                    if(pending)state=2;
                    FailIf(ball.Finished!=(native[at]==0)||!ball.Finished&&
                        (SomariaPrivate<Vector2>(ball,"_precisePosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                            (int)ball.State!=state||ball.CollisionCounter!=native[at+6]||
                            ball.Angle!=native[at+(pending?0x2c:9)]),
                        $"Gallery sword contact X${linkX:x2}/update{updates}: native complete PART/weapon collision must retain incoming fractional words (state={ball.State}/{native[at+4]}, XY={SomariaPrivate<Vector2>(ball,"_precisePosition")}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, signal=${native[at+0x2a]:x2}, sword={_player.SwordState}/{_player.IsAttacking}, hitbox={_player.GetSwordHitbox()}, nativeSword={native[0xd624]:x2}/{native[0xd62a]:x2} XY={native[0xd60d]},{native[0xd60b]} radius={native[0xd627]},{native[0xd626]}).");
                    FailIf(probes!=1||!observed.SequenceEqual(expected),
                        $"Gallery reflection update{updates}: native PART movement publication/no-motion boundary differs before interactions.");
                    probes=0;
                });
            }
            while(ball.Position.Y<attackY&&!ball.Finished&&updates<80)Step(1);
            Step(1,attack:true,pressed:true);Step(12,attack:true);
            _dialogue.ShowMessage("Reflected gallery hold.",_player.Position.Y);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,8);native[0xcc8a]=8;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(!ball.Finished&&updates<200)Step(3);
            FailIf(!contacted||!reflected||!ball.Finished,
                "Gallery regression must use an actual sword contact, original reflected state and complete flight.");
            session.BeginBall();ball=_entities.Spawn<ShootingGalleryBall>(new ShootingGalleryBallSpawn(session,new(80,8)));
            adapter=_entities.EntityAdapters<ShootingGalleryBallRoomEntity>().Single();at=0xd0c0+F1Parts[adapter]*256;
            native[at]=1;native[at+1]=0x38;native[at+11]=8;native[at+13]=80;native[0xccd6]=native[0xcfd6]=0;
            updates=0;contacted=reflected=false;
            while(ball.Position.Y<attackY&&!ball.Finished&&updates<120)Step(1);
            Step(1,attack:true,pressed:true);Step(12,attack:true);
            while(!ball.Finished&&updates<240)Step(3);
            FailIf(!contacted||!reflected||!ball.Finished,
                "A completed gallery reflection must permit another genuine swing/contact and flight using the released PART slot.");
            _entities.Clear();
        }
    }
}
