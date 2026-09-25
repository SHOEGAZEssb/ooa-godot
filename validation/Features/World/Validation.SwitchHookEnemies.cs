using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwitchHookGibdoExchange()
    {
        var collisions = SwitchHookCollisionDatabase.Shared;
        // Independent readings of objectCollisionTable's $0d column.
        FailIf(collisions.Effect(0x16) != 0x2e || collisions.Effect(0x06) != 0 ||
            collisions.Effect(0x13) != 0x1b || collisions.Effect(0x02) != 0x1c ||
            !collisions.EnemyEnabled(0x12) || collisions.EnemyEnabled(0x16) ||
            collisions.EnemyEnabled(0x2d), "Switch Hook lost native effect or dbrev eligibility operands.");
        Vector2[] probes = [new(77, 77), new(82, 77), new(77, 87), new(82, 87),
            new(75, 80), new(75, 85), new(84, 80), new(84, 85)];
        foreach (bool side in new[] { false, true })
        for (int bits = 0; bits < 256; bits++)
        {
            int index = 0;
            bool surrounded = LinkWallProbe.Shared.SurroundedByWalls(new(80, 80), side, point =>
            {
                FailIf(point != probes[index], "Link adjacent-wall probes must accumulate the source YX offsets.");
                return (bits & (0x80 >> index++)) != 0;
            });
            bool expected = Enumerable.Range(0, 4).All(pair => (bits & (3 << (pair * 2))) != 0);
            FailIf(index != 8 || surrounded != expected, "Surrounded-wall rotation lost a cardinal pair boundary.");
        }
        // calculateAdjacentWallsBitset accumulates byte coordinates. Probe
        // order matters even when the original coordinate has subpixels.
        Vector2[] wrapped = [new(253,253),new(2,253),new(253,7),new(2,7),
            new(251,0),new(251,5),new(4,0),new(4,5)];
        foreach (bool side in new[] { false, true })
        {
            int index = 0;
            int raw = LinkWallProbe.Shared.RawWalls(new(0.75f,0.25f), side, point =>
            {
                FailIf(point != wrapped[index], "Link raw wall probe lost byte wrapping or high-byte coordinates.");
                return (0xdb & (0x80 >> index++)) != 0;
            });
            FailIf(raw != 0xdb || index != 8 || !LinkWallProbe.AllSidesBlocked(raw) ||
                !LinkWallProbe.AllSidesBlocked(0xee) || LinkWallProbe.AllSidesBlocked(0xc3) ||
                LinkWallProbe.AllSidesBlocked(0xcc),
                "Capture/placement probes must preserve raw $db/$ee instead of movement's $c3/$cc.");
        }

        void Step(int count = 1, bool press = false) =>
            StepGameplayUpdates(count, Vector2.Zero, press ? ["attack"] : [], press ? ["attack"] : [], batched: true);
        _inventory.GiveTreasure(TreasureDatabase.TreasureSwitchHook, 1);
        _inventory.EquipA(InventoryState.ItemSwitchHook);
        var random = CaptureOracleRandomForValidation();
        Vector2 origin = new(120.875f, 88.25f);
        GibdoCharacter Prepare()
        {
            RestoreOracleRandomForValidation(random);
            LoadValidationRoom(4, 0x6e);
            var target = _entities.Entities<GibdoCharacter>()[0];
            _player.WarpTo(origin);
            _player.Face(Vector2I.Up);
            Step();
            FailIf(!target.BeginPegasusHit(), "Failed to prepare a stationary stunned Gibdo.");
            Step(16); // ENEMYDMG_38 invincibility expires; the long stun remains.
            target.Position += new Vector2(0.25f, 0.75f);
            for (int y = 58; y <= 88; y++)
                FailIf(_currentRoom.IsSolid(new(120, y)), "Gibdo hook approach must follow the room's real open corridor.");
            FailIf(!_entities.SwitchHook!.CanLiftEnemy(target.Position), "Source Gibdo placement is unexpectedly surrounded.");
            return target;
        }
        SwitchHookItem Hit(GibdoCharacter target)
        {
            Step(press: true);
            var hook = _entities.SwitchHook!.Item!;
            for (int i = 0; target.State != 3 && i < 40; i++) Step();
            FailIf(target.State != 3 || target.SwitchHookSubstate != 0 || target.CollisionEnabled ||
                target.StunCounter != 0 || target.KnockbackCounter != 0 || target.Health != 8 ||
                hook.State != 1 || hook.CollisionEnabled || _entities.SwitchHook.ExchangeActive,
                "Late hook collision must clear Gibdo stun/recoil and enter enemy state3 before the next item dispatch.");
            Step();
            FailIf(hook.State != 3 || hook.Substate != 0 || target.SwitchHookSubstate != 1 ||
                !_entities.PlayerMenusDisabled || !_entities.PlayerContactDisabled,
                "Next item/enemy passes must enter latch and held substate1, with exchange locks.");
            return hook;
        }
        var gibdo = Prepare();
        Vector2 targetOrigin = gibdo.Position;
        var item = Hit(gibdo);
        int animation = gibdo.AnimationFrame;
        Step(17);
        FailIf(item.Substate != 0 || gibdo.Position != targetOrigin || gibdo.AnimationFrame != animation,
            "Held Gibdo must neither move nor animate during the17-update latch.");
        Step();
        FailIf(item.Substate != 1 || item.ZHigh != 0 || gibdo.ZFixed != 0 ||
            _entities.SwitchHook!.CameraFocus != targetOrigin.Floor(), "Gibdo lift start lost high-byte camera focus.");
        var textSource = _entities.TextActiveSource;
        _entities.TextActiveSource = () => true;
        Step(3);
        FailIf(item.ZHigh != 0 || gibdo.ZFixed != 0, "Dialogue advanced the held Gibdo or initialized hook.");
        _entities.TextActiveSource = textSource;
        Step(16);
        FailIf(item.Substate != 2 || gibdo.ZFixed != -4096 || gibdo.Position != targetOrigin ||
            _player.PrecisePosition != origin, "Enemy lift must preserve XY through all16 updates.");
        Step();
        Vector2 enemyDestination = origin.Floor() + targetOrigin - targetOrigin.Floor();
        FailIf(item.Substate != 3 || gibdo.SwitchHookSubstate != 2 || gibdo.Position != enemyDestination ||
            _player.PrecisePosition != targetOrigin.Floor() || _player.FacingVector != Vector2I.Down ||
            item.PrecisePosition != origin, "Object exchange must copy high bytes to Gibdo without tile centering or losing target fractions.");
        Step(15);
        FailIf(gibdo.ZFixed != -256 || gibdo.CollisionEnabled || item.Finished, "Gibdo released before lowering update16.");
        Step();
        FailIf(!item.Finished || gibdo.State != 8 || gibdo.SwitchHookSubstate != 3 ||
            gibdo.ZFixed != 0 || !gibdo.CollisionEnabled || _entities.SwitchHook.Helper is not null ||
            !_player.IsUsingSwitchHook, "Release must fall/setState8 in the later enemy pass; parent clears next update.");
        Step();
        FailIf(_player.IsUsingSwitchHook || gibdo.State != 9, "Gibdo failed to resume choosing its native walk after exchange.");
        FailIf(!gibdo.BeginPegasusHit(), "Could not hold Gibdo for the reverse approach.");
        Step(16);
        item = Hit(gibdo); // Link now faces down through the same real corridor.
        for (int i = 0; !item.Finished && i < 80; i++) Step();
        FailIf(!item.Finished || gibdo.Health != 8 || gibdo.State != 8,
            "Gibdo could not be exchanged again from the opposite end of the corridor.");

        (Vector2 Link, Vector2 Enemy, int State, int Z, int HookState, int Substate) Run(bool batch)
        {
            var target = Prepare();
            var hook = Hit(target);
            if (batch) Step(35); else for (int i = 0; i < 35; i++) Step();
            return (_player.PrecisePosition, target.Position, target.State, target.ZFixed, hook.State, hook.Substate);
        }
        FailIf(Run(false) != Run(true), "Batched gameplay updates changed the enemy latch/lift/swap handoff.");

        // A room collision writer can close the space during the latch. The
        // release decision reads the live terrain after the animation ends.
        gibdo = Prepare();
        item = Hit(gibdo);
        _currentRoom.SetPositionTileAndCollision(gibdo.Position,
            _currentRoom.GetMetatile(gibdo.Position), 0x0f, (long)_animationTicks);
        Step(18);
        FailIf(item.State != 2 || item.Finished || gibdo.State != 8 || !gibdo.CollisionEnabled ||
            _entities.SwitchHook!.Helper is not null || _entities.SwitchHook.ExchangeActive,
            "A newly surrounded target must be released and retract without allocating the exchange helper.");

        int ContactPriority(bool useHook)
        {
            LoadValidationRoom(4, 0x6e);
            _player.WarpTo(origin);
            _player.Face(Vector2I.Up);
            Step(2);
            var target = _entities.Entities<GibdoCharacter>()[0];
            // Collision fixture: move the enemy into the open approach; Link
            // stays on the same real floor used by the traversal above.
            target.Position = origin.Floor() + Vector2.Up * 10;
            int health = _inventory.HealthQuarters;
            Step(press: useHook);
            if (useHook) FailIf(target.State != 3, "Overlapping hook failed to catch Gibdo in the late collision pass.");
            return health - _inventory.HealthQuarters;
        }
        FailIf(ContactPriority(false) != 4 || ContactPriority(true) != 0,
            "An accepted weapon collision must skip this enemy's Link contact in the same update.");

        gibdo = Prepare();
        item = Hit(gibdo);
        item.RequestCancellation();
        Step();
        FailIf(!item.Finished || gibdo.State != 8 || !gibdo.CollisionEnabled || _entities.SwitchHook!.ExchangeActive,
            "Latch cancellation failed to release Gibdo in its later enemy update.");
        gibdo = Prepare();
        item = Hit(gibdo);
        Step(18 + 5);
        _entities.SwitchHook!.Cancel();
        FailIf(gibdo.SwitchHookSubstate != 3 || gibdo.ZFixed != -1280 || _player.SwitchHookZFixed != 0,
            "Hard cancellation must release the raised enemy while clearing Link's owned exchange altitude.");
        Step(9);
        FailIf(gibdo.State != 3 || gibdo.ZFixed != -128, "Released Gibdo gravity must add old speedZ before acceleration $20.");
        Step();
        FailIf(gibdo.State != 8 || gibdo.ZFixed != 0 || !gibdo.CollisionEnabled, "Released Gibdo failed to land on fall update10.");
    }
}
