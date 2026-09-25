using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonFireKeese()
    {
        var database = new EnemyDatabase();
        var definition = database.ImportedEnemy(0x39);
        FailIf(string.Concat(EnemyBehaviorTables.Shared.FireKeeseActiveCollisions.Select(v => v.Value)) != "10111111110101110000011111111110",
            "Fire Keese active collision mask lost source item ordering or eligibility.");
        FailIf(definition is not { Health: 2, DamageQuarters: 2, RadiusX: 6, RadiusY: 4, Palette: 5, TileBase: 0, Animations.Length: 4 },
            "Fire Keese lost enemyData $9d/$ab/$09/$50 or its four source animations.");
        FailIf(!EnemyBehaviorTables.Shared.FireKeeseZOffsets.Select(v => v.Value).SequenceEqual(new[] {128,96,64,48,32,32}) ||
            !EnemyBehaviorTables.Shared.FireKeeseCollisionEffects.Select(v => v.Value).SequenceEqual(new[] {
                2,0,15,15,8,9,9,10,10,8,0,10,0,8,8,37,0,0,0,0,0,47,9,9,10,9,32,28,8,40,41,0 }) ||
            !EnemyBehaviorTables.Shared.KeeseFireCollisionEffects.Select(v => v.Value).SequenceEqual(new[] {
                1,0,0,30,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0 }),
            "Fire Keese dive offsets or source collision rows $2b/$71 changed.");
        foreach (var (room, count) in new[] { (0x85,2), (0x8d,1), (0x8e,2) })
        {
            _saveData.SetRoomFlag(4, room, 0xff, false);
            LoadValidationRoom(4, room);
            FailIf(_entities.Entities<FireKeeseCharacter>().Count != count,
                $"Room 4:{room:x2} lost its ordered Fire Keese placements.");
        }
        var testRoom = _world.LoadRoom(4, 0x85);
        var random = new OracleRandom();
        var bat = new FireKeeseCharacter();
        Vector2 origin = new(120,88);
        bat.Initialize(definition, testRoom, origin, random);
        bat.UpdateFrame(Vector2.Zero);
        FailIf(bat.State != 11 || bat.Counter != 8 || bat.ZFixed != -7168 || bat.Speed != 20 || random.Calls != 2 || bat.DamageQuarters != 4,
            "Fire Keese state0 lost common/random-angle RNG, Z=-$1c, speed80, counter8 or lit damage override.");
        bat.UpdateFrame(bat.Position + new Vector2(33,0));
        FailIf(bat.State != 11 || bat.Counter != 7, "Fire Keese accepted a 33-pixel dive trigger.");
        bat.UpdateFrame(bat.Position + new Vector2(32,32));
        FailIf(bat.State != 12 || bat.Counter != 90 || bat.Speed != 25 || bat.ZFixed != -7168,
            "Inclusive +/-32 dive trigger must set91 then decrement90 and move without Z on the entry update.");
        for (int i = 0; i < 89; i++) bat.UpdateFrame(origin);
        FailIf(bat.State != 12 || bat.Counter != 1 || bat.ZFixed != -1088,
            "Fire Keese dive must sum exactly $17c0 subpixels over counter89..1.");
        Vector2 beforeZero = bat.Position;
        bat.UpdateFrame(origin);
        FailIf(bat.State != 13 || bat.ZFixed != -1088 || bat.Position != beforeZero,
            "Dive counter zero must enter recovery and animate without movement/Z integration.");
        for (int i = 0; i < 95; i++) bat.UpdateFrame(Vector2.Zero);
        FailIf(bat.State != 13 || bat.ZFixed != -7168, "Fire Keese recovery ended at zh=$e4 instead of below it.");
        bat.UpdateFrame(Vector2.Zero);
        FailIf(bat.State != 11 || bat.ZFixed != -7232 || bat.Counter != 8 || bat.Speed != 20 || random.Calls != 2,
            "Fire Keese recovery must retain the fractional overshoot below -28 and avoid extra RNG.");

        byte[] layout = (byte[])testRoom.Layout.Clone();
        try
        {
            for (int i = 0; i < 176; i++) if (testRoom.Layout[i] == 9) testRoom.Layout[i] = 0;
            bat.Position = origin;
            bat.NotifyLinkCollision();
            FailIf(!bat.UpdateFrame(origin) || bat.Lit || bat.State != 8 || bat.DamageQuarters != 2,
                "Link collision must shed fire once and set the unlit damage, animation and torch-search state.");
            testRoom.Layout[0x56] = 9;
            testRoom.Layout[0x58] = 9;
            bat.UpdateFrame(origin);
            FailIf(bat.State != 9 || bat.TorchPosition != new Vector2(136,88),
                "Equal Manhattan-distance torch candidates must select the later tile in source order.");
            for (int i = 0; i < 150 && bat.State != 10; i++) bat.UpdateFrame(origin);
            FailIf(bat.State != 10 || bat.Counter != 60 || bat.ZFixed >> 8 != -6,
                "Fire Keese must reach exact torch high XY and descend to zh=$fa before relighting.");
            for (int i = 0; i < 29; i++) bat.UpdateFrame(origin);
            FailIf(bat.Lit || bat.Counter != 31 || bat.AnimationIndex != 2, "Fire Keese relit before counter30.");
            bat.UpdateFrame(origin);
            FailIf(!bat.Lit || bat.Counter != 30 || bat.DamageQuarters != 4 || bat.AnimationIndex != 0,
                "Relight midpoint must restore lit palette/damage and animation0 exactly at counter30.");
            int oldAngle = bat.Angle;
            for (int i = 0; i < 30; i++) bat.UpdateFrame(origin);
            FailIf(bat.State != 13 || bat.Angle != ((oldAngle + 16) & 31) || bat.AnimationIndex != 1,
                "Relight zero update must reverse the full angle and enter recovery with animation1.");
            testRoom.Layout[0x56] = testRoom.Layout[0x58] = 0;
            bat.NotifyLinkCollision(); bat.UpdateFrame(origin); bat.UpdateFrame(origin);
            FailIf(bat.State != 13, "An unlit bat without a torch must resume recovery.");
            testRoom.Layout[175] = 9; // Last stored tile, including the padding column.
            for (int i = 0; i < 7; i++) bat.UpdateFrame(Vector2.Zero);
            FailIf(bat.State == 8 || bat.ScanPosition != 154, "Unlit Fire Keese scanned more than22 tiles per update.");
            bat.UpdateFrame(Vector2.Zero);
            FailIf(bat.State != 8 || bat.ScanPosition != 0, "Eighth scan update must detect the last stored torch and abort movement.");
        }
        finally { Array.Copy(layout, testRoom.Layout, layout.Length); bat.Free(); }

        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Step(int n = 1) =>
            StepGameplayUpdates(n, Vector2.Zero, [], [], batched: true);
        var snapshot = CaptureOracleRandomForValidation();
        Vector2 SafePoint() => Enumerable.Range(0, 176)
            .Select(i => new Vector2((i & 15) * 16 + 8, (i >> 4) * 16 + 8))
            .Where(p => p.X < _currentRoom.Width && !_currentRoom.IsSolid(p) && _currentRoom.GetTerrainInfo(p).Hazard == HazardType.None)
            .OrderBy(p => p.DistanceSquaredTo(new Vector2(120,88))).First();
        (Vector2, int, int, int)[] Run(bool batched)
        {
            RestoreOracleRandomForValidation(snapshot);
            LoadValidationRoom(4, 0x85);
            _player.WarpTo(SafePoint());
            FailIf(_currentRoom.IsSolid(_player.Position), "Fire Keese gameplay fixture must stand in actual open room geometry.");
            if (batched) Step(40); else for (int i = 0; i < 40; i++) Step();
            return _entities.Entities<FireKeeseCharacter>().Select(e => (e.Position,e.ZFixed,e.State,e.Counter)).ToArray();
        }
        FailIf(!Run(false).SequenceEqual(Run(true)), "Fire Keese diverged between individual and batched gameplay updates.");
        LoadValidationRoom(4, 0x8d);
        Step();
        var contactBat = _entities.Entities<FireKeeseCharacter>().Single();
        Vector2 contactPoint = SafePoint();
        contactBat.Position = contactPoint + new Vector2(0.75f,0.25f);
        typeof(FireKeeseCharacter).GetField("<ZFixed>k__BackingField", flags)!.SetValue(contactBat, -256);
        _player.WarpTo(contactPoint);
        int health = _inventory.HealthQuarters;
        Step();
        FailIf(_inventory.HealthQuarters != health - 4 || !contactBat.Lit || _entities.Entities<KeeseFirePart>().Count != 0,
            "Fire Keese contact must apply four-quarter damage and leave fire shedding pending until its next enemy dispatch.");
        Vector2 copiedPosition = OracleObjectMath.ToPixelPosition(contactBat.Position);
        int copiedZ = contactBat.ZFixed >> 8;
        Step();
        var fire = _entities.Entities<KeeseFirePart>().Single();
        FailIf(contactBat.Lit || fire.Counter != 180 || fire.Position != copiedPosition || fire.ZHigh != copiedZ || !fire.Visible,
            "Fire Keese must copy only high XYZ to a newly allocated part and run part state0 in the same update.");
        _player.WarpTo(contactPoint);
        Step(179);
        FailIf(fire.Finished || fire.Counter != 1, "PART_FIRE expired before180 updates after initialization.");
        Step();
        FailIf(_entities.Entities<KeeseFirePart>().Count != 0, "PART_FIRE failed to release its part slot at counter zero.");

        var hazard = new KeeseFirePart();
        hazard.Initialize(contactPoint, 0, _ => { }); hazard.UpdateFrame();
        var contactInventory = new InventoryState(_treasures, OracleSaveData.CreateStandardGame());
        var contactWorld = new ValidationRingPlayerWorld();
        var contactPlayer = new Player();
        AddChild(contactPlayer);
        contactPlayer.Initialize(contactWorld, contactInventory, contactPoint, new OracleRandom());
        health = contactInventory.HealthQuarters;
        hazard.HandleLinkContact(contactPlayer);
        FailIf(contactInventory.HealthQuarters != health - 2 || contactPlayer.InvincibilityFrames != 25 || contactPlayer.KnockbackFrames != 7,
            "PART_FIRE contact must use LINKDMG_00's25 invincibility/7 knockback updates and two-quarter damage.");
        hazard.Free();
        contactInventory.GiveTreasure(TreasureDatabase.TreasureShield, 3);
        contactInventory.EquipA(InventoryState.ItemShield);
        contactPlayer.WarpTo(contactPoint);
        contactPlayer.Face(Vector2I.Up);
        contactPlayer.UpdateShieldForValidation(attackHeld: true, itemHeld: false);
        var shieldSounds = new List<int>();
        var shieldFire = new KeeseFirePart();
        shieldFire.Initialize(contactPlayer.ShieldCollisionBounds.GetCenter(), 0, shieldSounds.Add);
        shieldFire.UpdateFrame();
        health = contactInventory.HealthQuarters;
        shieldFire.HandleLinkContact(contactPlayer);
        FailIf(contactInventory.HealthQuarters != health || contactPlayer.InvincibilityFrames != 0 || contactPlayer.KnockbackFrames != 16 ||
            !shieldSounds.SequenceEqual(new[] { OracleSoundEngine.SndClink2 }) || shieldFire.Counter != 180 || shieldFire.Finished,
            "Mirror Shield must use LINKDMG_28 recoil/SND_CLINK2 without damage, invincibility or deleting the fire.");
        shieldFire.Free();
        contactPlayer.Free();
        LoadValidationRoom(4, 0x8d);
        _player.WarpTo(SafePoint());
        Step();
        var poolBat = _entities.Entities<FireKeeseCharacter>().Single();
        var add = typeof(RoomEntityManager).GetMethod("AddEntity", flags)!;
        for (int i = 0; i < 16; i++)
        {
            var occupied = new StalfosBoneProjectile();
            occupied.Initialize(StalfosBoneRecord.Load(), _currentRoom, new Vector2(40,40), p => p);
            add.Invoke(_entities, [new StalfosBoneRoomEntity(occupied)]);
        }
        poolBat.NotifyLinkCollision();
        Step();
        FailIf(poolBat.Lit || poolBat.State != 8 || _entities.Entities<KeeseFirePart>().Count != 0,
            "A full part pool must suppress the dropped fire while still applying Fire Keese's unlit state transition.");
        LoadValidationRoom(4, 0x91);
        FailIf(_entities.Entities<FireKeeseCharacter>().Count != 0 || _entities.Entities<KeeseFirePart>().Count != 0,
            "Room exit retained Fire Keese or their damaging fire parts.");
        GD.Print("Validated Skull Dungeon Fire Keese placements, source RNG, dive/rise boundaries, ordered torch search/relight, incremental scan, contact fire handoff, source low knockback and batched gameplay updates.");
    }
}
