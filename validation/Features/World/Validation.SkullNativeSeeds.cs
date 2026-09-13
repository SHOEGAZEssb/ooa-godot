using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullNativeSeeds()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        OracleSaveData.TryDeserialize(_saveData.Serialize(), out var initialSave);
        var random = CaptureOracleRandomForValidation();
        var entityState = _entities.CaptureDebugState();
        var results = new Dictionary<(bool Fire, int Item, int Selected, bool Lethal), (int Health, int Rng, Vector2 Link)>();
        Vector2[] directions = [Vector2.Up, new(1,-1), Vector2.Right, new(1,1), Vector2.Down, new(-1,1), Vector2.Left, new(-1,-1)];
        foreach (bool fire in new[] { false, true })
        foreach (var (item, selected) in new[] { (0x20,0), (0x21,1), (0x22,2), (0x23,3), (0x24,0), (0x24,1), (0x24,2), (0x24,3) })
        foreach (bool lethal in !fire && selected == 1 ? new[] { false, true } : new[] { false })
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1, Vector2 movement = default, bool attack = false)
            {
                input.CaptureForValidation(attack ? ["attack"] : [], attack ? ["attack"] : [], movement);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60, update);
            }
            _saveData.RestoreFrom(initialSave!);
            RestoreOracleRandomForValidation(random);
            _entities.RestoreDebugStateBeforeRoomParse(entityState);
            typeof(InventoryState).GetMethod("LoadFromSaveData", flags)!.Invoke(_inventory, null);
            _seedSatchel.InterruptShooter();
            _inventory.GiveTreasure(0x19, 1);
            _inventory.GiveTreasure(0x0f, 1);
            _inventory.GiveTreasure(item, 0x50);
            _inventory.SelectShooterSeeds(item - 0x20);
            _inventory.EquipA(InventoryState.ItemShooter);
            while (_inventory.MaxHealthQuarters < 56) _inventory.GiveTreasure(TreasureDatabase.TreasureHeartContainer, 4);
            _inventory.RefillHealth();
            LoadValidationRoom(4, fire ? 0x8d : 0x6e);
            _entities.RestoreDebugStateAfterRoomParse(entityState);
            EnemyCharacter enemy = fire ? _entities.Entities<FireKeeseCharacter>().Single() : _entities.Entities<GibdoCharacter>()[0];
            if (lethal) enemy.Health = 2; // Reachable remaining-health precondition; movement and input stay live.
            Vector2 start = fire ? Enumerable.Range(0, 176).Select(p => new Vector2((p & 15) * 16 + 8, (p >> 4) * 16 + 8))
                .Where(p => p.X < 240 && !_collision.Collides(p) && _currentRoom.GetTerrainInfo(p).Hazard == HazardType.None &&
                    p.DistanceTo(enemy.Position) >= 20 && p.DistanceTo(enemy.Position) <= 36)
                .OrderBy(p => p.DistanceTo(enemy.Position + Vector2.Down * 28)).First() : new Vector2(124, 88);
            _player.WarpTo(start);
            FailIf(_collision.Collides(start), "Native seed fixture must start outside obstacles on the actual room floor.");
            EmberSeedEffect? hit = null;
            var sounds = new List<int>();
            _entities.SoundRequested += sounds.Add;
            int health = enemy.Health;
            try
            {
                for (int tick = 0; hit is null && tick < 1600; tick++)
                {
                    var bat = enemy as FireKeeseCharacter;
                    Vector2 delta = enemy.Position - _player.Position;
                    Vector2 aim = directions.OrderByDescending(d => d.Normalized().Dot(delta)).First();
                    bool ready = bat is null || bat.State == 12 && bat.Counter <= 20 || bat.ZFixed >= -9 * 256;
                    if (!_seedSatchel.ShooterActive && !_entities.HasActiveShooterSeed && ready)
                    {
                        Step(movement: aim, attack: true);
                        if (item == 0x24) SetDiggingRoll(_random, selected);
                        sounds.Clear();
                        Step();
                    }
                    else
                    {
                        Vector2 move = delta.Length() > 30 ? aim : delta.Length() < 18 ? -aim : Vector2.Zero;
                        sounds.Clear();
                        Step(movement: _seedSatchel.ShooterActive ? Vector2.Zero : move);
                    }
                    hit = _entities.Entities<EmberSeedEffect>().FirstOrDefault(s => s.HasPendingNativeCollision);
                }
                FailIf(hit is null, $"Native seed${item:x2}/{selected} missed fire={fire}, batch={batch}: enemy={enemy.Position}, Link={_player.Position}, hp={enemy.Health}, LinkHP={_inventory.HealthQuarters}, dying={_player.IsDying}.");
                FailIf(hit!.SeedItem != item || hit.AnimationFrame != 0 || hit.State != EmberState.Flying || hit.CollisionType != 0x1b + selected,
                    "Native seed hit must remain pending in its original flight ID/animation until the next item pass.");
                int damage = selected == 1 || !fire && selected == 0 ? 2 : 0;
                int inv = selected == 2 ? -16 : selected == 1 ? fire ? 16 : 32 : !fire && selected == 0 ? -90 : 0;
                FailIf(enemy.Health != health - damage || enemy.InvincibilityCounter != inv ||
                    selected == 2 && (fire ? ((FireKeeseCharacter)enemy).StunCounter : ((GibdoCharacter)enemy).StunCounter) != 240 ||
                    selected == 1 && enemy.KnockbackCounter != (fire ? 8 : 0),
                    $"Native seed hit lost source health/invincibility/knockback: fire={fire}, item${item:x2}/{selected}, hp={enemy.Health}/{health}, inv={enemy.InvincibilityCounter}, recoil={enemy.KnockbackCounter}.");
                if (fire) FailIf(!((FireKeeseCharacter)enemy).Lit ||
                    !RoomEntityManager.ObjectCollisionZOverlaps(((FireKeeseCharacter)enemy).ZFixed >> 8, hit.ZFixed >> 8, 7),
                    "A real seed may hit the diving Fire Keese only inside the source Z interval, without running Link-only fire shedding.");
                Vector2 afterHit = enemy.Position;
                Step();
                FailIf(hit.HasPendingNativeCollision || hit.SeedItem != 0x20 + selected || hit.AnimationFrame != 1,
                    "The next ITEM update must consume the hit once and load Mystery's selected effect.");
                int activationSound = selected switch { 0 or 2 => OracleSoundEngine.SndLightTorch,
                    1 => OracleSoundEngine.SndPirateBell, _ => 0x90 /* SND_GALE_SEED */ };
                int[] expectedSounds = selected is 1 or 2 ? [OracleSoundEngine.SndDamageEnemy, activationSound] : [activationSound];
                // Other live room enemies can jump during these two updates.
                var seedSounds = sounds.Where(sound => sound != OracleSoundEngine.SndEnemyJump);
                FailIf(!seedSounds.SequenceEqual(expectedSounds),
                    $"Native seed sound order fire={fire}, item${item:x2}/{selected}: [{string.Join(',', sounds)}]/[{string.Join(',', expectedSounds)}].");
                if (lethal)
                    FailIf(enemy.Health != 0 || enemy.IsDead || ((GibdoCharacter)enemy).HasPendingHit || enemy.InvincibilityCounter != 31,
                        "A lethal Scent hit must consume JUST_HIT before its following zero-health death dispatch.");
                if (selected == 3)
                    FailIf(enemy.Position != afterHit, "Gale's first JUST_HIT enemy update must return before the state5 shake counter advances.");
                if (!fire && selected == 0)
                    FailIf(((GibdoCharacter)enemy).State != 10 || ((GibdoCharacter)enemy).Counter != 30 || enemy.Health != 1 ||
                        _entities.Entities<BurningEnemyPart>().Single().Counter != 58 || hit.FlameCounter != 58,
                        "Gibdo transformation and PART flame must initialize independently from the ordinary Ember item's58-update effect.");
                if (selected == 1 || selected == 2)
                {
                    Step(8);
                    FailIf(hit.Finished, "Scent/Pegasus impact animation must survive eight updates.");
                    Step();
                    FailIf(!hit.Finished || _entities.HasActiveShooterSeed, "Scent/Pegasus impact animation must release the Shooter on update9.");
                }
                else
                {
                    int duration = selected == 0 ? 58 : 50;
                    bool water = selected == 0 && _currentRoom.GetTerrainInfo(hit.Position).Hazard is HazardType.Water or HazardType.Lava;
                    if (water)
                    {
                        for (int i = 0; !hit.Finished && i < duration; i++) Step();
                        FailIf(hit.FlameCounter != 0 && (hit.ZFixed != 0 || hit.AnimationFrame != 4),
                            "An airborne Ember effect over water may end early only at ground height on its source parameter$40 frame.");
                    }
                    else
                    {
                        Step(duration - 1);
                        FailIf(hit.Finished, $"Native Ember/Gale ended early: fire={fire}, item${item:x2}/{selected}, counter={hit.FlameCounter}, state={hit.State}, position={hit.Position}, z={hit.ZFixed}.");
                        Step();
                    }
                    FailIf(!hit.Finished || _entities.HasActiveShooterSeed, "Native Ember/Gale effect did not end at its source counter boundary.");
                }
                if (selected == 2)
                {
                    int remaining = fire ? ((FireKeeseCharacter)enemy).StunCounter : ((GibdoCharacter)enemy).StunCounter;
                    int state = fire ? ((FireKeeseCharacter)enemy).State : ((GibdoCharacter)enemy).State;
                    int counter = fire ? ((FireKeeseCharacter)enemy).Counter : ((GibdoCharacter)enemy).Counter;
                    Vector2 frozen = enemy.Position;
                    int duration = remaining * 2 - ((_entities.FrameCounter + 1) & 1);
                    Step(duration - 1);
                    FailIf((fire ? ((FireKeeseCharacter)enemy).StunCounter : ((GibdoCharacter)enemy).StunCounter) != 1,
                        "A real Pegasus stun must survive until its final odd global update.");
                    Step();
                    var expected = new Vector2(((int)Mathf.Floor(frozen.X) ^ 1) + frozen.X - Mathf.Floor(frozen.X), frozen.Y);
                    FailIf((fire ? ((FireKeeseCharacter)enemy).StunCounter : ((GibdoCharacter)enemy).StunCounter) != 0 ||
                        (fire ? ((FireKeeseCharacter)enemy).State : ((GibdoCharacter)enemy).State) != state ||
                        (fire ? ((FireKeeseCharacter)enemy).Counter : ((GibdoCharacter)enemy).Counter) != counter ||
                        (fire ? ((FireKeeseCharacter)enemy).ZFixed : ((GibdoCharacter)enemy).ZFixed) != 0 ||
                        enemy.Position != expected,
                        $"Stun zero must finish15 high-byte shakes, retain the X fraction, land, and defer normal AI: fire={fire}, XY={enemy.Position}/{expected}.");
                    Step();
                    FailIf((fire ? ((FireKeeseCharacter)enemy).Counter : ((GibdoCharacter)enemy).Counter) == counter &&
                        (fire ? ((FireKeeseCharacter)enemy).State : ((GibdoCharacter)enemy).State) == state && enemy.Position == expected,
                        "The update following stun zero must resume the enemy's native state handler.");
                }
            }
            finally { _entities.SoundRequested -= sounds.Add; }
            var result = (_inventory.HealthQuarters, _entities.RandomCalls, _player.Position);
            if (lethal) FailIf(_entities.Entities<GibdoCharacter>().Count != 1,
                "A lethal Scent hit must remove its Gibdo after the JUST_HIT update.");
            FailIf(results.TryGetValue((fire, item, selected, lethal), out var prior) && prior != result,
                $"Single/batched native seeds differ: {prior}/{result}.");
            results[(fire, item, selected, lethal)] = result;
            LoadValidationRoom(4, fire ? 0x8d : 0x6e);
            FailIf(_entities.HasActiveShooterSeed || _entities.Entities<BurningEnemyPart>().Count != 0,
                "Room replacement must release both item and attached-flame lifetimes.");
        }
    }
}
