using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDebugSavestateMinecarts()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4,0x33);
            // staticDungeonObjects.s has three ordered D2 carts. Their live
            // slots can change: INTERAC $16 state0 deletes its related static
            // slot and saves into the first free slot. Use a reordered live
            // buffer so the default slot $00 cannot stand in for saved $02.
            MinecartRuntimeState.Reset(_runtimeState,[
                new(0,0x35,0x68,0xb8,"data/ages/staticDungeonObjects.s:dungeon2StaticObjects"),
                new(1,0x40,0x58,0xa8,"data/ages/staticDungeonObjects.s:dungeon2StaticObjects"),
                new(2,0x33,0x38,0xc8,"data/ages/staticDungeonObjects.s:dungeon2StaticObjects")]);
            LoadValidationRoom(4,0x33);
            _player.WarpTo(new Vector2(0xb8,0x38));
            FailIf(_rooms.CurrentRoom.GetTerrainInfo(_player.Position).Collision!=0,
                "Debug-cart boarding must approach $4:33 from the platform floor.");
            var snapshot=CaptureDebugSavestate();
            RestoreDebugSavestate(snapshot);
            var cart=_entities.Entities<MinecartRoomEntity>().Single();
            FailIf(cart.Position!=new Vector2(0xc8,0x38)||cart.Riding,
                "Debug restore lost the source stationary cart at $4:33 ($c8,$38).");
            StepGameplayUpdates(60,Vector2.Right,["move_right"],["move_right"],batched,
                afterUpdate:()=> {
                    if(!cart.Riding) return;
                    FailIf(_runtimeState.ReadWramByte(0xcd90)!=0||
                        _runtimeState.ReadWramByte(0xcd80)!=3||
                        _runtimeState.ReadWramByte(0xcd81)!=0x35||
                        _runtimeState.ReadWramByte(0xcd89)!=0x40,
                        "Restored INTERAC $16 cleared a default static slot instead of saved $cd90, leaving a phantom cart.");
                });
            FailIf(!cart.Riding||!_player.MinecartRideActive||
                MinecartRuntimeState.StationaryInRoom(_runtimeState,0x33).Any(),
                $"Debug-cart restoration did not remove the stationary record on boarding; batched={batched}.");
            RestoreDebugSavestate(snapshot);
            FailIf(_entities.Entities<MinecartRoomEntity>().Count!=1||
                _entities.Entities<MinecartRoomEntity>()[0].Position!=new Vector2(0xc8,0x38),
                "Repeated debug restore duplicated or hid the saved $4:33 cart.");
        }
    }
}
