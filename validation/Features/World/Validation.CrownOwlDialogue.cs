using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownOwlDialogue()
    {
        foreach(bool batch in new[]{false,true})
        {
            void Step(int count=1,Vector2 movement=default,bool attack=false) =>
                StepGameplayUpdates(count,movement,attack?["attack"]:[],attack?["attack"]:[],batch);
            _inventory.GiveTreasure(TreasureId.SeedSatchel,1);
            _inventory.GiveTreasure(TreasureId.MysterySeeds,0x20);
            _inventory.SelectSatchelSeeds(4);
            _inventory.EquipA(TreasureId.SeedSatchel);
            LoadValidationRoom(4,0x9b);
            _player.WarpTo(new(136,120));
            FailIf(_currentRoom.IsSolid(_player.Position),"Crown owl approach must begin on the actual floor.");
            Step(32,Vector2.Up);
            var owl=_entities.Entities<OwlStatueRoomEntity>().Single();
            FailIf(_currentRoom.IsSolid(_player.Position) || _player.Position.Y<96 || _player.Position.Y>=112,
                $"Walking toward Crown's owl must stop below its solid tile: {_player.Position}.");
            for(int attempt=0;attempt<2;attempt++)
            {
                // Seed inventory uses packed BCD, including the tens borrow.
                int seeds=_inventory.MysterySeeds;
                int remaining=(seeds>>4)*10+(seeds&15)-1;
                int expectedSeeds=(remaining/10)*16+remaining%10;
                Step(attack:true);
                for(int i=0;owl.State==OwlStatueState.Idle && i<50;i++) Step();
                FailIf(owl.State!=OwlStatueState.Activating || _inventory.MysterySeeds!=expectedSeeds,
                    $"Normal Satchel input must hit Crown's owl and consume one Mystery Seed: state={owl.State}, seeds=${_inventory.MysterySeeds:x2}, expected=${expectedSeeds:x2}.");
                for(int i=0;!_dialogue.IsOpen && i<90;i++) Step();
                // enemyData.s $13:$00 selects TX_3900: no text indirection.
                FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage!="Bring all to me." ||
                    owl.State!=OwlStatueState.Speaking || owl.Counter!=22,
                    "Crown's owl must show TX_3900 at the source speaking-counter$16 boundary.");
                Step(8);
                FailIf(owl.Counter!=22,"The owl PART must freeze while its message is open.");
                _dialogue.Close();
                Step(21);
                FailIf(owl.State!=OwlStatueState.Speaking || owl.Counter!=1,
                    "Closing Crown's owl text must resume the remaining22-update speaking phase.");
                Step();
                FailIf(owl.State!=OwlStatueState.Idle || owl.AnimationIndex!=0,
                    "Crown's owl must return to idle and accept another seed after completion.");
            }
            LoadValidationRoom(0,0x60);
        }
    }
}
