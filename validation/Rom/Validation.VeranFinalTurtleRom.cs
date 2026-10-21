using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVeranFinalTurtleRom()
    {
        var forms=new HashSet<int>();
        foreach(bool batched in new[]{false,true})
        foreach(byte rng in new byte[]{0x11,0x37})
        {
            ReinitializeGameplayForValidation();
            for(int heart=0;heart<11;heart++)_inventory.GiveTreasure(TreasureId.HeartContainer,4);
            _inventory.RefillHealth();_statusBar.SynchronizeHealth();LoadValidationRoom(4,0xd4);
            var boss=_entities.EntityAdapters<VeranFinalRoomEntity>().Single();var actor=boss.Actor;
            foreach(var other in _entities.EntityAdapters<IRoomEntity>().Where(entity=>entity!=boss).ToArray())_entities.RetireEntity(other);
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(0x78,0x88));_player.Face(Vector2I.Up);_transitions.ResetCamera();
            FailIf(_collision.Collides(_player.Position)||boss.Slot!=0||actor.Position!=new Vector2(0x78,0x40),
                "Final Veran must use the source $4:d4 main-data enemy placement before respawning bushes.");
            _random.RestoreState(_random.CaptureState() with {Rng1=rng,Rng2=0x19});var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x88)
                {CompanionDispatchEnabled=true,HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xd008]=0;
            rom[0xcc08]=0xd7;rom[0xcc0a]=0xd8;rom[0xcc0c]=0xd9;rom[0xcc0e]=0xda;
            rom[0xd080]=1;rom[0xd081]=2;rom[0xd08b]=0x40;rom[0xd08d]=0x78;rom[0xcdd1]=1;
            rom[0xcd16]=0;rom[0xcd17]=0xd0;Vector2 camera=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var sounds=_sound.AttachPlayRequestAudit();int update=0,generation=0,hold=0;bool sawAcid=false,sawOpen=false,protectedAttack=false;
            var attacks=new HashSet<int>();var children=new HashSet<int>();bool capture=false,sawGrab=false,sawThrow=false;
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.UpdateGameplay(0,0,0xff,++update);rom.AdvanceTileGraphics();rom.AdvanceScreenShake();rom.AdvanceCameraPosition();
                FailIf(actor.SubId!=rom[0xd082]||actor.State!=rom[0xd084]||actor.Substate!=rom[0xd085]||actor.Counter!=rom[0xd086]||actor.Counter2!=rom[0xd087]||
                    actor.Angle!=rom[0xd089]||actor.Speed!=rom[0xd090]||actor.Position!=new Vector2(rom.Word(0xd08c)/256f,rom.Word(0xd08a)/256f)||
                    actor.ZFixed!=unchecked((short)rom.Word(0xd08e))||actor.SpeedZ!=unchecked((short)rom.Word(0xd094))||
                    actor.Health!=rom[0xd0a9]||(actor.InvincibilityCounter&255)!=rom[0xd0ab]||
                    actor.TurtleHealth!=rom[0xd0b0]||actor.SpiderHealth!=rom[0xd0b1]||actor.BeeHealth!=rom[0xd0b2]||
                    actor.TurtleHit!=rom[0xd0b3]||actor.JumpCount!=rom[0xd0b4]||actor.TransformIndex!=rom[0xd0b5]||
                    actor.Target!=new Vector2I(rom[0xd0b7],rom[0xd0b6])||actor.CollisionMode!=rom[0xd0a5]||
                    actor.Attack!=rom[0xd083]||actor.WebSignal!=rom[0xd0b8]||actor.Corner!=rom[0xd0b9]||
                    SomariaPrivate<int>(actor.Animation,"_frameCounter")!=rom[0xd0a0]||actor.AnimationParameter!=rom[0xd0a1],
                    $"Final Veran update {update}, RNG=${rng:x2}, batch={batched}: port={actor.SubId}:{actor.State}:{actor.Substate}/{actor.Counter},{actor.Counter2}/{actor.Position}/{actor.Speed},{actor.Angle}/{actor.ZFixed},{actor.SpeedZ}/HP{actor.Health},inv{actor.InvincibilityCounter}/mode{actor.CollisionMode:x2}/anim{SomariaPrivate<int>(actor.Animation,"_frameCounter")},{actor.AnimationParameter}; "+
                    $"ROM={rom[0xd082]}:{rom[0xd084]}:{rom[0xd085]}/{rom[0xd086]},{rom[0xd087]}/{rom.Word(0xd08c)/256f},{rom.Word(0xd08a)/256f}/{rom[0xd090]},{rom[0xd089]}/{unchecked((short)rom.Word(0xd08e))},{unchecked((short)rom.Word(0xd094))}/HP{rom[0xd0a9]},inv{rom[0xd0ab]}/mode{rom[0xd0a5]:x2}/anim{rom[0xd0a0]},{rom[0xd0a1]}.");
                foreach(var acid in _entities.EntityAdapters<VeranAcidPoolRoomEntity>())
                {
                    int slot=Enumerable.Range(0,16).Single(index=>ReferenceEquals(_entities.ResolvePartSlot(index),acid));int at=0xd0c0+slot*256;
                    FailIf(rom[at+1]!=0x57||acid.State!=rom[at+4]||acid.Counter!=rom[at+6]||acid.Packed!=rom[at+0x30],
                        $"Acid PART $57 slot ${slot:x2} update {update}: port={acid.State}/{acid.Counter}/${acid.Packed:x2}, ROM={rom[at+4]}/{rom[at+6]}/${rom[at+0x30]:x2}.");sawAcid=true;
                }
                foreach(var web in _entities.EntityAdapters<VeranSpiderwebRoomEntity>())
                {
                    int at=0xd0c0+web.Slot*256;children.Add(0x5600+web.SubId);
                    FailIf(web.SubId!=rom[at+2]||web.State!=rom[at+4]||web.Counter!=rom[at+6]||web.Counter2!=rom[at+7]||
                        web.Angle!=rom[at+9]||web.Speed!=rom[at+16]||web.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                        web.ZFixed!=unchecked((short)rom.Word(at+14))||web.Visible!=((rom[at+0x1a]&128)!=0),
                        $"Web $56:{web.SubId:x2} slot ${web.Slot:x2} update {update}: port={web.State}/{web.Counter},{web.Counter2}/{web.Position}/{web.Angle},{web.Speed}/{web.ZFixed}; ROM={rom[at+4]}/{rom[at+6]},{rom[at+7]}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}/{rom[at+9]},{rom[at+16]}/{unchecked((short)rom.Word(at+14))}.");
                }
                var parts=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots");
                foreach(var shot in _entities.EntityAdapters<VeranBeeProjectileRoomEntity>())
                {
                    int at=0xd0c0+parts[shot]*256;children.Add(0x58);
                    FailIf(shot.State!=rom[at+4]||shot.Counter!=rom[at+6]||shot.Angle!=rom[at+9]||shot.Speed!=rom[at+16]||
                        shot.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f),$"Bee PART $58 update {update}: native state/counter/XY mismatch.");
                }
                var enemies=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
                foreach(var child in _entities.EntityAdapters<VeranChildBeeRoomEntity>())
                {
                    int at=0xd080+enemies[child]*256;var bee=child.Actor;children.Add(0x1f);
                    FailIf(bee.SubId!=rom[at+2]||bee.State!=rom[at+4]||bee.Counter!=rom[at+6]||bee.Angle!=rom[at+9]||bee.Speed!=rom[at+16]||
                        bee.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f),$"Child bee $1f:{bee.SubId:x2} update {update}: port={bee.State}/{bee.Counter}/{bee.Position}/{bee.Angle}, ROM={rom[at+4]}/{rom[at+6]}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}/{rom[at+9]}.");
                }
                int nativeParts=Enumerable.Range(0,16).Count(slot=>rom[0xd0c0+slot*256]!=0&&rom[0xd0c1+slot*256] is 0x56 or 0x58);
                int nativeBees=Enumerable.Range(0,16).Count(slot=>rom[0xd080+slot*256]!=0&&rom[0xd081+slot*256]==0x1f);
                FailIf(_entities.EntityAdapters<VeranSpiderwebRoomEntity>().Count()+_entities.EntityAdapters<VeranBeeProjectileRoomEntity>().Count()!=nativeParts||
                    _entities.EntityAdapters<VeranChildBeeRoomEntity>().Count()!=nativeBees,$"Veran attack object counts differ at update {update}.");
                for(int packed=0;packed<0xb0;packed++)
                {
                    if((packed&15)==15)continue;Vector2 point=new((packed&15)*16+8,(packed&0xf0)+8);
                    FailIf(_currentRoom.GetMetatile(point)!=rom[0xcf00+packed],$"Veran acid logical tile ${packed:x2} differs at update {update}: port=${_currentRoom.GetMetatile(point):x2}, ROM=${rom[0xcf00+packed]:x2}, collision={_currentRoom.GetTerrainInfo(point).Collision:x2}/{rom[0xce00+packed]:x2}; pools={string.Join(',',_entities.EntityAdapters<VeranAcidPoolRoomEntity>().Select(p=>$"{p.State}/{p.Counter}/{p.Packed:x2}"))}.");
                }
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls)||
                    !sounds.Requests.SequenceEqual(rom.Sounds)||_entities.RoomEnemyCount!=rom[0xcdd1]||
                    _runtimeState.ReadWramByte(WramAddress.wDisabledObjects)!=rom[WramAddress.wDisabledObjects]||
                    _runtimeState.ReadWramByte(WramAddress.wMenuDisabled)!=rom[WramAddress.wMenuDisabled]||
                    _player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||_player.HealthQuarters!=rom[0xc6aa],
                    $"Final Veran shared state update {update}: RNG {_random.CaptureState().Calls-seed.Calls}/{rom.RandomCalls}, count {_entities.RoomEnemyCount}/{rom[0xcdd1]}, SFX {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                if(generation!=rom.TextGeneration)
                {FailIf(rom.Word(0xcba2)!=0x5a14||!_dialogue.IsOpen,"Final battle introduction must show source TX_5614.");generation=rom.TextGeneration;hold=3;}
                if(hold>0&&--hold==0){_dialogue.Close();rom[0xcba0]=0;}
                sawOpen|=actor.State==7;
                if(actor.SubId==1&&actor.State==4)attacks.Add(actor.Attack);
                if(protectedAttack){_player.ApplyInteractionInvincibility(128);rom[0xd02b]=0x80;}
                if(capture)
                {
                    FailIf(_player.EnemyGrabActive!=(rom[0xd004]==0x0d)||_player.EnemyGrabActive&&_player.EnemyGrabSubstate!=rom[0xd005]||
                        _player.EnemyContactZ!=unchecked((sbyte)rom[0xd00f]),
                        $"Spider grab Link update {update}: port={_player.EnemyGrabActive}/{_player.EnemyGrabSubstate}/Z{_player.EnemyContactZ}, ROM={rom[0xd004]:x2}/{rom[0xd005]}/Z{unchecked((sbyte)rom[0xd00f])}.");
                    sawGrab|=_player.EnemyGrabActive;sawThrow|=_player.EnemyGrabSubstate==3;
                }
            }
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            while(update<1200&&!(actor.State==7&&actor.InvincibilityCounter==0))Step(4);
            FailIf(!sawAcid||!sawOpen||actor.Health!=20||actor.SpiderHealth!=8||actor.BeeHealth!=15,
                "Unlinked turtle introduction, jumps, acid terrain and vulnerable face must retain source health20/8/15.");
            var spawns=new List<RoomEntitySpawn>();rom.HitEnemyWithItem(0,4,1);boss.SetLinkSwordState(SwordActionState.Swing,1);
            FailIf(!boss.ApplySwordHit(actor.CollisionBounds,actor.Position-Vector2.Right,1,EnemyKnockbackStrength.Low,spawns),"Open turtle must accept source effect$21 damage.");
            while(update<2200&&actor.SubId==0)Step(1);
            FailIf(actor.SubId==0||actor.TurtleHealth!=19||actor.TransformIndex!=1,
                "A struck turtle must transform within its four source probability steps, preserving health19 in the same enemy slot.");
            forms.Add(actor.SubId);
            int transformed=actor.SubId;
            for(int wait=0;wait<100&&actor.State!=2;wait+=4)Step(4);
            FailIf(actor.State!=2||actor.Health!=(transformed==1?8:15),"The selected form must initialize its own source health and collision state in the retained boss slot.");
            protectedAttack=true;_player.ApplyInteractionInvincibility(128);rom[0xd02b]=0x80;Step(1600);
            for(int wait=0;wait<3000&&actor.State!=2;wait+=4)Step(4);
            FailIf(actor.State!=2||transformed==1&&(attacks.Count!=3||Enumerable.Range(0,4).Any(sub=>!children.Contains(0x5600+sub)))||transformed==2&&(!children.Contains(0x58)||!children.Contains(0x1f)),
                $"Transformed attacks must exercise webs/rush or bee projectiles/children: form={transformed}, attacks={string.Join(',',attacks)}, children={string.Join(',',children.Select(id=>id.ToString("x")))}.");
            if(transformed==1)
            {
                // Start outside the body, on the arena's real floor, then let
                // the native web travel, catch Link, return and pound him.
                for(int wait=0;wait<4000&&!capture;wait++)
                {
                    Vector2 target=actor.Position.Floor()+new Vector2(0,40);
                    if(actor.State==4&&actor.Attack==2&&actor.Substate==0&&target.Y<152&&!_collision.Collides(target))
                    {
                        _player.WarpTo(target);rom.Word(0xd00a,(int)target.Y<<8);rom.Word(0xd00c,(int)target.X<<8);
                        _player.ApplyInteractionInvincibility(0);rom[0xd02b]=0;
                        _transitions.ResetCamera();Vector2 c=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)c.Y;rom[0xffac]=(byte)c.X;
                        capture=true;protectedAttack=false;
                    }
                    Step(1);
                }
                FailIf(!capture,"The source spider fixture must reach a web attack above open arena floor.");
                for(int wait=0;wait<300&&!(sawGrab&&actor.State==2);wait++)Step(1);
                FailIf(!sawGrab||actor.State!=2||!_player.EnemyGrabActive,"The returning web must hand Link to the spider's full pound sequence.");
                rom.HitEnemyWithItem(0,0x18,0);
                FailIf(!boss.ApplyItemCollision((RoomEntityItemCollision)0x18,actor.CollisionBounds,actor.Position-Vector2.Right,0,spawns),
                    "Bomb contact must knock the armored spider into its vulnerable state without damaging it.");
                Step(1);while(actor.InvincibilityCounter!=0)Step(1);
                FailIf(actor.State!=3||actor.CollisionMode!=0x6b||actor.Health!=8,"Bomb recoil must expose the spider's source collision mode $6b.");
            }
            protectedAttack=false;
            rom.HitEnemyWithItem(0,4,actor.Health);
            FailIf(!boss.ApplySwordHit(actor.CollisionBounds,actor.Position-Vector2.Right,actor.Health,EnemyKnockbackStrength.Low,spawns),
                "Vulnerable transformed Veran must accept the source terminal sword hit.");
            for(int wait=0;wait<180&&!(actor.SubId==0&&actor.State==8);wait++)Step(1);
            FailIf(actor.SubId!=0||actor.State!=8||actor.Health!=19||actor.TurtleHit!=0||actor.JumpCount!=0||boss.Slot!=0,
                "Defeating a temporary form must restore turtle health19, clear its jump/hit flags and close the face in the same slot.");
            if(capture)
            {
                Step(40);FailIf(!sawThrow||_player.EnemyGrabActive,"Interrupting the spider must throw Link for the source 30 updates and return control.");
            }
            LoadValidationRoom(4,0xd5);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.EntityAdapters<VeranFinalRoomEntity>().Any()||_entities.EntityAdapters<VeranAcidPoolRoomEntity>().Any(),
                "Room replacement must retire the final boss and its terrain parts.");
        }
        FailIf(forms.Count!=2,$"The two source RNG fixtures must exercise both turtle transformation choices; got {string.Join(',',forms)}.");
    }
}
