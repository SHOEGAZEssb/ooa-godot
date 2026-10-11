using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateOceanScaleAndStormGuardsRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.MermaidSuit,0);_inventory.GiveTreasure(TreasureId.Flippers,0);
            _inventory.GiveTreasure(TreasureId.Essence,6);
            _saveData.SetRoomFlag(2,0xa0,0x20,false);_inventory.LoseTreasure(TreasureId.ZoraScale);
            LoadValidationRoom(2,0xa0);_player.WarpTo(new(0x48,8));
            var giver=_entities.Entities<NpcCharacter>().Single(n=>n.Record.SubId==0x10&&n.Record.Id==0xab);
            SomariaRom? rom=null;
            NpcCharacter? nativeActor=null;
            int nativeSlot=0;
            void BeginReference(NpcCharacter actor)
            {
                nativeActor=actor;
                rom=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,(int)_player.Position.X,(int)_player.Position.Y);
                // initializeGame copies the file header into live wIsLinkedGame.
                rom[0xcc01]=_saveData.IsLinkedGame?(byte)1:(byte)0;
                var records=new NpcDatabase().GetRoomNpcs(_rooms.ActiveGroup,_currentRoom.Id);
                for(int index=0;index<records.Count;index++)
                {
                    var record=records[index];int slot=0xd240+index*0x100;
                    rom[slot]=1;rom[slot+1]=(byte)record.Id;rom[slot+2]=(byte)record.SubId;
                    rom[slot+11]=(byte)record.Y;rom[slot+13]=(byte)record.X;
                    if(record.Id==actor.Record.Id&&record.SubId==actor.Record.SubId)nativeSlot=slot;
                }
            }
            void Compare()
            {
                if(rom is null || nativeActor is null)return;
                // Link motion is an explicit input to this native NPC pass.
                // The approach itself still runs through production collisions.
                rom.Word(0xd00a,(int)(_player.PrecisePosition.Y*256));rom.Word(0xd00c,(int)(_player.PrecisePosition.X*256));
                if(!_dialogue.IsOpen)rom[0xcba0]=0;
                rom.AdvanceInteractions(_entities.FrameCounter);
                FailIf(nativeActor.Active!=(rom[nativeSlot]!=0) || nativeActor.Active &&
                    nativeActor.Position!=new Vector2(rom[nativeSlot+13],rom[nativeSlot+11]),
                    $"Ocean Zora native motion differs at frame {_entities.FrameCounter}: port={nativeActor.Active}/{nativeActor.Position}/command={_roomEvents.Get<OceanZoraEvent>().CurrentCommandIndex}/counter={_roomEvents.Get<OceanZoraEvent>().Counter}, ROM={rom[nativeSlot]!=0}/({rom[nativeSlot+13]},{rom[nativeSlot+11]}), counter={rom[nativeSlot+6]}/{rom[nativeSlot+7]}, ptr=${rom.Word(nativeSlot+0x18):x4}.");
            }
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            BeginReference(giver);
            Step(4);FailIf(_player.CutsceneControlled,"Zora scale triggered before Link reached Y $18.");
            for(int i=0;i<8&&!_player.CutsceneControlled;i++)
            {
                Step(1);StepGameplayUpdates(1,Vector2.Down,["move_down"],["move_down"],batched,Compare);
                StepGameplayUpdates(7,Vector2.Down,["move_down"],batched:batched,afterUpdate:Compare);
            }
            FailIf(!_player.CutsceneControlled||_player.FacingVector!=Vector2I.Down,"Zora scale failed to acquire Link at the source Y gate.");
            for(int i=0;i<400&&!_dialogue.IsOpen;i++)Step(1);
            FailIf(!_dialogue.IsOpen||!_dialogue.CurrentMessage.Contains("Zora"),"Scale giver did not reach TX_3430.");
            _dialogue.Close();
            for(int i=0;i<500&&giver.Active;i++){if(_dialogue.IsOpen)_dialogue.Close();Step(1);}
            FailIf(giver.Active||!_inventory.HasTreasure(TreasureId.ZoraScale)||!_saveData.HasRoomFlag(2,0xa0,0x20)||_player.CutsceneControlled,
                "Scale handoff failed to grant $4e, set room item $20, delete the giver or release Link.");
            rom=null;
            LoadValidationRoom(2,0xa0);Step(4);
            FailIf(_entities.Entities<NpcCharacter>().Any(n=>n.Record is {Id:0xab,SubId:0x10}&&n.Active),"Scale giver repeated after completion.");

            foreach(bool linked in new[]{false,true})
            {
                _saveData.SetLinkedGame(linked);
                int group=linked?2:3,room=linked?0xd7:0xd6;
                _saveData.SetRoomFlag(group,room,0x40,false);
                LoadValidationRoom(group,room);
                var guard=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0xab);
                BeginReference(guard);Step(3);
                Vector2 approach=linked?Vector2.Up:Vector2.Right;
                string action=linked?"move_up":"move_right";
                _player.WarpTo(guard.Position+(linked?new Vector2(0,32):new Vector2(-32,0)));
                for(int i=0;i<10&&!guard.CanTalkTo(_player);i++)
                {
                    Step(1);StepGameplayUpdates(1,approach,[action],[action],batched,Compare);
                    StepGameplayUpdates(7,approach,[action],batched:batched,afterUpdate:Compare);
                }
                FailIf(!guard.CanTalkTo(_player)||_rooms.CurrentRoom.GetTerrainInfo(_player.Position).Collision!=0||_player.Position.DistanceTo(guard.Position)<12,
                    $"Sea guard {group:x}:{room:x2} unreachable through the room geometry: {_player.Position}.");
                StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched,Compare);rom![nativeSlot+0x31]=1;Step(1);
                FailIf(!_dialogue.IsOpen||!_player.CutsceneControlled,"Sea guard lost scale conversation/input handoff.");
                for(int i=0;i<400&&guard.Active;i++){if(_dialogue.IsOpen)_dialogue.Close();Step(1);}
                FailIf(guard.Active||!_saveData.HasRoomFlag(group,room,0x40)||_player.CutsceneControlled,
                    $"Sea guard {group:x}:{room:x2} failed its linked/unlinked exit.");
                rom=null;LoadValidationRoom(group,room);Step(3);
                FailIf(_entities.Entities<NpcCharacter>().Any(n=>n.Record.Id==0xab&&n.Active),"Sea guard repeated after room flag $40.");
            }
        }
    }



}
