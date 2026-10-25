using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateGanonCombatRom()
    {
        FailIf(ValidationRom.LoadCleanUs().Span[4]!=0x30,"Ganon split-child null related pointer must read the clean ROM's $0004 JR NC opcode.");
        var animationCounter=typeof(EnemyAnimationPlayer).GetField("_frameCounter",BindingFlags.NonPublic|BindingFlags.Instance)!;
        foreach(bool batched in new[]{false,true})
        foreach(int attack in new[]{8,9,0x0a,0x0b,0x0c,0x0d})
        foreach(int linkX in attack==0x0a?new[]{0x58,0x98}:new[]{0x78})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(5,0xf5);
            foreach(var twins in _entities.EntityAdapters<TwinrovaRoomEntity>().ToArray())_entities.RetireEntity(twins);
            _player.WarpTo(new(0x78,0x78));_player.Face(Vector2I.Up);_transitions.ResetCamera();_inventory.EquipA(0);_inventory.EquipB(0);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,1);_runtimeState.SetWramByte(AgesWramAddress.wDisableLinkCollisionsAndMenu,1);
            var target=_roomEvents.Get<RoomOfRitesBattleEvent>().CreateGanon()!;var a=target.Actor;
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x78){HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false,CompanionDispatchEnabled=true,NativeGameKeysEnabled=attack==0x0c};
            rom.EnableVramDma();rom.CreateMenuView().LoadHudGraphics();rom[0xd000]=0;rom[0xd004]=0;rom[0xcc8a]=1;rom[0xcbca]=1;
            rom[0xd080]=1;rom[0xd081]=4;rom[0xcdd1]=1;rom[0xcd17]=0xd0;
            rom.AdvanceWarpPalette();rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter+1);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();
            StepGameplayUpdates(1,Vector2.Zero);
            // Stage one independently traced attack rather than replaying the
            // revival for each case. Source choice1's next byte is its zero
            // terminator at $5770; the actual next random choice still runs.
            a.World.ClearPalette();rom[0xc4ab]=0;
            Set("State",attack);Set("Counter",0);Set("AttackChoice",1);Set("SequenceIndex",1);Set("ZFixed",0);rom.Word(0xd08e,0);
            a.Animation.SetAnimation(2);rom[0xd084]=(byte)attack;rom[0xd086]=0;rom.Word(0xd0b5,0x5770);
            _player.WarpTo(new(linkX,0x88));_player.Face(Vector2I.Up);_transitions.ResetCamera();
            rom.Word(0xd00c,linkX<<8);rom.Word(0xd00a,0x8800);
            Vector2 camera=-_transitions.WorldToGameplayScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            // enemySetAnimation requires its native caller; anim0 remains a
            // held pose in this staged fixture until the attack selects art.
            a.Animation.SetAnimation(0);
            if(attack==0x0c){_runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,0);rom[0xcc8a]=0;}
            var sounds=_sound.AttachPlayRequestAudit();int update=0,previousSubstate=0;bool ended=false,sawSplit=false,sawTrident=false;
            int keys=0,linkAngle=255;
            do
            {
                Vector2 movement=Vector2.Zero;keys=0;linkAngle=255;
                if(attack==0x0c&&a.Substate is 7 or 8)
                {
                    int direction=(update/6)&3;
                    movement=direction switch{0=>Vector2.Up,1=>Vector2.Down,2=>Vector2.Left,_=>Vector2.Right};
                    keys=direction switch{0=>64,1=>128,2=>32,_=>16};linkAngle=direction switch{0=>0,1=>16,2=>24,_=>8};
                }
                string[] held=keys switch{64=>["move_up"],128=>["move_down"],32=>["move_left"],16=>["move_right"],_=>[]};
                StepGameplayUpdates(3,movement,held:held,batched:batched,afterUpdate:Compare);
            }while(!ended&&update<1600);
            _sound.SetRequestObserver(null);
            FailIf(!ended||attack==9&&!sawSplit||attack==0x0a&&!sawTrident,"Ganon combat fixture missed its source attack/child handoff or completion.");

            void Set(string name,int value)=>typeof(GanonCharacter).GetProperty(name,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(a,value);
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.UpdateGameplay(0,keys,linkAngle,_entities.FrameCounter);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();update++;
                ended|=previousSubstate>0&&a.Substate==0;previousSubstate=a.Substate;
                if(attack==0x0c)
                {
                    FailIf(a.Palette!=(rom[0xd09b]&7)||_runtimeState.ReadWramByte(0xcbc3)!=rom[0xcbc3]||
                        _runtimeState.ReadWramByte(0xcbe3)!=rom[0xcbe3]||_transitions.NativePaletteThreadMode!=rom[0xc4ab],
                        $"Ganon reverse update{update}: palette/input/fade handoff differs.");
                    if(a.Substate is 6 or 10)CompareTwinrovaArena(rom,$"Ganon reverse substate${a.Substate:x2}");
                    FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                        $"Ganon reverse update{update}: Link movement={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}.");
                }
                FailIf(a.State!=rom[0xd084]||a.Substate!=rom[0xd085]||a.Counter!=rom[0xd086]||a.Counter2!=rom[0xd087]||a.Angle!=rom[0xd089]||a.Speed!=rom[0xd090]||
                    a.BaseX!=rom[0xd0b0]||a.SlashSide!=rom[0xd0b2]||a.CollisionType!=rom[0xd0a4]||a.ZFixed!=unchecked((short)rom.Word(0xd08e))||a.SpeedZ!=unchecked((short)rom.Word(0xd094))||
                    a.Position!=new Vector2(rom.Word(0xd08c)/256f,rom.Word(0xd08a)/256f)||a.Visible!=((rom[0xd09a]&128)!=0)||
                    (int)animationCounter.GetValue(a.Animation)! !=rom[0xd0a0]||a.AnimationParameter!=rom[0xd0a1],
                    $"Ganon attack${attack:x2},update{update}: state={a.State:x2}:{a.Substate}/{rom[0xd084]:x2}:{rom[0xd085]},counter={a.Counter}/{rom[0xd086]},pos={a.Position}/{rom.Word(0xd08c)/256f},{rom.Word(0xd08a)/256f},angle={a.Angle}/{rom[0xd089]},anim={animationCounter.GetValue(a.Animation)}/{rom[0xd0a0]}.");
                for(int slot=0;slot<16;slot++)
                {
                    int at=0xd0c0+slot*256;var part=_entities.ResolvePartSlot(slot) as GanonAttackPart;
                    FailIf((part is not null)!=(rom[at]!=0&&rom[at+1] is 0x50 or 0x51 or 0x52),$"Ganon attack${attack:x2},update{update}: PART slot${slot:x2} admission/lifetime differs.");
                    if(part is null)continue;
                    sawSplit|=part.Var03==1;sawTrident|=part.Id==0x50;
                    FailIf(part.Id!=rom[at+1]||part.Subid!=rom[at+2]||part.Var03!=rom[at+3]||part.State!=rom[at+4]||part.Counter!=rom[at+6]||
                        part.Angle!=rom[at+9]||part.Speed!=rom[at+16]||part.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                        part.ZFixed!=unchecked((short)rom.Word(at+14))||part.Visible!=((rom[at+0x1a]&128)!=0)||
                        part.CollisionBounds.Size!=new Vector2(rom[at+0x27]*2,rom[at+0x26]*2)||
                        (int)animationCounter.GetValue(part.Animation)! !=rom[at+0x20]||part.AnimationParameter!=rom[at+0x21],
                        $"Ganon attack${attack:x2},update{update},PART${part.Id:x2} slot${slot:x2}: state={part.State}/{rom[at+4]},counter={part.Counter}/{rom[at+6]},angle={part.Angle}/{rom[at+9]},pos={part.Position}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f},anim={animationCounter.GetValue(part.Animation)}/{rom[at+0x20]}.");
                }
                FailIf(_player.CollapsedActive!=(rom[0xd004]==0x14)||_player.CollapsedActive&&(_player.CollapseSubstate!=rom[0xd005]||_player.CollapseSubstate==1&&_player.CollapseCounter!=rom[0xd006]),
                    $"Ganon shockwave update{update}: collapse={_player.CollapsedActive}:{_player.CollapseSubstate}:{_player.CollapseCounter}/{rom[0xd004]:x2}:{rom[0xd005]}:{rom[0xd006]}.");
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Ganon attack${attack:x2},update{update}: RNG/audio differs {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                _player.ApplyInteractionInvincibility(128);rom[0xd02b]=128;
            }
        }
        GD.Print("Validated Ganon teleports, triple volley, splitting shots, both trident-lunge sides, attack-sequence continuation and native slot/timer/RNG/audio handoffs against the clean ROM.");
    }
}
