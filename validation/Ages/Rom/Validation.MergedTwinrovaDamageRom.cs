using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMergedTwinrovaDamageRom()
    {
        foreach(bool batched in new[] {false,true})
        foreach(bool critical in new[] {false,true})
        foreach(int seedItem in new[] {ItemId.EmberSeed,ItemId.GaleSeed})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(5,0xf5);
            var placed=_entities.EntityAdapters<TwinrovaRoomEntity>().Single();var world=placed.Actor.World;_entities.RetireEntity(placed);
            var target=new MergedTwinrovaRoomEntity(new(world,new(0x78,0x48),0));var actor=target.Actor;
            _entities.RegisterEnemySlot(target,0);_entities.AddEntity(target);
            _player.WarpTo(new(0x78,0x88));_player.Face(Vector2I.Up);_transitions.ResetCamera();_inventory.EquipA(0);_inventory.EquipB(0);
            var seedState=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seedState,_currentRoom,0,0x78,0x88)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            rom.InitializeLinkGameplay();rom.InitializeLinkWalkingAnimation();
            rom[0xd080]=1;rom[0xd081]=1;rom[0xd08b]=0x48;rom[0xd08d]=0x78;rom[0xcdd1]=1;rom[0xcd17]=0xd0;
            rom[0xcc08]=0xdb;rom[0xcc0a]=0xdc;rom[0xcc0c]=0xdd;rom[0xcc0e]=0xde;rom[0xcc10]=0xdf;
            Vector2 camera=-_transitions.WorldToGameplayScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            int cutscene=0,update=0,generation=0,hold=0;
            _entities.TwinrovaCutscene=value=>cutscene=value;
            var sounds=_sound.AttachPlayRequestAudit();
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            Step(1);_dialogue.Close();rom[0xcba0]=0;
            // A source waiting state isolates hit publication from attack AI.
            Set("State",0x0b);Set("Counter",200);Set("CollisionType",0x81);
            rom[0xd084]=0x0b;rom[0xd086]=200;rom[0xd0a4]=0x81;
            if(critical){Set("BossHealth",1);rom[0xd0b4]=1;}
            FailIf(!new SeedSatchelDatabase().TryGet(seedItem,out var seed),"Missing Twinrova seed fixture.");
            var spawns=new List<RoomEntitySpawn>();
            // Seeds publish their harmless contact while ordinary health is
            // intact; they must not reduce the separate five-hit counter.
            rom.HitEnemyWithItem(0,(seed.Collision&127),-(sbyte)seed.Damage,seedItem);
            FailIf(!target.ApplySeedCollision(actor.CollisionBounds,actor.Position-Vector2.Right,seed,(seed.Collision&127),spawns).Contact,
                "Merged Twinrova rejected its source initial seed collision.");
            Step(1);
            FailIf(actor.BossHealth!=(critical?1:5),"Seeds must not damage Twinrova outside the vulnerable window.");
            rom.HitEnemyWithItem(0,5,20,ItemId.Sword);
            target.SetLinkSwordState(SwordActionState.Swing,2,5);
            FailIf(!target.ApplySwordHit(actor.CollisionBounds,actor.Position-Vector2.Right,20,EnemyKnockbackStrength.None,spawns),
                "Merged Twinrova rejected the source sword-health depletion.");
            Step(2);
            FailIf(actor.Vulnerability!=120 || actor.Health!=20 || actor.CollisionType!=255,
                "Sword health depletion must restore HP20, open exactly120 updates and select collisionType$ff.");
            // Pending collisions retain their publication through category
            // freezes; the invincibility counter also stays frozen there.
            Step(31);
            rom.HitEnemyWithItem(0,(seed.Collision&127),-(sbyte)seed.Damage,seedItem);
            FailIf(!target.ApplySeedCollision(actor.CollisionBounds,actor.Position-Vector2.Right,seed,(seed.Collision&127),spawns).Contact,
                "Vulnerable Twinrova rejected the source seed hit.");
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,2);rom[0xcc8a]=2;Step(3);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,0);rom[0xcc8a]=0;Step(1);
            if(critical)
            {
                while(cutscene==0&&update<400)Step(1);
                FailIf(cutscene!=0x19||rom[0xcc04]!=0x19||actor.Substate!=3||actor.BossHealth!=0,
                    "Merged Twinrova death must finish its explosions/text and publish CUTSCENE$19.");
            }
            else
            {
                Step(actor.Vulnerability);
                FailIf(actor.Vulnerability!=0||actor.State!=0x0a||actor.BossHealth!=4,
                    "Noncritical vulnerable hit must consume one seed hit and resume the native room swap at expiry.");
            }
            _sound.SetRequestObserver(null);
            void Set(string name,int value)=>typeof(MergedTwinrovaCharacter).GetProperty(name,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(actor,value);
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();update++;
                FailIf(actor.Health!=rom[0xd0a9]||actor.BossHealth!=rom[0xd0b4]||actor.Vulnerability!=rom[0xd0ba]||
                    actor.InvincibilityCounter!=unchecked((sbyte)rom[0xd0ab])||actor.State!=rom[0xd084]||actor.Substate!=rom[0xd085]||
                    actor.Counter!=rom[0xd086]||actor.Palette!=rom[0xd09b]||actor.CollisionType!=rom[0xd0a4]||actor.Visible!=((rom[0xd09a]&128)!=0),
                    $"Merged damage update{update}: HP={actor.Health}/{rom[0xd0a9]},bossHP={actor.BossHealth}/{rom[0xd0b4]},vuln={actor.Vulnerability}/{rom[0xd0ba]},inv={actor.InvincibilityCounter}/{unchecked((sbyte)rom[0xd0ab])},state={actor.State:x2}:{actor.Substate}/{rom[0xd084]:x2}:{rom[0xd085]},count={actor.Counter}/{rom[0xd086]},visible={actor.Visible}/{rom[0xd09a]:x2}.");
                FailIf(RandomDiffers(seedState,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds)||
                    _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects)!=rom[0xcc8a]||_runtimeState.ReadWramByte(0xcbca)!=rom[0xcbca],
                    $"Merged damage update{update}: native RNG/sounds/control differs; audio={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                if(generation!=rom.TextGeneration){generation=rom.TextGeneration;hold=3;}
                if(hold!=0&&--hold==0){_dialogue.Close();rom[0xcba0]=0;}
                _player.ApplyInteractionInvincibility(128);rom[0xd02b]=128;
            }
        }
        GD.Print("Validated merged Twinrova sword health, seed-vulnerability/expiry, pending-hit freezes and death-to-sacrifice handoff against the clean ROM.");
    }
}
