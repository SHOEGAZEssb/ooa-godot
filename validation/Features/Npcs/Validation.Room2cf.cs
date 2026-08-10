using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom2cfCucco()
    {
        const double frame = 1.0 / 60.0;
        var database = new EnemyDatabase();
        RoomObjectRecord source = database.GetRoomObjects(2, 0xcf).Single();
        EnemyHandlerDescriptor handler =
            database.EnemyHandlers.ResolveHandler(source);
        ImportedEnemyDefinition definition = database.ImportedEnemy(0x36);
        ImportedEnemyDefinition giantDefinition = database.ImportedEnemy(0x3b);
        CuccoBehaviorProfile behavior = EnemyBehaviorTables.Shared.Cucco;
        GiantCuccoBehaviorProfile giantBehavior =
            EnemyBehaviorTables.Shared.GiantCucco;
        FailIf(
            source is not
                {
                    Group: 2,
                    Room: 0xcf,
                    Order: 0,
                    Kind: RoomObjectKind.RandomEnemy,
                    Id: 0x36,
                    SubId: 0x00,
                    Flags: 0x40,
                    Count: 2
                } ||
            handler.Classification !=
                EnemyHandlerClassification.OrderedImplemented ||
            handler.Handler != EnemyHandlerKind.Cucco ||
            handler.CollisionMode != 0xaa ||
            handler.SupportsCombatSource ||
            definition is not
                {
                    TileBase: 0,
                    Palette: 2,
                    RadiusY: 6,
                    RadiusX: 6,
                    DamageQuarters: 128,
                    Health: 32,
                    Animations.Length: 2
                } ||
            giantDefinition is not
                {
                    TileBase: 0,
                    Palette: 2,
                    RadiusY: 7,
                    RadiusX: 12,
                    DamageQuarters: 2,
                    Health: 2,
                    Animations.Length: 2
                } ||
            behavior.BabyReplacementId != 0x33 ||
            behavior.GiantReplacementId != 0x3b ||
            giantBehavior is not
                {
                    WanderSpeedRaw: 0x1e,
                    InitialScreenShakeUpdates: 0x30,
                    PostHitHealth: 0x40
                } ||
            behavior.HopZValues.Select(value => value.Value).ToArray() is not
                [0, -1, -1, -2, -2, -2, -3, -3,
                 -3, -3, -2, -2, -2, -1, -1, 0] ||
            behavior.RevengeDelays.Select(value => value.Value).ToArray() is not
                [30, 26, 24, 22, 20, 18, 16, 14, 12],
            "Room 2:cf lost obj_RandomEnemy $40 $36 $00, its Cucco " +
            "handler, or the source hop/revenge tables.");

        LoadValidationRoom(2, 0xcf);
        CuccoCharacter[] cuccos =
            _entities.Entities<CuccoCharacter>().ToArray();
        CuccoCharacter cucco = cuccos[0];
        FailIf(
            cuccos.Length != 2 ||
            _entities.RoomEnemyCount != 2 ||
            cuccos.Any(entity =>
                entity.State != CuccoState.Uninitialized ||
                entity.Visible ||
                entity.Record.Id != definition.Id ||
                entity.Record.SubId != definition.SubId),
            $"Room 2:cf did not construct its two source-ordered Cuccos " +
            $"(entities={cuccos.Length}, room-count={_entities.RoomEnemyCount}, " +
            $"states={string.Join(',', cuccos.Select(entity => entity.State))}, " +
            $"visible={string.Join(',', cuccos.Select(entity => entity.Visible))}).");

        int callsBeforeInitialization = _random.Calls;
        _entities.Update(frame, _player);
        FailIf(
            cuccos.Any(entity =>
                entity.State != CuccoState.Standing ||
                !entity.Visible ||
                entity.CurrentAnimationPixelHash == 0) ||
            _random.Calls != callsBeforeInitialization,
            "ENEMY_CUCCO $36:$00 state 0 changed RNG or failed to enter " +
            "visible state $08.");

        OracleRandomState randomState = _random.CaptureState();
        _random.RestoreState(randomState with { Rng1 = 0x40, Rng2 = 0x00 });
        int callsBeforeHop = _random.Calls;
        Vector2 hopOrigin = cucco.Position;
        _entities.Update(frame, _player);
        FailIf(
            cucco.State != CuccoState.Hopping ||
            cucco.Counter1 != 0 ||
            cucco.Counter2 != 2 ||
            cucco.Angle != 0 ||
            _random.Calls != callsBeforeHop + 2,
            "Cucco state $08 did not consume one shared RNG result per " +
            "source-ordered instance and " +
            "decode b=$00/c=$00/e=$00 into its source hop.");

        _entities.Update(frame, _player);
        FailIf(
            cucco.State != CuccoState.Hopping ||
            cucco.Counter1 != 1 ||
            cucco.Counter2 != 1 ||
            cucco.Z != 0 ||
            cucco.Position != hopOrigin + Vector2.Up * 0.5f,
            "Cucco state $09 lost its zero-update counter decrement or " +
            "raw SPEED_80 movement boundary.");
        for (int update = 1; update < 16; update++)
            _entities.Update(frame, _player);
        FailIf(
            cucco.State != CuccoState.Standing ||
            cucco.Counter1 != 16 ||
            cucco.Counter2 != 0 ||
            cucco.Z != 0,
            "Cucco state $09 did not finish its source 16-value Z cycle.");

        cucco.Position = new Vector2(0x50, 0x48);
        cuccos[1].Position = new Vector2(0x20, 0x20);
        _player.WarpTo(new Vector2(0x4a, 0x48), recordSafe: false);
        _player.Face(Vector2I.Right);
        FailIf(
            !_entities.TryUseBracelet(_player, Vector2I.Zero) ||
            cucco.State != CuccoState.Held ||
            cucco.CurrentAnimationFrame != 0 ||
            !_player.IsCarryingObject,
            "Room 2:cf's adult Cucco was not grabbable from state $08.");
        for (int update = 0; update < 7; update++)
            _entities.Update(frame, _player);
        FailIf(
            cucco.CurrentAnimationFrame != 0,
            "Held adult Cucco advanced before its source eight-update " +
            "animation boundary.");
        _entities.Update(frame, _player);
        FailIf(
            cucco.CurrentAnimationFrame != 1,
            "Held adult Cucco did not run enemyAnimate on its source " +
            "eight-update frame boundary.");
        FailIf(
            !_entities.TryUseBracelet(_player, Vector2I.Right) ||
            cucco.State != CuccoState.Thrown ||
            cucco.ThrowDirection != Vector2I.Right ||
            cucco.SpeedZ != -0xf0 ||
            cucco.ThrowSpeedRaw != 0x3c ||
            _player.IsCarryingObject,
            "Adult Cucco release did not start the shared weight-0 throw path.");
        for (int update = 0;
            update < 180 && cucco.State == CuccoState.Thrown;
            update++)
        {
            _entities.Update(frame, _player);
        }
        FailIf(
            cucco.State != CuccoState.Runaway ||
            _entities.RoomEnemyCount != 2,
            "Thrown adult Cucco did not land into source state $0a.");

        var transformationSpawns = new List<RoomEntitySpawn>();
        var secondAdapter = new CuccoRoomEntity(cuccos[1]);
        SeedHitResult mysteryResult = secondAdapter.ApplySeedHit(
            cuccos[1].CollisionBounds.Grow(1),
            cuccos[1].Position,
            OwlStatueDatabase.MysterySeedItem,
            transformationSpawns);
        FailIf(
            mysteryResult != SeedHitResult.Consume ||
            cuccos[1].State != CuccoState.Transforming ||
            cuccos[1].Visible ||
            transformationSpawns is not [PuzzlePuffSpawn { Sound: 0 }],
            "A Mystery Seed did not hide the calm Cucco and create " +
            "INTERAC_PUFF $05:$02.");
        _entities.Spawn<PuzzlePuffEffect>(transformationSpawns[0]);
        for (int update = 0; update < 18; update++)
            _entities.Update(frame, _player);
        FailIf(
            _entities.Entities<BabyCuccoCharacter>().Count != 0,
            "Mystery-seed replacement occurred before the puff terminal " +
            "parameter boundary.");
        _entities.Update(frame, _player);
        FailIf(
            _entities.Entities<CuccoCharacter>().Count != 1 ||
            _entities.Entities<BabyCuccoCharacter>().Count != 1 ||
            _entities.RoomEnemyCount != 2,
            "Mystery-seed INTERAC_PUFF completion did not replace the calm " +
            "adult with ENEMY_BABY_CUCCO $33:$00 in the same enemy slot.");

        // ENEMYDMG_0c writes exactly $20 invincibility updates. Arrange the
        // source's sixteenth accepted hit and verify the child part consumes
        // the next shared RNG value in the same update it is spawned.
        for (int hit = 1; hit <= 16; hit++)
        {
            cucco.Position = new Vector2(0x50, 0x48);
            FailIf(
                !_entities.ApplySwordHit(
                    cucco.CollisionBounds.Grow(1), cucco.Position) ||
                cucco.HitCount != hit ||
                cucco.State != CuccoState.Runaway,
                $"Cucco accepted-hit count diverged at hit {hit}.");
            if (hit == 16)
                break;
            for (int update = 0; update < 32; update++)
                _entities.Update(frame, _player);
        }

        randomState = _random.CaptureState();
        _random.RestoreState(randomState with { Rng1 = 0x40, Rng2 = 0x00 });
        int callsBeforeAttacker = _random.Calls;
        _entities.Update(frame, _player);
        CuccoAttackerCharacter attacker =
            _entities.Entities<CuccoAttackerCharacter>().Single();
        FailIf(
            cucco.RevengeCounter != 30 ||
            attacker.State != CuccoAttackerState.Entering ||
            attacker.Counter != 24 ||
            attacker.Position != new Vector2(0x08, 0x05) ||
            attacker.Speed != 0x32 ||
            _random.Calls != callsBeforeAttacker + 1,
            "The sixteenth Cucco hit did not spawn/update PART_CUCCO_ATTACKER " +
            "with the source delay, edge decode, speed, and RNG order.");

        var giantSpawns = new List<RoomEntitySpawn>();
        var angryAdapter = new CuccoRoomEntity(cucco);
        mysteryResult = angryAdapter.ApplySeedHit(
            cucco.CollisionBounds.Grow(1),
            cucco.Position,
            OwlStatueDatabase.MysterySeedItem,
            giantSpawns);
        FailIf(
            mysteryResult != SeedHitResult.Consume ||
            cucco.State != CuccoState.Transforming ||
            giantSpawns is not [PuzzlePuffSpawn { Sound: 0 }],
            "A Mystery Seed did not begin the aggressive Cucco's source " +
            "ENEMY_GIANT_CUCCO replacement.");
        _entities.Spawn<PuzzlePuffEffect>(giantSpawns[0]);
        for (int update = 0; update < 19; update++)
            _entities.Update(frame, _player);
        FailIf(
            !cucco.IsGiant ||
            cucco.Record.Id != 0x3b ||
            cucco.State != CuccoState.Uninitialized ||
            cucco.Visible ||
            _entities.RoomEnemyCount != 2,
            "The aggressive Mystery Seed puff did not replace the adult " +
            "in-place with ENEMY_GIANT_CUCCO $3b:$00.");
        _entities.Update(frame, _player);
        FailIf(
            cucco.State != CuccoState.Standing ||
            !cucco.Visible ||
            _entities.ScreenShakeCounter != 0x2f ||
            cucco.HitCount != 0 ||
            cucco.Health != 0x02 ||
            cucco.CurrentAnimationDrawOffset != new Vector2(-16, -24) ||
            cucco.CurrentAnimationTexture.GetWidth() != 32 ||
            cucco.CurrentAnimationTexture.GetHeight() != 32 ||
            cucco.CurrentAnimationPixelHash == 0,
            "Giant Cucco state $00 lost its source health, positioned OAM, " +
            "visibility, or 48-update screen shake initialization.");
        FailIf(
            !_entities.ApplySwordHit(
                cucco.CollisionBounds.Grow(1), cucco.Position) ||
            cucco.HitCount != 0x01 ||
            cucco.Health != 0x40 ||
            cucco.CollisionEnabled ||
            cucco.State != CuccoState.Runaway,
            "The Ages Giant Cucco bug did not disable collisions on its " +
            "first sword slash while restoring health to $40.");
        FailIf(
            _entities.ApplySwordHit(
                cucco.CollisionBounds.Grow(1), cucco.Position) ||
            cucco.HitCount != 0x01,
            "The collision-disabled Giant Cucco incorrectly accepted a " +
            "second sword slash.");
        Vector2 runawayOrigin = cucco.Position;
        _entities.Update(frame, _player);
        FailIf(
            cucco.State != CuccoState.Runaway ||
            cucco.Position.DistanceTo(runawayOrigin) < 0.74f,
            "The collision-disabled Giant Cucco did not continue running " +
            "away with raw SPEED_c0.");

        GD.Print(
            "Validated room 2:cf ENEMY_CUCCO $36:$00: ordered random " +
            "placement, shared-RNG hop cycle, bracelet throw/landing, " +
            "held animation, both Mystery Seed replacements, " +
            "no-knockback hit path, " +
            "PART_CUCCO_ATTACKER revenge spawn, full Giant Cucco OAM, and " +
            "the Ages one-slash collision bug.");
    }
}
