using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateAncientTombRoomClosure()
    {
        ReinitializeGameplayForValidation();
        int sarcophagi=0,panels=0,sockets=0,keys=0,spinners=0,owls=0,darkRooms=0;
        // The original contiguous mainData streams are $5:$73..$aa. This is
        // an admission check, not dungeon progression or 56 mechanic traces.
        for(int room=0x73;room<=0xaa;room++)
        {
            LoadValidationRoom(5,room);
            FailIf(_rooms.CurrentDungeonIndex!=8 || _currentRoom.WidthInTiles!=15 || _currentRoom.HeightInTiles!=11,
                $"Ancient Tomb room$5:${room:x2} must retain imported dungeon$08 identity and the 15x11 playable geometry.");
            StepGameplayUpdates(3,Vector2.Zero);
            sarcophagi+=_entities.Entities<SarcophagusRoomEntity>().Count;
            panels+=_entities.Entities<StonePanelRoomEntity>().Count;
            sockets+=_entities.Entities<SlateSlotRoomEntity>().Count;
            keys+=_entities.Entities<EnemyAttachedKeyRoomEntity>().Count;
            spinners+=_entities.EntityAdapters<DungeonSpinnerRoomEntity>().Count();
            owls+=_entities.EntityAdapters<OwlStatueRoomEntity>().Count();
            darkRooms+=_entities.EntityAdapters<DarkRoomHandlerRoomEntity>().Count();
            if(room is 0x78 or 0x82)
            {
                FailIf(!_entities.EntityAdapters<DungeonDoorRoomEntity>().All(door=>door.EnemyCompletionSupported),
                    $"Ancient Tomb boss room$5:${room:x2} must admit its source enemy-count shutters.");
                FailIf(room==0x78 ? _entities.Entities<RamrockCharacter>().Count!=1 :
                    _entities.Entities<BlueStalfosCharacter>().Count!=3,
                    $"Ancient Tomb room$5:${room:x2} must initialize its original counted boss stream.");
            }
        }
        // Independently counted original placements, including all pointers:
        // eleven$82, four$7b, four$db, one$77, two$7d, two PART$13 and one$08.
        FailIf(sarcophagi!=11 || panels!=4 || sockets!=4 || keys!=1 || spinners!=2 || owls!=2 || darkRooms!=1,
            $"D8 native admission closure differs: sarcophagi/panels/sockets/key/spinners/owls/dark={sarcophagi}/{panels}/{sockets}/{keys}/{spinners}/{owls}/{darkRooms}.");
        GD.Print("Validated all 56 Ancient Tomb room admissions, shared enemy/mechanic owners, counted boss shutters and source-derived native placement closure.");
    }
}
