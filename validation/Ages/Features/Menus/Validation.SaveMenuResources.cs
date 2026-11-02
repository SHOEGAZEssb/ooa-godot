using System.Reflection;
using System.Threading.Tasks;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private async Task ValidateSaveMenuBackgroundIsolation()
    {
        // Pixel hashes captured before sharing resources: ordinary save,
        // game over, then all eight port-options flag combinations. These
        // guard the optimization's output; source strokes are checked by
        // ValidateSaveOptions independently of these cached textures.
        ulong[] expected =
        [
            0x5bfa0475a5a60480UL, 0xebe837cda9add896UL,
            0x397cd55088151dadUL, 0xff4d7cca2e4c9048UL,
            0x912a3bc97944feb1UL, 0x22b5ed1fd6e0d984UL,
            0x393e9f6cd4438c68UL, 0x08669956644eff11UL,
            0xd996185279c4571cUL, 0x38412527a7a8c0ddUL
        ];
        var first = new SaveQuitScreen { AssetRoot = AgesGameContent.AssetRoot };
        var second = new SaveQuitScreen { AssetRoot = AgesGameContent.AssetRoot };
        var seasons = new SaveQuitScreen { AssetRoot = SeasonsGameContent.AssetRoot };
        var seasonsSecond = new SaveQuitScreen { AssetRoot = SeasonsGameContent.AssetRoot };
        AddChild(first);
        AddChild(second);
        AddChild(seasons);
        AddChild(seasonsSecond);
        try
        {
            // code/bank2.s:saveQuitMenu_drawSprites@acornSprite, shared by both US builds.
            foreach (string pack in new[] { AgesGameContent.AssetRoot, SeasonsGameContent.AssetRoot })
            {
                MenuOamPart acorn = FileMenuPresentation.LoadOam(pack)["save-quit-acorn"][0];
                FailIf(acorn.Y != 0x48 || acorn.X != 0x29 || acorn.Tile != 0x28 || acorn.Attributes != 0x04,
                    $"{pack}: native save-menu OAM must preserve $48,$29,$28,$04.");
            }
            for (int variant = 0; variant < 10; variant++)
            {
                Select(first, variant);
                Select(second, variant);
                Select(seasons, variant);
                Select(seasonsSecond, variant);
                Texture2D firstTexture = Background(first);
                Texture2D secondTexture = Background(second);
                Texture2D seasonsTexture = Background(seasons);
                using Image pixels = (Image)firstTexture.GetImage().Duplicate();
                ulong hash = OracleGraphicsCache.PixelHash(pixels);
                FailIf(firstTexture.GetInstanceId() != secondTexture.GetInstanceId(),
                    $"Save menu variant {variant} rebuilt an immutable background.");
                FailIf(seasonsTexture.GetInstanceId() != Background(seasonsSecond).GetInstanceId() ||
                    seasonsTexture.GetInstanceId() == firstTexture.GetInstanceId(),
                    $"Save menu variant {variant} must share immutable resources within its pack, without retaining another game's resource set.");
                FailIf(hash != expected[variant] ||
                    first.BackgroundPixelHash != second.BackgroundPixelHash ||
                    !first.BackgroundIsOpaque || !second.BackgroundIsOpaque,
                    $"Save menu variant {variant} changed pixels between instances.");
                // Exercise reader disposal before inspecting the shared texture again.
                FailIf(second.BackgroundPixelHash != hash,
                    $"Save menu variant {variant} lost its pixels after an opacity read.");
                // The native save maps, graphics and palettes are common source;
                // the HUD sheets differ but none of their differing cells is used here.
                FailIf(seasons.BackgroundPixelHash != expected[variant] ||
                    seasonsSecond.BackgroundPixelHash != expected[variant] || !seasons.BackgroundIsOpaque,
                    $"Seasons save menu variant {variant} did not preserve the native common panel and port options.");
                if (DisplayServer.GetName() != "headless")
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                }
            }
            first.Open();
            second.Open();
            first.Move(1);
            first.ShowSaveError();
            first.DelayCounter = 12;
            first.OpenOptions();
            first.Move(1);
            FailIf(second.Cursor != 0 || second.OptionsOpen || second.OptionsCursor != 0 ||
                second.SaveErrorVisible || second.DelayCounter != 0 || seasons.SaveErrorVisible ||
                seasons.OptionsCursor != 0 || seasons.DelayCounter != 0,
                "Save menu instances shared mutable menu state.");
            first.Free();
            second.Open(gameOver: true);
            FailIf(!second.BackgroundIsOpaque,
                "Freeing one save menu invalidated another menu's background.");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(first)) first.Free();
            second.Free();
            seasons.Free();
            seasonsSecond.Free();
        }

        static void Select(SaveQuitScreen screen, int variant)
        {
            screen.Open(gameOver: variant == 1);
            if (variant < 2) return;
            int flags = variant - 2;
            screen.OpenOptions();
            screen.RefreshOptions((flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0);
        }

        static Texture2D Background(SaveQuitScreen screen) =>
            (Texture2D)typeof(SaveQuitScreen).GetField("_background",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(screen)!;
    }
}
