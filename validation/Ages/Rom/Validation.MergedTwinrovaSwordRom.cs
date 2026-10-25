using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMergedTwinrovaSwordRom()
    {
        foreach(bool batch in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(5,0xf5);
            var placed=_entities.EntityAdapters<TwinrovaRoomEntity>().Single();var world=placed.Actor.World;_entities.RetireEntity(placed);
            var target=new MergedTwinrovaRoomEntity(new(world,new(0x78,0x48),0));var actor=target.Actor;
            _entities.RegisterEnemySlot(target,0);_entities.AddEntity(target);
            _inventory.GiveTreasure(TreasureId.Sword,2);_inventory.EquipA(0);_inventory.EquipB(TreasureId.Sword);
            _player.WarpTo(new(0x78,0x78));_player.Face(Vector2I.Up);_transitions.ResetCamera();
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x78){HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom.InitializeLinkWalkingAnimation();
            rom[0xd080]=1;rom[0xd081]=1;rom[0xd08b]=0x48;rom[0xd08d]=0x78;rom[0xcdd1]=1;rom[0xcd17]=0xd0;
            rom[0xcc08]=0xdb;rom[0xcc0a]=0xdc;rom[0xcc0c]=0xdd;rom[0xcc0e]=0xde;rom[0xcc10]=0xdf;
            Vector2 camera=-_transitions.WorldToGameplayScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            rom.UpdateGameplay(0,0,255,_entities.FrameCounter+1);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();StepGameplayUpdates(1,Vector2.Zero);
            _dialogue.Close();rom[0xcba0]=0;
            Set("State",0x0b);Set("Counter",200);Set("CollisionType",0x81);Set("SwapDisabled",1);Set("ZFixed",0);
            rom[0xd084]=0x0b;rom[0xd086]=200;rom[0xd0a4]=0x81;rom[0xd0b9]=1;rom.Word(0xd08e,0);
            var sounds=_sound.AttachPlayRequestAudit();int initialSounds=rom.Sounds.Count,update=0,pressed=0,held=64,angle=0;
            FailIf(_collision.Collides(_player.Position),"Twinrova sword fixture must start on original south floor.");
            StepGameplayUpdates(12,Vector2.Up,batched:batch,afterUpdate:Compare);
            FailIf(_player.Position.Y>=0x78||_collision.Collides(_player.Position),"Twinrova's sword hit must approach through original collision geometry.");
            pressed=held=2;angle=255;StepGameplayUpdates(1,Vector2.Zero,held:["item"],pressed:["item"],batched:batch,afterUpdate:Compare);
            pressed=held=0;StepGameplayUpdates(45,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(actor.Health>=20,"Twinrova must receive the physically approached sword hit.");
            _sound.SetRequestObserver(null);
            void Set(string name,int value)=>typeof(MergedTwinrovaCharacter).GetProperty(name,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(actor,value);
            void Compare()
            {
                rom.UpdateGameplay(pressed,held,angle,_entities.FrameCounter);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();update++;
                FailIf(actor.Health!=rom[0xd0a9]||actor.InvincibilityCounter!=unchecked((sbyte)rom[0xd0ab])||
                    _player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                    $"Twinrova sword update{update}: HP={actor.Health}/{rom[0xd0a9]},inv={actor.InvincibilityCounter}/{unchecked((sbyte)rom[0xd0ab])},Link={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}.");
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds.Skip(initialSounds)),
                    $"Twinrova sword update{update}: RNG/audio handoff differs.");
            }
        }
        GD.Print("Validated Twinrova's physical sword approach, native effect$2c damage and attacker recoil through split/batched gameplay against the clean ROM.");
    }
}
