using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePostD7ReentryRom()
    {
        // Suppression belongs to room flag$40, independently of Essences.
        foreach(bool allEssences in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            if(allEssences)for(int essence=0;essence<8;essence++)_inventory.GiveTreasure(TreasureId.Essence,essence);
            _saveData.SetRoomFlag(4,0xf6,0x40);
            for(int attempt=0;attempt<2;attempt++)
            {
                LoadValidationRoom(4,0xf6);_inventory.EquipA(0);_inventory.EquipB(0);
                _player.WarpTo(new(120,72));_player.Face(Vector2I.Down);_transitions.ResetCamera();
                var actor=_entities.EntityAdapters<PostD7ActorRoomEntity>().Single();
                int at=0xd040+_entities.InteractionSlot(actor.Node)*256;
                var initial=_random.CaptureState();var rom=new SomariaRom(_saveData,initial,_currentRoom,2,120,72);
                rom.InitializeLinkGameplay();rom[at]=1;rom[at+1]=0x4d;rom[at+2]=3;rom[at+11]=0xf8;rom[at+13]=120;
                var sounds=_sound.AttachPlayRequestAudit();int update=0;
                void Compare()
                {
                    rom.UpdateGameplay(0,0,255,++update);
                    FailIf(!actor.Finished||rom[at]!=0||_entities.EntityAdapters<PostD7ActorRoomEntity>().Any()||
                        _roomEvents.Get<PostD7Event>().HasState||!_player.Visible||_entities.PlayerMenusDisabled||
                        _runtimeState.ReadWramByte(WramAddress.wDisabledObjects)!=0||
                        _runtimeState.ReadWramByte(WramAddress.wMenuDisabled)!=0||_dialogue.IsOpen||
                        _random.CaptureState().Calls!=initial.Calls+rom.RandomCalls||!sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Completed Ambi $4:f6 allEssences{allEssences}, attempt{attempt}, update{update}: native state0 deletion/control/RNG/sound differs.");
                }
                StepGameplayUpdates(4,Vector2.Zero,batched:allEssences,afterUpdate:Compare);
                LoadValidationRoom(4,0xfd);StepGameplayUpdates(1,Vector2.Zero);
            }
        }
    }
}
