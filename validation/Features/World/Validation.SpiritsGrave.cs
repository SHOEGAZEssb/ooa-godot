using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSpiritsGrave()
    {
        const double update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        static bool IsGbcColor(Color color, int red, int green, int blue)
        {
            const float textureTolerance = 1.5f / 255.0f;
            return Mathf.Abs(color.R - red / 31.0f) <= textureTolerance &&
                Mathf.Abs(color.G - green / 31.0f) <= textureTolerance &&
                Mathf.Abs(color.B - blue / 31.0f) <= textureTolerance;
        }
        static int CountOpaquePixels(Image image)
        {
            int result = 0;
            for (int y = 0; y < image.GetHeight(); y++)
            for (int x = 0; x < image.GetWidth(); x++)
            {
                if (image.GetPixel(x, y).A > 0.1f)
                    result++;
            }
            return result;
        }
        int[] dungeonRooms = Enumerable.Range(0x10, 0x16).ToArray();
        byte[] originalFlags = dungeonRooms
            .Select(room => _saveData.GetRoomFlags(4, room))
            .ToArray();

        void RestoreFlags()
        {
            for (int index = 0; index < dungeonRooms.Length; index++)
            for (int bit = 0; bit < 8; bit++)
            {
                byte mask = (byte)(1 << bit);
                _saveData.SetRoomFlag(
                    4, dungeonRooms[index], mask,
                    (originalFlags[index] & mask) != 0);
            }
        }

        void PrepareRoom(int room)
        {
            _player.Visible = true;
            _player.EndCutsceneControl();
            _player.EndGetItemTwoHandPose();
            _entities.ClearRecentEnemyDefeats();
            LoadValidationRoom(4, room);
        }

        void StepEntities(int count = 1)
        {
            for (int frame = 0; frame < count; frame++)
                _entities.Update(update, _player);
        }

        var data = new SpiritsGraveDatabase();
        var enemyData = new EnemyDatabase();
        RoomObjectRecord fallingRope = enemyData.GetRoomObjects(4, 0x73)
            .Single(record => record.Id == 0x10);
        RoomObjectRecord linkedGhini = enemyData.GetRoomObjects(5, 0x99)
            .Single(record => record.Id == 0x17);
        VisualRecord essenceGlow = data.Visual("essence-glow");
        AnimationDefinition essenceGlowAnimation =
            OracleGraphicsCache.GetAnimationDefinition(
                essenceGlow.Animations.Single());
        VisualRecord energyBead = data.Visual("energy-bead");
        AnimationDefinition[] energyBeadAnimations = energyBead.Animations
            .Select(OracleGraphicsCache.GetAnimationDefinition)
            .ToArray();
        Image energyBeadSource = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_circlebeads.png");
        using Texture2D energyBeadFirstTexture =
            NpcCharacter.BuildOamTextureUncachedForValidation(
                energyBeadSource,
                energyBeadAnimations[0].Frames[0].EncodedOam,
                energyBead.TileBase,
                energyBead.Palette,
                sourceGrayscaleInverted: false);
        using Image energyBeadFirstImage = energyBeadFirstTexture.GetImage();
        int energyBeadFirstOpaquePixels =
            CountOpaquePixels(energyBeadFirstImage);
        int[][] expectedEnergyBeadDurations =
        [
            [6, 6, 5, 1],
            [6, 6, 5, 1],
            [6, 6, 5, 1],
            [6, 6, 6, 1],
            [6, 6, 5, 1],
            [6, 6, 5, 1],
            [6, 6, 5, 1],
            [6, 6, 6, 1]
        ];
        int nativeCount = dungeonRooms.Sum(
            room => data.GetRoomRecords(4, room).Count);
        if (nativeCount != 17 ||
            data.GetRoomRecords(4, 0x20).Count != 7 ||
            enemyData.ImportedEnemy(0x0a) is not { Health: 3, DamageQuarters: 2 } ||
            enemyData.ImportedEnemy(0x10) is not { Health: 2, DamageQuarters: 2 } ||
            enemyData.ImportedEnemy(0x17) is not { Health: 10, DamageQuarters: 2 } ||
            enemyData.ImportedEnemy(0x28) is not { Health: 5, DamageQuarters: 2 } ||
            enemyData.TryGetImportedEnemyDefinition(fallingRope, out _) ||
            enemyData.TryGetImportedEnemyDefinition(linkedGhini, out _) ||
            data.Enemy(0x3f) is not
                { Health: 2, DamageQuarters: 128, SourceGrayscaleInverted: false } ||
            data.Enemy(0x70) is not
                { Health: 12, DamageQuarters: 1, SourceGrayscaleInverted: false } ||
            data.Enemy(0x78) is not { Health: 8, DamageQuarters: 2 } ||
            data.Visual("colored-cube").Animations.Length != 30 ||
            data.Visual("colored-cube").SourceGrayscaleInverted ||
            energyBead is not
                {
                    Sprites: ["spr_circlebeads"],
                    TileBase: 0,
                    Palette: 4,
                    SourceGrayscaleInverted: false,
                    Animations.Length: 8
                } ||
            energyBeadFirstOpaquePixels != 100 ||
            energyBeadAnimations.Where((animation, index) =>
                animation.Frames.Length != 4 ||
                !animation.Frames
                    .Select(frame => frame.Duration)
                    .SequenceEqual(expectedEnergyBeadDurations[index]) ||
                !animation.Frames
                    .Select(frame => frame.Parameter)
                    .SequenceEqual([0, 0, 0, 0xff]))
                .Any() ||
            enemyData.MoblinBoomerang is not
                { TileBase: 10, Palette: 4, Animations.Length: 1 } ||
            data.Visual("pumpkin-projectile").Animations.Length != 1 ||
            essenceGlow is not
                {
                    Sprites: ["spr_pedestal_flame_crystal"],
                    TileBase: 6,
                    Palette: 4,
                    SourceGrayscaleInverted: true
                } ||
            essenceGlowAnimation.Frames.Length != 4 ||
            essenceGlowAnimation.LoopStart != 0 ||
            essenceGlowAnimation.Frames.Any(frame => frame.Duration != 2) ||
            !essenceGlowAnimation.Frames
                .Select(frame => frame.Parameter)
                .SequenceEqual([0, 1, 0, 1]) ||
            essenceGlowAnimation.Frames.Any(
                frame => frame.EncodedOam.Split(';').Length != 8) ||
            !IsGbcColor(data.CubePalettes[6][1], 0x1b, 0x00, 0x00) ||
            !IsGbcColor(data.CubePalettes[6][2], 0x03, 0x10, 0x1f) ||
            !IsGbcColor(data.CubePalettes[7][1], 0x03, 0x10, 0x1f) ||
            !IsGbcColor(data.CubePalettes[7][2], 0x1f, 0x16, 0x06) ||
            !data.EssenceMessage.Contains("Eternal Spirit", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Spirit's Grave native object/enemy/visual contract is incomplete.");
        }

        var chests = new ChestDatabase();
        (int Room, int Position, string Treasure)[] expectedChests =
        {
            (0x15, 0x5a, "TREASURE_OBJECT_GASHA_SEED_01"),
            (0x16, 0x17, "TREASURE_OBJECT_SMALL_KEY_03"),
            (0x1a, 0x2c, "TREASURE_OBJECT_SMALL_KEY_03"),
            (0x1c, 0x59, "TREASURE_OBJECT_RING_0e"),
            (0x1d, 0x53, "TREASURE_OBJECT_COMPASS_02"),
            (0x1f, 0x73, "TREASURE_OBJECT_RING_04"),
            (0x23, 0x16, "TREASURE_OBJECT_BOSS_KEY_03"),
            (0x25, 0x12, "TREASURE_OBJECT_MAP_02")
        };
        foreach ((int room, int position, string treasure) in expectedChests)
        {
            if (!chests.TryGet(4, room, position, out ChestRecord chest) ||
                chest.TreasureObject != treasure)
            {
                throw new InvalidOperationException(
                    $"Spirit's Grave chest 4:{room:x2}/${position:x2} is not {treasure}.");
            }
        }

        // Tile $69 uses breakable-room action $8c: set the left flag in 4:1d
        // and the opposite right flag in its dungeon-layout neighbor 4:1c.
        // The linked flags must be committed before SND_SOLVEPUZZLE and before
        // the Ember child deletes itself, otherwise the exception repeats from
        // flame counter zero forever and re-entry restores the solid wall.
        var breakables = new BreakableTileDatabase();
        if (!breakables.TryGet(
                2, 0x69,
                out BreakableTileRecord burnableWall) ||
            burnableWall.RoomFlagAction != 0x8c ||
            burnableWall.Replacement != 0x37 ||
            burnableWall.Effect != 0xdf ||
            burnableWall.GashaMaturity != 50)
        {
            throw new InvalidOperationException(
                "Spirit's Grave burnable wall $69 did not retain its imported " +
                "$8c linked-room flag, $37 replacement, $df effect, and 50 maturity.");
        }
        _saveData.SetRoomFlag(4, 0x1d, 0x08, false);
        _saveData.SetRoomFlag(4, 0x1c, 0x02, false);
        PrepareRoom(0x1d);
        Vector2 burnableWallCenter = new(0x08, 0x58);
        if (_currentRoom.GetMetatile(burnableWallCenter) != 0x69 ||
            !_currentRoom.IsSolid(burnableWallCenter))
        {
            throw new InvalidOperationException(
                "Room 4:1d did not begin with the solid left burnable wall $69.");
        }
        int maturityBeforeWall = _saveData.GashaMaturity;
        _sound.ClearPlayRequestAudit();
        EmberSeedEffect wallSeed = _entities.Spawn<EmberSeedEffect>(
            new EmberSeedSpawn(
                burnableWallCenter + new Vector2(0, 10),
                Vector2I.Up, new SeedSatchelDatabase().Ember, 4));
        StepEntities(67);
        if (!wallSeed.Finished ||
            _entities.Entities<EmberSeedEffect>().Count != 0 ||
            _currentRoom.GetMetatile(burnableWallCenter) != 0x37 ||
            _currentRoom.IsSolid(burnableWallCenter) ||
            !_saveData.HasRoomFlag(4, 0x1d, 0x08) ||
            !_saveData.HasRoomFlag(4, 0x1c, 0x02) ||
            _saveData.GashaMaturity != maturityBeforeWall + 50 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1)
        {
            throw new InvalidOperationException(
                "Burning room 4:1d's left exit did not terminate the Ember flame, " +
                "play SND_SOLVEPUZZLE, open tile $69->$37, and set both linked flags.");
        }
        PrepareRoom(0x1c);
        PrepareRoom(0x1d);
        if (_currentRoom.GetMetatile(burnableWallCenter) != 0x37 ||
            _currentRoom.IsSolid(burnableWallCenter))
        {
            throw new InvalidOperationException(
                "Room 4:1d re-entry from the left did not apply flag $08's " +
                "persistent $69->$37 wall substitution.");
        }

        // Room 4:1d's right shutter is layout tile $79 without a placed door
        // controller. replaceShutterForLinkEntering still changes the crossed
        // tile to $a0 during the destination preload, before object parsing.
        Vector2 layoutShutterCenter = new(0xe8, 0x58);
        if (_currentRoom.GetMetatile(layoutShutterCenter) != 0x79 ||
            !_currentRoom.IsSolid(layoutShutterCenter))
        {
            throw new InvalidOperationException(
                "A direct room 4:1d load did not restore solid layout shutter $79.");
        }
        PrepareRoom(0x1e);
        _player.WarpTo(new Vector2(0x06, 0x58));
        _transitions.BeginScroll(_player, Vector2I.Left, 0x1d);
        if (!_transitions.ScrollActive ||
            _currentRoom.Id != 0x1d ||
            _currentRoom.GetMetatile(layoutShutterCenter) != 0xa0 ||
            _currentRoom.IsSolid(layoutShutterCenter))
        {
            throw new InvalidOperationException(
                "Scrolling from room 4:1e into 4:1d did not preload the crossed " +
                "right layout shutter $79 as non-solid floor $a0.");
        }
        _transitions.UpdateScroll(1.0);
        if (_transitions.ScrollActive ||
            _currentRoom.GetPackedPosition(_player.Position) != 0x5e ||
            _currentRoom.IsSolid(_player.Position))
        {
            throw new InvalidOperationException(
                "Room 4:1d's right-entry scroll left Link stuck in shutter $79.");
        }
        _saveData.WriteWramByte(0xc65f, (byte)maturityBeforeWall);
        _saveData.WriteWramByte(0xc660, (byte)(maturityBeforeWall >> 8));

        // Every room in dungeon01 must parse completely. This catches a later
        // sub-ID accidentally being routed through a D1-only enemy handler.
        foreach (int room in dungeonRooms)
            PrepareRoom(room);

        PrepareRoom(0x17);
        if (_entities.Entities<BoomerangMoblinCharacter>().Count != 2)
            throw new InvalidOperationException("Room 4:17 did not create both boomerang Moblins.");
        int moblinRandomCalls = _entities.RandomCalls;
        StepEntities();
        if (_entities.RandomCalls != moblinRandomCalls + 4)
        {
            throw new InvalidOperationException(
                "Each Boomerang Moblin did not consume separate duration and " +
                "cardinal-angle RNG values on its first object update.");
        }
        _player.WarpTo(new Vector2(0x78, 0x78));
        for (int frame = 0;
             frame < 480 && _entities.Entities<MoblinBoomerangProjectile>().Count == 0;
             frame++)
        {
            StepEntities();
        }
        if (_entities.Entities<MoblinBoomerangProjectile>() is not
            [{ Finished: false, Counter: 0x2d, Speed: 2.0f }])
        {
            throw new InvalidOperationException(
                "Room 4:17's Moblins did not create the imported rotating boomerang " +
                "with its state-0 $2d/SPEED_200 initialization.");
        }
        MoblinBoomerangProjectile moblinBoomerang =
            _entities.Entities<MoblinBoomerangProjectile>().Single();
        StepEntities();
        if (moblinBoomerang.Counter != 0x2c || moblinBoomerang.Speed != 2.0f ||
            moblinBoomerang.Returning ||
            moblinBoomerang.CollisionBounds.Size != new Vector2(4, 4))
        {
            throw new InvalidOperationException(
                "The Moblin boomerang did not begin its source 6-update gradual slowdown.");
        }

        PrepareRoom(0x1c);
        if (_entities.Entities<RopeCharacter>().Count != 2)
            throw new InvalidOperationException("Room 4:1c did not create both Ropes.");
        int ropeRandomCalls = _entities.RandomCalls;
        StepEntities();
        if (_entities.RandomCalls != ropeRandomCalls ||
            _entities.Entities<RopeCharacter>().Any(rope => rope.Counter != 0))
        {
            throw new InvalidOperationException(
                "D1 Ropes consumed RNG while source state 0 was only installing SPEED_80.");
        }
        _player.WarpTo(new Vector2(-100, -100), recordSafe: false);
        List<RopeCharacter> ropes = _entities.Entities<RopeCharacter>();
        Vector2[] ropeStarts = ropes.Select(rope => rope.Position).ToArray();
        StepEntities();
        if (_entities.RandomCalls != ropeRandomCalls ||
            ropes.Any(rope => rope.Speed != 0.5f) ||
            ropes.Where((rope, index) =>
                !Mathf.IsEqualApprox(rope.Position.DistanceTo(ropeStarts[index]), 0.5f)).Any())
        {
            throw new InvalidOperationException(
                "D1 Ropes did not retain source SPEED_80 before their first charge ends.");
        }

        // rope_state_chargeLink keeps its cardinal lock until the shared
        // side-view movement helper reports a wall. That collision immediately
        // restores SPEED_60, installs counter2=$40, and consumes one random
        // value through rope_changeDirection.
        RopeCharacter chargingRope = ropes[0];
        RopeCharacter idleRope = ropes[1];
        chargingRope.Position = new Vector2(0x38, 0x38);
        idleRope.Position = new Vector2(0x78, 0x48);
        _player.WarpTo(new Vector2(0x00, 0x42), recordSafe: false);
        Vector2 chargeStart = chargingRope.Position;
        chargingRope.UpdateFrame(_player.Position);
        if (chargingRope.State != RopeState.Charging ||
            chargingRope.Angle != 0x18 ||
            chargingRope.Speed != 1.25f ||
            chargingRope.Position != chargeStart)
        {
            throw new InvalidOperationException(
                "A room 4:1c Rope did not acquire Link to the left without moving " +
                "on the source lock-on update.");
        }

        _player.WarpTo(new Vector2(0xc8, 0x78), recordSafe: false);
        int chargeRandomCalls = _entities.RandomCalls;
        int chargeUpdates = 0;
        while (chargingRope.State == RopeState.Charging &&
               chargeUpdates < 64)
        {
            chargingRope.UpdateFrame(_player.Position);
            chargeUpdates++;
            if (chargingRope.State == RopeState.Charging &&
                chargingRope.Angle != 0x18)
            {
                throw new InvalidOperationException(
                    "A charging Rope retargeted after its source cardinal lock.");
            }
        }
        if (chargingRope.State != RopeState.Wandering ||
            chargingRope.Cooldown != 0x40 ||
            chargingRope.Speed != 0.375f ||
            chargingRope.Counter is < 0x70 or > 0xe0 ||
            (chargingRope.Counter & 0x0f) != 0 ||
            _entities.RandomCalls != chargeRandomCalls + 1)
        {
            throw new InvalidOperationException(
                "Room 4:1c's Rope did not leave its charge on wall collision, " +
                "restore SPEED_60/counter2 $40, and run rope_changeDirection once " +
                $"(state={chargingRope.State}, cooldown=${chargingRope.Cooldown:x2}, " +
                $"speed={chargingRope.Speed}, counter=${chargingRope.Counter:x2}, " +
                $"rng={_entities.RandomCalls - chargeRandomCalls}, " +
                $"updates={chargeUpdates}, position={chargingRope.Position}).");
        }

        // Dungeon metatile $16 uses breakable mode $28, whose effect byte
        // creates INTERAC_ROCKDEBRIS $06 at the broken tile center. Its state
        // 0 requests SND_BREAK_ROCK before the 4/4/4/4/terminal animation.
        Vector2 rockCenter = new(0x78, 0x38);
        _player.WarpTo(new Vector2(0x78, 0x46), recordSafe: false);
        _sound.ClearPlayRequestAudit();
        if (_currentRoom.GetMetatile(rockCenter) != 0x16 ||
            !_combat.ApplySwordTileHit(
                _player, direction: 0, swordPoke: false) ||
            _currentRoom.GetMetatile(rockCenter) != 0xa0 ||
            _entities.Entities<RockDebrisEffect>() is not
                [{ Position: var debrisPosition }])
        {
            throw new InvalidOperationException(
                "Room 4:1c's metatile $16 did not break to $a0 and create " +
                "INTERAC_ROCKDEBRIS $06.");
        }
        RockDebrisEffect rockDebris =
            _entities.Entities<RockDebrisEffect>().Single();
        using (Image firstRockDebrisFrame = rockDebris.CurrentTexture.GetImage())
        {
            ulong firstRockDebrisHash =
                OracleGraphicsCache.PixelHash(firstRockDebrisFrame);
            if (debrisPosition != rockCenter ||
                firstRockDebrisHash != 0x12f5a6fa793c2798UL)
            {
                throw new InvalidOperationException(
                    "INTERAC_ROCKDEBRIS $06 did not use its tile-centered " +
                    "first four-chip OAM frame " +
                    $"(position={debrisPosition}, hash={firstRockDebrisHash:x16}).");
            }
        }
        StepEntities();
        if (rockDebris.ElapsedUpdates != 1 ||
            rockDebris.AnimationFrame != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBreakRock) != 1)
        {
            throw new InvalidOperationException(
                "INTERAC_ROCKDEBRIS state 0 did not request SND_BREAK_ROCK " +
                "without advancing animation 0.");
        }
        for (int frame = 1; frame <= 4; frame++)
        {
            StepEntities(4);
            if (rockDebris.AnimationFrame != frame || rockDebris.Finished)
            {
                throw new InvalidOperationException(
                    $"INTERAC_ROCKDEBRIS did not enter animation frame {frame} " +
                    $"after {frame * 4} state-1 updates.");
            }
        }
        if ((rockDebris.CurrentParameter & 0x80) == 0 ||
            rockDebris.ElapsedUpdates != 17)
        {
            throw new InvalidOperationException(
                "INTERAC_ROCKDEBRIS did not expose terminal parameter $ff " +
                "after its four 4-update frames.");
        }
        StepEntities();
        if (!rockDebris.Finished ||
            rockDebris.ElapsedUpdates != 18 ||
            _entities.Entities<RockDebrisEffect>().Count != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBreakRock) != 1)
        {
            throw new InvalidOperationException(
                "INTERAC_ROCKDEBRIS did not delete one update after its " +
                "terminal frame without replaying SND_BREAK_ROCK.");
        }

        PrepareRoom(0x1e);
        if (_entities.Entities<GhiniCharacter>().Count != 1 ||
            _entities.Entities<SpiritsGraveRewardController>().Count != 1)
        {
            throw new InvalidOperationException(
                "Room 4:1e did not create its Ghini and native falling-key reward.");
        }
        GhiniCharacter ghini = _entities.Entities<GhiniCharacter>().Single();
        int ghiniRandomCalls = _entities.RandomCalls;
        StepEntities();
        if (_entities.RandomCalls != ghiniRandomCalls ||
            ghini.State != GhiniState.Choosing)
        {
            throw new InvalidOperationException(
                "The room 4:1e Ghini consumed its route RNG during source state 0.");
        }
        StepEntities();
        if (_entities.RandomCalls != ghiniRandomCalls + 1 ||
            ghini.State != GhiniState.Moving)
        {
            throw new InvalidOperationException(
                "The room 4:1e Ghini did not choose its route in state 8 on the next update.");
        }
        int ghiniAngleBeforeBounce = ghini.Angle;
        ghini.Position = ghiniAngleBeforeBounce switch
        {
            0x00 => new Vector2(0x50, 5.75f),
            0x08 => new Vector2(_currentRoom.Width - 6.25f, 0x40),
            0x10 => new Vector2(0x50, _currentRoom.Height - 6.25f),
            _ => new Vector2(5.75f, 0x40)
        };
        StepEntities();
        int expectedGhiniBounceAngle = ghiniAngleBeforeBounce switch
        {
            0x00 => 0x10,
            0x08 => 0x18,
            0x10 => 0x00,
            _ => 0x08
        };
        if (ghini.Angle != expectedGhiniBounceAngle ||
            ghini.AnimationIndex != (expectedGhiniBounceAngle < 0x10 ? 1 : 0))
        {
            throw new InvalidOperationException(
                "The D1 Ghini did not update its facing animation after the source " +
                "screen-boundary bounce.");
        }
        _sound.ClearPlayRequestAudit();
        if (!_entities.ApplySwordHit(
                new Rect2(Vector2.Zero, new Vector2(240, 176)), damage: 2) ||
            ghini.Health != 8 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDamageEnemy) != 1)
        {
            throw new InvalidOperationException(
                "The room 4:1e Ghini did not request SND_DAMAGE_ENEMY for an " +
                "accepted ordinary sword hit.");
        }
        StepEntities(21);
        if (!_entities.ApplySwordHit(
                new Rect2(Vector2.Zero, new Vector2(240, 176)), damage: 20) ||
            !ghini.PendingKnockbackDeath ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDamageEnemy) != 2)
        {
            throw new InvalidOperationException(
                "The room 4:1e Ghini did not retain its collision-effect sound " +
                "and begin recoil on the lethal sword hit.");
        }
        while (ghini.KnockbackCounter > 0)
            ghini.UpdateFrame();
        ghini.UpdateFrame();
        StepEntities();
        if (_entities.Entities<GroundTreasurePickup>() is not
            [{ Record.TreasureObject: "TREASURE_OBJECT_SMALL_KEY_01" }])
        {
            throw new InvalidOperationException(
                "Room 4:1e did not spawn its above-screen two-bounce small " +
                "key after the Ghini's lethal recoil.");
        }
        GroundTreasurePickup fallingKey =
            _entities.Entities<GroundTreasurePickup>().Single();
        _player.WarpTo(new Vector2(0xd8, 0x98), recordSafe: false);
        for (int frame = 0;
             frame < 300 && fallingKey.State != PickupState.Waiting;
             frame++)
        {
            StepEntities();
        }
        if (fallingKey.State != PickupState.Waiting)
        {
            throw new InvalidOperationException(
                "Room 4:1e's small key did not finish its source two-bounce fall.");
        }
        int keysBeforePickup = _inventory.GetDungeonSmallKeys(1);
        _sound.ClearPlayRequestAudit();
        _player.WarpTo(fallingKey.Position, recordSafe: false);
        StepEntities();
        if (_inventory.GetDungeonSmallKeys(1) != keysBeforePickup + 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetSeed) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 0)
        {
            throw new InvalidOperationException(
                "Room 4:1e's touched key did not request its SND_GETSEED " +
                "collection behavior before the held-item sound.");
        }
        StepEntities();
        if (!fallingKey.Held ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 1)
        {
            throw new InvalidOperationException(
                "Room 4:1e's key did not enter its held pose with SND_GETITEM " +
                "on the following interaction update.");
        }

        _saveData.SetRoomFlag(4, 0x1b, OracleSaveData.RoomFlag80, false);
        PrepareRoom(0x1b);
        SpiritsGraveTorchStairs torch =
            _entities.Entities<SpiritsGraveTorchStairs>().Single();
        var directSpawns = new List<RoomEntitySpawn>();
        foreach (int packed in torch.UnlitPositions.ToArray())
        {
            Vector2 center = new(
                (packed & 0x0f) * 16 + 8,
                (packed >> 4) * 16 + 8);
            if (torch.ApplySeedHit(
                new Rect2(center - new Vector2(4, 4), new Vector2(8, 8)),
                center,
                directSpawns) != SeedHitResult.Consume)
            {
                throw new InvalidOperationException(
                    $"Room 4:1b torch ${packed:x2} rejected an Ember collision.");
            }
            torch.UpdateFrame(
                new RoomEntityFrame(_player, _entities.FrameCounter, false),
                directSpawns);
        }
        if (!torch.Finished || torch.LitCount != 2 ||
            !_saveData.HasRoomFlag(4, 0x1b, OracleSaveData.RoomFlag80) ||
            _currentRoom.GetMetatile(new Vector2(0xb8, 0x28)) != 0x45 ||
            directSpawns.OfType<PuzzlePuffSpawn>().Count() != 1)
        {
            throw new InvalidOperationException(
                "Room 4:1b did not light two torches and reveal its persistent staircase.");
        }

        var warps = new WarpDatabase();
        _saveData.SetRoomFlag(4, 0x10, OracleSaveData.RoomFlagItem, false);
        _player.WarpTo(new Vector2(0xb8, 0x28), recordSafe: false);
        if (!warps.TryGetTileWarp(
                4, 0x1b, 0x2b, 0x45, out Warp braceletStairs) ||
            braceletStairs is not
                { DestinationGroup: 6, DestinationRoom: 0x10,
                  DestinationPosition: 0x02, DestinationTransition: 3 } ||
            !_world.HasRoom(6, 0x10) ||
            !_transitions.CheckTileWarp(_player))
        {
            throw new InvalidOperationException(
                "Room 4:1b's revealed `$45 staircase did not start its source warp to 6:10.");
        }
        for (int frame = 0; frame < 120 && _transitions.IsTransitioning; frame++)
            _transitions.Update(update);
        if (_transitions.IsTransitioning ||
            _rooms.ActiveGroup != 6 || _currentRoom.Id != 0x10 ||
            _currentRoom.Group != 6 || _currentRoom.TilesetId != 0x4c ||
            (_currentRoom.TilesetFlags & 0x20) == 0 ||
            _rooms.CurrentDungeonIndex != 1 ||
            _entities.Entities<SpiritsGraveRewardController>().Count != 1 ||
            !warps.TryGetEdgeWarp(
                6, 0x10, Vector2I.Up, new Vector2(0x28, 0),
                new Vector2(_currentRoom.Width, _currentRoom.Height),
                out Warp braceletReturn) ||
            braceletReturn is not
                { DestinationGroup: 4, DestinationRoom: 0x1b,
                  DestinationPosition: 0x2b })
        {
            throw new InvalidOperationException(
                "The 4:1b staircase did not load side-scrolling D1 room 6:10 " +
                "with its bracelet object and return warp.");
        }
        StepEntities(3);
        if (_entities.Entities<GroundTreasurePickup>() is not
            [{
                State: PickupState.Waiting,
                Position: { X: 0x58, Y: 0x58 },
                Record:
                {
                    Group: 4,
                    Room: 0x10,
                    TreasureObject: "TREASURE_OBJECT_BRACELET_00"
                }
            }])
        {
            throw new InvalidOperationException(
                "Side-scrolling room 6:10 did not create the source-positioned " +
                "collectible TREASURE_OBJECT_BRACELET_00 reward.");
        }

        PrepareRoom(0x20);
        SpiritsGraveColoredCube cube =
            _entities.Entities<SpiritsGraveColoredCube>().Single();
        Image cubeImage = cube.CurrentTexture.GetImage();
        bool cubeHasBlue = false;
        bool cubeHasYellow = false;
        var cubeColors = new HashSet<uint>();
        for (int y = 0; y < cubeImage.GetHeight(); y++)
        for (int x = 0; x < cubeImage.GetWidth(); x++)
        {
            Color pixel = cubeImage.GetPixel(x, y);
            if (pixel.A > 0.1f)
                cubeColors.Add(pixel.ToRgba32());
            cubeHasBlue |= IsGbcColor(pixel, 0x03, 0x10, 0x1f);
            cubeHasYellow |= IsGbcColor(pixel, 0x1f, 0x16, 0x06);
        }
        if (cube.Orientation != 5 || cube.Position != new Vector2(0xa8, 0x78) ||
            _entities.Entities<SpiritsGraveCubeFlame>().Count != 4 ||
            _entities.Entities<SpiritsGraveCubeSensor>().Count != 2 ||
            !cubeHasBlue || !cubeHasYellow ||
            cubeImage.GetPixel(8, 8).A > 0.1f ||
            cubeImage.GetPixel(11, 8).A <= 0.1f ||
            _currentRoom.GetTerrainInfo(cube.Position).Collision != 0x0f)
        {
            throw new InvalidOperationException(
                "Room 4:20 did not create its source-ordered cube/flame/sensor set " +
                "with PALH_89's initial blue/yellow cube sprite " +
                $"(orientation={cube.Orientation}, position={cube.Position}, " +
                $"flames={_entities.Entities<SpiritsGraveCubeFlame>().Count}, " +
                $"sensors={_entities.Entities<SpiritsGraveCubeSensor>().Count}, " +
                $"colors={string.Join(',', cubeColors.Select(color => $"{color:x8}"))}).");
        }
        _sound.ClearPlayRequestAudit();
        for (int push = 0; push < 3; push++)
        {
            Vector2 oldPosition = cube.Position;
            Vector2 approachPosition = oldPosition + Vector2.Down * 14;
            for (int approach = 0; approach < 8; approach++)
            {
                Vector2 movement = _playerWorld.ResolveMovement(
                    approachPosition, Vector2.Up, allowWallSlide: true);
                if (movement == Vector2.Zero)
                    break;
                approachPosition += movement;
            }
            _player.WarpTo(approachPosition);
            _player.Face(Vector2I.Up);
            _player.UpdatePushingState(Vector2.Up);
            if (_player.Position != oldPosition + Vector2.Down * 10 ||
                !_player.IsPushing)
            {
                throw new InvalidOperationException(
                    "Room 4:20 normal movement did not stop Link at the source " +
                    "cube-collision boundary and select his wall-push animation " +
                    $"(position={_player.Position}, cube={oldPosition}, " +
                    $"pushing={_player.IsPushing}, " +
                    $"top-left-solid={_currentRoom.IsSolid(_player.Position + new Vector2(-3, -3))}, " +
                    $"top-right-solid={_currentRoom.IsSolid(_player.Position + new Vector2(2, -3))}, " +
                    $"entity-blocked={_entities.BlocksLink(_player.Position)}).");
            }
            StepEntities(20);
            if (_currentRoom.GetTerrainInfo(oldPosition).Collision != 0x00)
            {
                throw new InvalidOperationException(
                    "Room 4:20 cube did not clear its old collision cell while rolling.");
            }
            for (int frame = 0; frame < 100 && cube.Moving; frame++)
                StepEntities();
            if (cube.Moving || cube.Position != oldPosition + Vector2.Up * 16)
            {
                throw new InvalidOperationException(
                    $"Room 4:20 cube did not complete upward roll {push + 1}.");
            }
            if (_currentRoom.GetTerrainInfo(cube.Position).Collision != 0x0f)
            {
                throw new InvalidOperationException(
                    "Room 4:20 cube did not restore collision at its centered endpoint.");
            }
        }
        _player.UpdatePushingState(Vector2.Zero);
        StepEntities();
        if (cube.Position != new Vector2(0xa8, 0x48) || cube.Orientation != 4 ||
            _entities.ActiveTriggers != 0x01 ||
            _entities.Entities<SpiritsGraveCubeFlame>().Any(flame => !flame.Visible) ||
            _entities.Entities<SpiritsGraveCubeFlame>().Any(flame => flame.Palette != 2) ||
            _sound.PlayRequestsFor(0x7f) != 3 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndLightTorch) != 1)
        {
            throw new InvalidOperationException(
                "Room 4:20 cube did not solve at $4a with color $82 and four matching flames.");
        }

        // Leaving clears wRotatingCubeColor with the room session. The new
        // flame objects must observe the reinitialized cube before their first
        // drawable frame, not after their first fixed update.
        PrepareRoom(0x1f);
        PrepareRoom(0x20);
        SpiritsGraveColoredCube reenteredCube =
            _entities.Entities<SpiritsGraveColoredCube>().Single();
        if (reenteredCube.Position != new Vector2(0xa8, 0x78) ||
            _entities.Entities<SpiritsGraveCubeFlame>().Any(flame => flame.Visible))
        {
            throw new InvalidOperationException(
                "Room 4:20 re-entry exposed solved colored flames before the first entity update.");
        }

        PrepareRoom(0x16);
        _player.WarpTo(new Vector2(0xc8, 0x28));
        StepEntities(2);
        // The right button follows the native script in source order, so the
        // script observes trigger bit 1 on the next update before wait 30.
        StepEntities(31);
        if (_entities.Entities<SpiritsGraveMovingPlatform>() is not
            [{ Script: 1, CollisionRadii: var horizontalRadii }])
        {
            string buttons = string.Join(", ",
                _entities.Entities<GroundButtonRoomEntity>().Select(button =>
                    $"${button.PackedPosition:x2}:pressed={button.Pressed}"));
            throw new InvalidOperationException(
                "Room 4:16 did not spawn its trigger-bit-1 horizontal moving platform " +
                $"after 30 updates (triggers=${_entities.ActiveTriggers:x2}; {buttons}).");
        }
        SpiritsGraveMovingPlatform horizontalPlatform =
            _entities.Entities<SpiritsGraveMovingPlatform>().Single();
        _player.WarpTo(
            horizontalPlatform.Position + new Vector2(7, -5),
            recordSafe: false);
        StepEntities();
        if (horizontalRadii != new Vector2(8, 16) ||
            !horizontalPlatform.LinkRiding ||
            !_entities.PlayerRidingObject ||
            _terrain.GetActiveTerrain(_player.Position).Terrain.Hazard !=
                HazardType.Hole)
        {
            throw new InvalidOperationException(
                "Room 4:16's subid-$09 platform did not claim Link inside its " +
                "strict $08-by-$10 collision radii over the hole.");
        }
        _player._PhysicsProcess(update);
        if (_player.IsPullingIntoHole || _player.IsFallingInHole)
        {
            throw new InvalidOperationException(
                "Room 4:16 applied its underlying hole while Link was the " +
                "active moving-platform rider.");
        }
        _player.WarpTo(
            horizontalPlatform.Position + new Vector2(8, -5),
            recordSafe: false);
        StepEntities();
        if (horizontalPlatform.LinkRiding || _entities.PlayerRidingObject)
        {
            throw new InvalidOperationException(
                "Room 4:16's moving platform included the strict X=$08 " +
                "collision-radius boundary.");
        }

        PrepareRoom(0x15);
        SpiritsGraveMovingPlatform verticalPlatform =
            _entities.Entities<SpiritsGraveMovingPlatform>().Single();
        Vector2 platformStart = verticalPlatform.Position;
        _player.WarpTo(platformStart + new Vector2(0, -5), recordSafe: false);
        if (verticalPlatform.CollisionRadii != new Vector2(16, 16) ||
            _terrain.GetActiveTerrain(_player.Position).Terrain.Hazard !=
                HazardType.Hole)
        {
            throw new InvalidOperationException(
                "Room 4:15's subid-$05 platform did not retain its imported " +
                "$10-by-$10 collision radii over hole tile $f6.");
        }
        // Physics can sample the hole before this frame's interaction pass.
        // The next platform update must claim Link, and the following Link
        // update must cancel that partial pull just as wLinkRidingObject does.
        _player._PhysicsProcess(update);
        if (!_player.IsPullingIntoHole)
        {
            throw new InvalidOperationException(
                "Room 4:15's uncovered $f6 tile did not begin the ordinary " +
                "hole-pull path before the platform claimed Link.");
        }
        StepEntities();
        if (!verticalPlatform.LinkRiding || !_entities.PlayerRidingObject)
        {
            throw new InvalidOperationException(
                "Room 4:15's moving platform did not claim centered Link.");
        }
        _player._PhysicsProcess(update);
        if (_player.IsPullingIntoHole || _player.IsFallingInHole)
        {
            throw new InvalidOperationException(
                "wLinkRidingObject-style support did not cancel room 4:15's " +
                "partial hole pull.");
        }
        Vector2 linkPreciseStart = _player.PrecisePosition;
        StepEntities(9);
        if (verticalPlatform.Script != 0 ||
            verticalPlatform.PrecisePosition !=
                platformStart + Vector2.Up * 0.5f ||
            verticalPlatform.Position != platformStart + Vector2.Up ||
            _player.PrecisePosition != linkPreciseStart + Vector2.Up * 0.5f)
        {
            throw new InvalidOperationException(
                "Room 4:15 moving platform did not honor wait-8/SPEED_80 " +
                "vertical script 0 with Link's 8.8 riding displacement.");
        }
        _player._PhysicsProcess(update);
        if (_player.IsPullingIntoHole || _player.IsFallingInHole)
        {
            throw new InvalidOperationException(
                "Room 4:15 applied a hole after moving Link by its half-pixel " +
                "platform velocity.");
        }

        // enemyBoss_initializeRoom arms LINK_STATE_FORCE_MOVEMENT after the
        // enemy's first update. Link initializes that state on the following
        // update, advances at standard speed while its $16 counter remains
        // nonzero, and thereby clears the incoming shutter before it closes.
        _saveData.SetRoomFlag(4, 0x18, OracleSaveData.RoomFlag80, false);
        PrepareRoom(0x17);
        _player.WarpTo(new Vector2(_currentRoom.Width - 6, 0x58));
        _transitions.BeginScroll(_player, Vector2I.Right, 0x18);
        OracleRoomData scrollingMinibossRoom = _world.LoadRoom(4, 0x18);
        _transitions.UpdateScroll(1.0);
        Vector2 minibossEntry = _player.Position;
        StepEntities();
        if (_player.Position != minibossEntry)
        {
            throw new InvalidOperationException(
                "Room 4:18 moved Link on the boss initialization update.");
        }
        StepEntities();
        if (_player.Position != minibossEntry || !_player.Walking)
        {
            throw new InvalidOperationException(
                "Room 4:18 did not initialize LINK_STATE_FORCE_MOVEMENT one " +
                "update after the miniboss initialized.");
        }
        StepEntities(21);
        if (_player.Position != minibossEntry + Vector2.Right * 21.0f)
        {
            throw new InvalidOperationException(
                $"Room 4:18 forced Link to {_player.Position} instead of 21 " +
                $"pixels inward from {minibossEntry}.");
        }
        StepEntities();
        Vector2 leftShutter = new(0x08, 0x58);
        if (_player.Position != minibossEntry + Vector2.Right * 21.0f ||
            _player.Walking || !scrollingMinibossRoom.IsSolid(leftShutter))
        {
            throw new InvalidOperationException(
                "Room 4:18 did not finish the $16 forced-entry counter after " +
                "Link cleared and closed the left miniboss shutter.");
        }

        _saveData.SetRoomFlag(4, 0x18, OracleSaveData.RoomFlag80, false);
        _sound.ClearPlayRequestAudit();
        PrepareRoom(0x18);
        GiantGhiniBoss giant = _entities.Entities<GiantGhiniBoss>().Single();
        StepEntities();
        ulong giantVisualHash = OracleGraphicsCache.PixelHash(
            giant.CurrentAnimationTexture.GetImage());
        ulong childVisualHash = OracleGraphicsCache.PixelHash(
            _entities.Entities<GiantGhiniChild>().First()
                .CurrentAnimationTexture.GetImage());
        if (giantVisualHash != 0xc57b0cfc29363e48UL ||
            childVisualHash != 0x70071fba065d4d57UL)
        {
            throw new InvalidOperationException(
                "Giant Ghini or its children did not compose the imported " +
                "black-on-white sprite chain with white color-0 transparency.");
        }
        if (giant.ChildrenAlive != 3 ||
            _entities.Entities<GiantGhiniChild>().Count != 3)
        {
            throw new InvalidOperationException(
                "Room 4:18 did not spawn Giant Ghini's three linked children.");
        }
        StepEntities(181);
        BossShadowEffect giantShadow =
            _entities.Entities<BossShadowEffect>().Single();
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndCtrlStopMusic) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.MusMiniboss) != 1 ||
            giantShadow.Size != 1 || giantShadow.YOffset != 12 ||
            giantShadow.Position != giant.Position + Vector2.Down * 12 ||
            giantShadow.AnimationIndex != 2 ||
            BossShadowEffect.AnimationIndexFor(0, -1) != 1 ||
            BossShadowEffect.AnimationIndexFor(0, -33) != 1 ||
            BossShadowEffect.AnimationIndexFor(0, -65) != 0 ||
            BossShadowEffect.AnimationIndexFor(0, -97) != 0 ||
            BossShadowEffect.AnimationIndexFor(1, -1) != 2 ||
            BossShadowEffect.AnimationIndexFor(1, -33) != 1 ||
            BossShadowEffect.AnimationIndexFor(1, -65) != 1 ||
            BossShadowEffect.AnimationIndexFor(1, -97) != 0 ||
            BossShadowEffect.AnimationIndexFor(2, -1) != 3 ||
            BossShadowEffect.AnimationIndexFor(2, -33) != 2 ||
            BossShadowEffect.AnimationIndexFor(2, -65) != 1 ||
            BossShadowEffect.AnimationIndexFor(2, -97) != 0)
        {
            throw new InvalidOperationException(
                "Giant Ghini did not stop dungeon music for its intro and begin " +
                "miniboss music with its source PART_SHADOW after state 0, " +
                "shutter completion, and the 120/60-update entrance.");
        }
        var respawnPuffSpawns = new List<RoomEntitySpawn>();
        var respawnedChild = new GiantGhiniChild();
        respawnedChild.Initialize(
            _entities.Entities<GiantGhiniChild>().First().Record,
            giant,
            _currentRoom,
            0);
        respawnedChild.UpdateFrame(
            _player, anyButtonJustPressed: false, frameCounter: 0,
            respawnPuffSpawns);
        if (respawnPuffSpawns is not
            [PuzzlePuffSpawn { Sound: OracleSoundEngine.SndPoof }])
        {
            throw new InvalidOperationException(
                "A respawned Giant Ghini child did not create the source " +
                "INTERAC_PUFF/SND_POOF appearance effect.");
        }
        respawnedChild.Free();
        giant.ChildAttached(_player);
        StepEntities(7);
        ulong chargingVisualHash = OracleGraphicsCache.PixelHash(
            giant.CurrentAnimationTexture.GetImage());
        giant.ChildDetached();
        if (chargingVisualHash != 0x170a29eaf8025ffcUL)
        {
            throw new InvalidOperationException(
                "Giant Ghini's charge animation did not reach the corrected " +
                "second block of its imported black-on-white sprite chain.");
        }
        int giantHealth = giant.Health;
        _sound.ClearPlayRequestAudit();
        _player.WarpTo(giant.Position + Vector2.Down * 12.0f);
        _player.Face(Vector2I.Up);
        _player.StartSwordAttackForValidation(Vector2.Up);
        _player.AdvanceSwordForValidation(17, buttonHeld: false);
        if (giant.Health >= giantHealth ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBossDamage) != 1)
        {
            throw new InvalidOperationException(
                "Giant Ghini did not accept Link's real upward sword swing with " +
                "the source boss-damage response.");
        }
        int giantHealthAfterHit = giant.Health;
        if (giant.TakeSwordHit(giant.Position, damage: 20) ||
            giant.Health != giantHealthAfterHit)
        {
            throw new InvalidOperationException(
                "Giant Ghini accepted a second hit during boss invincibility.");
        }
        StepEntities(31);
        if (giant.TakeSwordHit(giant.Position, damage: 20) ||
            giant.Health != giantHealthAfterHit)
        {
            throw new InvalidOperationException(
                "Giant Ghini's source 32-update invincibility ended early.");
        }
        StepEntities();
        _sound.ClearPlayRequestAudit();
        int childrenAtBossDeath =
            _entities.Entities<GiantGhiniChild>().Count;
        giant.TakeSwordHit(giant.Position, damage: 20);
        if (!giant.Defeated ||
            !giant.DrawEnabled ||
            !_entities.LinkCollisionsAndMenuDisabled ||
            !_entities.PlayerMenusDisabled ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBossDamage) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBossDead) != 1)
        {
            throw new InvalidOperationException(
                "Giant Ghini did not enter the common 120-update boss death phase.");
        }
        StepEntities();
        if (_entities.Entities<EnemyDeathPuffEffect>().Count != childrenAtBossDeath ||
            _entities.Entities<PuzzlePuffEffect>().Count != 0 ||
            _entities.Entities<KillEnemyPuffEffect>().Count != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) !=
                childrenAtBossDeath)
        {
            throw new InvalidOperationException(
                "Giant Ghini's parent-death cleanup did not route every live " +
                "child through the ordinary enemy death puff " +
                $"(before={childrenAtBossDeath}, " +
                $"children={_entities.Entities<GiantGhiniChild>().Count}, " +
                $"death={_entities.Entities<EnemyDeathPuffEffect>().Count}, " +
                $"puzzle={_entities.Entities<PuzzlePuffEffect>().Count}, " +
                $"kill={_entities.Entities<KillEnemyPuffEffect>().Count}, " +
                $"sounds={_sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy)}).");
        }
        StepEntities(119);
        BossDeathExplosionEffect giantExplosion =
            _entities.Entities<BossDeathExplosionEffect>().Single();
        if (_entities.Entities<GiantGhiniBoss>().Count != 0 ||
            _entities.Entities<GiantGhiniChild>().Count != 0 ||
            _entities.Entities<BossShadowEffect>().Count != 0 ||
            _entities.Entities<EnemyDeathPuffEffect>().Count != 0 ||
            giantExplosion.BossId != 0x70 || giantExplosion.AnimationDuration != 78 ||
            giantExplosion.CurrentTextureSize != new Vector2(48, 48) ||
            giantExplosion.CurrentDrawOffset != new Vector2(-24, -24) ||
            _saveData.HasRoomFlag(4, 0x18, OracleSaveData.RoomFlag80) ||
            _entities.Entities<MinibossPortal>().Count != 0 ||
            !_entities.LinkCollisionsAndMenuDisabled ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBigExplosion) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.MusSpiritsGrave) != 1 ||
            _sound.ActiveMusic != OracleSoundEngine.MusSpiritsGrave)
        {
            throw new InvalidOperationException(
                "Giant Ghini did not enter the source 78-update, enemy-counting " +
                "boss explosion with the complete 48x48 source OAM before its reward wait.");
        }
        StepEntities(78);
        if (_entities.Entities<BossDeathExplosionEffect>().Count != 1 ||
            _saveData.HasRoomFlag(4, 0x18, OracleSaveData.RoomFlag80))
        {
            throw new InvalidOperationException(
                "Giant Ghini's terminal boss-explosion frame did not retain the " +
                "enemy count for its complete source duration.");
        }
        StepEntities();
        if (_entities.Entities<BossDeathExplosionEffect>().Count != 0 ||
            _saveData.HasRoomFlag(4, 0x18, OracleSaveData.RoomFlag80))
        {
            throw new InvalidOperationException(
                "Giant Ghini's boss explosion did not release its enemy count on " +
                "the update after the terminal frame.");
        }
        StepEntities();
        if (!_saveData.HasRoomFlag(4, 0x18, OracleSaveData.RoomFlag80) ||
            _entities.Entities<MinibossPortal>().Count != 0)
        {
            throw new InvalidOperationException(
                "Room 4:18 did not begin its reward wait after the boss explosion ended.");
        }
        StepEntities(19);
        if (_entities.Entities<MinibossPortal>().Count != 0)
        {
            throw new InvalidOperationException(
                "Room 4:18 created its portal before the native 20-update reward wait.");
        }
        StepEntities();
        if (_entities.Entities<MinibossPortal>().Count != 1 ||
            _entities.LinkCollisionsAndMenuDisabled ||
            _entities.PlayerMenusDisabled)
        {
            throw new InvalidOperationException(
                "Room 4:18 did not create its portal after the native 20-update reward wait.");
        }

        _sound.ClearPlayRequestAudit();
        PrepareRoom(0x18);
        Vector2 upperMinibossShutter = new(0x78, 0x08);
        if (_entities.Entities<GiantGhiniBoss>().Count != 0 ||
            _entities.Entities<GiantGhiniChild>().Count != 0 ||
            !_currentRoom.IsSolid(upperMinibossShutter) ||
            !_currentRoom.IsSolid(leftShutter))
        {
            throw new InvalidOperationException(
                "Completed room 4:18 did not begin re-entry with its source " +
                "boss stream suppressed and both layout shutters closed.");
        }
        StepEntities();
        StepEntities();
        StepEntities(5);
        if (!_currentRoom.IsSolid(upperMinibossShutter) ||
            !_currentRoom.IsSolid(leftShutter))
        {
            throw new InvalidOperationException(
                "Completed room 4:18 opened its shutters before the source " +
                "six-update interleaved animation elapsed.");
        }
        StepEntities();
        if (_currentRoom.IsSolid(upperMinibossShutter) ||
            _currentRoom.IsSolid(leftShutter))
        {
            throw new InvalidOperationException(
                "Completed room 4:18 did not reopen both zero-enemy shutters " +
                "with the source interleaved animation " +
                $"(upSolid={_currentRoom.IsSolid(upperMinibossShutter)}, " +
                $"leftSolid={_currentRoom.IsSolid(leftShutter)}).");
        }

        _saveData.SetRoomFlag(4, 0x13, OracleSaveData.RoomFlag80, false);
        _saveData.SetRoomFlag(4, 0x13, OracleSaveData.RoomFlagItem, false);
        _sound.ClearPlayRequestAudit();
        PrepareRoom(0x13);
        PumpkinHeadBoss pumpkin = _entities.Entities<PumpkinHeadBoss>().Single();
        int pumpkinImpactRandomCalls = _entities.RandomCalls;
        bool observedInitialPumpkinShake = false;
        for (int frame = 0; frame < 240 && pumpkin.IntroActive; frame++)
        {
            StepEntities();
            if (!observedInitialPumpkinShake && _entities.ScreenShakeCounter > 0)
            {
                observedInitialPumpkinShake = true;
                if (_entities.ScreenShakeCounter != 29 ||
                    _entities.RandomCalls != pumpkinImpactRandomCalls + 2)
                {
                    throw new InvalidOperationException(
                        "Pumpkin Head's first landing did not begin the source " +
                        "30-update shake with Y/X RNG consumption on its impact update.");
                }
            }
        }
        BossShadowEffect pumpkinShadow =
            _entities.Entities<BossShadowEffect>().Single();
        if (pumpkin.IntroActive ||
            !observedInitialPumpkinShake ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCtrlStopMusic) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.MusBoss) != 1 ||
            pumpkin.BodyPalette != 1 ||
            pumpkin.GhostPalette != 5 ||
            pumpkin.HeadPalette != 3 ||
            pumpkinShadow.Size != 1 || pumpkinShadow.YOffset != 6 ||
            pumpkinShadow.Position != pumpkin.Position + Vector2.Down * 6 ||
            pumpkinShadow.Visible)
        {
            throw new InvalidOperationException(
                "Pumpkin Head did not complete its three-height ceiling fall, " +
                "impact delay, source body/ghost/head palettes, and boss-music handoff.");
        }
        for (int frame = 0; frame < 2400 &&
             _entities.Entities<PumpkinHeadProjectile>().Count == 0;
             frame++)
        {
            _player.WarpTo(
                pumpkin.Position + OracleObjectMath.VectorFromAngle32(pumpkin.Angle) * 32.0f);
            StepEntities();
        }
        List<PumpkinHeadProjectile> pumpkinShots =
            _entities.Entities<PumpkinHeadProjectile>();
        Vector2 mouthOffset = pumpkin.Angle switch
        {
            0x00 => new Vector2(0, -4),
            0x08 => new Vector2(4, 2),
            0x10 => new Vector2(0, 4),
            _ => new Vector2(-4, 2)
        };
        Vector2 expectedShotPosition =
            pumpkin.HeadPosition + new Vector2(0, pumpkin.HeadZ) + mouthOffset;
        var expectedShotAngles = new HashSet<int>
        {
            (pumpkin.Angle - 2) & 0x1f,
            pumpkin.Angle,
            (pumpkin.Angle + 2) & 0x1f
        };
        if (pumpkinShots.Count != 3 ||
            pumpkinShots.Any(shot => shot.Position != expectedShotPosition || shot.Delay != 8) ||
            pumpkinShots.Any(shot => shot.CollisionBounds.Size != new Vector2(4, 8)) ||
            !expectedShotAngles.SetEquals(pumpkinShots.Select(shot => shot.Angle)))
        {
            throw new InvalidOperationException(
                "Pumpkin Head did not create its delayed three-way projectile spread " +
                "from the source cardinal mouth offset " +
                $"(boss angle=${pumpkin.Angle:x2}, expected={expectedShotPosition}, " +
                $"shots={string.Join("; ", pumpkinShots.Select(shot =>
                    $"{shot.Position}/${shot.Angle:x2}/d{shot.Delay}"))}).");
        }
        int pumpkinHealth = pumpkin.BodyHealth;
        _sound.ClearPlayRequestAudit();
        _player.WarpTo(pumpkin.Position + Vector2.Down * 12.0f);
        _player.Face(Vector2I.Up);
        _player.StartSwordAttackForValidation(Vector2.Up);
        _player.AdvanceSwordForValidation(17, buttonHeld: false);
        if (pumpkin.BodyHealth >= pumpkinHealth ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBossDamage) != 1)
        {
            throw new InvalidOperationException(
                "Pumpkin Head's body did not accept Link's real upward sword swing " +
                "with the source boss-damage response.");
        }
        int pumpkinHealthAfterHit = pumpkin.BodyHealth;
        if (pumpkin.ApplySwordHit(
                pumpkin.CollisionBounds, pumpkin.Position, damage: 20) ||
            pumpkin.BodyHealth != pumpkinHealthAfterHit)
        {
            throw new InvalidOperationException(
                "Pumpkin Head accepted a second hit during boss invincibility.");
        }
        StepEntities(31);
        if (pumpkin.ApplySwordHit(
                pumpkin.CollisionBounds, pumpkin.Position, damage: 20) ||
            pumpkin.BodyHealth != pumpkinHealthAfterHit)
        {
            throw new InvalidOperationException(
                "Pumpkin Head's source 32-update invincibility ended early.");
        }
        StepEntities();
        var bodyDestructionSpawns = new List<RoomEntitySpawn>();
        pumpkin.ApplySwordHit(
            pumpkin.CollisionBounds, pumpkin.Position, damage: 20,
            bodyDestructionSpawns);
        if (pumpkin.State != BossState.HeadExposed ||
            bodyDestructionSpawns is not
                [PuzzlePuffSpawn { Sound: OracleSoundEngine.SndPoof }])
        {
            throw new InvalidOperationException(
                "Pumpkin Head's body did not expose its grabbable head with " +
                "the source INTERAC_PUFF/SND_POOF disappearance effect.");
        }
        // The head and ghost first launch with speed -$120 and separate
        // gravities. The head only joins the grabbable buffer after landing.
        for (int frame = 0; frame < 60 && pumpkin.HeadZ < 0; frame++)
            StepEntities();
        if (pumpkin.HeadZ != 0)
        {
            throw new InvalidOperationException(
                "Pumpkin Head's severed head did not finish its source airborne arc.");
        }
        // $0d keeps the lifted head on the ghost's source six-pixel-radius
        // collision path once the straight-up throw reaches matching Z.
        _player.WarpTo(pumpkin.Position + Vector2.Down * 13);
        _player.Face(Vector2I.Up);
        _sound.ClearPlayRequestAudit();
        if (!_playerWorld.TryUseBracelet(_player, primaryButton: false) ||
            !pumpkin.HeadHeld ||
            _bracelet.State != BraceletState.LiftingEntity)
        {
            throw new InvalidOperationException(
                "Pumpkin Head's exposed head did not enter the shared Bracelet lift parent.");
        }
        for (int frame = 0; frame < 12; frame++)
        {
            if (!_playerWorld.UpdateBracelet(
                    _player, Vector2.Zero,
                    primaryHeld: false, secondaryHeld: false,
                    itemButtonJustPressed: false))
            {
                throw new InvalidOperationException(
                    "Pumpkin Head's shared Bracelet lift released Link before its 13-update boundary.");
            }
            StepEntities();
        }
        if (_playerWorld.UpdateBracelet(
                _player, Vector2.Zero,
                primaryHeld: false, secondaryHeld: false,
                itemButtonJustPressed: false) ||
            _bracelet.State != BraceletState.Idle ||
            !_player.IsCarryingObject ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndPickup) != 1)
        {
            throw new InvalidOperationException(
                "Pumpkin Head's shared Bracelet lift did not finish with its carried pose and SND_PICKUP.");
        }
        StepEntities();

        // Send the first head across the room. The exposed ghost's $5e
        // collision row accepts a normal sword strike independently of the
        // thrown-head collision, so this cycle validates the ordinary sword
        // path without accidentally killing the ghost with its head.
        _player.Face(Vector2I.Right);
        StepEntities();

        // State 3 accepts either newly pressed item button. The native entity
        // owns its body flight while the shared parent retains Link's throw.
        if (!_playerWorld.UpdateBracelet(
                _player, Vector2.Zero,
                primaryHeld: true, secondaryHeld: false,
                itemButtonJustPressed: true) ||
            pumpkin.HeadHeld ||
            _bracelet.State != BraceletState.Throwing ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndThrow) != 1)
        {
            throw new InvalidOperationException(
                "Pumpkin Head's held head did not use the shared either-button Bracelet throw.");
        }
        StepEntities();
        for (int frame = 0; frame < 120 && pumpkin.HeadThrown; frame++)
        {
            if (_bracelet.State == BraceletState.Throwing)
            {
                _playerWorld.UpdateBracelet(
                    _player, Vector2.Zero,
                    primaryHeld: false, secondaryHeld: false,
                    itemButtonJustPressed: false);
            }
            StepEntities();
        }
        if (pumpkin.HeadThrown ||
            _bracelet.State != BraceletState.Idle ||
            pumpkin.GhostHealth != 8)
        {
            throw new InvalidOperationException(
                "Pumpkin Head's source weight-zero Bracelet throw did not land " +
                "clear of the fleeing ghost " +
                $"(head={pumpkin.HeadPosition}, z={pumpkin.HeadZ}, " +
                $"thrown={pumpkin.HeadThrown}, ghost={pumpkin.GhostPosition}, " +
                $"ghostHealth={pumpkin.GhostHealth}, state={pumpkin.State}).");
        }

        Vector2 firstHeadLandingPosition = pumpkin.HeadPosition;
        _sound.ClearPlayRequestAudit();
        _player.WarpTo(pumpkin.GhostPosition + Vector2.Down * 12);
        _player.Face(Vector2I.Up);
        _player.StartSwordAttackForValidation(Vector2.Up);
        _player.AdvanceSwordForValidation(17, buttonHeld: false);
        if (pumpkin.GhostHealth != 6 ||
            pumpkin.State != BossState.HeadExposed ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBossDamage) != 1)
        {
            throw new InvalidOperationException(
                "Pumpkin Head's exposed $5e ghost did not accept Link's normal " +
                "level-one sword damage with its source hit response " +
                $"(health={pumpkin.GhostHealth}, state={pumpkin.State}).");
        }
        if (pumpkin.ApplySwordHit(
                pumpkin.CollisionBounds, pumpkin.GhostPosition, damage: 2) ||
            pumpkin.GhostHealth != 6)
        {
            throw new InvalidOperationException(
                "Pumpkin Head's exposed ghost accepted another hit during its " +
                "source 32-update invincibility.");
        }

        for (int frame = 0; frame < 360 &&
             pumpkin.State != BossState.Active;
             frame++)
        {
            StepEntities();
        }
        if (pumpkin.State != BossState.Active ||
            pumpkin.Position != firstHeadLandingPosition ||
            pumpkin.GhostHealth != 6 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndPoof) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) != 0)
        {
            throw new InvalidOperationException(
                "Pumpkin Head did not regenerate its full body at the landed " +
                "head's copied position with INTERAC_PUFF/SND_POOF while " +
                "preserving ghost health " +
                $"(body={pumpkin.Position}, head={firstHeadLandingPosition}, " +
                $"ghostHealth={pumpkin.GhostHealth}, state={pumpkin.State}).");
        }

        // The ghost owns eight persistent health points. A thrown weight-zero
        // head uses ITEM_BRACELET's three damage, so two more exposure cycles
        // are required after the level-one sword hit above.
        for (int expectedHealth = 3; expectedHealth >= 0; expectedHealth -= 3)
        {
            if (!pumpkin.ApplySwordHit(
                    pumpkin.CollisionBounds, pumpkin.Position, damage: 20) ||
                pumpkin.State != BossState.HeadExposed)
            {
                throw new InvalidOperationException(
                    "Pumpkin Head's regenerated body did not enter another " +
                    "head-exposure cycle.");
            }
            for (int frame = 0; frame < 60 && pumpkin.HeadZ < 0; frame++)
                StepEntities();
            if (pumpkin.HeadZ != 0)
            {
                throw new InvalidOperationException(
                    "Pumpkin Head's head did not become grabbable on a later " +
                    "exposure cycle.");
            }

            _player.WarpTo(pumpkin.Position + Vector2.Down * 13);
            _player.Face(Vector2I.Up);
            if (!_playerWorld.TryUseBracelet(_player, primaryButton: false))
            {
                throw new InvalidOperationException(
                    "Pumpkin Head's head could not be lifted on a later " +
                    "exposure cycle.");
            }
            for (int frame = 0; frame < 13; frame++)
            {
                _playerWorld.UpdateBracelet(
                    _player, Vector2.Zero,
                    primaryHeld: false, secondaryHeld: false,
                    itemButtonJustPressed: false);
                StepEntities();
            }
            StepEntities();
            if (!_playerWorld.UpdateBracelet(
                    _player, Vector2.Zero,
                    primaryHeld: true, secondaryHeld: false,
                    itemButtonJustPressed: true))
            {
                throw new InvalidOperationException(
                    "Pumpkin Head's head could not be thrown on a later " +
                    "exposure cycle.");
            }

            for (int frame = 0; frame < 240 &&
                 pumpkin.GhostHealth > expectedHealth &&
                 pumpkin.State != BossState.Dying;
                 frame++)
            {
                if (_bracelet.State == BraceletState.Throwing)
                {
                    _playerWorld.UpdateBracelet(
                        _player, Vector2.Zero,
                        primaryHeld: false, secondaryHeld: false,
                        itemButtonJustPressed: false);
                }
                StepEntities();
            }
            if (pumpkin.GhostHealth != expectedHealth)
            {
                throw new InvalidOperationException(
                    "Pumpkin Head's thrown head did not apply ITEM_BRACELET's " +
                    "source three damage to the exposed ghost " +
                    $"(expected={expectedHealth}, actual={pumpkin.GhostHealth}, " +
                    $"head={pumpkin.HeadPosition}, ghost={pumpkin.GhostPosition}).");
            }
            if (expectedHealth > 0)
            {
                for (int frame = 0; frame < 360 &&
                     pumpkin.State != BossState.Active;
                     frame++)
                {
                    if (_bracelet.State == BraceletState.Throwing)
                    {
                        _playerWorld.UpdateBracelet(
                            _player, Vector2.Zero,
                            primaryHeld: false, secondaryHeld: false,
                            itemButtonJustPressed: false);
                    }
                    StepEntities();
                }
                if (pumpkin.State != BossState.Active)
                {
                    throw new InvalidOperationException(
                        "Pumpkin Head did not regenerate after a nonlethal " +
                        "thrown-head hit.");
                }
            }
        }
        if (pumpkin.State != BossState.Dying)
        {
            throw new InvalidOperationException(
                "Pumpkin Head did not begin its death sequence when the " +
                "persistent ghost health reached zero.");
        }
        if (!_entities.LinkCollisionsAndMenuDisabled ||
            !_entities.PlayerMenusDisabled)
        {
            throw new InvalidOperationException(
                "Pumpkin Head's common boss-death path did not disable Link " +
                "collisions and menus.");
        }
        StepEntities(120);
        BossDeathExplosionEffect pumpkinExplosion =
            _entities.Entities<BossDeathExplosionEffect>().Single();
        if (_entities.Entities<PumpkinHeadBoss>().Count != 0 ||
            pumpkinExplosion.BossId != 0x78 || pumpkinExplosion.AnimationDuration != 78 ||
            pumpkinExplosion.CurrentTextureSize != new Vector2(48, 48) ||
            pumpkinExplosion.CurrentDrawOffset != new Vector2(-24, -24) ||
            _saveData.HasRoomFlag(4, 0x13, OracleSaveData.RoomFlag80) ||
            _entities.Entities<GroundTreasurePickup>().Count != 0 ||
            _entities.Entities<EnemyDeathPuffEffect>().Count != 0 ||
            !_entities.LinkCollisionsAndMenuDisabled ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBigExplosion) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.MusSpiritsGrave) != 1 ||
            _sound.ActiveMusic != OracleSoundEngine.MusSpiritsGrave)
        {
            throw new InvalidOperationException(
                "Pumpkin Head did not enter the source 78-update, enemy-counting " +
                "boss explosion before its Heart Container reward " +
                $"(bosses={_entities.Entities<PumpkinHeadBoss>().Count}, " +
                $"bossId=${pumpkinExplosion.BossId:x2}, " +
                $"duration={pumpkinExplosion.AnimationDuration}, " +
                $"flag={_saveData.HasRoomFlag(4, 0x13, OracleSaveData.RoomFlag80)}, " +
                $"treasures={_entities.Entities<GroundTreasurePickup>().Count}, " +
                $"sounds={_sound.PlayRequestsFor(OracleSoundEngine.SndBigExplosion)}).");
        }
        StepEntities(78);
        if (_entities.Entities<BossDeathExplosionEffect>().Count != 1 ||
            _saveData.HasRoomFlag(4, 0x13, OracleSaveData.RoomFlag80))
        {
            throw new InvalidOperationException(
                "Pumpkin Head's terminal boss-explosion frame did not retain the " +
                "enemy count for its complete source duration.");
        }
        StepEntities(2);
        if (_entities.Entities<BossDeathExplosionEffect>().Count != 0 ||
            !_saveData.HasRoomFlag(4, 0x13, OracleSaveData.RoomFlag80) ||
            _entities.Entities<GroundTreasurePickup>() is not
                [{ Record.TreasureObject: "TREASURE_OBJECT_HEART_CONTAINER_00" }] ||
            _entities.LinkCollisionsAndMenuDisabled ||
            _entities.PlayerMenusDisabled)
        {
            throw new InvalidOperationException(
                "Pumpkin Head's boss explosion did not release its enemy count and " +
                "create the Heart Container on the following reward update.");
        }

        // The room-$12 spawner represents five hands. Verify its delayed drop,
        // Link capture, lift, and dungeon-entry warp rather than contact damage.
        PrepareRoom(0x12);
        WallmasterCharacter wallmaster =
            _entities.Entities<WallmasterCharacter>().Single();
        Vector2 blockedSpawn = (
            from y in Enumerable.Range(0, _currentRoom.HeightInTiles)
            from x in Enumerable.Range(0, _currentRoom.WidthInTiles)
            let center = new Vector2(x * 16 + 8, y * 16 + 8)
            where _currentRoom.IsSolid(center)
            select center).First();
        _player.WarpTo(blockedSpawn);
        _sound.ClearPlayRequestAudit();
        StepEntities(181);
        if (wallmaster.State != WallmasterState.Waiting ||
            wallmaster.Counter != 120 || wallmaster.Visible ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndFallInHole) != 0)
        {
            throw new InvalidOperationException(
                "Room 4:12 Wallmaster spawner did not reject Link's solid tile " +
                "and reload its 120-update delay.");
        }
        _player.WarpTo(new Vector2(0x78, 0x78));
        StepEntities(120);
        if (_player.CutsceneControlled ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBossDead) != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndFallInHole) != 1)
        {
            throw new InvalidOperationException(
                "Room 4:12 Wallmaster collided with Link while still $60 pixels overhead.");
        }
        for (int frame = 0; frame < 180 &&
             wallmaster.State != WallmasterState.Grounded;
             frame++)
        {
            StepEntities();
        }
        StepEntities(12);
        if (!_player.CutsceneControlled || _player.Visible ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBossDead) != 1)
        {
            throw new InvalidOperationException(
                "Room 4:12 Wallmaster did not capture and hide Link with the " +
                "original immediate capture sound after landing.");
        }
        for (int frame = 0; frame < 120 && _rooms.CurrentRoom.Id == 0x12; frame++)
            StepEntities();
        if (_rooms.ActiveGroup != 4 || _rooms.CurrentRoom.Id != 0x24 ||
            !_transitions.IsTransitioning ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBossDead) != 1)
        {
            throw new InvalidOperationException(
                "Room 4:12 Wallmaster did not request its $24/$87 fall destination " +
                $"(group={_rooms.ActiveGroup:x}, room=${_rooms.CurrentRoom.Id:x2}, " +
                $"transitioning={_transitions.IsTransitioning}, state={wallmaster.State}, " +
                $"counter={wallmaster.Counter}, z=${wallmaster.ZFixed:x}, " +
                $"warp={wallmaster.WarpRequested}, sound=" +
                $"{_sound.PlayRequestsFor(OracleSoundEngine.SndBossDead)}).");
        }
        for (int frame = 0; frame < 40 && _transitions.IsTransitioning; frame++)
            _transitions.Update(update);
        if (_transitions.IsTransitioning || !_player.Visible)
            throw new InvalidOperationException("Wallmaster destination transition did not finish.");

        // Exercise the complete INTERAC_ESSENCE sequence last because it
        // intentionally mutates the live validation inventory's essence bit.
        _saveData.SetRoomFlag(4, 0x11, OracleSaveData.RoomFlagItem, false);
        PrepareRoom(0x11);
        SpiritsGraveEssence essence =
            _entities.Entities<SpiritsGraveEssence>().Single();
        using Image liveEnergyBeadImage = essence.EnergyBeadTexture(0).GetImage();
        if (!liveEnergyBeadImage.GetData().AsSpan().SequenceEqual(
                energyBeadFirstImage.GetData()))
        {
            throw new InvalidOperationException(
                "The live Eternal Spirit energy-bead compositor ignored " +
                "spr_circlebeads' white-background source polarity.");
        }
        bool observedHiddenGlow = false;
        bool observedVisibleGlowAfterToggle = false;
        for (int frame = 0; frame < 8; frame++)
        {
            StepEntities();
            if (!essence.GlowVisible && essence.GlowFrameIndex == 1)
                observedHiddenGlow = true;
            if (observedHiddenGlow &&
                essence.GlowVisible && essence.GlowFrameIndex == 3)
            {
                observedVisibleGlowAfterToggle = true;
            }
        }
        if (!observedHiddenGlow || !observedVisibleGlowAfterToggle)
        {
            throw new InvalidOperationException(
                "The Eternal Spirit glow did not use animation 3's two-update " +
                "eight-cell frames and parameter-driven visibility toggles.");
        }
        _player.WarpTo(new Vector2(0x78, 0x3a));
        _player.Face(Vector2I.Up);
        _sound.ClearPlayRequestAudit();
        for (int frame = 0; frame < 180 && !_dialogue.IsOpen; frame++)
        {
            StepEntities();
            _roomEvents.Update(update);
        }
        if (!_dialogue.IsOpen || !essence.ReadyForDialogue ||
            !_player.IsHoldingItemTwoHands ||
            !_saveData.HasRoomFlag(4, 0x11, OracleSaveData.RoomFlagItem) ||
            (_inventory.Essences & 0x01) == 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.MusGetEssence) != 1 ||
            _sound.ActiveMusic != OracleSoundEngine.MusGetEssence)
        {
            throw new InvalidOperationException(
                "The Eternal Spirit did not fall, enter the two-hand pose, show " +
                "TX_000e, set D1's item bit, and start MUS_GET_ESSENCE.");
        }
        _dialogue.Close();
        bool[] observedEnergyBead = new bool[8];
        bool[] observedEnergyBeadRestart = new bool[8];
        for (int frame = 0; frame < 64; frame++)
        {
            StepEntities();
            _roomEvents.Update(update);
            for (int index = 0; index < observedEnergyBead.Length; index++)
            {
                if (essence.EnergyBeadVisible(index))
                    observedEnergyBead[index] = true;
                else if (observedEnergyBead[index])
                    observedEnergyBeadRestart[index] = true;
            }
        }
        if (!essence.SwirlActive ||
            observedEnergyBead.Any(observed => !observed) ||
            observedEnergyBeadRestart.Any(observed => !observed))
        {
            throw new InvalidOperationException(
                "The Eternal Spirit's eight inward energy beads did not consume " +
                "their terminal $ff frames, disappear, and restart from the perimeter.");
        }
        for (int frame = 0; frame < 520 && !_transitions.IsTransitioning; frame++)
        {
            StepEntities();
            _roomEvents.Update(update);
        }
        if (!_transitions.IsTransitioning || essence.SwirlActive ||
            !_player.IsHoldingItemTwoHands ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDropEssence) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCtrlSlowFadeOut) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.MusGetEssence) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.MusEssence) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndEnergyThing) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndFadeOut) != 4 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCtrlStopMusic) != 1)
        {
            throw new InvalidOperationException(
                "The Eternal Spirit's 360/20/20/40/30 sequence, held pose, or " +
                "sound cadence diverged.");
        }
        _transitions.Update(update);
        if (!_player.IsHoldingItemTwoHands)
        {
            throw new InvalidOperationException(
                "Link released the Eternal Spirit pose during the source-room white fade.");
        }
        for (int frame = 0; frame < 170 && _transitions.IsTransitioning; frame++)
            _transitions.Update(update);
        if (_transitions.IsTransitioning || _rooms.ActiveGroup != 0 ||
            _rooms.CurrentRoom.Id != 0x8d ||
            _currentRoom.Id != 0x8d ||
            _roomView.BackgroundFadeAlpha != 0.0f ||
            _player.IsHoldingItemTwoHands)
        {
            throw new InvalidOperationException(
                "D1's Essence did not finish the delayed white warp to 0:8d/$26 cleanly.");
        }

        PrepareRoom(0x11);
        SpiritsGraveEssence collectedEssence =
            _entities.Entities<SpiritsGraveEssence>().Single();
        if (!collectedEssence.Collected ||
            _currentRoom.GetTerrainInfo(new Vector2(0x78, 0x28)).Collision != 0x0f ||
            !collectedEssence.BlocksLink(new Vector2(0x78, 0x28)))
        {
            throw new InvalidOperationException(
                "Collected D1 Essence re-entry did not retain subid-$01's pedestal " +
                "and packed-position $0f collision while deleting only the essence/glow.");
        }

        // Common subid-$00 species are not owned by D1. Room 4:ed is outside
        // the old 4:10-$25 factory gate and contains six fixed Ropes.
        PrepareRoom(0xed);
        if (_entities.Entities<RopeCharacter>().Count != 6)
        {
            throw new InvalidOperationException(
                "Common ENEMY_ROPE $10:$00 did not instantiate outside " +
                "Spirit's Grave in room 4:ed.");
        }

        // Dungeon $0b's room 4:c5 uses the same common Wallmaster handler but
        // dungeonData0b sends Link to $ce, not Spirit's Grave entrance $24.
        if (_rooms.DungeonMaps.GetDungeon(1).WallmasterDestinationRoom != 0x24 ||
            _rooms.DungeonMaps.GetDungeon(0x0b).WallmasterDestinationRoom != 0xce)
        {
            throw new InvalidOperationException(
                "Imported dungeon Wallmaster destinations lost dungeon01=$24 " +
                "or dungeon0b=$ce.");
        }
        PrepareRoom(0xc5);
        WallmasterCharacter laterWallmaster =
            _entities.Entities<WallmasterCharacter>().Single();
        Vector2 laterOpenTile = (
            from y in Enumerable.Range(0, _currentRoom.HeightInTiles)
            from x in Enumerable.Range(0, _currentRoom.WidthInTiles)
            let center = new Vector2(x * 16 + 8, y * 16 + 8)
            where !_currentRoom.IsSolid(center)
            select center).First();
        _player.WarpTo(laterOpenTile);
        StepEntities(181);
        for (int frame = 0; frame < 180 &&
             laterWallmaster.State != WallmasterState.Grounded;
             frame++)
        {
            StepEntities();
        }
        StepEntities(12);
        for (int frame = 0;
             frame < 120 && _rooms.CurrentRoom.Id == 0xc5;
             frame++)
        {
            StepEntities();
        }
        if (_rooms.ActiveGroup != 4 || _rooms.CurrentRoom.Id != 0xce ||
            !_transitions.IsTransitioning)
        {
            throw new InvalidOperationException(
                "Common Wallmaster in dungeon $0b room 4:c5 did not request " +
                $"its imported $ce destination (group={_rooms.ActiveGroup:x1}, " +
                $"room=${_rooms.CurrentRoom.Id:x2}).");
        }
        for (int frame = 0; frame < 40 && _transitions.IsTransitioning; frame++)
            _transitions.Update(update);
        if (_transitions.IsTransitioning || !_player.Visible)
        {
            throw new InvalidOperationException(
                "Dungeon $0b Wallmaster destination transition did not finish.");
        }

        RestoreFlags();
        _player.EndCutsceneControl();
        _player.EndGetItemTwoHandPose();
        _player.Visible = true;
        LoadValidationRoom(0, 0x00);

        GD.Print("Validated complete Spirit's Grave dungeon01 coverage: all 22 rooms, " +
            "eight chests, small/boss doors, Moblins/Ropes/Ghini/Wallmasters, " +
            "platforms, torch/side-room/cube puzzles, linked burnable wall, layout-only entry " +
            "shutter, falling rewards, " +
            "Giant Ghini, Pumpkin Head, Heart Container, Eternal Spirit sequence, " +
            "exit warp, shared room 4:ed Ropes, and dungeon0b Wallmaster destination $ce.");
    }
}
