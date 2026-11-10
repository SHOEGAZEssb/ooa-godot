
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedObjectSpawner(FrontendValidationRoot root, RoomSession rooms,
        RoomEntityManager entities, Player player, bool batched)
    {
        var app = new ApplicationValidationFixture(root);
        GameplayMenus menus = root._gameplayMenus!;
        DebugObjectSpawnerScreen screen = root._scene.DebugObjectSpawnerScreen;
        var catalog = new DebugObjectCatalog(rooms.AssetRoot);
        bool seasons = rooms.SaveData.Game == OracleGame.Seasons;
        if (!seasons)
        {
            var contextual = new EnemyHandlerRegistry(rooms.AssetRoot).Handlers.Where(handler =>
                handler.Handler is EnemyHandlerKind.TargetCartCrystal or EnemyHandlerKind.GreatFairy).ToArray();
            FailIf(contextual.Length == 0 || contextual.Any(handler => !handler.SupportsStandaloneConstruction ||
                catalog.Enemies.Single(entry => entry.Id == handler.Id && entry.SubId == handler.SubId).Supported),
                "Room-owned native enemy producers must retain construction without exposing contextual actors in F4.");
        }
        if (seasons)
        {
            var native = new EnemyHandlerRegistry(rooms.AssetRoot).Handlers.ToArray();
            FailIf(native.Length != 154 || native.Count(entry => entry.Classification == EnemyHandlerClassification.Unimplemented) != 110 ||
                native.Where(entry => entry.Id is not (0x08 or 0x09 or 0x0a or 0x0c or 0x0e or 0x10 or 0x17 or 0x20 or 0x22 or 0x2d or 0x31 or 0x32 or 0x34 or 0x35 or 0x43 or 0x51 or 0x70 or 0x78)).Any(entry => entry.SubId != 0 || entry.SupportsCombatSource) ||
                native.Where(entry=>entry.Id==0x78).Any(entry=>entry.SubId is <0 or >3||entry.Handler!=EnemyHandlerKind.Aquamentus||entry.CollisionMode!=0xc8||entry.SupportsStandaloneConstruction) ||
                native.Where(entry=>entry.Id==0x35).Any(entry=>entry.SubId is <0 or >2||entry.Handler!=EnemyHandlerKind.Floormaster||entry.CollisionMode!=0x24||!entry.SupportsStandaloneConstruction) ||
                native.Where(entry=>entry.Id==0x70).Any(entry=>entry.SubId is not (0 or 1)||entry.Handler!=EnemyHandlerKind.BrotherGoriyas||entry.CollisionMode!=0x41||!entry.SupportsStandaloneConstruction) ||
                native.Single(entry=>entry.Id==0x0a) is not {Handler:EnemyHandlerKind.BoomerangMoblin,SubId:0,CollisionMode:0x91,SupportsStandaloneConstruction:true} ||
                native.Where(entry=>entry.Id==0x0e).Any(entry=>entry.SubId is not (0 or 1 or 2 or 5) || entry.Handler!=EnemyHandlerKind.BladeTrap || entry.CollisionMode!=0x93 || !entry.SupportsStandaloneConstruction) ||
                native.Single(entry => entry.Id == 0x31) is not {Handler:EnemyHandlerKind.Stalfos,SubId:0,CollisionMode:0xa8,SupportsStandaloneConstruction:true} ||
                native.Single(entry => entry.Id == 0x08) is not {Handler:EnemyHandlerKind.RiverZora,CollisionMode:0x0f,SupportsStandaloneConstruction:true} ||
                native.Single(entry => entry.Id == 0x43) is not {Handler:EnemyHandlerKind.Gel,CollisionMode:0xb1,SupportsStandaloneConstruction:true} ||
                native.Where(entry => entry.Id == 0x34).Any(entry=>entry.SubId is <0 or >1 || entry.Handler!=EnemyHandlerKind.Zol ||
                    entry.CollisionMode!=0x2a || !entry.SupportsStandaloneConstruction) ||
                native.Where(entry => entry.Id is 0x0c or 0x20 or 0x22).Any(entry => entry.SubId is < 0 or > 1 ||
                    entry.Handler != EnemyHandlerKind.ArrowMoblin || !entry.SupportsStandaloneConstruction) ||
                native.Where(entry => entry.Id == 0x09).Any(entry => entry.SubId is < 0 or > 3 ||
                    entry.CollisionMode != 0x90 || !entry.SupportsStandaloneConstruction || entry.Handler != EnemyHandlerKind.Octorok) ||
                native.Where(entry => entry.Id == 0x32).Any(entry => entry.SubId is < 0 or > 1 ||
                    entry.CollisionMode != 0xa9 || !entry.SupportsCombatSource || entry.Handler != EnemyHandlerKind.Keese) ||
                native.Where(entry => entry.Id == 0x17).Any(entry => entry.SubId is < 0 or > 2 ||
                    entry.CollisionMode != 0x99 || !entry.SupportsStandaloneConstruction || entry.Handler != EnemyHandlerKind.Ghini) ||
                native.Single(entry => entry.Id == 2) is not { EnemyName: "ENEMY_GENERAL_ONOX", CollisionMode: 0x89 } ||
                native.Single(entry => entry.Id == 0x16) is not { Handler: EnemyHandlerKind.Beamos, CollisionMode: 0x00, SupportsStandaloneConstruction: true } ||
                !native.Single(entry => entry.Id == 0x54).EnemyName.Equals("ENEMY_BLAINO",StringComparison.Ordinal) ||
                native.Where(entry => entry.Id == 0x10).Any(entry => entry.SubId is < 0 or > 3 || entry.CollisionMode != 0x10 || entry.Handler != EnemyHandlerKind.Rope) ||
                native.Where(entry => entry.Id == 0x51 && entry.SubId != 0).Any(entry => entry.SubId is not (2 or 3) || entry.CollisionMode != 0x39 || entry.Handler != EnemyHandlerKind.Beetle),
                "Seasons debug identities must retain native handlers, collision modes and implemented common variants.");
        }
        int[] expectedDrops = seasons ? [0,1,2,3,4,5,6,7,8,9,0x0c,0x0d,0x0e,0x0f] : [0,1,2,3,4,5,6,7,8,9,0x0f];
        FailIf(!catalog.Drops.Select(entry => entry.SubId).SequenceEqual(expectedDrops),
            "Debug drop availability must preserve native unused masks and Seasons' ore rows.");
        entities.Clear();
        int rng = entities.Random.Calls;
        app.Step(1,Vector2.Zero,held:["debug_object_spawner"],pressed:["debug_object_spawner"],batched:batched);
        FailIf(!menus.Spawner.IsActive || !screen.Visible || !menus.Pause.IsOwnedBy(menus.Spawner) ||
            !ReferenceEquals(root._debugObjectSpawner,menus.Spawner),
            "Both real file entries must dispatch F4 through the shared spawner and pause owner.");
        Vector2 position = player.PrecisePosition;
        app.Step(4,Vector2.Right,batched:batched);
        FailIf(player.PrecisePosition != position || entities.Random.Calls != rng,
            "Browsing F4 must freeze world motion and preserve gameplay RNG in a host batch.");
        if (seasons)
        {
            while (screen.Selection.Id != 2) screen.MoveObject(1);
            app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
            string error = screen.GetNode<Label>("Status").Text;
            FailIf(!menus.Spawner.IsActive || !error.Contains("$02:$00",StringComparison.Ordinal) ||
                !error.Contains("ENEMY_GENERAL_ONOX",StringComparison.Ordinal) ||
                !error.Contains("data/enemyCodeTable.s",StringComparison.Ordinal) || entities.Random.Calls != rng ||
                entities.Entities<Node2D>().Count != 0,
                "Unimported Seasons enemies must report native source identity without allocating, consuming RNG or selecting Ages content.");
        }
        // Type -> drops, then Object -> HEART. Exercise the real input owner.
        app.Step(1,Vector2.Zero,held:["move_right"],pressed:["move_right"],batched:batched);
        app.Step(1,Vector2.Zero,held:["move_down"],pressed:["move_down"],batched:batched);
        app.Step(1,Vector2.Zero,held:["move_right"],pressed:["move_right"],batched:batched);
        FailIf(!screen.IsDrop || screen.Selection.SubId != 1 || screen.GetNode<TextureRect>("Preview").Texture is null,
            "The common F4 drop picker must use native PART$01 visuals for both packs.");
        app.Step(4,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
        var heart = entities.Entities<ItemDropEffect>().Single();
        FailIf(heart.SubId != 1 || heart.ElapsedFrames != 0 || entities.Random.Calls != rng,
            "A batched F4 spawn edge must create one deferred native PART$01 without initialization or RNG.");
        for (int slot = 1; slot < 16; slot++)
            app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
        app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
        FailIf(entities.Entities<ItemDropEffect>().Count != 16 || entities.Random.Calls != rng ||
            !screen.GetNode<Label>("Status").Text.Contains("$10 PART slots",StringComparison.Ordinal),
            "F4 must reject full native PART pools without spilling allocations or RNG.");
        entities.Clear();
        app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
        heart = entities.Entities<ItemDropEffect>().Single();
        app.Step(1,Vector2.Zero,held:["attack","item"],pressed:["attack","item"],batched:batched);
        FailIf(menus.Spawner.IsActive || menus.Pause.IsLeased || heart.ElapsedFrames != 0 ||
            entities.Entities<ItemDropEffect>().Count != 1,
            "Closing F4 must consume simultaneous A/B without another spawn or object update.");
        app.Step(1,Vector2.Zero,batched:batched);
        FailIf(heart.ElapsedFrames != 1,"Native PART initialization must resume on the first eligible update after F4.");
        entities.Clear();
        for (int repeat = 0; repeat < 2; repeat++)
        {
            app.Step(1,Vector2.Zero,held:["debug_object_spawner"],pressed:["debug_object_spawner"],batched:batched);
            FailIf(!menus.Spawner.IsActive,"The shared F4 owner must reopen after spawning and closing.");
            app.Step(1,Vector2.Zero,held:["debug_object_spawner"],pressed:["debug_object_spawner"],batched:batched);
            FailIf(menus.Spawner.IsActive || menus.Pause.IsLeased,"The shared F4 owner must close repeatedly without leaking its lease.");
        }
    }
}
