using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private Action? _graphicsCacheExitAudit;

    public override void _ExitTree()
    {
        base._ExitTree();
        Action? audit = _graphicsCacheExitAudit;
        _graphicsCacheExitAudit = null;
        audit?.Invoke();
    }

    private void ValidateGraphicsCacheLifetime()
    {
        (Type Owner, string Field)[] featureCaches =
        [
            (typeof(SaveQuitScreen), "SharedBackgrounds"), (typeof(ClinkEffect), "Textures"),
            (typeof(TerrainShadow), "Definitions"), (typeof(LinkTerrainEffectDatabase), "Packs"),
            (typeof(LinkSquishDatabase), "Packs"), (typeof(GrassDebrisEffect), "Definitions"),
            (typeof(RockDebrisEffect), "Definitions"), (typeof(FallingDownHoleEffect), "Definitions"),
            (typeof(StationaryDebrisEffect), "Definitions"), (typeof(SideScrollBubbleRoomEntity), "Definitions"),
            (typeof(KillEnemyPuffEffect), "Definitions"), (typeof(BossShadowEffect), "Definitions"),
            (typeof(BossDeathExplosionEffect), "Definitions"), (typeof(EnemyDeathPuffEffect), "Definitions")
        ];
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons }) WarmFeatureGraphics(game);
        var caches = new List<(string Name, object Cache, int Count)>();
        foreach (var entry in featureCaches)
        {
            object cache = entry.Owner.GetField(entry.Field, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            int count = CacheCount(cache);
            FailIf(count == 0, $"{entry.Owner.Name}: the graphics-lifetime fixture did not warm its production cache.");
            caches.Add(($"{entry.Owner.Name}.{entry.Field}", cache, count));
        }
        const string path = AgesGameContent.AssetRoot + "gfx/gfx_font.png";
        Image source = OracleGraphicsCache.LoadImage(path);
        Texture2D font = OracleGraphicsCache.LoadMonochromeFont(path);
        OracleImagePixels pixels = OracleGraphicsCache.GetImagePixels(source);
        using Image original = OracleGraphicsCache.LoadRawPngForValidation(path);
        ulong expected = OracleGraphicsCache.PixelHash(original);
        for (int repeat = 0; repeat < 8; repeat++)
        {
            var nested = new FrontendValidationRoot { _selectedGame = (repeat & 1) == 0 ? OracleGame.Ages : OracleGame.Seasons };
            AddChild(nested);
            nested.Free(); // The surrounding application still owns the shared cache.
            CollectReleasedSessions();
            Image retained = OracleGraphicsCache.LoadImage(path);
            Texture2D retainedFont = OracleGraphicsCache.LoadMonochromeFont(path);
            FailIf(!ReferenceEquals(source, retained) || !ReferenceEquals(font, retainedFont) ||
                !ReferenceEquals(pixels, OracleGraphicsCache.GetImagePixels(retained)),
                "Exiting a temporary application root invalidated another live root's shared graphics cache.");
            FailIf(!GodotObject.IsInstanceValid(source) || !GodotObject.IsInstanceValid(font) ||
                OracleGraphicsCache.PixelHash(retained) != expected,
                "Shared source graphics changed or were released while an application root remained active.");
            foreach (var entry in caches)
                FailIf(CacheCount(entry.Cache) != entry.Count,
                    $"{entry.Name}: a nested root exit cleared another application's feature graphics.");
        }
        // Observe the real final tree exit, without forging owner counters or
        // disposing the running validation session to simulate shutdown.
        _graphicsCacheExitAudit = () =>
        {
            foreach (var entry in caches)
                FailIf(CacheCount(entry.Cache) != 0, $"{entry.Name}: last application exit retained graphics.");
            foreach (string field in new[] { "SourceImages", "MonochromeFonts", "OamCells", "OamFrames", "CompositeImages" })
            {
                object cache = typeof(OracleGraphicsCache).GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                FailIf(CacheCount(cache) != 0, $"OracleGraphicsCache.{field}: last application exit retained graphics.");
            }
            FailIf(!GodotObject.IsInstanceValid(source) || !GodotObject.IsInstanceValid(font),
                "Cache release must drop its retained references without disposing a reader's still-held resources.");
            GD.Print("VALIDATION_GRAPHICS_SHUTDOWN cleared=14 feature caches and core graphics; retained readers remain valid.");
        };
        GD.Print("Validated both packs' feature graphics and font retention across eight root exits; final cache release is checked at actual tree shutdown.");
    }

    private static int CacheCount(object cache) => (int)cache.GetType().GetProperty("Count")!.GetValue(cache)!;

    private void WarmFeatureGraphics(OracleGame game)
    {
        string pack = OracleAssetPack.ForGame(game);
        var nodes = new List<Node>();
        T Own<T>(T node) where T : Node { nodes.Add(node); return node; }
        try
        {
            var menu = Own(new SaveQuitScreen { AssetRoot = pack });
            foreach (bool _ in menu.PrepareResources()) { }
            _ = TerrainShadow.Load(pack);
            _ = LinkTerrainEffectDatabase.For(pack);
            Own(new ClinkEffect()).Initialize(pack, Vector2.Zero, false);
            Own(new GrassDebrisEffect()).Initialize(pack, Vector2.Zero);
            Own(new RockDebrisEffect()).Initialize(pack, Vector2.Zero);
            Own(new FallingDownHoleEffect()).Initialize(pack, Vector2.Zero);
            Own(new StationaryDebrisEffect()).Initialize(pack, Vector2.Zero, 0);
            Own(new EnemyDeathPuffEffect()).Initialize(pack, Vector2.Zero);
            var room = new OracleRoomData(0, 0, 0, 0, 0, 0, new byte[80], new byte[256],
                OracleGraphicsCache.LoadImage(pack + "gfx/spr_link.png"),
                OracleGraphicsCache.LoadImage(pack + "gfx/gfx_hud.png"), new byte[2048], new Color[6,4],
                new BackgroundPaletteState(new Color[4], new Color[3,4]), new OracleAnimationData(pack)) { AssetRoot = pack };
            Own(new SideScrollBubbleRoomEntity(Vector2.Zero, room, new OracleRandom(), new OracleRuntimeState(game)));
            // These four visual catalogs are currently imported only for Ages.
            if (game == OracleGame.Ages)
            {
                _ = LinkSquishDatabase.For(pack);
                Own(new KillEnemyPuffEffect()).Initialize(pack, Vector2.Zero, static () => { });
                Own(new BossShadowEffect()).Initialize(pack, static () => Vector2.Zero, static () => 0, static () => true, 0, 0);
                Own(new BossDeathExplosionEffect()).Initialize(pack, Vector2.Zero, 0, static _ => { });
            }
        }
        finally { foreach (Node node in nodes) node.Free(); }
    }
}
