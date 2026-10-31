using Godot;
using System;
using System.Threading.Tasks;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private async Task ValidateSharedGameplayScene()
    {
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            bool seasons = game == OracleGame.Seasons;
            string path = seasons ? "res://scenes/seasons/opening.tscn" : AgesGameplaySceneResource.ScenePath;
            var scene = GD.Load<PackedScene>(path).Instantiate<GameplaySceneGraph>();
            try
            {
                foreach (bool _ in scene.PreparePresentation()) { }
                string pack = OracleAssetPack.ForGame(game);
                FailIf(scene.Game != game || scene.AssetRoot != pack || scene.Hud.AssetRoot != pack ||
                    scene.Dialogue.AssetRoot != pack || scene.MapScreen.AssetRoot != pack ||
                    scene.InventoryScreen.AssetRoot != pack || scene.RingMenuScreen.AssetRoot != pack ||
                    scene.SaveQuitScreen.AssetRoot != pack,
                    $"{game}: every shared gameplay presenter must bind the selected pack before entering the tree.");
                bool rejected = false;
                scene.Game = seasons ? OracleGame.Ages : OracleGame.Seasons;
                try { foreach (bool _ in scene.PreparePresentation()) { } }
                catch (InvalidOperationException) { rejected = true; }
                scene.Game = game;
                FailIf(!rejected || scene.Hud.AssetRoot != pack,
                    "Rebinding a prepared hierarchy must fail before changing any presenter's pack.");
                scene.Player.ApplicationUpdateOwned = true;
                scene.Dialogue.ApplicationUpdateOwned = true;
                AddChild(scene);
                // Camera2D selection is cached within a rendered frame.
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                FailIf(scene.WorldRoot.GetParent() != scene || scene.InterfaceLayer.GetParent() != scene ||
                    scene.Player.GetParent() != scene.WorldRoot || scene.RoomView.GetParent() != scene.WorldRoot ||
                    scene.RoomCamera.GetParent() != scene.WorldRoot || scene.MapScreen.GetParent() != scene.InterfaceLayer ||
                    scene.InventoryScreen.GetParent() != scene.InterfaceLayer || scene.DebugFlagScreen.GetParent() != scene.InterfaceLayer,
                    $"{game}: shared world, modal and development nodes must have the same ownership hierarchy.");
                FailIf((scene.InterfaceLayer.GetNodeOrNull("EndingScreen") is not null) == seasons ||
                    (scene.GetNodeOrNull("OpeningBackground/Cutscene") is not null) != seasons,
                    "Game-specific story presenters must live only in their native scene additions.");
                scene.Player.Position = new(32, 48);
                scene.SetCameraOrigin(new(3, 5));
                FailIf(scene.Player.Position != new Vector2(32, 48) ||
                    !scene.Player.GetGlobalTransformWithCanvas().Origin.IsEqualApprox(new(29, 59)),
                    $"{game}: shared camera projection must add the $10 HUD origin without changing world coordinates; " +
                    $"world={scene.Player.Position}, canvas={scene.Player.GetGlobalTransformWithCanvas().Origin}, " +
                    $"camera={scene.RoomCamera.Position}, current={scene.RoomCamera.IsCurrent()}, enabled={scene.RoomCamera.Enabled}, " +
                    $"tree={scene.RoomCamera.IsInsideTree()}/{scene.IsInsideTree()}, viewport={GetViewport().GetVisibleRect().Size}.");
                int top = scene.ApplyHudPlacement(true, false);
                scene.SetCameraOrigin(new(3, 5), top);
                FailIf(top != 0 || scene.Hud.Position != new Vector2(0, 128) ||
                    !scene.Player.GetGlobalTransformWithCanvas().Origin.IsEqualApprox(new(29, 43)),
                    $"{game}: bottom HUD placement must change only screen projection and interface positions.");
            }
            finally { scene.Free(); }
        }
    }
}
