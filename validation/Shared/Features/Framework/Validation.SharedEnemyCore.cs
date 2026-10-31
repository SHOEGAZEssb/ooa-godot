using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedEnemyCore(FrontendValidationRoot root, bool batched)
    {
        var engine = root._gameplaySession!;
        var rooms = engine.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var room = rooms.CurrentRoom;
        var app = new ApplicationValidationFixture(root);
        entities.Clear();
        Vector2? dry = null;
        for (int y = 24; y < 112 && dry is null; y += 16)
        for (int x = 24; x < 144; x += 16)
            if (!(x is >= 56 and <= 104 && y is >= 56 and <= 88) && !player.World.Collides(new(x,y)) &&
                player.World.GetActiveTerrain(new(x,y)).Terrain.Hazard == HazardType.None)
            { dry = new(x,y); break; }
        FailIf(dry is null,"Shared enemy core fixture requires an actual safe player floor outside its controlled probes.");
        player.WarpTo(dry!.Value);
        int rng = entities.Random.Calls;
        SharedEnemyCoreCharacter Spawn(Vector2 position, bool hazards = false)
        {
            var character = new SharedEnemyCoreCharacter();
            character.Initialize(rooms.AssetRoot,room,position,hazards);
            var entity = new SharedEnemyCoreRoomEntity(character);
            entities.RegisterEnemySlot(entity,0); entities.AddEntity(entity);
            return character;
        }
        var enemy = Spawn(new(64,64));
        enemy.ApplySwordKnockback(new(48,64),EnemyKnockbackStrength.Low);
        FailIf(enemy.InvincibilityCounter != 16 || enemy.KnockbackCounter != 8 || enemy.KnockbackAngle != 8,
            "Native collisionEffects low recoil must publish $10 invincibility, $08 recoil and the opposite item angle in either pack.");
        app.Step(4,Vector2.Zero,batched:batched);
        FailIf(enemy.Position != new Vector2(72,64) || enemy.KnockbackCounter != 4 || enemy.InvincibilityCounter != 12,
            "Four actual shared enemy visits must apply SPEED_200, one counter decrement per update and native invincibility.");
        app.Step(1,Vector2.Zero,held:["debug_object_spawner"],pressed:["debug_object_spawner"],batched:batched);
        int updates = enemy.Updates;
        app.Step(4,Vector2.Zero,batched:batched);
        FailIf(!engine.Menus.Spawner.IsActive || enemy.Updates != updates || enemy.Position != new Vector2(72,64),
            "The common modal lease must freeze enemy recoil and counters in both selected games.");
        app.Step(1,Vector2.Zero,held:["debug_object_spawner"],pressed:["debug_object_spawner"],batched:batched);
        app.Step(4,Vector2.Zero,batched:batched);
        FailIf(enemy.Position != new Vector2(80,64) || enemy.KnockbackCounter != 0,
            "Recoil must resume at its retained counter and move on the terminal update.");
        entities.Clear(); enemy = Spawn(new(64,64)); enemy.SetHighRecoil();
        app.Step(9,Vector2.Zero,batched:batched);
        var dust = entities.EntityAdapters<KnockbackDustRoomEntity>().ToArray();
        FailIf(enemy.Position != new Vector2(91,64) || enemy.KnockbackCounter != 0x80 ||
            dust.Length != 3 || !dust.Select(effect => effect.Position.X).SequenceEqual(new[] { 64f,76f,88f }),
            "High-bit recoil must use SPEED_300 and allocate native $0f:$01 dust at decremented counters $88/$84/$80 before movement.");
        entities.Clear();
        Vector2 left = new(72,72), right = new(88,72);
        var originalLeft = room.GetTerrainInfo(left); var originalRight = room.GetTerrainInfo(right);
        FailIf(room.ActiveCollisions != 0,"Shared hazard fixture requires the native overworld collision set.");
        try
        {
            // Both native hazards.s overworld streams put $f3 before water $fe.
            room.SetPositionTileAndCollision(left,0xf3,0,0); room.SetPositionTileAndCollision(right,0xfe,0,0);
            enemy = Spawn(new(79,64),true);
            app.Step(1,Vector2.Zero,batched:batched);
            FailIf(!enemy.IsFallingIntoHole || enemy.IsDead || enemy.Position != new Vector2(78,64),
                "Native y+$05/x-$01 probe must win over the adjacent water probe and nudge left on the entry update.");
            app.Step(58,Vector2.Zero,batched:batched);
            FailIf(enemy.IsDead || enemy.Updates != 59,"A non-centered native hole fall must remain active through update $3b.");
            app.Step(1,Vector2.Zero,batched:batched);
            FailIf(!enemy.IsDead || enemy.Updates != 60 || enemy.DeathHazard != HazardType.Hole,
                "Native hole counter must complete on update $3c, counting its entry update.");
            entities.Clear();
            room.SetPositionTileAndCollision(left,1,0,0);
            enemy = Spawn(new(80,64),true);
            app.Step(1,Vector2.Zero,batched:batched);
            FailIf(!enemy.IsDead || !enemy.DiedInHazard || enemy.DeathHazard != HazardType.Water ||
                enemy.Position != new Vector2(81,64) || enemy.TakeHazardEffect() is not { Hazard: HazardType.Water },
                "When the left probe is safe, the native right water probe must nudge right and publish its immediate splash/deletion outcome.");
        }
        finally
        {
            entities.Clear();
            room.SetPositionTileAndCollision(left,originalLeft.Tile,originalLeft.Collision,0);
            room.SetPositionTileAndCollision(right,originalRight.Tile,originalRight.Collision,0);
        }
        var scent = new ScentSeedAttraction(); scent.Initialize(1);
        int angle = scent.UpdateAngle(new(64,64),new(64,40),16,false);
        FailIf(angle != 0 || scent.Counter != 0,"Scent angle must refresh after decrementing counter $01 to $00.");
        for (int update = 0; update < 15; update++) angle = scent.UpdateAngle(new(64,64),new(88,64),angle,false);
        FailIf(angle != 0 || scent.Counter != 0xf1,"Wrapping Scent counter must retain its target angle for the next $0f visits.");
        angle = scent.UpdateAngle(new(64,64),new(88,64),angle,false);
        FailIf(angle != 8 || scent.Counter != 0xf0 || entities.Random.Calls != rng,
            "Scent steering must refresh on the next low-nibble zero without adding gameplay RNG draws.");
    }
}
