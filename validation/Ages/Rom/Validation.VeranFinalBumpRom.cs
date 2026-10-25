using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateVeranFinalBumpRom()
    {
        // Independent source branches: hook on bee, ordinary/Biggoron swords
        // on armored spider, and wrapped byte geometry. No attack-cycle wait.
        foreach(var spec in new (int Form,int Mode,int Item,bool Wrap)[]{(2,0x6b,13,false),
            (1,0x6c,4,false),(1,0x6c,7,false),(2,0x6b,13,true)})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0xd4);
            var boss=_entities.EntityAdapters<VeranFinalRoomEntity>().Single();var actor=boss.Actor;
            foreach(var other in _entities.EntityAdapters<IRoomEntity>().Where(entity=>entity!=boss).ToArray())_entities.RetireEntity(other);
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(120,136));_transitions.ResetCamera();
            var initial=_random.CaptureState();var rom=new SomariaRom(_saveData,initial,_currentRoom,0,120,136)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xcc08]=0xd7;rom[0xcc0a]=0xd8;rom[0xcc0c]=0xd9;rom[0xcc0e]=0xda;
            rom[0xd080]=1;rom[0xd081]=2;rom[0xd08b]=64;rom[0xd08d]=120;rom[0xcdd1]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            Vector2 camera=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var sounds=_sound.AttachPlayRequestAudit();int update=0;
            void Compare()
            {
                rom.UpdateGameplay(0,0,255,++update);
                FailIf(actor.State!=rom[0xd084]||actor.Counter!=rom[0xd086]||actor.Health!=rom[0xd0a9]||
                    (actor.InvincibilityCounter&255)!=rom[0xd0ab]||actor.KnockbackCounter!=rom[0xd0ad]||
                    actor.JustHit!=((rom[0xd0aa]&128)!=0)||actor.ZFixed!=unchecked((short)rom.Word(0xd08e))||
                    actor.Position!=new Vector2(rom.Word(0xd08c)/256f,rom.Word(0xd08a)/256f)||
                    !sounds.Requests.SequenceEqual(rom.Sounds)||_random.CaptureState().Calls!=initial.Calls+rom.RandomCalls,
                    $"Veran mode${spec.Mode:x2}, item${spec.Item:x2}, wrap{spec.Wrap}, update{update}: state/counter/health/inv/recoil/XY/Z/sound/RNG differs: " +
                    $"{actor.State}/{rom[0xd084]}, {actor.Counter}/{rom[0xd086]}, inv{actor.InvincibilityCounter}/{unchecked((sbyte)rom[0xd0ab])}, recoil{actor.KnockbackCounter}/{rom[0xd0ad]}, XY{actor.Position}/{rom.Word(0xd08c)/256f},{rom.Word(0xd08a)/256f}.");
            }
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            void Set(string name,int value)=>typeof(VeranFinalCharacter).GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(actor,value);
            Set("SubId",spec.Form);Set("CollisionMode",spec.Mode);Set("State",spec.Form==2?9:2);
            Set("Counter",90);Set("Counter2",90);Set("Speed",0);Set("Angle",8);
            rom[0xd082]=(byte)spec.Form;rom[0xd0a5]=(byte)spec.Mode;rom[0xd084]=(byte)actor.State;
            rom[0xd086]=rom[0xd087]=90;rom[0xd090]=0;rom[0xd089]=8;
            actor.Position=new(spec.Wrap?248.75f:120.75f,84.75f);
            rom.Word(0xd08c,(int)(actor.Position.X*256));rom.Word(0xd08a,(int)(actor.Position.Y*256));
            Vector2 origin=new(spec.Wrap?0.75f:119.75f,84.75f);
            void Hit()
            {
                rom.HitEnemyWithItem(0,spec.Item,4,origin:origin);
                FailIf(!boss.ApplyItemCollision((RoomEntityItemCollision)spec.Item,new(origin-new Vector2(6,6),new(12,12)),origin,4,new List<RoomEntitySpawn>()),
                    $"Veran mode${spec.Mode:x2} must accept source item${spec.Item:x2}.");
                int inv=spec.Item==7?-26:-20;
                FailIf(actor.Health!=rom[0xd0a9]||actor.InvincibilityCounter!=inv||rom[0xd0ab]!=unchecked((byte)inv)||
                    actor.KnockbackCounter!=rom[0xd0ad]||!actor.JustHit||rom[0xd0aa]<128,
                    $"Veran blocked item${spec.Item:x2} must publish source inv{inv}, recoil, JUST_HIT and no damage.");
                var clink=_entities.EntityAdapters<SwordBeamClinkRoomEntity>().Single();
                int address=Enumerable.Range(0,16).Select(slot=>0xd040+slot*256).Single(slot=>rom[slot]!=0&&rom[slot+1]==7);
                Vector2 expected=new(spec.Wrap?252:119,84);
                FailIf(clink.Node.Position!=expected||new Vector2(rom[address+13],rom[address+11])!=expected,
                    $"Clink item${spec.Item:x2} must use high-byte signed/wrapped midpoint {expected}.");
            }
            Hit();StepGameplayUpdates(spec.Item==7?16:20,Vector2.Zero,batched:spec.Wrap,afterUpdate:Compare);
            if(spec.Item!=7)
            {
                FailIf(actor.InvincibilityCounter!=0,"Veran's blocked-hit invincibility must expire on update20.");
                Hit();StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            }
            LoadValidationRoom(4,0xd5);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.EntityAdapters<VeranFinalRoomEntity>().Any()||_entities.EntityAdapters<SwordBeamClinkRoomEntity>().Any(),
                "Room replacement must retire blocked Veran and its clink.");
        }
    }
}
