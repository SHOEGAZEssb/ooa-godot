using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownWarpMarker()
    {
        // func_60e9 checks wLinkInAir before checkStandingOnDeactivatedWarpTile
        // and the wEnteredWarpPosition=$ff write. The marker survives leaving
        // its tile in a jump, then clears on the first grounded check elsewhere.
        var marker = typeof(RoomTransitionController).GetField("_deactivatedWarpPosition",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (bool batched in new[] { false, true })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.GiveTreasure(TreasureId.Feather, 0);
            _inventory.EquipA(TreasureId.Feather);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 1);
            _player.WarpTo(new(120, 40));
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetMetatile(new(120, 24)) != 0x44,
                "Warp marker fixture must approach Crown4:a1/$17 through original floor.");
            StepGameplayUpdates(15, Vector2.Up, batched: batched);
            _transitions.DeactivateWarpAtPlayerPosition(_player);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 0);
            FailIf((int)marker.GetValue(_transitions)! != 0x17,
                "The fixture must establish the same entered-stair marker as arrival or hazard recovery.");
            bool locked = false;
            var observer = new ItemPhaseValidationEntity(() =>
            {
                if (locked || _player.Position.Y < 28) return;
                locked = true;
                _entities.LockSmogLinkAndMenu();
            });
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!.Invoke(_entities, [observer, 0]);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [observer]);
            StepGameplayUpdates(3, Vector2.Down, batched: batched);
            FailIf(!locked || (int)marker.GetValue(_transitions)! != -1 || IsTransitioning,
                "wMenuDisabled rejects tile warps after a grounded departure has cleared the old marker.");
            FailIf(_runtimeState.ReadWramByte(WramAddress.wTmpcec0) != 0xff,
                "checkTileWarps menu rejection must still publish the unmatched edge-warp scratch byte $ff.");
            typeof(RoomEntityManager).GetMethod("ReleaseSmogLinkAndMenu", flags)!.Invoke(_entities, null);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 1);
            StepGameplayUpdates(3, Vector2.Up, batched: batched);
            _transitions.DeactivateWarpAtPlayerPosition(_player);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 0);
            StepGameplayUpdates(1, Vector2.Down, ["attack"], ["attack"]);
            StepGameplayUpdates(14, Vector2.Down, batched: batched);
            FailIf(!_player.TopDownAirborne || _player.Position != new Vector2(120, 40),
                "A normal Feather jump must leave the marked stair while still airborne.");
            // Exercise func_60e9 itself as well as Player's ordinary request
            // suppression: the dispatcher must preserve its own guard order.
            _runtimeState.SetWramByte(WramAddress.wTmpcec0, 0x56);
            FailIf(_transitions.CheckTileWarp(_player) || (int)marker.GetValue(_transitions)! != 0x17,
                "Air-state rejection must precede clearing the entered-warp marker on a different tile.");
            FailIf(_runtimeState.ReadWramByte(WramAddress.wTmpcec0) != 0x56,
                "func_60e9 air rejection must leave scratch unchanged before checkScreenEdgeWarps.");
            for (int update = 0; update < 80 && _player.TopDownAirborne; update++)
            {
                FailIf(IsTransitioning || (int)marker.GetValue(_transitions)! != 0x17,
                    "The entered-warp marker must remain intact throughout the airborne interval.");
                StepGameplayUpdates(1, Vector2.Zero);
            }
            FailIf(_player.TopDownAirborne || IsTransitioning || (int)marker.GetValue(_transitions)! != -1 ||
                _currentRoom.GetPackedPosition(_player.Position + new Vector2(0, 4)) == 0x17,
                $"Landing away from the stair must clear its marker on that first grounded check; position={_player.Position}, marker={marker.GetValue(_transitions)}, air={_player.TopDownAirborne}, warp={IsTransitioning}.");
            for (int update = 0; update < 80 && !IsTransitioning; update++)
                StepGameplayUpdates(1, Vector2.Up);
            FailIf(!IsTransitioning, "Returning on the ground must activate the stair after marker release.");
        }
        ReinitializeGameplayForValidation();
    }
}
