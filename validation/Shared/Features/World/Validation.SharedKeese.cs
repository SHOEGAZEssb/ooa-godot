
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedKeese(FrontendValidationRoot root, bool batched)
    {
        var engine = root._gameplaySession!;
        var rooms = engine.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var screen = root._scene.DebugObjectSpawnerScreen;
        var app = new ApplicationValidationFixture(root);
        foreach (int subId in new[] { 0,1 })
        {
            entities.Clear();
            int calls = entities.Random.Calls, roomCount = entities.RoomEnemyCount;
            app.Step(1,Vector2.Zero,held:["debug_object_spawner"],pressed:["debug_object_spawner"],batched:batched);
            FailIf(!engine.Menus.Spawner.IsActive || screen.IsDrop,"Native Keese fixture must enter the actual enemy picker.");
            while (screen.Selection.Id != 0x32 || screen.Selection.SubId != subId) screen.MoveObject(1);
            FailIf(!screen.Selection.Supported || screen.GetNode<TextureRect>("Preview").Texture is null || entities.Random.Calls != calls,
                "Both native Keese variants must have scoped previews without constructing another actor or consuming RNG.");
            // Category -> Y; keep the native flyer forty pixels from the safe floor.
            for (int field = 0; field < 3; field++) screen.MoveVertical(1);
            int direction = player.Position.Y <= 80 ? 1 : -1;
            for (int step = 0; step < 5; step++) screen.MoveHorizontal(direction);
            screen.MoveVertical(1);
            FailIf(screen.Selection.Id != 0x32 || screen.Selection.SubId != subId,
                "Editing native room coordinates must retain the selected Keese variant.");
            Vector2 position = screen.SpawnPosition;
            var random = entities.Random.CaptureState();
            var reference = new KeeseRom(rooms.SaveData.Game,subId,position,random);
            app.Step(4,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
            var character = entities.Entities<KeeseCharacter>().Single();
            FailIf(!character.InitializationPending || character.Visible || character.State != KeeseState.Uninitialized ||
                character.Counter1 != 0 || character.SpriteHeight != 0 || entities.Random.Calls != calls || entities.RoomEnemyCount != roomCount,
                "Paused F4 construction must allocate one uncounted Keese without executing state zero, publishing visibility or drawing RNG.");
            app.Step(1,Vector2.Zero,held:["debug_object_spawner"],pressed:["debug_object_spawner"],batched:batched);
            int visits = 0, total = subId == 0 ? 49 : 18;
            bool paused = false;
            while (visits < total)
            {
                int count = batched ? Math.Min(4,total - visits) : 1;
                if (visits < 4) count = Math.Min(count,4 - visits);
                app.Step(count,Vector2.Zero,batched:batched,afterUpdate:() =>
                {
                    visits++;
                    if (visits == 1) reference.Initialize();
                    else reference.Update(player.Position,player.EnemyContactPosition,rooms.RuntimeState.FrameCounter);
                    var state = entities.Random.CaptureState();
                    FailIf(character.InitializationPending || !character.Visible || (int)character.State != reference.State ||
                        character.Counter1 != reference.Counter1 || character.Counter2 != reference.Counter2 ||
                        character.Angle != reference.Angle || character.Speed != reference.Speed ||
                        character.SpriteHeight != reference.Z || character.TurnAmount != reference.TurnAmount ||
                        character.Position != reference.Position || state.Rng1 != reference.Rng1 || state.Rng2 != reference.Rng2 ||
                        entities.Random.Calls != calls + reference.RandomUpdates,
                        $"{rooms.SaveData.Game}: ENEMY$32:${subId:x2} visit ${visits:x2} diverged from clean-US state/position/counters/speed/Z/turn/RNG: " +
                        $"state={(int)character.State:x2}/{reference.State:x2}, count={character.Counter1:x2}/{reference.Counter1:x2}, pos={character.Position}/{reference.Position}, " +
                        $"speed={character.Speed}/{reference.Speed}, count2={character.Counter2}/{reference.Counter2}, angle={character.Angle}/{reference.Angle}, visible={character.Visible}, pending={character.InitializationPending}, z={character.SpriteHeight}/{reference.Z}, turn={character.TurnAmount}/{reference.TurnAmount}, " +
                        $"rng={state.Rng1:x2},{state.Rng2:x2}/{reference.Rng1:x2},{reference.Rng2:x2}, calls={entities.Random.Calls-calls}/{reference.RandomUpdates}.");
                });
                if (visits != 4 || paused) continue;
                paused = true;
                int retained = character.Counter1, frozenCalls = entities.Random.Calls;
                Vector2 frozen = character.Position;
                app.Step(1,Vector2.Zero,held:["debug_object_spawner"],pressed:["debug_object_spawner"],batched:batched);
                app.Step(4,Vector2.Zero,batched:batched);
                FailIf(character.Position != frozen || character.Counter1 != retained || entities.Random.Calls != frozenCalls,
                    "Native Keese must retain its state and random stream during a shared pause.");
                app.Step(1,Vector2.Zero,held:["debug_object_spawner"],pressed:["debug_object_spawner"],batched:batched);
            }
        }
        entities.Clear();
        Vector2 textPosition = player.Position + Vector2.Down * (player.Position.Y <= 80 ? 40 : -40);
        FailIf(!entities.TrySpawnDebugEnemy(0x32,1,textPosition,out string textError),textError);
        var underText = entities.Entities<KeeseCharacter>().Single();
        var textReference = new KeeseRom(rooms.SaveData.Game,1,textPosition,entities.Random.CaptureState());
        int textCalls = entities.Random.Calls;
        root._scene.Dialogue.ShowMessage("Native state zero admission.",player.Position.Y);
        app.Step(1,Vector2.Zero,batched:batched); textReference.Initialize();
        FailIf(underText.InitializationPending || !underText.Visible || (int)underText.State != textReference.State ||
            underText.TurnAmount != textReference.TurnAmount || entities.Random.Calls != textCalls + 2,
            "Native Keese state zero must draw common var3d and subid01 turning RNG under text before ordinary AI becomes frozen.");
        app.Step(4,Vector2.Zero,batched:batched);
        FailIf(underText.Counter1 != 0 || underText.Counter2 != 0 || underText.Position != textPosition ||
            entities.Random.Calls != textCalls + 2,
            "An initialized Keese must stop ordinary AI, proximity and random draws during the same text lifetime.");
        root._scene.Dialogue.Close();
        entities.Clear();
        CheckSharedKeeseShield(root,app,entities,player,batched);
        entities.Clear();
        CheckSharedKeeseBomb(root,app,entities,player,batched);
        entities.Clear();
        CheckSharedKeeseCombat(root,app,rooms,entities,player,batched);
    }

    private static Vector2 SharedKeeseCombatOrigin(Player player)
    {
        Vector2? origin = null;
        for (int y = 24; y <= 96 && origin is null; y += 8)
        for (int x = 24; x <= 104; x += 8)
        {
            bool clear = true;
            for (int step = 0; step <= 32; step++)
            {
                Vector2 point = new(x + step,y);
                clear &= !player.World.Collides(point) && player.World.GetActiveTerrain(point).Terrain.Hazard == HazardType.None;
            }
            if (clear) { origin = new(x,y); break; }
        }
        FailIf(origin is null,"Native Keese combat needs a reachable dry sword approach through real room collision.");
        return origin!.Value;
    }

    private static void CheckSharedKeeseShield(FrontendValidationRoot root, ApplicationValidationFixture app,
        RoomEntityManager entities, Player player, bool batched)
    {
        Vector2 origin = SharedKeeseCombatOrigin(player);
        player.WarpTo(origin); player.Face(Vector2I.Right);
        player.Inventory.GiveTreasure(TreasureId.Shield,1); player.Inventory.EquipA(TreasureId.Shield);
        int health = player.Inventory.HealthQuarters;
        FailIf(!entities.TrySpawnDebugEnemy(0x32,0,origin + Vector2.Right * 32,out string error),error);
        var actor = entities.Entities<KeeseCharacter>().Single();
        app.Step(1,Vector2.Zero,batched:batched);
        var source = new EnemyHandlerRegistry(entities.AssetRoot).Handlers.Single(row => row.Id == 0x32 && row.SubId == 0)
            .CombatSource(2,0,"cross-pack combat binding probe");
        string foreign = entities.AssetRoot == AgesGameContent.AssetRoot ? SeasonsGameContent.AssetRoot : AgesGameContent.AssetRoot;
        bool rejected = false;
        try
        {
            _ = EnemyCombatDescriptor.WithContactDamage(source with { AssetRoot = foreign },actor,2,
                (_,damage) => actor.TakeSwordHit(damage),actor.TakeSwordHit,actor.ApplySwordKnockback,static _ => { });
        }
        catch (InvalidOperationException mismatch) { rejected = mismatch.Message.Contains(foreign) && mismatch.Message.Contains(entities.AssetRoot); }
        FailIf(!rejected, "An actor must reject combat metadata from the other source pack before attaching hit callbacks.");
        int visits = 0;
        while (actor.InvincibilityCounter == 0 && visits++ < 28)
            app.Step(1,Vector2.Right,held:["attack"],pressed:visits == 1 ? ["attack"] : [],batched:batched);
        FailIf(actor.InvincibilityCounter != -0x15 || actor.KnockbackCounter != 0x0b || !actor.NativeHitPending ||
            player.InvincibilityFrames != -0x0f || player.KnockbackFrames != 0x13 ||
            player.Inventory.HealthQuarters != health || player.World.Collides(player.PrecisePosition),
            $"{root._gameplaySession!.Rooms.SaveData.Game}: reachable wooden-shield/Keese contact must select native effect $10 and publish JUST_HIT without health damage.");
        int rest = actor.Counter1;
        app.Step(1,Vector2.Zero,held:["attack"],batched:batched);
        FailIf(actor.NativeHitPending || actor.InvincibilityCounter != -0x14 || actor.KnockbackCounter != 0x0b || actor.Counter1 != rest,
            "Keese's next JUST_HIT visit must preserve rest/recoil counters and consume the signal once.");
        app.Step(1,Vector2.Zero,held:["attack"],batched:batched);
        FailIf(actor.KnockbackCounter != 0x0a || actor.InvincibilityCounter != -0x13,
            "Native shield recoil must begin on the following eligible visit.");
        app.Step(1,Vector2.Zero,batched:batched);
    }

    private static void CheckSharedKeeseBomb(FrontendValidationRoot root, ApplicationValidationFixture app,
        RoomEntityManager entities, Player player, bool batched)
    {
        Vector2 origin = SharedKeeseCombatOrigin(player);
        player.WarpTo(origin);
        Vector2 position = origin + Vector2.Right * 32;
        FailIf(!entities.TrySpawnDebugEnemy(0x32,0,position,out string error),error);
        var actor = entities.Entities<KeeseCharacter>().Single();
        app.Step(1,Vector2.Zero,batched:batched);
        actor.Health = 9;
        var bomb = new BombCollisionValidationEntity(position);
        entities.AddEntity(bomb);
        app.Step(1,Vector2.Zero,batched:batched);
        FailIf(actor.Health != 9 || actor.NativeHitPending, "A disabled explosion must not dispatch target collisions.");
        app.Step(1,Vector2.Zero,held:["debug_object_spawner"],pressed:["debug_object_spawner"],batched:batched);
        bomb.CollisionEnabled = true;
        app.Step(2,Vector2.Zero,batched:batched);
        FailIf(actor.Health != 9 || actor.NativeHitPending, "An enabled explosion must not dispatch while the shared debug menu owns pause.");
        app.Step(1,Vector2.Zero,held:["debug_object_spawner"],pressed:["debug_object_spawner"],batched:batched);
        app.Step(1,Vector2.Zero,batched:batched);
        // Both native Keese Bomb columns select effect $0a / ENEMYDMG_08: $1a invincibility.
        FailIf(actor.Health != 7 || !actor.NativeHitPending || actor.InvincibilityCounter != 0x1a,
            $"{root._gameplaySession!.Rooms.SaveData.Game}: native Bomb row must damage Keese once and publish deferred hit/invincibility in the actual post-object scan; got health={actor.Health}, pending={actor.NativeHitPending}, invincibility=${actor.InvincibilityCounter:x2}, position={actor.Position}, explosion={position}.");
        app.Step(1,Vector2.Zero,batched:batched);
        FailIf(actor.Health != 7 || actor.NativeHitPending || actor.InvincibilityCounter != 0x19,
            "The next enemy visit must consume the hit once and prevent repeated explosion damage during invincibility.");
        bomb.CollisionEnabled = false;
        app.Step(2,Vector2.Zero,batched:batched);
        FailIf(actor.Health != 7 || actor.InvincibilityCounter != 0x17,
            "Cancelling the explosion must preserve the enemy's independent invincibility lifetime.");
    }

    private static void CheckSharedKeeseCombat(FrontendValidationRoot root, ApplicationValidationFixture app,
        RoomSession rooms, RoomEntityManager entities, Player player, bool batched)
    {
        Vector2 origin = SharedKeeseCombatOrigin(player);
        var inventory = player.Inventory;
        inventory.GiveTreasure(TreasureId.Sword,1); inventory.EquipA(TreasureId.Sword);
        foreach (int subId in new[] { 0,1 })
        {
            entities.Clear(); player.WarpTo(origin); player.Face(Vector2I.Right);
            int roomCount = entities.RoomEnemyCount;
            int health = inventory.HealthQuarters;
            int mapleKills = rooms.SaveData.MapleKillCounter;
            int lifetimeKills = inventory.TotalEnemiesKilled;
            bool slayerAwarded = rooms.SaveData.HasGlobalFlag(0x00);
            FailIf(!entities.TrySpawnDebugEnemy(0x32,subId,origin + Vector2.Right * 32,out string error),error);
            var character = entities.Entities<KeeseCharacter>().Single();
            app.Step(1,Vector2.Zero,batched:batched);
            app.Step(3,Vector2.Right,batched:batched);
            FailIf(player.PrecisePosition.X <= origin.X || player.World.Collides(player.PrecisePosition),
                "Sword combat must approach the native Keese on its actual walkable floor.");
            app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
            for (int update = 0; character.Health > 0 && update < 10; update++) app.Step(1,Vector2.Zero,batched:batched);
            FailIf(character.Health != 0 || !character.PendingKnockbackDeath || character.CollisionEnabled || inventory.HealthQuarters != health,
                $"{rooms.SaveData.Game}: the actual Sword parent/hit scan must damage native Keese mode ${((rooms.SaveData.Game == OracleGame.Seasons) ? 0x29 : 0x1f):x2} without a same-hit contact reaction.");
            Vector2 terminalPosition = character.Position;
            EnemyDeathPuffEffect? puff = null;
            for (int update = 0; puff is null && update < 16; update++)
            {
                terminalPosition = character.Position;
                app.Step(1,Vector2.Zero,batched:batched);
                puff = entities.Entities<EnemyDeathPuffEffect>().SingleOrDefault();
            }
            FailIf(puff is null || puff.EnemyId != 0x32 || puff.ZHigh != -subId || puff.Position != terminalPosition.Floor() ||
                puff.DurationFrames != 20 || puff.ElapsedFrames != 1 || entities.RoomEnemyCount != roomCount,
                "Native Keese retirement must use the shared PART$02 factory, copy world XY/Z separately, preserve the uncounted flag and visit its later PART slot on birth.");
            FailIf(rooms.SaveData.MapleKillCounter != Math.Min(0xff, mapleKills + 1) ||
                inventory.TotalEnemiesKilled != (slayerAwarded ? lifetimeKills : Math.Min(0xffff, lifetimeKills + 1)),
                $"{rooms.SaveData.Game}: the actual enemy-death outcome must reach the shared session's Maple/lifetime counters exactly once, including uncounted room enemies.");
            Vector2 puffPosition = puff!.Position;
            var random = entities.Random.CaptureState();
            // Both ROMs: enemy row $32=$ae; probability5 accepts roll$2b;
            // set$0e index$00 is HEART. Native RNG $04/$0d -> $2b/$27 -> $a0/$75.
            entities.Random.RestoreState(random with { Rng1 = 4,Rng2 = 0x0d });
            int calls = entities.Random.Calls;
            app.Step(18,Vector2.Zero,batched:batched);
            FailIf(puff!.ElapsedFrames != 19 || entities.Random.Calls != calls || entities.Entities<ItemDropEffect>().Count != 0,
                "PART$02 must retain its last displayed frame through update $13 without selecting a reward early.");
            app.Step(1,Vector2.Zero,batched:batched);
            var drop = entities.Entities<ItemDropEffect>().Single();
            var after = entities.Random.CaptureState();
            FailIf(drop.SubId != 1 || drop.ElapsedFrames != 0 || drop.ZFixed != (-subId << 8) ||
                drop.Position != puffPosition || after.Rng1 != 0xa0 || after.Rng2 != 0x75 || entities.Random.Calls != calls + 2 ||
                entities.RoomEnemyCount != roomCount,
                "The terminal shared puff must select native HEART with two draws and replace its own PART slot, retaining high XY/Z without updating the new drop early.");
        }
        entities.Clear();
    }
}
