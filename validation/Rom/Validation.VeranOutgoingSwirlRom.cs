using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVeranOutgoingSwirlRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0xd4);
            var boss=_entities.EntityAdapters<VeranFinalRoomEntity>().Single();var actor=boss.Actor;
            foreach(var other in _entities.EntityAdapters<IRoomEntity>().Where(e=>e!=boss).ToArray())_entities.RetireEntity(other);
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(120,136));_transitions.ResetCamera();
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,120,136)
                {CompanionDispatchEnabled=true,HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xcc08]=0xd7;rom[0xcc0a]=0xd8;rom[0xcc0c]=0xd9;rom[0xcc0e]=0xda;
            rom[0xd080]=1;rom[0xd081]=2;rom[0xd08b]=64;rom[0xd08d]=120;rom[0xcdd1]=1;
            rom[0xcd16]=0;rom[0xcd17]=0xd0;Vector2 camera=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            int update=0,generation=0,hold=0;var sounds=_sound.AttachPlayRequestAudit();bool sawText=false;
            var parts=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots");
            var seen=new HashSet<int>();
            void Compare()
            {
                rom.UpdateGameplay(0,0,0xff,++update);
                FailIf(actor.State!=rom[0xd084]||actor.Substate!=rom[0xd085]||actor.Counter2!=rom[0xd087]||
                    _player.InvincibilityFrames!=unchecked((sbyte)rom[0xd02b]),
                    $"Veran death eligibility update{update}: state={actor.State}:{actor.Substate}/{rom[0xd084]}:{rom[0xd085]}, counter={actor.Counter2}/{rom[0xd087]}, Link inv={_player.InvincibilityFrames}/{unchecked((sbyte)rom[0xd02b])}.");
                if(generation!=rom.TextGeneration)
                {
                    FailIf(sawText||rom.Word(0xcba2)!=0x5a15||!_dialogue.IsOpen||_player.InvincibilityFrames!=0,
                        "Normal final death must wait until Link is vulnerable before source TX_5615.");
                    sawText=true;generation=rom.TextGeneration;hold=3;
                }
                if(hold>0&&--hold==0){_dialogue.Close();rom[0xcba0]=0;}
                var beads=_entities.Entities<BlueEnergyBeadRoomEntity>();
                FailIf(beads.Count!=Enumerable.Range(0,16).Count(slot=>rom[0xd0c0+slot*256]!=0&&rom[0xd0c1+slot*256]==0x53),
                    "Outgoing swirl allocation/deletion must retain native PART slot count.");
                foreach(var bead in beads)
                {
                    int at=0xd0c0+parts[bead]*256;
                    FailIf(bead.Index!=rom[at+3]||bead.Initialized!=(rom[at+4]!=0)||bead.Delay!=rom[at+7]||
                        bead.Duration!=rom[at+6]||bead.Visible!=((rom[at+26]&128)!=0)||
                        bead.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f),
                        $"Outgoing energy $53 index{bead.Index}, update{update}, batch={batched}: port={bead.PrecisePosition}/{bead.Delay}/{bead.Visible}, ROM={rom.Word(at+12)/256f},{rom.Word(at+10)/256f}/{rom[at+7]}/{rom[at+26]:x2}.");
                    if(bead.Visible)seen.Add(bead.Index);
                }
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls)||
                    !sounds.Requests.SequenceEqual(rom.Sounds),"Outgoing energy must preserve native ordered SFX and RNG under the object mask.");
            }
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            // Focus on the source death state, retaining a live Link damage
            // counter. Ordinary updates must finish that counter before text.
            actor.Health=1;rom[0xd0a9]=1;
            typeof(VeranFinalCharacter).GetProperty("State",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(actor,10);
            rom[0xd084]=10;rom[0xd085]=0;
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,0);rom[WramAddress.wDisabledObjects]=0;
            _runtimeState.SetWramByte(WramAddress.wMenuDisabled,0);rom[WramAddress.wMenuDisabled]=0;
            _player.ApplyInteractionInvincibility(8);rom[0xd02b]=0xf8;
            for(int wait=0;wait<20&&actor.Substate!=2;wait++)StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            FailIf(!sawText||_entities.Entities<BlueEnergyBeadRoomEntity>().Count!=8||actor.Counter2!=40,
                "Veran's native post-text death dispatch must allocate eight outgoing beads and arm counter2=40.");
            // Hold the boss before the independent fade handoff. Source $53
            // retains enabled bit7 and keeps updating under this whole mask.
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,0x89);rom[WramAddress.wDisabledObjects]=0x89;
            StepGameplayUpdates(96,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(seen.Count!=8,"Every outgoing index must leave the center, finish its imported animation and restart.");
            _runtimeState.SetWramByte(WramAddress.wDeleteEnergyBeads,1);rom[WramAddress.wDeleteEnergyBeads]=1;
            StepGameplayUpdates(4,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(_entities.Entities<BlueEnergyBeadRoomEntity>().Any(),"The shared delete byte must cancel every outgoing bead under the freeze mask.");
        }
    }
}
