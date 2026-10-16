using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBlackTowerPossessionScrollRom()
    {
        ReinitializeGameplayForValidation();
        // Unfinished re-entry, then completed-room suppression. Approach the
        // actual north exit of $4:fe; do not direct-load the boss room.
        for(int attempt=0;attempt<3;attempt++)
        {
            bool completed=attempt==2;
            if(completed)_saveData.SetRoomFlag(4,0xfc,0x80);
            LoadValidationRoom(4,0xfe);_entities.Clear();_inventory.EquipA(0);_inventory.EquipB(0);
            _player.WarpTo(new(120,16));_player.Face(Vector2I.Up);_transitions.ResetCamera();
            FailIf(_collision.Collides(_player.Position)||!_transitions.TryGetScreenTransitionDestinationForValidation(Vector2I.Up,out int target)||target!=0xfc,
                "Ambi scroll fixture must use the imported $4:fe north route to $4:fc on real floor.");
            var sounds=_sound.AttachPlayRequestAudit();
            for(int walk=0;walk<24&&!_transitions.ScrollActive;walk++)StepGameplayUpdates(1,Vector2.Up,["move_up"]);
            FailIf(!_transitions.ScrollActive||_currentRoom.Id!=0xfc,"Walking through the entrance must start the real Ambi scroll.");
            var initial=_random.CaptureState();var rom=new SomariaRom(_saveData,initial,_currentRoom,0,120,0);
            rom.InitializeLinkGameplay();rom[WramAddress.wScrollMode]=8;
            var door=_entities.EntityAdapters<DungeonDoorRoomEntity>().Single();
            int doorAt=0xd040+_entities.InteractionSlot(door)*256;
            // Reserve the already placed shutter's slot. Its separate owner
            // is not part of this bounded native interaction comparison.
            rom[doorAt]=1;rom[doorAt+4]=1;
            int ambiAt=completed?doorAt+256:0xd040+_entities.InteractionSlot(_entities.EntityAdapters<BlackTowerPossessionActor>().Single(actor=>actor.Id==0x4d).Node)*256;
            rom[ambiAt]=1;rom[ambiAt+1]=0x4d;rom[ambiAt+2]=6;rom[ambiAt+11]=72;rom[ambiAt+13]=120;
            rom.AdvanceInteractions();
            void CompareActors()
            {
                var actors=_entities.EntityAdapters<BlackTowerPossessionActor>().ToArray();
                FailIf(actors.Length!=(completed?0:2),"Scroll preload must initialize Ambi and one ghost, or delete completed Ambi.");
                foreach(var actor in actors)
                {
                    int at=0xd040+_entities.InteractionSlot(actor.Node)*256;var npc=(NpcCharacter)actor.Node;
                    FailIf(rom[at+1]!=actor.Id||rom[at+2]!=actor.SubId||rom[at+4]!=actor.State||actor.State!=1||
                        npc.Visible!=((rom[at+0x1a]&128)!=0)||actor.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                        npc.CurrentAnimationCounter!=rom[at+0x20]||npc.CurrentAnimationParameter!=rom[at+0x21],
                        $"Ambi scroll attempt{attempt}, INTERAC${actor.Id:x2}:${actor.SubId:x2}: native initialization/visibility/position/animation differs.");
                }
                FailIf(_random.CaptureState().Calls!=initial.Calls+rom.RandomCalls,"Ambi scrolling must preserve shared RNG consumption.");
            }
            CompareActors();
            FailIf(_entities.RoomEnemyCount!=(completed?0:1)||
                sounds.Requests.Count(sound=>sound==SoundId.SndCtrlStopMusic)!=(completed?0:1)||
                _runtimeState.ReadWramByte(WramAddress.wDisabledObjects)!=0,
                "Ambi state0 must publish the count/music once while leaving script input disable until after scrolling.");
            int scrollUpdates=0;
            while(_transitions.ScrollActive&&scrollUpdates++<96)
                StepGameplayUpdates(1,Vector2.Zero,afterUpdate:()=>{rom.AdvanceInteractions();CompareActors();});
            FailIf(_transitions.ScrollActive||scrollUpdates<8,"Ambi scroll must complete without advancing its initialized script.");
            rom[WramAddress.wScrollMode]=0;rom[doorAt]=0;
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:()=>{rom.AdvanceInteractions();CompareActors();});
            FailIf(_runtimeState.ReadWramByte(WramAddress.wDisabledObjects)!=rom[WramAddress.wDisabledObjects]||
                !completed&&_runtimeState.ReadWramByte(WramAddress.wDisabledObjects)!=0x81,
                "Ambi's first post-scroll dispatch must resume disableinput exactly once.");
            LoadValidationRoom(4,0xfd);StepGameplayUpdates(1,Vector2.Zero);
            FailIf(_entities.EntityAdapters<BlackTowerPossessionActor>().Any()||_entities.PlayerMenusDisabled,
                "Cancelling the entered Ambi encounter must release actors and input ownership.");
        }
    }
}
