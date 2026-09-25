using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwitchHookDungeonSwitches()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        void Step(int count = 1, Vector2 movement = default, bool fire = false)
        {
            input.CaptureForValidation(fire ? ["attack"] : [], fire ? ["attack"] : [], movement);
            scheduler.Advance(count / 60.0, update);
        }
        void Wait(int count, bool batch)
        {
            if (batch) Step(count); else for (int i = 0; i < count; i++) Step();
        }
        static Vector2 Point(int p) => new((p & 15) * 16 + 8, (p >> 4) * 16 + 8);
        var collisions = PartSwitchCollisionDatabase.Shared;
        // Independent partActiveCollisions $05 bit order and collision mode03.
        const string enabled = "00001111111101100000001101111110";
        for (int item = 0; item < 32; item++)
        {
            int expected = enabled[item] == '0' ? -1 : item >= 0x19 ? 0 : 28;
            FailIf(collisions.HitLockout(item) != expected,
                $"PART_SWITCH collision ${item:x2} lost the source mask/effect lockout.");
        }
        _inventory.GiveTreasure(TreasureDatabase.TreasureSwitchHook, 1);
        _inventory.EquipA(InventoryState.ItemSwitchHook);
        for (int i = 0; i < 11; i++) _inventory.GiveTreasure(TreasureDatabase.TreasureHeartContainer, 4);
        var runtime = _entities.RuntimeState;
        foreach (var c in new[] {
            (Room: 0x89, Mask: 4, Switch: 0x62, Rail: 0x67, Off: 0x5c, On: 0x5d, Start: 0x92, Approach: 0x82, Direction: 0, Move: Vector2.Up, Travel: 12),
            (Room: 0x8f, Mask: 8, Switch: 0x81, Rail: 0x52, Off: 0x59, On: 0x5e, Start: 0x51, Approach: 0x61, Direction: 2, Move: Vector2.Down, Travel: 11) })
        foreach (bool batch in new[] { false, true })
        {
            runtime.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0x80);
            LoadValidationRoom(4, c.Room);
            _player.WarpTo(Point(c.Start));
            _inventory.RefillHealth();
            _player.SetBraceletLiftCollisionsDisabled(true);
            // Moving Keese can intercept repeated hook flights. Remove them
            // through their combat path to isolate the stationary switch timing.
            for (int attempt = 0; _entities.Entities<KeeseCharacter>().Count != 0 && attempt < 8; attempt++)
            {
                foreach (var keese in _entities.Entities<KeeseCharacter>())
                    _entities.ApplySwordHit(keese.CollisionBounds, _player.Position, 0x7f);
                Step(40);
            }
            FailIf(_entities.Entities<KeeseCharacter>().Count != 0, "Could not clear moving Keese from the switch timing fixture.");
            FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
                "Hook switch fixture must start on actual safe floor.");
            Step();
            for (int n = 0; _player.Position.DistanceTo(Point(c.Approach)) > 0.8f && n < 40; n++) Step(movement: c.Move);
            FailIf(_player.Position.DistanceTo(Point(c.Approach)) > 0.8f,
                $"4:{c.Room:x2} hook approach was blocked by native room geometry.");
            var part = _entities.Entities<DungeonSwitchRoomEntity>().Single();
            Vector2 origin = _player.PrecisePosition;
            for (int repetition = 0; repetition < 2; repetition++)
            {
                int before = runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress);
                int switchBefore = _currentRoom.GetMetatile(Point(c.Switch));
                int railBefore = _currentRoom.GetMetatile(Point(c.Rail));
                _sound.ClearPlayRequestAudit();
                Step(fire: true);
                var hook = _entities.SwitchHook!.Item!;
                Wait(c.Travel - 1, batch);
                FailIf(part.HitLockout != 0 || runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != before,
                    "Hook hit PART_SWITCH before its strict radius boundary.");
                Step();
                FailIf(part.HitLockout != 28 || hook.State != 1 ||
                    runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != before ||
                    _currentRoom.GetMetatile(Point(c.Switch)) != switchBefore ||
                    _currentRoom.GetMetatile(Point(c.Rail)) != railBefore || _sound.PlayRequestsFor(0x7e) != 0,
                    "Hook collision must set signed lockout $e4 without executing the part handler early.");
                if (repetition == 0)
                {
                    var text = _entities.TextActiveSource;
                    Vector2 contact = hook.Position;
                    try
                    {
                        _entities.TextActiveSource = () => true;
                        Wait(4, batch);
                        FailIf(part.HitLockout != 28 || hook.Position != contact || hook.State != 1 ||
                            runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != before,
                            "Text must preserve the pending hook/part collision and freeze its counters.");
                    }
                    finally { _entities.TextActiveSource = text; }
                }
                Step();
                bool on = (before & c.Mask) == 0;
                FailIf(part.HitLockout != 27 || hook.State != 2 ||
                    runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != (before ^ c.Mask) ||
                    _currentRoom.GetMetatile(Point(c.Switch)) != (on ? 0x0b : 0x0a) ||
                    _currentRoom.GetMetatile(Point(c.Rail)) != (on ? c.On : c.Off) ||
                    _sound.PlayRequestsFor(0x7e) != 1 || _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 0 ||
                    _player.PrecisePosition != origin,
                    "The next part update must toggle before the rail interaction, retract without exchange/clink, and decrement $e4->$e3.");
                Wait(26, batch);
                FailIf(part.HitLockout != 1, "PART_SWITCH's hook lockout ended before update28.");
                Step();
                FailIf(part.HitLockout != 0, "PART_SWITCH's hook lockout did not expire on update28.");
                for (int n = 0; _player.IsUsingSwitchHook && n < 100; n++) Step();
                FailIf(_player.IsUsingSwitchHook || _player.PrecisePosition != origin,
                    "Hook switch interaction did not return control at its original position for repeat activation.");
            }
            // ITEM_SWORD_BEAM uses effect20: no invincibility and one clinked
            // beam deletion. Exercise the shared production projectile path.
            _sound.ClearPlayRequestAudit();
            FailIf(!_entities.TrySpawnSwordBeam(_player.Position, c.Direction), "Could not create the switch beam fixture.");
            Step();
            var beam = _entities.Entities<SwordBeamEffect>().Single();
            for (int n = 0; !beam.Finished && n < 20; n++) Step();
            FailIf(!beam.Finished || part.HitLockout != 0 ||
                runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != (0x80 | c.Mask) ||
                _sound.PlayRequestsFor(0x7e) != 1,
                $"Sword beam must flip the switch once and be consumed without the sword/hook lockout: room {c.Room:x2}, " +
                $"finished {beam.Finished}, lockout {part.HitLockout}, bits {runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress):x2}, " +
                $"sounds {_sound.PlayRequestsFor(0x7e)}, beam {beam.PrecisePosition}, Link {_player.PrecisePosition}.");
            Wait(30, batch);
            // Cancel after contact but before the native part handler: room
            // teardown discards that pending hit rather than toggling a new room.
            Step(fire: true);
            Wait(c.Travel, batch);
            FailIf(part.HitLockout != 28, "Cancellation fixture did not reach a pending switch hit.");
            LoadValidationRoom(4, 0x91);
            LoadValidationRoom(4, c.Room);
            _player.WarpTo(Point(c.Approach));
            Step();
            FailIf(runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != (0x80 | c.Mask) ||
                _entities.Entities<DungeonSwitchRoomEntity>().Single().HitLockout != 0 ||
                _currentRoom.GetMetatile(Point(c.Switch)) != 0x0b || _currentRoom.GetMetatile(Point(c.Rail)) != c.On,
                "Reload replayed a canceled collision or lost the last completed switch state.");
            _player.SetBraceletLiftCollisionsDisabled(false);
        }
        GD.Print("Validated PART_SWITCH hook collisions, exact pending/lockout timing, ordered rail handoff, text pause, repeat activation, beam response and cancellation.");
    }
}
