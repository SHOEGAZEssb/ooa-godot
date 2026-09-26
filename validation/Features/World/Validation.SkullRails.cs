using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonRails()
    {
        void Step(int count = 1, Vector2 move = default, bool sword = false) =>
            StepGameplayUpdates(count, move, sword ? ["attack"] : [], sword ? ["attack"] : [], batched: true);
        static Vector2 Point(int p) => new((p & 15) * 16 + 8, (p >> 4) * 16 + 8);
        var runtime = _entities.RuntimeState;
        var data = new SkullDungeonDatabase();
        var staticData = new StaticDungeonObjectDatabase();
        var railData = new DungeonInteractionDatabase();
        // switchTileToggler.s @tileReplacement $0b/$0c; mainData.s
        // and replaceSwitchTiles @group4SwitchData independently agree.
        FailIf(railData.SwitchTiles(0x0b) != (0x5c, 0x5d) || railData.SwitchTiles(0x0c) != (0x59, 0x5e) ||
            data.GetRoomRecords(4, 0x89) is not [{ Id: InteractionId.SwitchTileToggler, SubId: 4, Y: 0x67, X: 0x0b, Order: 0 }] ||
            data.GetRoomRecords(4, 0x8f) is not [{ Id: InteractionId.SwitchTileToggler, SubId: 8, Y: 0x52, X: 0x0c, Order: 0 }],
            "Skull rail junctions lost source placement, switch masks, or replacement table rows.");
        _inventory.GiveTreasure(TreasureId.Sword, 1);
        _inventory.EquipA(TreasureId.Sword);
        for (int i = 0; i < 11; i++) _inventory.GiveTreasure(TreasureId.HeartContainer, 4);
        runtime.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0);
        LoadValidationRoom(4, 0x89);
        _player.WarpTo(Point(0x82));
        var text = _entities.TextActiveSource;
        try
        {
            _entities.TextActiveSource = () => true;
            runtime.SetWramByte(OracleRuntimeState.SwitchStateAddress, 4);
            Step();
            FailIf(_currentRoom.GetMetatile(Point(0x67)) != 0x5c,
                "Rail controller state0 must sample the current byte under text without retroactively applying a change.");
            runtime.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0x84);
            Step();
            FailIf(_currentRoom.GetMetatile(Point(0x67)) != 0x5c,
                "Initialized rail controller advanced while text was active.");
        }
        finally { _entities.TextActiveSource = text; }
        Step();
        FailIf(_currentRoom.GetMetatile(Point(0x67)) != 0x5d,
            "Rail controller must react to a change in the complete switch byte, including an unrelated bit.");
        foreach (bool batch in new[] { false, true })
        foreach (int switches in new[] { 0, 4, 8, 12 })
        {
            MinecartRuntimeState.Reset(runtime, staticData.Minecarts(4));
            runtime.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0x80);
            foreach (var c in new[] {
                (Room: 0x89, Mask: 4, Switch: 0x62, Rail: 0x67, Off: 0x5c, On: 0x5d, Start: 0x82, Approach: 0x72, Move: Vector2.Up),
                (Room: 0x8f, Mask: 8, Switch: 0x81, Rail: 0x52, Off: 0x59, On: 0x5e, Start: 0x61, Approach: 0x71, Move: Vector2.Down) })
            {
                LoadValidationRoom(4, c.Room);
                _player.WarpTo(Point(c.Start));
                _inventory.RefillHealth();
                _player.SetBraceletLiftCollisionsDisabled(true);
                FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
                    $"4:{c.Room:x2} switch approach must begin on real floor.");
                Step();
                for (int n = 0; _player.Position.DistanceTo(Point(c.Approach)) > 0.8f && n < 40; n++) Step(move: c.Move);
                FailIf(_player.Position.DistanceTo(Point(c.Approach)) > 0.8f,
                    $"4:{c.Room:x2} switch could not be reached through native collision geometry: {_player.Position}.");
                FailIf(_currentRoom.GetMetatile(Point(c.Rail)) != c.Off || _currentRoom.GetMetatile(Point(c.Switch)) != 0x0a,
                    "Unpressed switch/junction did not begin at the source layout tiles.");
                if ((switches & c.Mask) == 0) continue;
                foreach (bool on in new[] { true, false, true })
                {
                    byte before = runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress);
                    _sound.ClearPlayRequestAudit();
                    Step(sword: true);
                    for (int n = 0; runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress) == before && n < 35; n++) Step();
                    Step(); // The ordered interaction observes the preceding weapon collision.
                    FailIf(runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != (before ^ c.Mask) ||
                        _currentRoom.GetMetatile(Point(c.Switch)) != (on ? 0x0b : 0x0a) ||
                        _currentRoom.GetMetatile(Point(c.Rail)) != (on ? c.On : c.Off) ||
                        _sound.PlayRequestsFor(SoundId.SndSwitch) != 1,
                        $"4:{c.Room:x2} sword switch did not update only mask ${c.Mask:x2}, its rail, and SND_SWITCH once.");
                    if (batch) Step(40); else for (int n = 0; n < 40; n++) Step();
                    FailIf(_entities.Entities<DungeonSwitchRoomEntity>().Single().HitLockout != 0 || _player.IsAttacking,
                        "Switch lockout or Sword parent did not finish before repeat activation.");
                }
                Vector2 approach = _player.PrecisePosition;
                LoadValidationRoom(4, 0x91);
                LoadValidationRoom(4, c.Room);
                _player.WarpTo(approach);
                FailIf(_currentRoom.GetMetatile(Point(c.Switch)) != 0x0b || _currentRoom.GetMetatile(Point(c.Rail)) != c.On,
                    "Retained switch bit did not restore the junction and switch tile before room updates.");
                Step();
            }
            LoadValidationRoom(4, 0x89);
            _player.WarpTo(Point(0x83));
            _player.SetBraceletLiftCollisionsDisabled(true);
            var cart = _entities.Entities<MinecartRoomEntity>().Single();
            for (int n = 0; !cart.Mounting && n < 40; n++) Step(move: Vector2.Right);
            for (int n = 0; !cart.Riding && n < 80; n++) Step();
            FailIf(!cart.Riding, "Switch-route fixture could not board the original 4:89 cart.");
            var route = new List<int> { 0x89 };
            for (int n = 0; !cart.Dismounting && n < 3000; n++)
            {
                Step(batch ? 3 : 1);
                if (route[^1] != _currentRoom.Id) route.Add(_currentRoom.Id);
                _inventory.RefillHealth();
            }
            // Source track layouts route a clear bit04 through column7; set
            // bit04 through column9. Bit08 blocks the west turn in 4:8f.
            int[] expected = (switches & 4) != 0 ? [0x89, 0x8f]
                : (switches & 8) != 0 ? [0x89, 0x8f, 0x89] : [0x89, 0x8f, 0x92];
            int endpoint = (switches & 4) != 0 ? 0x49 : (switches & 8) != 0 ? 0x84 : 0x32;
            FailIf(!cart.Dismounting || !route.SequenceEqual(expected) || cart.Position != Point(endpoint),
                $"Switch bits ${switches:x2} selected the wrong native cart route: " +
                $"{string.Join(',', route.Select(r => r.ToString("x2")))} at {cart.Position}.");
            for (int n = 0; cart.Dismounting && n < 80; n++) Step();
            FailIf(_player.MinecartJumpActive || _player.MinecartRideActive ||
                runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != (0x80 | switches),
                "The rail ride failed to release Link or changed unrelated switch bits.");
            _player.SetBraceletLiftCollisionsDisabled(false);
            LoadValidationRoom(4, 0x91);
        }
        GD.Print("Validated Skull Dungeon's two Sword rail switches, repeat hits, retained junctions and all four cart routes with individual/batched gameplay updates.");
    }
}
