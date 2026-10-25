using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateGiantGhiniContactRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool mash in new[]{false,true})
        foreach(bool cancel in new[]{false,true})
        foreach(int item in new[]{TreasureId.Sword,TreasureId.Shovel})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x18);_entities.Clear();
            _inventory.GiveTreasure(item,item==TreasureId.Sword?1:0);_inventory.EquipA(item);_inventory.EquipB(0);
            _player.WarpTo(new(160,64));_player.Face(Vector2I.Left);_transitions.ResetCamera();
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(AgesWramAddress.wScrollMode,0);
            FailIf(_collision.Collides(_player.Position)||_collision.Collides(new(144,64)),
                "Giant Ghini contact must approach its child across the room's actual clear floor.");
            var data=new DungeonBossDatabase();var parent=new GiantGhiniBoss();
            parent.Initialize(data.Enemy(0x70),_currentRoom,new(120,64),_random,_sound.PlaySound,
                ()=>false,_entities.DisableLinkCollisionsAndMenu,()=>{},_entities.ToScreen);
            F1Set(parent,"_initialized",true);F1Set(parent,"_state",GiantGhiniBossBossState.Moving);
            F1Set(parent,"_angle",24);F1Set(parent,"_targetScreenY",104);F1Set(parent,"_childRespawnCounter",180);F1Set(parent,"_childrenAlive",1);
            // Declared body collision gate isolates child contact from boss damage;
            // body steering, charge and its physical earlier ENEMY slot remain live.
            parent.GaleCollisionDisabled=true;
            var body=new GiantGhiniBossRoomEntity(parent,Vector2I.Zero);F1Set(body,"_initialized",true);
            _entities.RegisterEnemySlot(body,0);_entities.AddEntity(body);
            var child=new GiantGhiniChild();child.Initialize(data.Enemy(0x3f),parent,_currentRoom,0,_random);
            var adapter=new GiantGhiniChildRoomEntity(child,_sound.PlaySound);
            _entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,3,160,64)
                {CompanionDispatchEnabled=true,HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom.InitializeLinkWalkingAnimation();rom[0xcc08]=0xad;
            rom[0xcd16]=0;rom[0xcd17]=0xd0; // Actual Link camera-focus pointer.
            rom[AgesWramAddress.wWarpsDisabled]=1;rom[AgesWramAddress.wScrollMode]=0;
            Vector2 camera=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            const int bossAt=0xd080,at=0xd180;
            rom[bossAt]=1;rom[bossAt+1]=0x70;rom[bossAt+4]=9;rom[bossAt+6]=2;rom[bossAt+7]=180;
            rom[bossAt+9]=24;rom[bossAt+11]=64;rom[bossAt+13]=120;rom[bossAt+16]=0x1e;
            rom[bossAt+0x29]=(byte)data.Enemy(0x70).Health;rom[bossAt+0x30]=1;rom[bossAt+0x33]=16;rom[bossAt+0x34]=104;
            rom.Word(bossAt+14,0xf800);rom.Word(bossAt+0x14,0x40);rom.SetEnemyAnimation(0,0);
            rom[at]=1;rom[at+1]=0x3f;rom[at+2]=3;rom[at+11]=64;rom[at+13]=120;rom[at+0x17]=0xd0;
            rom[0xcdd1]=2;
            var sounds=_sound.AttachPlayRequestAudit();int update=0,pressed=0,held=0,angle=255;
            bool sawMove=false,sawStop=false;
            void Compare()
            {
                rom.UpdateGameplay(pressed,held,angle,_entities.FrameCounter);rom.AdvanceTileGraphics();rom.AdvanceScreenShake();rom.AdvanceCameraPosition();update++;
                int state=child.InitializationPending?0:child.State switch
                {ChildState.Waiting=>8,ChildState.SpawnDelay=>9,ChildState.Charging=>10,ChildState.Attached=>11,_=>12};
                int bossState=parent.State==GiantGhiniBossBossState.Moving?9:10;
                FailIf(-_entities.ToScreen(Vector2.Zero)!=new Vector2(rom[0xffac],rom[0xffaa]),
                    $"Giant contact/update{update}: post-object camera catch-up differs.");
                bool shovelParent=Enumerable.Range(0xd2,4).Any(page=>rom[page<<8]!=0&&rom[(page<<8)+1]==0x15);
                bool shovelChild=Enumerable.Range(0xd6,10).Any(page=>rom[page<<8]!=0&&rom[(page<<8)+1]==0x15);
                bool swordParent=Enumerable.Range(0xd2,4).Any(page=>rom[page<<8]!=0&&rom[(page<<8)+1]==5);
                FailIf((_player.SwordState!=SwordActionState.None)!=swordParent,
                    $"Giant contact/item${item:x2}/update{update}: active sword parent differs {_player.SwordState}/{swordParent}.");
                FailIf(_player.IsUsingShovel!=shovelParent||_player.ShovelChildActive!=shovelChild,
                    $"Giant contact/item${item:x2}/update{update}/mash{mash}: active shovel parent/child differs {_player.IsUsingShovel}/{_player.ShovelChildActive} vs {shovelParent}/{shovelChild}, parentFrame{_player.ShovelFrame}, childC1{_shovel.ChildCounter}, native parents[{string.Join(';',Enumerable.Range(0xd2,4).Select(page=>$"{page:x2}:{rom[page<<8]}/{rom[(page<<8)+1]:x2}/{rom[(page<<8)+0x20]}/{rom[(page<<8)+0x21]}"))}], children[{string.Join(';',Enumerable.Range(0xd6,10).Where(page=>rom[page<<8]!=0).Select(page=>$"{page:x2}:{rom[(page<<8)+1]:x2}/{rom[(page<<8)+6]}"))}].");
                FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||
                    _player.HealthQuarters!=rom[0xc6aa]||_entities.PlayerSwordDisabled!=((rom[0xccd8]&32)!=0)||
                    _entities.PlayerMovementDisabled!=((rom[0xcc61]&32)!=0)||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Giant contact/update{update}/mash{mash}: Link{_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, HP{_player.HealthQuarters}/{rom[0xc6aa]}, sword/move{_entities.PlayerSwordDisabled}/{_entities.PlayerMovementDisabled}, native{rom[0xccd8]:x2}/{rom[0xcc61]:x2}, sounds[{string.Join(',',sounds.Requests)}]/[{string.Join(',',rom.Sounds)}].");
                FailIf(child.IsDead!=(rom[at]==0)||!child.IsDead&&(state!=rom[at+4]||SomariaPrivate<int>(child,"_counter")!=rom[at+6]||
                    child.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||child.ZHigh!=unchecked((sbyte)rom[at+15])||
                    child.Visible!=((rom[at+0x1a]&128)!=0)||SomariaPrivate<int>(child.Animation,"_frameCounter")!=rom[at+0x20])||
                    bossState!=rom[bossAt+4]||parent.Position!=new Vector2(rom.Word(bossAt+12)/256f,rom.Word(bossAt+10)/256f)||
                    SomariaPrivate<int>(parent,"_angle")!=rom[bossAt+9]||SomariaPrivate<int>(parent,"_nudgeCounter")!=rom[bossAt+6]||
                    SomariaPrivate<bool>(parent,"_childRequestsCharge")!=(rom[bossAt+0x32]!=0)||parent.ChildrenAlive!=rom[bossAt+0x30],
                    $"Giant contact/update{update}: child{state}/{rom[at+4]}, C1{SomariaPrivate<int>(child,"_counter")}/{rom[at+6]}, XY{child.Position}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}, parent{bossState}/{rom[bossAt+4]}, XY{parent.Position}/{rom.Word(bossAt+12)/256f},{rom.Word(bossAt+10)/256f}, angle{SomariaPrivate<int>(parent,"_angle")}/{rom[bossAt+9]}, C1{SomariaPrivate<int>(parent,"_nudgeCounter")}/{rom[bossAt+6]}, target{SomariaPrivate<int>(parent,"_targetScreenY")}/{rom[bossAt+0x34]}, camera{-_entities.ToScreen(Vector2.Zero)}/{rom[0xffac]},{rom[0xffaa]}.");
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls),
                    $"Giant contact/update{update}: shared RNG differs {_random.CaptureState().Calls-seed.Calls}/{rom.RandomCalls}.");
                sawMove|=child.State==ChildState.Attached&&!_entities.PlayerMovementDisabled;sawStop|=_entities.PlayerMovementDisabled;pressed=0;
            }
            void Step(int count,Vector2 movement)=>StepGameplayUpdates(count,movement,MenuRomActions(held),MenuRomActions(pressed),batched,Compare);
            Step(1,Vector2.Zero);held=0x20;angle=24;
            for(int bound=0;bound<12&&!SomariaPrivate<bool>(child,"_linkContactPending");bound++)Step(1,Vector2.Left);
            FailIf(!SomariaPrivate<bool>(child,"_linkContactPending")||child.State!=ChildState.SpawnDelay||parent.State!=GiantGhiniBossBossState.Moving,
                "Reachable spawn-delay contact must leave JUST_HIT for the next child handler, without an early parent charge.");
            held=1;pressed=1;angle=255;Step(1,Vector2.Zero);
            FailIf(child.State!=ChildState.Attached||SomariaPrivate<int>(child,"_counter")!=116||
                parent.State!=GiantGhiniBossBossState.Moving||item==TreasureId.Sword&&_player.SwordState==SwordActionState.None||
                item==TreasureId.Shovel&&!_player.IsUsingShovel,
                "Link must start a sword before the child attaches and subtracts the fresh key, while its earlier parent remains in normal motion.");
            if(mash)
            {
                _dialogue.ShowMessage("Hold before parent consumption.",104);rom[0xcba0]=1;held=0;Step(2,Vector2.Zero);_dialogue.Close();rom[0xcba0]=0;
                FailIf(_entities.PlayerSwordDisabled||_entities.PlayerMovementDisabled,
                    "Text must clear the previous child publication while leaving the item parent frozen.");
            }
            Step(1,Vector2.Zero);
            if(mash)Step(1,Vector2.Zero);
            FailIf(parent.State!=GiantGhiniBossBossState.Charging||_player.SwordState!=SwordActionState.None,
                "The next parent visit must begin charge and Link must consume the prior child's sword restriction.");
            if(cancel)
            {
                LoadValidationRoom(4,0x1e);StepGameplayUpdates(2,Vector2.Zero);
                FailIf(_entities.Entities<GiantGhiniChild>().Any()||_entities.PlayerSwordDisabled||_entities.PlayerMovementDisabled,
                    "Replacing the room while attached must retire the restriction owner and release both signals.");
                continue;
            }
            _dialogue.ShowMessage("Giant child signal hold.",104);rom[0xcba0]=1;held=0;Step(2,Vector2.Zero);_dialogue.Close();rom[0xcba0]=0;
            FailIf(_entities.PlayerSwordDisabled||_entities.PlayerMovementDisabled,"Frozen child dispatch must leave no restrictions after Link clears their prior publication.");
            held=0x10;angle=8;
            // A fresh key can store zero after subtraction; the next DEC wraps
            // it to ff. Allow that original extension, already checked explicitly.
            for(int bound=0;bound<512&&child.State==ChildState.Attached;bound+=4)
            {
                if(mash&&bound%8==0)pressed=0x11;
                Step(4,Vector2.Right);
            }
            FailIf(child.State!=ChildState.Fading||!sawMove||!sawStop||_entities.PlayerSwordDisabled||_entities.PlayerMovementDisabled,
                $"The real loop must alternate attachment restrictions and release them on detachment: item${item:x2}, mash{mash}, state{child.State}, C1{SomariaPrivate<int>(child,"_counter")}, move/stop{sawMove}/{sawStop}.");
            for(int bound=0;bound<64&&!child.IsDead;bound+=2)Step(2,Vector2.Right);
            FailIf(!child.IsDead||parent.ChildrenAlive!=0,"The fading child must finish and release the source parent count.");
            LoadValidationRoom(4,0x1e);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.PlayerSwordDisabled||_entities.PlayerMovementDisabled,"Room replacement must release child restrictions.");
        }
    }
}
