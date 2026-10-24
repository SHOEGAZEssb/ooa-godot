using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGanonSwordCollisionsRom()
    {
        foreach(bool batch in new[]{false,true})
        foreach(int level in new[]{2,3})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(5,0xf5);
            foreach(var enemy in _entities.EntityAdapters<TwinrovaRoomEntity>().ToArray())_entities.RetireEntity(enemy);
            _inventory.GiveTreasure(TreasureId.Sword,level);_inventory.EquipA(0);_inventory.EquipB(TreasureId.Sword);
            _player.WarpTo(new(0x78,0x78));_player.Face(Vector2I.Up);_transitions.ResetCamera();
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,1);_runtimeState.SetWramByte(WramAddress.wDisableLinkCollisionsAndMenu,1);
            var target=_roomEvents.Get<RoomOfRitesBattleEvent>().CreateGanon()!;var actor=target.Actor;
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x78){HostileEnemiesEnabled=true,HostilePartsEnabled=true,CompanionDispatchEnabled=true};
            rom.EnableVramDma();rom.CreateMenuView().LoadHudGraphics();rom[0xd000]=0;rom[0xd004]=0;rom[0xcc8a]=1;rom[0xcbca]=1;rom[0xd080]=1;rom[0xd081]=4;rom[0xcdd1]=1;rom[0xcd17]=0xd0;
            rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter+1);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();StepGameplayUpdates(1,Vector2.Zero);
            actor.World.ClearPalette();rom[0xc4ab]=0;
            Set("State",8);Set("Substate",4);Set("Counter",200);Set("ZFixed",0);rom[0xd084]=8;rom[0xd085]=4;rom[0xd086]=200;rom.Word(0xd08e,0);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,0);_runtimeState.SetWramByte(WramAddress.wDisableLinkCollisionsAndMenu,0);rom[0xcc8a]=rom[0xcbca]=0;
            _player.WarpTo(new(0x78,0x78));_player.Face(Vector2I.Up);_transitions.ResetCamera();rom[0xd004]=1;rom[0xd005]=0;rom.Word(0xd00c,0x7800);rom.Word(0xd00a,0x7800);rom.InitializeLinkWalkingAnimation();
            Vector2 camera=-_transitions.WorldToGameplayScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var sounds=_sound.AttachPlayRequestAudit();int update=0,pressed=0,held=0,angle=0xff;
            FailIf(_collision.Collides(_player.Position),"Ganon sword fixture must start on original collision-safe south floor.");
            held=0x40;angle=0;StepGameplayUpdates(12,Vector2.Up,batched:batch,afterUpdate:Compare);
            FailIf(_player.Position.Y>=0x78||_collision.Collides(_player.Position),"Ganon sword fixture must approach through the original floor geometry.");
            float attackY=_player.Position.Y;
            pressed=held=2;angle=0xff;StepGameplayUpdates(1,Vector2.Zero,held:["item"],pressed:["item"],batched:batch,afterUpdate:Compare);
            pressed=held=0;StepGameplayUpdates(45,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(level==2&&actor.Health!=100||level==3&&actor.Health>=100,
                "Ganon mode$0b must bump an ordinary L2 sword and take damage from an ordinary L3 sword.");
            // Repeat after the original invincibility/recoil windows expire.
            held=0x40;angle=0;
            int approach=0;
            while(_player.Position.Y>attackY&&approach++<50)StepGameplayUpdates(1,Vector2.Up,batched:batch,afterUpdate:Compare);
            FailIf(approach>=50,"Ganon's deflected sword must allow another physical approach after recoil.");
            int previousHealth=actor.Health;
            angle=0xff;
            pressed=held=2;StepGameplayUpdates(1,Vector2.Zero,held:["item"],pressed:["item"],batched:batch,afterUpdate:Compare);
            pressed=held=0;StepGameplayUpdates(45,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(level==3&&actor.Health>=previousHealth,"Ganon must accept another reachable L3 swing after its invincibility ends.");
            _sound.SetRequestObserver(null);
            void Set(string name,int value)=>typeof(GanonCharacter).GetProperty(name,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(actor,value);
            void Compare()
            {
                rom.UpdateGameplay(pressed,held,angle,_entities.FrameCounter);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();update++;
                FailIf(actor.Health!=rom[0xd0a9]||actor.InvincibilityCounter!=unchecked((sbyte)rom[0xd0ab])||
                    _player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                    $"Ganon sword L{level},update{update}: HP={actor.Health}/{rom[0xd0a9]},inv={actor.InvincibilityCounter}/{unchecked((sbyte)rom[0xd0ab])},Link={_player.Position}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}.");
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Ganon sword L{level},update{update}: RNG/audio differs {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
            }
        }
        GD.Print("Validated physical L2/L3 sword approaches, native Ganon damage/bump masks, attacker recoil, deferred effects and repeat swings in split/batched gameplay against the clean ROM.");
    }
}
