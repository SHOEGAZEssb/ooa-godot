using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal partial class SeasonsOpeningSession
{
    internal bool ResourcesPrepared { get; private set; }
    internal IEnumerable<bool> PrepareResources()
    {
        if (ResourcesPrepared) yield break;
        Data = new SeasonsOpeningData();
        yield return false;
        foreach (bool step in PreparePresentation()) yield return step;
        _overlay = GetNode<ColorRect>("Interface/OpeningFade");
        SetCameraOrigin(Vector2.Zero);
        _screen = GetNode<SeasonsOpeningScreen>("OpeningBackground/Cutscene"); _screen.Initialize(Data); _screen.Hide();
        Hud.AssetRoot = SeasonsGameContent.AssetRoot; Hud.PrepareResources();
        Dialogue.PrepareResources(SeasonsGameContent.AssetRoot);
        yield return false;
        foreach (int room in new[] { 0xa7,0x97,0x98,0xb6,0xb8,0xc6,0xc8 })
        foreach (int season in Enumerable.Range(0,4))
        {
            _ = Data.World.LoadRoom(room, season).Texture;
            yield return false;
        }
        foreach (var entry in Data.ActorVariants)
        {
            if (entry.Id == 0x4f)
                _screen.Load("GFXH_SCENE_INSIDE_ONOX_CASTLE", "PALH_SEASONS_97", 0x0d);
            else Data.World.LoadRoom(0x98, 0);
            var visual = Data.CreateActor(entry.Id, entry.Subid, Vector2.Zero);
            try
            {
                foreach (string animation in Data.ActorAnimations(entry.Id, entry.Subid))
                {
                    visual.SetScriptAnimation(animation);
                    yield return false;
                }
            }
            finally { visual.Free(); }
        }
        foreach (var scene in new[] {
            ("GFXH_SCENE_OUTSIDE_ONOX_CASTLE","PALH_TILESET_ONOX_CASTLE_OUTSIDE_WINTER",9),
            ("GFXH_SCENE_INSIDE_ONOX_CASTLE","PALH_SEASONS_97",0x0d),
            ("GFXH_TEMPLEFALL_SCENE1","PALH_SEASONS_98",0x0e) })
        {
            _screen.Load(scene.Item1, scene.Item2, scene.Item3);
            yield return false;
        }
        _screen.Hide();
        foreach (bool step in Link.PrepareCutscenePresentation(Data.OpeningLinkFrames(), SeasonsGameContent.AssetRoot)) yield return step;
        _pregameScreen = GetNode<NewGameIntroScreen>("Interface/Pregame");
        _pregameScreen.DeferPreparation = true;
        foreach (bool step in _pregameScreen.PrepareResources()) yield return step;
        ResourcesPrepared = true;
    }
}
