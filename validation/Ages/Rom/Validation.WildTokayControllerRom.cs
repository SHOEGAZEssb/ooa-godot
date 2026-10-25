using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateWildTokayControllerRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool present in new[]{false,true})
        foreach(int level in Enumerable.Range(0,5))
        foreach(int capacity in level==0?new[]{14,0,1}:new[]{14})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);var data=new WildTokayGameDatabase();
            _saveData.WriteWramByte(data.WildLevelAddress,(byte)level);_inventory.GiveTreasure(TreasureId.Bracelet,1);_inventory.SetScriptedEquippedItems(TreasureId.Bombs,TreasureId.Sword);
            LoadValidationRoom(2,present?0xe5:0xde);var game=_roomEvents.Get<WildTokayGameEvent>();
            var record=_entities.Entities<TokayCharacter>().Single(a=>a.Record.SubId==(present?0x19:0x0d)).Record;
            game.Cancel();foreach(var other in _entities.EntityAdapters<IRoomEntity>().ToArray())_entities.RetireEntity(other);
            _runtimeState.SetWramByte(0xcfc0,present?(byte)1:(byte)0);_runtimeState.SetWramByte(0xcc8a,0x81);_runtimeState.SetWramByte(0xcc02,0x80);
            var controller=new WildTokayControllerRoomEntity(game,record);_entities.AddEntity(controller);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,80,72);
            native[0xcfc0]=present?(byte)1:(byte)0;native[0xcc8a]=0x81;native[0xcc02]=0x80;
            native[0xd240]=1;native[0xd241]=0x70;
            var sounds=_sound.AttachPlayRequestAudit();
            int updates=0;string source=$"Wild$70/present{present}/level{level}/capacity{capacity}/batch{batched}";
            void Step(int n)=>StepGameplayUpdates(n,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.AdvanceWarpPalette();native.AdvanceInteractions(_entities.FrameCounter);updates++;
                FailIf(controller.State!=native[0xd244]||controller.Substate!=native[0xd245]||controller.Counter!=native[0xd246]||
                    _runtimeState.ReadWramByte(0xcc8a)!=native[0xcc8a]||_runtimeState.ReadWramByte(0xcfdc)!=native[0xcfdc]||_runtimeState.ReadWramByte(0xcfdf)!=native[0xcfdf]||
                    _inventory.EquippedA!=native[0xc689]||_inventory.EquippedB!=native[0xc688],
                    source+$" update{updates}: native state/counter, masks, level, marker or equipped bytes differs (state{controller.State}/{native[0xd244]}, sub{controller.Substate}/{native[0xd245]}, c{controller.Counter}/{native[0xd246]}, equips{_inventory.EquippedB:x2}/{_inventory.EquippedA:x2} native{native[0xc688]:x2}/{native[0xc689]:x2}).");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95],source+$" update{updates}: shared RNG consumption differs.");
                foreach(var tile in data.StartTiles)
                    if(controller.Substate>=1)FailIf(_currentRoom.GetMetatile(new((tile.PackedPosition&15)*16+8,(tile.PackedPosition>>4)*16+8))!=native[0xcf00+tile.PackedPosition],source+": source arena write differs.");
                foreach(var meat in _entities.Entities<WildTokayMeat>())
                {
                    int at=0xd040+_entities.InteractionSlot(meat)*256;
                    FailIf(native[at+1]!=0x8c||meat.State!=native[at+4]||meat.Substate!=native[at+5]||meat.Counter!=native[at+6]||meat.ZFixed!=(short)native.Word(at+14)||meat.SpeedZ!=(short)native.Word(at+0x14),source+$" update{updates}: source meat initialization/fall differs.");
                }
                foreach(var actor in _entities.EntityAdapters<WildTokayParticipantRoomEntity>().ToArray())
                {
                    int at=0xd040+_entities.InteractionSlot(actor.Node)*256;
                    FailIf(native[at+1]!=0x48||actor.State!=native[at+4]||actor.Substate!=native[at+5]||actor.Node.Position!=new Vector2(native[at+13],native[at+11]),source+$" update{updates}: source participant birth/order differs (slot{at:x4}, id{native[at+1]:x2}, state{actor.State}/{native[at+4]}, sub{actor.Substate}/{native[at+5]}, position{actor.Node.Position}/{native[at+13]},{native[at+11]}).");
                    // Isolate the producer for a complete schedule. Participant
                    // catching/departure is checked separately through its
                    // actual handler, so no missed consumer can end this run.
                    _entities.RetireEntity(actor);for(int i=0;i<0x40;i++)native[at+i]=0;
                }
                if(_dialogue.IsOpen)
                {
                    FailIf(native.Word(0xcba2)!=0x0e16||_dialogue.CurrentMessage!=DialogueBox.PlainText(new TokayInteractionDatabase().Text(0x0a16)),source+": source start message differs.");
                    while(14-InteractionAdmissionPool.Count>capacity)
                    {
                        var puff=new PuzzlePuffEffect();puff.Initialize(new(24,24),SoundId.MusNone);_entities.AddEntity(new InteractionSlotValidationEntity(puff,static _=>{}));
                        // A held $8c substate1 has no timed or era-dependent
                        // deletion. Museum statues delete when inPresent=$01.
                        int at=0xd040+_entities.InteractionSlot(puff)*256;native[at]=1;native[at+1]=0x8c;native[at+4]=2;native[at+5]=1;
                    }
                    _dialogue.Close();native[0xcba0]=0;
                }
            });
            Step(data.WildCycleCount(present?2:level)*5*60+150);
            FailIf(controller.Substate!=4||native[0xd27b]!=0||_entities.EntityAdapters<WildTokayParticipantRoomEntity>().Any(),source+": full schedule did not consume its final empty interval.");
            FailIf(sounds.Requests.Count(id=>id==data.SoundWhistle)!=(capacity==0?0:1)||native.Sounds.Count(id=>id==data.SoundWhistle)!=(capacity==0?0:1),source+": failed first-meat allocation must suppress the source whistle.");
            controller.Cancel();game.Cancel();_entities.Clear();
        }
    }
}
