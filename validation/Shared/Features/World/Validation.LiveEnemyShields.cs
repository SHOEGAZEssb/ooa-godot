using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateLiveEnemyShieldCollisions()
    {
        foreach (var game in new[] { OracleGame.Ages,OracleGame.Seasons })
        {
            string root = game == OracleGame.Ages ? AgesGameContent.AssetRoot : SeasonsGameContent.AssetRoot;
            var source = new EnemyHandlerRegistry(root).Handlers.Single(row => row.Id == 0x32 && row.SubId == 0)
                .CombatSource(0,0,"native live shield mode probe");
            var rom = ValidationRom.LoadCleanUs(game).Span;
            int table = 7 * 0x4000 + (game == OracleGame.Ages ? 0x2d0a : 0x2a90);
            int count = game == OracleGame.Ages ? 0x7d : 0x73;
            FailIf(source.AssetRoot != root, $"{game}: combat source lost its selected pack.");
            source.ValidateNativeCollisionData();
            for (int mode = 0; mode < count; mode++)
            for (int shield = 1; shield <= 3; shield++)
            {
                EnemyShieldBumpResponse? expected = rom[table + mode * 32 + shield] switch
                {
                    0x0f => new(EnemyKnockbackStrength.Low,0x08,0x0b),
                    0x10 => new(EnemyKnockbackStrength.Normal,0x0f,0x13),
                    0x11 => new(EnemyKnockbackStrength.High,0x16,0x19),
                    _ => null
                };
                FailIf(source.ShieldBumpResponse(shield,mode) != expected,
                    $"{game}: live mode ${mode:x2}, shield ${shield:x2} must select the native current row independently of the original Keese mode.");
            }
            int masks = 7 * 0x4000 + (game == OracleGame.Ages ? 0x29a2 : 0x2740);
            for (int type = 0; type < 0x80; type++)
            for (int shield = 1; shield <= 3; shield++)
            {
                bool enabled = (rom[masks + type * 4] & (1 << shield)) != 0;
                // Mode $10's three shield effects are all bump responses in both games.
                FailIf((source.ShieldBumpResponse(shield,0x10,type) is not null) != enabled,
                    $"{game}: live collision type ${type:x2}, shield ${shield:x2} must use its current native active mask.");
            }
            bool rejected = false;
            try { (source with { CollisionMode = count }).ValidateNativeCollisionData(); }
            catch (InvalidOperationException error) { rejected = error.Message.Contains(root) && error.Message.Contains("$32:$00") && error.Message.Contains(source.Source); }
            FailIf(!rejected, $"{game}: a missing native combat mode must fail with the pack and source object identity.");
        }
        foreach (bool batched in new[] { false,true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x44); _entities.Clear();
            Vector2 origin = SharedKeeseCombatOrigin(_player);
            _player.WarpTo(origin); _player.Face(Vector2I.Right);
            _inventory.GiveTreasure(TreasureId.Shield,1); _inventory.EquipA(TreasureId.Shield);
            int health = _inventory.HealthQuarters;
            FailIf(!_entities.TrySpawnDebugEnemy(0x14,0,origin + Vector2.Right * 32,out string error),error);
            var actor = _entities.Entities<SpikedBeetleCharacter>().Single();
            var app = new ApplicationValidationFixture(this);
            void Step(Vector2 movement) => app.Step(1,movement,held:["attack"],batched:batched);
            app.Step(1,Vector2.Zero,batched:batched);
            int approaches = 0;
            while (!actor.FlipHitPending && approaches++ < 100) Step(Vector2.Right);
            FailIf(!actor.FlipHitPending || actor.FlippedCollision || actor.InvincibilityCounter != -0x15 || actor.KnockbackCounter != 0x0b ||
                _player.InvincibilityFrames != -0x0f || _player.KnockbackFrames != 0x13 || _inventory.HealthQuarters != health,
                "Upright Spiked Beetle mode $18 must select wooden-shield effect $10 through the actual gameplay contact pass.");
            Step(Vector2.Zero);
            FailIf(!actor.FlippedCollision || actor.FlipHitPending || actor.State != SpikedBeetleState.Flipped,
                "The next native JUST_HIT visit must flip the beetle and replace its collision mode with $51.");
            int landing = 0;
            while ((actor.InvincibilityCounter != 0 || actor.ZFixed != 0 || actor.KnockbackCounter != 0) && landing++ < 120) Step(Vector2.Zero);
            FailIf(landing >= 120 || !actor.FlippedCollision, "The flipped beetle must land and finish recoil before its next reachable shield contact.");
            approaches = 0;
            while (!actor.FlipHitPending && approaches++ < 100) Step(Vector2.Right);
            FailIf(!actor.FlipHitPending || !actor.FlippedCollision || actor.InvincibilityCounter != -0x10 || actor.KnockbackCounter != 0x08 ||
                _player.InvincibilityFrames != -0x08 || _player.KnockbackFrames != 0x0b || _inventory.HealthQuarters != health ||
                _player.World.Collides(_player.PrecisePosition),
                "The repeated wooden-shield contact must use live mode $51 effect $0f, including both low recoil profiles, without restoring the original $18 row.");
            int wait = actor.Counter1;
            Step(Vector2.Zero);
            FailIf(actor.FlipHitPending || !actor.FlippedCollision || actor.Counter1 != wait,
                "A shield hit on the already flipped beetle must consume JUST_HIT without restarting its flip timer.");
            landing = 0;
            while ((actor.InvincibilityCounter != 0 || actor.ZFixed != 0 || actor.KnockbackCounter != 0) && landing++ < 120)
                app.Step(1, Vector2.Zero, batched: batched);
            FailIf(landing >= 120 || !actor.FlippedCollision, "The flipped beetle must recover before the next native Shovel contact.");
            Vector2 approach = actor.Position - new Vector2(32, 4);
            FailIf(_player.World.Collides(approach), "The repeated Shovel approach must start on the room's actual walkable floor.");
            _player.WarpTo(approach); _player.Face(Vector2I.Right);
            _inventory.GiveTreasure(TreasureId.Shovel, 0); _inventory.EquipA(TreasureId.Shovel); _inventory.EquipB(0);
            approaches = 0;
            while (!RoomEntityManager.ObjectCollisionXYOverlaps(actor.CollisionBounds,
                new Rect2(_player.Position + new Vector2(3, 1), new(6, 6))) && approaches++ < 40)
                app.Step(1, Vector2.Right, batched: batched);
            FailIf(approaches >= 40 || _player.World.Collides(_player.Position), "The Shovel child must reach the flipped beetle through actual collision geometry.");
            int cues = _sound.PlayRequestsFor(SoundId.SndBombLand);
            app.Step(1, Vector2.Zero, held: ["attack"], pressed: ["attack"], batched: batched);
            for (int update = 0; !actor.FlipHitPending && update < 10; update++) app.Step(1, Vector2.Zero, batched: batched);
            FailIf(!actor.FlipHitPending || actor.InvincibilityCounter != -0x15 || actor.KnockbackCounter != 0x0b,
                "The live $51 Shovel column selects effect $0d / ENEMYDMG_14, rather than upright $18's effect $0f / ENEMYDMG_10.");
            wait = actor.Counter1;
            app.Step(1, Vector2.Zero, batched: batched);
            FailIf(actor.FlipHitPending || actor.Counter1 != wait || _sound.PlayRequestsFor(SoundId.SndBombLand) != cues,
                "Shovel bump on an already flipped beetle must consume one hit without replaying shield/flip cues or restarting its wait.");
        }
        GD.Print("Validated native live shield rows for both packs, missing-mode diagnostics, and repeated gameplay shield contacts across Spiked Beetle's $18->$51 mode change.");
    }
}
