using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateOceanNpcSelectionRom()
    {
        var records=NpcDatabase.Shared.AllRecords.Where(n=>
            n.Id==0xab && (n.SubId<0x0a || n.SubId is >=0x13 and <=0x1b) || n.Id==0xba).ToArray();
        int fixture=0;
        foreach(var roomRecords in records.GroupBy(n=>(n.Group,n.Room)))
        foreach(int world in new[]{0,1,2})
        foreach(bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Flippers,0);_inventory.GiveTreasure(TreasureId.MermaidSuit,0);
            _saveData.SetGlobalFlag(0x27,world>0);_saveData.SetGlobalFlag(0x30,world>1);
            _inventory.GiveTreasure(TreasureId.Essence,6);
            LoadValidationRoom(roomRecords.Key.Group,roomRecords.Key.Room);
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(8,8));
            var rom=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,8,8);
            var ordered=NpcDatabase.Shared.GetRoomNpcs(roomRecords.Key.Group,roomRecords.Key.Room).ToArray();
            for(int index=0;index<ordered.Length;index++)
            {
                var record=ordered[index];int slot=0xd240+index*0x100;
                rom[slot]=1;rom[slot+1]=(byte)record.Id;rom[slot+2]=(byte)record.SubId;
                rom[slot+3]=(byte)record.Var03;rom[slot+11]=(byte)record.Y;rom[slot+13]=(byte)record.X;
            }
            int update=0;
            StepGameplayUpdates(3,Vector2.Zero,batched:batch,afterUpdate:()=>rom.AdvanceInteractions(++update));
            foreach(var record in roomRecords)
            {
                int slot=0xd240+System.Array.IndexOf(ordered,record)*0x100;
                var actor=_entities.Entities<NpcCharacter>().Single(n=>n.BaseRecord==record);
                FailIf(actor.Active!=(rom[slot]!=0) || actor.Active && actor.TextId!=rom.Word(slot+0x32),
                    $"Ocean NPC ${record.Id:x2}:${record.SubId:x2} room${record.Group:x}:${record.Room:x2} world={world}: port text=${actor.TextId:x4}/active={actor.Active}, ROM=${rom.Word(slot+0x32):x4}/enabled=${rom[slot]:x2}.");
            }
            // Palace text changes live, ocean text remains selected at entry,
            // and deleted attendants stay deleted even after healing the king.
            _saveData.SetGlobalFlag(0x27);_saveData.SetGlobalFlag(0x30);
            for(int address=0xc5b0;address<0xcb00;address++)rom[address]=_saveData.ReadWramByte(address);
            StepGameplayUpdates(2,Vector2.Zero,batched:batch,afterUpdate:()=>rom.AdvanceInteractions(++update));
            foreach(var record in roomRecords)
            {
                int slot=0xd240+System.Array.IndexOf(ordered,record)*0x100;
                var actor=_entities.Entities<NpcCharacter>().Single(n=>n.BaseRecord==record);
                FailIf(actor.Active!=(rom[slot]!=0) || actor.Active && actor.TextId!=rom.Word(slot+0x32),
                    $"Ocean NPC ${record.Id:x2}:${record.SubId:x2} differs after live pollution/cure flags.");
            }
        }
        ValidateOceanZoraDialogue();
        LoadValidationRoom(3,0x90);
        var child=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0xba);
        Vector2 approach=new[]{Vector2.Up,Vector2.Right,Vector2.Down,Vector2.Left}.First(direction=>
            Enumerable.Range(14,19).All(distance=>
            {
                Vector2 point=child.Position-direction*distance;
                return point.X>=8&&point.X<=152&&point.Y>=8&&point.Y<=120&&!_collision.Collides(point);
            }));
        string action=approach==Vector2.Up?"move_up":approach==Vector2.Right?"move_right":approach==Vector2.Down?"move_down":"move_left";
        _player.WarpTo(child.Position-approach*32);
        for(int tap=0;tap<10&&!child.CanTalkTo(_player);tap++)
        {
            StepGameplayUpdates(1,Vector2.Zero);
            StepGameplayUpdates(1,approach,[action],[action]);
            StepGameplayUpdates(7,approach,[action],batched:true);
        }
        FailIf(!child.CanTalkTo(_player),"Young Jabu is unreachable through room 3:90 geometry.");
        for(int repeat=0;repeat<2;repeat++)
        {
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
            FailIf(!_dialogue.IsOpen,"Young Jabu lost its generic conversation.");
            _dialogue.Close();StepGameplayUpdates(3,Vector2.Zero);
        }
    }

    private void ValidateOceanTextExpansionRom()
    {
        var data=new OceanInteractionDatabase();
        foreach(var stream in new[]{data.KingPast,data.KingPresent,data.Scale,data.StormGuard})
        foreach(var text in stream.OfType<CutsceneShowTextCommand>().Where(t=>t.TextId is 0x3401 or 0x340d or 0x3430))
            RunDialogueRom(text.TextId,text.Message,3,batched:true,accelerate:true,choice:text.TextId==0x3401);
        RunDialogueRom(0x5711,NpcDatabase.Shared.GetRoomNpcs(3,0x90).Single(n=>n.Id==0xba).Message,3,batched:true,accelerate:true);
        RunDialogueRom(0x3442,NpcDatabase.Shared.GetRoomNpcs(3,0xb1).Single(n=>n.SubId==0x18).Message,3,batched:true,accelerate:true);
    }
}
