using Godot;
using System.Linq;
using System;
using System.Collections.Generic;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownDungeonBeamosSourceData()
    {
        var data = EnemyBehaviorTables.Shared.Beamos;
        // Independent transcription of enemyCode16 and partCode29 operands.
        FailIf(data.RotationFrames != 5 || data.CooldownFrames != 40 ||
            data.SoundCounter != 11 || data.ChargeFrames != 20 || data.GlowFrames != 20 ||
            data.TargetAngleWindow != 2 || data.BeamBlinkMask != 1 ||
            data.BeamCollisionDelay != 2 || data.BeamSpeed != 0x50 || data.BeamVelocityScale != 4,
            "ENEMY_BEAMOS $16 / PART_BEAM $29 source timing or scaled velocity operands changed.");
        int[] angles = [0,0,1,1,1,1,1,2,2,2,3,3,3,3,3,4,
            4,4,5,5,5,5,5,6,6,6,7,7,7,7,7,0];
        int[] beamAngles = [0,0,1,2,2,2,3,4,4,4,5,6,6,6,7,0];
        FailIf(!data.AngleAnimations.Select(row => row.Value).SequenceEqual(angles) ||
            !data.BeamAngleAnimations.Select(row => row.Value).SequenceEqual(beamAngles),
            "ENEMY_BEAMOS $16 and PART_BEAM $29 must preserve their distinct angle animation tables.");
        FailIf(data.AngleAnimations.Any(row => !row.Source.StartsWith("object_code/common/enemies/beamos.s:")) ||
            data.BeamAngleAnimations.Any(row => !row.Source.StartsWith("object_code/common/parts/beam.s:")),
            "Beamos animation operands must retain their disassembly source identity.");
        GD.Print("Validated Crown Dungeon Beamos source timing, beam velocity operands and both angle animation tables.");
    }

    private void ValidateCrownDungeonBeamosTiming()
    {
        var database = new EnemyDatabase();
        var random = new OracleRandom();
        var sounds = new List<int>();
        var spawns = new List<RoomEntitySpawn>();
        var room = Room060MovementFixture();
        var eye = new BeamosCharacter();
        bool available = true;
        eye.Initialize(database.ImportedEnemy(0x16), room, new Vector2(72, 72),
            random, sounds.Add, () => available, () => 0);
        byte tile = room.GetMetatile(eye.Position);
        eye.InitializeState();
        FailIf(random.Calls != 1 || eye.State != 8 || eye.Counter != 5 ||
            !eye.Visible || eye.CollisionEnabled || !room.IsSolid(eye.Position) || room.GetMetatile(eye.Position) != tile,
            "Beamos $16 state0 must consume one RNG value and solidify its existing tile.");
        // Target to the left cannot match angles $00-$08.
        for (int n = 1; n <= 40; n++)
        {
            eye.UpdateFrame(new Vector2(24, 72), spawns);
            FailIf(eye.Angle != n / 5 || eye.Counter != 5 - n % 5 || eye.State != 8,
                $"Beamos $16 rotation boundary at update {n} changed.");
        }
        eye.UpdateFrame(new Vector2(120, 72), spawns);
        FailIf(eye.State != 9 || eye.Counter != 20 || eye.InvincibilityCounter != 19,
            "Beamos $16 must charge on angle $08, then the shared post-handler update decrements glow $14 to $13.");
        for (int n = 1; n <= 20; n++)
        {
            available = n != 12; // Failed getFreePartSlot consumes this segment.
            eye.UpdateFrame(new Vector2(24, 72), spawns);
            FailIf(eye.InvincibilityCounter != Math.Max(0, 19 - n),
                $"$16 post-handler glow boundary at charge update {n} changed.");
            FailIf(sounds.Count != (n >= 9 ? 1 : 0), "$16 beam sound must occur only at counter $0b.");
            int expected = Math.Max(0, Math.Min(n - 9, 10)) - (n >= 12 ? 1 : 0);
            FailIf(spawns.Count != expected || eye.Angle != 8,
                $"$16 charge update {n} changed segment count or held angle.");
        }
        int[] subids = [0,1,1,0,1,0,1,0,1]; // counter $08 was dropped.
        FailIf(!spawns.Cast<BeamosBeamSpawn>().Select(x => x.SubId).SequenceEqual(subids) ||
            spawns.Cast<BeamosBeamSpawn>().Any(x => x.Position != eye.Position || x.Angle != 8) ||
            eye.State != 8 || eye.Counter != 5 || eye.Cooldown != 40 || random.Calls != 1,
            "$16 must preserve source segment alternation, failed allocation and the 40-update cooldown.");
        available = true;
        // Arrange each target along the exact angle the next update will use.
        for (int n = 1; n <= 40; n++)
        {
            int nextAngle = (eye.Angle + (eye.Counter == 1 ? 1 : 0)) & 31;
            Vector2 target = eye.Position + OracleObjectMovement.Shared.Direction(nextAngle) * 32;
            eye.UpdateFrame(target, spawns);
            FailIf(eye.State != (n == 40 ? 9 : 8),
                $"$16 refired before/after cooldown update {n}.");
        }
        eye.Free();

        void Step(int n) =>
            StepGameplayUpdates(n, Vector2.Zero, [], [], batched: true);
        foreach (bool batch in new[] { false, true })
        {
            LoadValidationRoom(4, 0xb2);
            var placed = _entities.Entities<BeamosCharacter>().Single();
            FailIf(placed.Position != new Vector2(0x78, 0x58) || _entities.RoomEnemyCount != 3,
                "Crown Dungeon $4:$b2 must count its Beamos at YX $58,$78 and two Green Zols.");
            _player.WarpTo(new Vector2(0x78, 0x38), recordSafe: false);
            Step(1);
            Step(1);
            FailIf(placed.State != 9, "$4:$b2 Beamos did not acquire Link through the actual update loop.");
            if (batch) Step(10); else for (int i = 0; i < 10; i++) Step(1);
            var first = _entities.Entities<BeamosBeamPart>().Single();
            FailIf(first.State != 1 || first.Position != placed.Position || first.Visible || first.SubId != 0,
                "$29 must initialize in the native part phase without moving or displaying its first segment.");
            Step(1);
            FailIf(first.Position != placed.Position + Vector2.Up * 8 || first.Counter != 1 || first.Finished,
                "$29 first movement must skip collision against the Beamos tile.");
            Step(1);
            FailIf(first.Position != placed.Position + Vector2.Up * 16 || first.State != 2,
                "$29 second movement must enter tile-tested state2.");
        }
        using (var fixture = RoomEntityValidationFixture.ForRoot(this,
            new() { Enemies = database, Random = new OracleRandom() }))
        {
            fixture.Manager.LoadRoom(4, _world.LoadRoom(4, 0xb2));
            fixture.Manager.BeginScreenTransition(4, _world.LoadRoom(4, 0xa4), Vector2.Right * 160);
            var incoming = fixture.Manager.Entities<BeamosCharacter>();
            FailIf(incoming.Count != 2 || incoming.Any(x => x.State != 8 || x.Counter != 5 || !x.Visible),
                "$4:$a4 Beamos state0 must finish before incoming-room presentation.");
            int calls = fixture.Manager.RandomCalls;
            fixture.Manager.Update(30.0 / 60.0, _player);
            FailIf(incoming.Any(x => x.Counter != 5 || x.Angle != 0) || fixture.Manager.RandomCalls != calls,
                "$16 must not rotate or consume RNG during room scrolling.");
            fixture.Manager.FinishScreenTransition();
            fixture.Manager.Update(1.0 / 60.0, _player);
            FailIf(incoming.Any(x => x.State == 0) || fixture.Manager.RandomCalls != calls,
                "$16 must not repeat state0 when scrolling completes.");
        }
        GD.Print("Validated Beamos $16 rotation, charge, sound, dropped allocations, repeated cooldown and $4:$b2 native-part initialization with single/batched gameplay updates.");
    }

    private void ValidateCrownDungeonBeamosBeam()
    {
        var data = new BeamosBeamDatabase();
        FailIf(data.RadiusX != 3 || data.RadiusY != 3 || data.Damage != 2 ||
            data.TileBase != 8 || data.Palette != 4 || data.Animations.Length != 8,
            "PART_BEAM $29 source attributes changed.");
        var room = Room060MovementFixture();
        var beam = new BeamosBeamPart(new(new Vector2(72, 72), 8, 1), data, room);
        room.SetPositionTileAndCollision(new Vector2(80,72), room.GetMetatile(new Vector2(80,72)), 0x0f, 0);
        beam.UpdateFrame(0);
        beam.UpdateFrame(1);
        FailIf(beam.Position != new Vector2(80,72) || beam.Finished || beam.Visible,
            "$29 first moving update must ignore a solid tile and blink odd subid on odd frames.");
        beam.UpdateFrame(2);
        FailIf(!beam.Finished || beam.Visible || beam.Position != new Vector2(88,72),
            "$29 second moving update must collide with the same solid tile.");
        beam.Free();
        room = Room060MovementFixture();
        beam = new BeamosBeamPart(new(new Vector2(72,72), 1, 0), data, room);
        beam.UpdateFrame(0);
        // objectSpeedTable SPEED_200/angle $01 = Y -502, X 100, scaled by 4.
        beam.UpdateFrame(1);
        FailIf(beam.Position != new Vector2(73,64), "$29 diagonal velocity must multiply source 8.8 components before extracting pixels.");
        beam.ClearHealthAndCollision();
        beam.UpdateFrame(2);
        FailIf(beam.Finished || beam.CollisionEnabled, "$29 ignores zero health while retaining a cleared collision bit.");
        beam.Free();
        beam = new BeamosBeamPart(new(new Vector2(room.Width - 8,72), 8, 0), data, room);
        beam.UpdateFrame(0);
        beam.UpdateFrame(1);
        beam.UpdateFrame(2);
        FailIf(!beam.Finished, "$29 must delete at the source room boundary after its tile grace.");
        beam.Free();
        for (int level = 1; level <= 3; level++)
        {
            var save = OracleSaveData.CreateStandardGame();
            var inventory = new InventoryState(_treasures, save);
            inventory.GiveTreasure(_treasures.GetObject($"TREASURE_OBJECT_SHIELD_0{level - 1}"));
            inventory.EquipA(InventoryState.ItemShield);
            var world = new ValidationRingPlayerWorld();
            var player = new Player();
            AddChild(player);
            player.Initialize(world, inventory, new Vector2(72,72), new OracleRandom());
            player.Face(Vector2I.Right);
            player.UpdateShieldForValidation(attackHeld: true, itemHeld: false);
            beam = new BeamosBeamPart(new(player.ShieldCollisionBounds.GetCenter(), 24, 0), data, room);
            beam.UpdateFrame(0);
            beam.HandleLinkContact(player);
            FailIf(beam.Finished, "$29 must defer its $83 deletion until the next part update.");
            beam.UpdateFrame(1);
            FailIf(beam.Finished != (level == 3), $"$29 shield level {level} ignored its native active-collision mask.");
            beam.Free();
            player.Free();
        }
        foreach (bool ring in new[] { false, true })
        {
            var save = OracleSaveData.CreateStandardGame();
            if (ring) { save.WriteWramByte(0xc6cc, 1); save.WriteWramByte(0xc6c6, (byte)RingId.BlueLuck); }
            var inventory = new InventoryState(_treasures, save);
            if (ring) FailIf(!inventory.EquipRingAt(0), "Could not equip Blue Luck Ring for $29 collision.");
            var player = new Player();
            AddChild(player);
            player.Initialize(new ValidationRingPlayerWorld(), inventory, new Vector2(72,72), new OracleRandom());
            int health = player.HealthQuarters;
            beam = new BeamosBeamPart(new(player.Position, 8, 0), data, room);
            beam.UpdateFrame(0);
            beam.HandleLinkContact(player);
            FailIf(player.HealthQuarters != health - (ring ? 1 : 2) || beam.Finished,
                "$29 effect $3c must damage Link, halve damage for Blue Luck Ring and preserve the segment.");
            beam.Free();
            player.Free();
        }
        GD.Print("Validated PART_BEAM $29 attributes, fixed-point movement, blink, tile grace/boundaries, shield levels, Blue Luck Ring and cleared-health continuation.");
    }
}
