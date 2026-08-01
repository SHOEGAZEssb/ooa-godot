using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateHeadThwompFidelity()
    {
        const double update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        var visuals = new DungeonInteractionVisualDatabase();
        OracleRoomData isolatedRoom =
            new OracleWorldData().LoadRoom(4, 0x2b);
        DungeonInteractionVisual fireballVisual =
            visuals.Visual("head-thwomp-fireball");
        DungeonInteractionVisual fireballImpactVisual =
            visuals.Visual("head-thwomp-fireball-impact");
        DungeonInteractionVisual circularVisual =
            visuals.Visual("head-thwomp-circular-projectile");

        // PART $39 consumes its two launch RNG calls in the newly allocated
        // part's state-0 update, after the enemy has returned. Keeping this
        // check on a private stream prevents it from perturbing any gameplay
        // scenario or any later validation.
        var fireballRandom = new OracleRandom();
        var fireballReferenceRandom = new OracleRandom();
        OracleRandomResult expectedAngle = fireballReferenceRandom.Next();
        OracleRandomResult expectedSpeed = fireballReferenceRandom.Next();
        int[] fireballSpeeds = [0x0f, 0x19, 0x23, 0x2d];
        var fireballSounds = new List<int>();
        var randomFireball = new HeadThwompProjectile(
            new HeadThwompProjectileSpawn(
                new Vector2(0x78, 0x50),
                HeadThwompProjectileKind.Fireball,
                Angle: 0,
                Speed: 0,
                RandomizeLaunch: true),
            fireballVisual,
            fireballImpactVisual,
            isolatedRoom,
            fireballRandom,
            fireballSounds.Add);
        Vector2 fireballCreationPosition = randomFireball.Position;
        randomFireball.UpdateFrame(_player);
        FailIf(
            fireballRandom.Calls != 2 ||
            randomFireball.Position != fireballCreationPosition ||
            randomFireball.ZFixed != 0 ||
            randomFireball.Angle !=
                ((expectedAngle.Value & 0x10) + 0x08) ||
            randomFireball.Speed !=
                fireballSpeeds[expectedSpeed.Value & 0x03] ||
            fireballSounds is not [OracleSoundEngine.SndFallInHole],
            "PART_HEAD_THWOMP_FIREBALL did not defer its two launch RNG " +
            "calls, visibility, and SND_FALLINHOLE to its creation update.");
        randomFireball.Free();

        // PART $3c's counter2 grows cumulatively: turn intervals are
        // 2,3,3,4,4,5,5... rather than a fixed 2/3 alternation.
        var circularSounds = new List<int>();
        var circular = new HeadThwompProjectile(
            new HeadThwompProjectileSpawn(
                new Vector2(0x60, 0x60),
                HeadThwompProjectileKind.Circular,
                Angle: 0,
                Speed: 2),
            circularVisual,
            impactVisual: null,
            isolatedRoom,
            new OracleRandom(),
            circularSounds.Add);
        circular.UpdateFrame(_player);
        int[] expectedCircularAngles =
        [
            0, 2, 2, 2, 4, 4, 4, 6,
            6, 6, 6, 8, 8, 8, 8, 10
        ];
        for (int index = 0; index < expectedCircularAngles.Length; index++)
        {
            circular.UpdateFrame(_player);
            FailIf(
                circular.Finished ||
                circular.Angle != expectedCircularAngles[index],
                "PART_HEAD_THWOMP_CIRCULAR_PROJECTILE lost its cumulative " +
                $"turn interval at moving update {index + 1}.");
        }
        FailIf(
            circularSounds is not [OracleSoundEngine.SndBeam] ||
            circular.DeflectWithSword(),
            "PART_HEAD_THWOMP_CIRCULAR_PROJECTILE did not request SND_BEAM " +
            "once or incorrectly accepted a sword collision outside active " +
            "collision row $06.");
        circular.Free();

        // Mode $06 applies PART $3c's raw damage $f8 as four quarter-hearts.
        // Its shield table rejects L1 and destroys the part for L2/L3 with
        // LINKDMG_$20's single SND_CLINK2.
        var contactSave = OracleSaveData.CreateStandardGame();
        var contactInventory = new InventoryState(_treasures, contactSave);
        var contactWorld = new ValidationRingPlayerWorld();
        var contactPlayer = new Player { Name = "HeadThwompBeamContactPlayer" };
        AddChild(contactPlayer);
        contactPlayer.Initialize(
            contactWorld,
            contactInventory,
            new Vector2(0x60, 0x60),
            new OracleRandom());
        int contactHealth = contactPlayer.HealthQuarters;
        var harmlessFireball = new HeadThwompProjectile(
            new HeadThwompProjectileSpawn(
                contactPlayer.Position,
                HeadThwompProjectileKind.Fireball,
                Angle: 0,
                Speed: 0),
            fireballVisual,
            fireballImpactVisual,
            isolatedRoom,
            new OracleRandom(),
            static _ => { });
        harmlessFireball.UpdateFrame(contactPlayer);
        harmlessFireball.UpdateFrame(contactPlayer);
        FailIf(
            contactPlayer.HealthQuarters != contactHealth,
            "PART_HEAD_THWOMP_FIREBALL incorrectly enabled Link contact " +
            "outside active-collision row $04.");
        harmlessFireball.Free();
        var contactProjectile = new HeadThwompProjectile(
            new HeadThwompProjectileSpawn(
                contactPlayer.Position,
                HeadThwompProjectileKind.Circular,
                Angle: 0,
                Speed: 2),
            circularVisual,
            impactVisual: null,
            isolatedRoom,
            new OracleRandom(),
            static _ => { });
        contactProjectile.UpdateFrame(contactPlayer);
        contactProjectile.UpdateFrame(contactPlayer);
        FailIf(
            !contactProjectile.Finished ||
            contactPlayer.HealthQuarters != contactHealth - 4 ||
            contactWorld.Sounds.Count(sound =>
                sound == OracleSoundEngine.SndDamageLink) != 1,
            "PART_HEAD_THWOMP_CIRCULAR_PROJECTILE did not apply partData " +
            "damage $f8 as four quarter-hearts through mode $06.");
        contactProjectile.Free();
        contactPlayer.Free();

        var shieldSave = OracleSaveData.CreateStandardGame();
        var shieldInventory = new InventoryState(_treasures, shieldSave);
        shieldInventory.GiveTreasure(
            _treasures.GetObject("TREASURE_OBJECT_SHIELD_00"));
        shieldInventory.EquipA(InventoryState.ItemShield);
        var shieldWorld = new ValidationRingPlayerWorld();
        var shieldPlayer = new Player { Name = "HeadThwompBeamShieldPlayer" };
        AddChild(shieldPlayer);
        shieldPlayer.Initialize(
            shieldWorld,
            shieldInventory,
            new Vector2(0x60, 0x60),
            new OracleRandom());
        shieldPlayer.Face(Vector2I.Up);
        shieldPlayer.UpdateShieldForValidation(
            attackHeld: true,
            itemHeld: false);
        int woodenShieldHealth = shieldPlayer.HealthQuarters;
        var woodenShieldProjectile = new HeadThwompProjectile(
            new HeadThwompProjectileSpawn(
                shieldPlayer.ShieldCollisionBounds.GetCenter(),
                HeadThwompProjectileKind.Circular,
                Angle: 0,
                Speed: 2),
            circularVisual,
            impactVisual: null,
            isolatedRoom,
            new OracleRandom(),
            static _ => { });
        woodenShieldProjectile.UpdateFrame(shieldPlayer);
        woodenShieldProjectile.UpdateFrame(shieldPlayer);
        FailIf(
            !woodenShieldProjectile.Finished ||
            shieldPlayer.HealthQuarters != woodenShieldHealth - 4 ||
            shieldWorld.Sounds.Count(sound =>
                sound == OracleSoundEngine.SndClink2) != 0,
            "PART $3c incorrectly allowed ITEMCOLLISION_L1_SHIELD to use " +
            "mode $06's COLLISIONEFFECT_$1f response.");
        woodenShieldProjectile.Free();
        shieldPlayer.Free();

        var ironSave = OracleSaveData.CreateStandardGame();
        var ironInventory = new InventoryState(_treasures, ironSave);
        ironInventory.GiveTreasure(
            _treasures.GetObject("TREASURE_OBJECT_SHIELD_01"));
        ironInventory.EquipA(InventoryState.ItemShield);
        var ironWorld = new ValidationRingPlayerWorld();
        var ironPlayer = new Player { Name = "HeadThwompBeamIronShieldPlayer" };
        AddChild(ironPlayer);
        ironPlayer.Initialize(
            ironWorld,
            ironInventory,
            new Vector2(0x60, 0x60),
            new OracleRandom());
        ironPlayer.Face(Vector2I.Up);
        ironPlayer.UpdateShieldForValidation(
            attackHeld: true,
            itemHeld: false);
        int ironShieldHealth = ironPlayer.HealthQuarters;
        var ironShieldProjectile = new HeadThwompProjectile(
            new HeadThwompProjectileSpawn(
                ironPlayer.ShieldCollisionBounds.GetCenter(),
                HeadThwompProjectileKind.Circular,
                Angle: 0,
                Speed: 2),
            circularVisual,
            impactVisual: null,
            isolatedRoom,
            new OracleRandom(),
            static _ => { });
        ironShieldProjectile.UpdateFrame(ironPlayer);
        ironShieldProjectile.UpdateFrame(ironPlayer);
        FailIf(
            !ironShieldProjectile.Finished ||
            ironPlayer.HealthQuarters != ironShieldHealth ||
            ironWorld.Sounds.Count(sound =>
                sound == OracleSoundEngine.SndClink2) != 1,
            "PART $3c did not accept ITEMCOLLISION_L2_SHIELD and apply " +
            "COLLISIONEFFECT_$1f/LINKDMG_$20 exactly once.");
        ironShieldProjectile.Free();
        ironPlayer.Free();

        // Purple-face PART $3b has its own falling visual, consumes exactly
        // one X-position RNG call, and changes to fixed-bank common-sprite
        // impact graphics with the same expanding collision radii as PART $39.
        var boulderRandom = new OracleRandom();
        var boulderReferenceRandom = new OracleRandom();
        int expectedBoulderX = boulderReferenceRandom.Next().Value & 0x7c;
        var boulderSounds = new List<int>();
        var boulder = new HeadThwompBoulder(
            isolatedRoom,
            boulderRandom,
            visuals.Visual("head-thwomp-boulder"),
            visuals.Visual("head-thwomp-boulder-impact"),
            boulderSounds.Add);
        using Image fallingBoulderImage = boulder.CurrentTexture.GetImage();
        ulong fallingBoulderHash =
            OracleGraphicsCache.PixelHash(fallingBoulderImage);
        boulder.UpdateFrame(_player);
        FailIf(
            boulderRandom.Calls != 1 ||
            boulder.Position != new Vector2(expectedBoulderX, 0) ||
            boulder.SpeedYFixed != 0x0200 ||
            boulderSounds is not [OracleSoundEngine.SndFallInHole] ||
            boulder.DeflectWithSword(),
            "PART_3b did not choose its source top-of-camera X coordinate, " +
            "start at speedZ $0200, or request SND_FALLINHOLE once.");
        for (int frame = 0;
             frame < 240 && !boulder.Breaking && !boulder.Finished;
             frame++)
        {
            boulder.UpdateFrame(_player);
        }
        FailIf(
            boulder.Finished || !boulder.Breaking ||
            boulderSounds.Count(sound =>
                sound == OracleSoundEngine.SndBreakRock) != 1,
            "PART_3b did not enter its solid-floor impact state and request " +
            "one SND_BREAK_ROCK.");
        using Image impactBoulderImage = boulder.CurrentTexture.GetImage();
        FailIf(
            OracleGraphicsCache.PixelHash(impactBoulderImage) ==
                fallingBoulderHash,
            "PART_3b retained its falling boulder sheet after switching to " +
            "the fixed-bank common-sprite impact animation.");
        boulder.UpdateFrame(_player);
        FailIf(
            boulder.CollisionBounds.Size != new Vector2(0x12, 0x08),
            "PART_3b impact frame 0 did not apply source radii Y/X $04/$09.");
        for (int frame = 0; frame < 60 && !boulder.Finished; frame++)
            boulder.UpdateFrame(_player);
        FailIf(
            !boulder.Finished,
            "PART_3b did not delete on its terminal animation parameter.");
        boulder.Free();

        byte originalBossRoomFlags = _saveData.GetRoomFlags(4, 0x2b);
        try
        {
            for (int bit = 0; bit < 8; bit++)
                _saveData.SetRoomFlag(4, 0x2b, (byte)(1 << bit), false);
            _dialogue.Close();
            _player.EndCutsceneControl();
            _player.EndGetItemTwoHandPose();
            _player.Visible = true;
            _entities.ClearRecentEnemyDefeats();
            LoadValidationRoom(4, 0x2b);

            void Step(int count = 1)
            {
                for (int frame = 0; frame < count; frame++)
                    _entities.Update(update, _player);
            }

            void WaitFor(
                Func<bool> predicate,
                int maximumUpdates,
                string failure)
            {
                for (int frame = 0;
                     frame < maximumUpdates && !predicate();
                     frame++)
                {
                    Step();
                }
                FailIf(!predicate(), failure);
            }

            static Vector2 PackedPoint(int packedPosition) => new(
                (packedPosition & 0x0f) * 16 + 8,
                (packedPosition >> 4) * 16 + 8);

            byte CollisionAt(int packedPosition) =>
                _currentRoom.GetTerrainInfo(
                    PackedPoint(packedPosition)).Collision;

            HeadThwompBoss head =
                _entities.Entities<HeadThwompBoss>().Single();

            void CatchFace(
                int direction,
                HeadThwompState selectedState)
            {
                WaitFor(
                    () => head.State == HeadThwompState.Spinning &&
                        head.Direction == direction,
                    600,
                    $"Head Thwomp did not rotate to direction {direction} " +
                    "for the requested face fidelity scenario.");
                _player.WarpTo(new Vector2(0x77, 0x50), recordSafe: false);
                _player.Face(Vector2I.Right);
                BombEffect bomb = _entities.Spawn<BombEffect>(
                    new BombSpawn(
                        _player,
                        new BombDatabase().Data,
                        4,
                        static _ => { }));
                bomb.Throw(
                    _player,
                    Vector2I.Zero,
                    Vector2I.Zero,
                    speedZ: 0,
                    speedRaw: 0);
                Step();
                FailIf(
                    head.State != HeadThwompState.BombPause ||
                    !bomb.Finished,
                    "Head Thwomp did not consume one live Bomb in its " +
                    "$50/$78 mouth window before beginning the spin.");
                _player.WarpTo(new Vector2(0x10, 0x70), recordSafe: false);
                WaitFor(
                    () => head.State == selectedState,
                    1600,
                    $"Head Thwomp's post-bomb spin did not select " +
                    $"{selectedState} at direction {direction}.");
            }

            FailIf(
                head.Visible,
                "Head Thwomp was visible before source state 0 ran.");
            _player.WarpTo(new Vector2(0x28, 0x98), recordSafe: false);
            _sound.ClearPlayRequestAudit();
            Step();
            FailIf(
                !head.Visible ||
                head.State != HeadThwompState.WaitingForLink ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndCtrlStopMusic) != 1 ||
                CollisionAt(0x46) != 0x01 ||
                CollisionAt(0x48) != 0x02 ||
                CollisionAt(0x56) != 0x05 ||
                CollisionAt(0x57) != 0x0f ||
                CollisionAt(0x58) != 0x0a,
                "Head Thwomp state 0 did not stop music, become visible, " +
                "and install its five surrounding collision cells.");
            Step();
            FailIf(
                head.State != HeadThwompState.Spinning ||
                _currentRoom.GetMetatile(PackedPoint(0xa4)) != 0x3d ||
                _player.LocalRespawnPosition != new Vector2(0x48, 0x98) ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 1 ||
                _sound.PlayRequestsFor(OracleSoundEngine.MusBoss) != 1,
                "Head Thwomp did not close $a4, set the local $48/$98 " +
                "respawn, and request the door and boss sounds on fight start.");

            // Normal state $09 clinks on odd transition heads only.
            _sound.ClearPlayRequestAudit();
            Step(17);
            FailIf(
                head.Direction != 0 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndClink2) != 0,
                "Head Thwomp rotated before state $09's initial 18-count.");
            Step();
            FailIf(
                head.Direction != 1 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndClink2) != 1,
                "Head Thwomp's normal rotation did not clink on odd head 1.");
            Step(10);
            FailIf(head.Direction != 1,
                "Head Thwomp shortened health-4 odd-head delay $0b.");
            Step();
            FailIf(
                head.Direction != 2 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndClink2) != 1,
                "Head Thwomp's normal rotation incorrectly clinked on even head 2.");

            // Green: initialize on the update after selection, then create
            // seven PART $39 shots at counter values $e0..$20.
            CatchFace(0, HeadThwompState.Green);
            _sound.ClearPlayRequestAudit();
            int greenRandomStart = _random.Calls;
            Step();
            FailIf(
                CollisionAt(0x47) != 0x00 ||
                _random.Calls != greenRandomStart,
                "The green face did not reserve a separate initialization " +
                "update with its mouth open and no RNG consumption.");
            Step(15);
            FailIf(
                _entities.Entities<HeadThwompProjectile>().Any(projectile =>
                    projectile.Name == "HeadThwompFireball"),
                "The green face fired before counter1 reached $e0.");
            OracleRandomState firstGreenShotState = _random.CaptureState();
            var firstGreenShotReference = new OracleRandom();
            firstGreenShotReference.RestoreState(firstGreenShotState);
            int firstGreenAngle =
                (firstGreenShotReference.Next().Value & 0x10) + 0x08;
            int firstGreenSpeed = fireballSpeeds[
                firstGreenShotReference.Next().Value & 0x03];
            Step();
            HeadThwompProjectile firstGreenShot =
                _entities.Entities<HeadThwompProjectile>().Single(projectile =>
                    projectile.Name == "HeadThwompFireball");
            FailIf(
                firstGreenShot.Position != head.Position ||
                firstGreenShot.ZFixed != 0 ||
                firstGreenShot.Angle != firstGreenAngle ||
                firstGreenShot.Speed != firstGreenSpeed ||
                _random.Calls != greenRandomStart + 2 ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndFallInHole) != 1,
                "The green face's first PART $39 shot did not run state 0 " +
                "in the source parts phase with two ordered RNG calls.");
            WaitFor(
                () => head.State == HeadThwompState.Resume,
                260,
                "The green face did not resume after its $f0 countdown.");
            FailIf(
                _random.Calls != greenRandomStart + 14 ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndFallInHole) != 7 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndBeam) != 0,
                "The green face did not create exactly seven randomized " +
                "fireballs and no blue-face beams.");

            // Blue: one RNG call chooses signed turn step, then eight volleys
            // use 8 animated wait updates, 8 animated cooldown updates, and
            // 30 frozen cooldown updates apiece.
            CatchFace(2, HeadThwompState.Blue);
            _sound.ClearPlayRequestAudit();
            OracleRandomState blueRandomState = _random.CaptureState();
            var blueReferenceRandom = new OracleRandom();
            blueReferenceRandom.RestoreState(blueRandomState);
            int expectedTurnStep =
                (blueReferenceRandom.Next().Value & 0x02) == 0 ? -2 : 2;
            int blueRandomStart = _random.Calls;
            Step();
            FailIf(
                _random.Calls != blueRandomStart + 1 ||
                CollisionAt(0x47) != 0x00 ||
                head.AnimationIndex != 10,
                "The blue face did not consume one direction RNG call and " +
                "hold its closed-mouth animation in substate 0.");
            Step(7);
            FailIf(
                _sound.PlayRequestsFor(OracleSoundEngine.SndBeam) != 0,
                "The blue face fired before its source 8-update wait ended.");
            Step();
            HeadThwompProjectile firstCircular =
                _entities.Entities<HeadThwompProjectile>().Single(projectile =>
                    projectile.Name == "HeadThwompCircularProjectile");
            FailIf(
                firstCircular.Position != head.Position + Vector2.Up * 8 ||
                firstCircular.Angle != 0 ||
                firstCircular.Speed != expectedTurnStep ||
                CollisionAt(0x47) != 0x03 ||
                head.AnimationIndex != 2 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndBeam) != 1,
                "The blue face did not open its mouth and create PART $3c " +
                "eight pixels above itself on the eighth wait update.");
            Step(8);
            int frozenBlueFrame = head.AnimationFrame;
            Step(29);
            FailIf(
                head.AnimationFrame != frozenBlueFrame ||
                CollisionAt(0x47) != 0x03,
                "The blue face animated during substate 3's 30-update hold.");
            Step();
            FailIf(
                head.AnimationIndex != 10 ||
                CollisionAt(0x47) != 0x00 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndBeam) != 1,
                "The blue face did not close its mouth before the repeated volley.");
            Step(7);
            FailIf(
                _sound.PlayRequestsFor(OracleSoundEngine.SndBeam) != 1,
                "The blue face shortened its repeated 8-update wait.");
            Step();
            FailIf(
                _sound.PlayRequestsFor(OracleSoundEngine.SndBeam) != 2,
                "The blue face did not create its second volley on schedule.");
            WaitFor(
                () => head.State == HeadThwompState.Resume,
                400,
                "The blue face did not finish all eight source volleys.");
            FailIf(
                _random.Calls != blueRandomStart + 1 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndBeam) != 8 ||
                CollisionAt(0x47) != 0x00 ||
                head.AnimationIndex != 10,
                "The blue face did not emit exactly eight SND_BEAM volleys " +
                "without consuming per-projectile RNG.");

            // Purple: clear all six cells, pound once, and allocate exactly
            // six source-native PART $3b boulders at 16-update intervals.
            CatchFace(4, HeadThwompState.Purple);
            _player.WarpTo(new Vector2(0x10, 0x70), recordSafe: false);
            _sound.ClearPlayRequestAudit();
            int purpleRandomStart = _random.Calls;
            Step();
            FailIf(
                new[] { 0x46, 0x47, 0x48, 0x56, 0x57, 0x58 }
                    .Any(position => CollisionAt(position) != 0x00) ||
                head.Position.Y != 0x56,
                "The purple face did not clear all six collision cells and " +
                "reserve its source initialization update before falling.");
            WaitFor(
                () => _sound.PlayRequestsFor(
                    OracleSoundEngine.SndStrongPound) == 1,
                90,
                "The purple face did not pound at Y=$90 with SND_STRONG_POUND.");
            FailIf(
                head.Position.Y != 0x90 ||
                _entities.ScreenShakeCounter != 59,
                "The purple face did not begin the source 60-update vertical shake.");
            WaitFor(
                () => head.State == HeadThwompState.Resume,
                360,
                "The purple face did not rest, rise to Y=$56, and resume.");
            FailIf(
                head.Position.Y != 0x56 ||
                // The 60-update two-axis shake consumes two global RNG
                // calls per update; the six boulders add one state-0 call
                // apiece after that source-owned sequence.
                _random.Calls != purpleRandomStart + 126 ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndFallInHole) != 6 ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndBreakRock) != 6 ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndStrongPound) != 1 ||
                CollisionAt(0x46) != 0x01 ||
                CollisionAt(0x47) != 0x00 ||
                CollisionAt(0x48) != 0x02 ||
                CollisionAt(0x56) != 0x05 ||
                CollisionAt(0x57) != 0x0f ||
                CollisionAt(0x58) != 0x0a ||
                _entities.Entities<HeadThwompBoulder>().Count != 0,
                "The purple face did not spawn and finish exactly six PART " +
                "$3b boulders or restore its five surrounding solid cells " +
                $"(position={head.Position}, rng={_random.Calls - purpleRandomStart}, " +
                $"fall={_sound.PlayRequestsFor(OracleSoundEngine.SndFallInHole)}, " +
                $"break={_sound.PlayRequestsFor(OracleSoundEngine.SndBreakRock)}, " +
                $"pound={_sound.PlayRequestsFor(OracleSoundEngine.SndStrongPound)}, " +
                $"collisions={CollisionAt(0x46):x2}/{CollisionAt(0x47):x2}/" +
                $"{CollisionAt(0x48):x2}/{CollisionAt(0x56):x2}/" +
                $"{CollisionAt(0x57):x2}/{CollisionAt(0x58):x2}, " +
                $"live={_entities.Entities<HeadThwompBoulder>().Count}).");

            void CompleteNonlethalRed(int expectedHealth)
            {
                CatchFace(6, HeadThwompState.Red);
                _sound.ClearPlayRequestAudit();
                Step();
                ItemDropEffect[] hearts =
                    _entities.Entities<ItemDropEffect>()
                        .Where(drop => drop.SubId == ItemDropDatabase.Heart)
                        .ToArray();
                FailIf(
                    head.Health != expectedHealth ||
                    hearts.Count(heart =>
                        heart.ElapsedFrames == 1 &&
                        heart.Position == head.Position + Vector2.Down * 20) != 1 ||
                    _sound.PlayRequestsFor(
                        OracleSoundEngine.SndBossDamage) != 1,
                    "The red face did not remove one health point, request " +
                    "SND_BOSS_DAMAGE, and update its nonlethal heart in the " +
                    "same source parts phase " +
                    $"(health={head.Health}/{expectedHealth}, " +
                    $"hearts={hearts.Length}, " +
                    $"fresh={hearts.Count(heart => heart.ElapsedFrames == 1)}, " +
                    $"positions={string.Join(',', hearts.Select(heart => heart.Position))}, " +
                    $"sound={_sound.PlayRequestsFor(OracleSoundEngine.SndBossDamage)}).");
                WaitFor(
                    () => head.State == HeadThwompState.Resume,
                    140,
                    "A nonlethal red face did not resume after 120 updates.");
            }

            CompleteNonlethalRed(expectedHealth: 3);
            CompleteNonlethalRed(expectedHealth: 2);
            CompleteNonlethalRed(expectedHealth: 1);

            CatchFace(6, HeadThwompState.Red);
            _sound.ClearPlayRequestAudit();
            Step();
            FailIf(
                head.Health != 0 ||
                _entities.Entities<ItemDropEffect>().Any(drop =>
                    drop.SubId == ItemDropDatabase.Heart &&
                    drop.ElapsedFrames == 1) ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndBossDamage) != 1 ||
                new[] { 0x46, 0x47, 0x48, 0x56, 0x57, 0x58 }
                    .Any(position => CollisionAt(position) != 0x00),
                "The lethal red face did not use the source no-heart branch " +
                "and clear all six collision cells.");
            WaitFor(
                () => _sound.PlayRequestsFor(
                    OracleSoundEngine.SndStrongPound) == 1,
                120,
                "The lethal red face did not pound at Y=$90 before generic death.");
            int landingShake = _entities.ScreenShakeCounter;
            FailIf(
                head.Position.Y != 0x90 || landingShake != 59,
                "The lethal red face did not begin exactly one 60-update pound shake.");
            Step();
            FailIf(
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndBossDead) != 1 ||
                _entities.ScreenShakeCounter != landingShake - 1 ||
                head.Visible,
                "Head Thwomp generic death did not request SND_BOSS_DEAD, " +
                "preserve the existing pound shake, and begin visibility flicker.");
            Step(118);
            FailIf(
                head.IsDead,
                "Head Thwomp ended before generic boss death's 120 updates.");
            Step();
            FailIf(
                !head.IsDead ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndBossDead) != 1 ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndBigExplosion) != 1 ||
                _entities.Entities<BossDeathExplosionEffect>() is not
                    [{ BossId: 0x79 }],
                "Head Thwomp did not create one source-updated boss explosion " +
                "and restore room music after exactly 120 death updates.");
        }
        finally
        {
            for (int bit = 0; bit < 8; bit++)
            {
                byte mask = (byte)(1 << bit);
                _saveData.SetRoomFlag(
                    4,
                    0x2b,
                    mask,
                    (originalBossRoomFlags & mask) != 0);
            }
            _dialogue.Close();
            _player.EndCutsceneControl();
            _player.EndGetItemTwoHandPose();
            _player.Visible = true;
            LoadValidationRoom(0, 0x00);
        }

        GD.Print(
            "Validated source-faithful Head Thwomp entry, odd/even spin " +
            "clinks, green fireballs, blue beams, purple boulders, red " +
            "hearts, lethal pound, sounds, collision cells, and death timing.");
    }
}
