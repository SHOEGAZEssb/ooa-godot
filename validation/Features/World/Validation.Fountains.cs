using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFountainDecorations()
    {
        var data = new FountainDatabase();
        // mainData.s: four pairs; past $12/$14 share the volcano tail, not
        // group1Map13's preceding fountain rows.
        FailIf(data.InRoom(1, 0x12).Any() || data.InRoom(1, 0x14).Any(),
            "$80 fountain placement must not leak backward through room aliases.");
        foreach (int subid in new[] { 9, 10 })
        {
            var visual = data.Visual(subid);
            var frames = OracleGraphicsCache.GetAnimationDefinition(visual.Animation).Frames;
            FailIf(visual.Sprite != "spr_fountain" || visual.TileBase != (subid == 9 ? 0 : 16) ||
                visual.Palette != 6 || frames.Length != (subid == 9 ? 4 : 2) ||
                frames.Any(frame => frame.Duration != (subid == 9 ? 12 : 6)),
                "$80:$09/$0a must retain source graphics, palette, and 12/6-update animation periods.");
        }
        FailIf(data.UsesRuinedPalette(1, 0x20, _saveData),
            "decoration.s must use PALH_7d outside its $12/$13/$14 room-key table.");

        foreach (bool batched in new[] { false, true })
        foreach (bool restored in new[] { false, true })
        foreach ((int group, int room) in new[] { (0, 0x12), (0, 0x13), (0, 0x14), (1, 0x13) })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.TuniNutPlaced, restored);
            _saveData.SetRoomFlag(group, room, 1, restored);
            LoadValidationRoom(group, room);
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(80, 104));
            var actors = _entities.Entities<FountainDecorationRoomEntity>().OrderBy(a => a.Placement.SubId).ToArray();
            FailIf(actors.Length != 2, $"Room {group}:{room:x2} must create both fountain layers.");
            bool ruined = group == 1 || !restored;
            int x = room == 0x12 ? 0x20 : room == 0x14 ? 0x80 : 0x50;
            int y = room == 0x13 ? 0x1e : 0x5e;
            FailIf(actors[0].Position != new Vector2(x, y) || actors[1].Position != new Vector2(x, y - 6) ||
                actors.Any(a => a.RuinedPalette != ruined) ||
                actors[0].ZIndex != 8 || actors[1].ZIndex != 12 ||
                _entities.InteractionSlot(actors[1]) != _entities.InteractionSlot(actors[0]) + 1,
                "Fountain lost native positions, source palette branch, visible83/80 priorities, or layer order.");
            foreach (var actor in actors)
            {
                bool basin = actor.Placement.SubId == 9;
                FailIf(actor.Texture.GetWidth() != (basin ? 24 : 8) || actor.Texture.GetHeight() != 16 ||
                    actor.FrameOffset != new Vector2(basin ? -12 : -4, -8),
                    "Fountain OAM bounds must preserve all three basin cells and the separate stream origin.");
                using Image image = actor.Texture.GetImage();
                // spr_fountain uses shade 1 in the ripples, but the stream
                // cells contain only shade 3 (plus transparent shade 0).
                Color expected = basin
                    ? ruined ? new Color(1, 4 / 31f, 0) : new Color(13 / 31f, 22 / 31f, 1)
                    : ruined ? new Color(1, 18 / 31f, 4 / 31f) : new Color(28 / 31f, 1, 1);
                bool found = false;
                var colors = new System.Collections.Generic.HashSet<Color>();
                for (int py = 0; py < image.GetHeight(); py++)
                for (int px = 0; px < image.GetWidth(); px++)
                {
                    Color actual = image.GetPixel(px, py);
                    if (actual.A > 0) colors.Add(actual);
                    found |= actual.A > 0.99f && Mathf.Abs(actual.R - expected.R) <= 1 / 255f &&
                        Mathf.Abs(actual.G - expected.G) <= 1 / 255f && Mathf.Abs(actual.B - expected.B) <= 1 / 255f;
                }
                FailIf(!found, $"Fountain ${actor.Placement.SubId:x2} pixels must use source paletteData5948/5940 shade {(basin ? 1 : 3)}: {string.Join(';', colors)}.");
            }
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            for (int n = 2; n <= 48; n += 2)
            {
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(actors[0].AnimationFrame != n / 12 % 4 || actors[1].AnimationFrame != n / 6 % 2,
                    $"Fountain diverged from source animation after {n} state1 updates.");
            }
            // Re-entry via preload initializes both frames and freezes them
            // until the room scroll finishes, without editing world positions.
            _entities.BeginScreenTransition(group, _currentRoom, new(160, 0), _player);
            actors = _entities.Entities<FountainDecorationRoomEntity>().OrderBy(a => a.Placement.SubId).ToArray();
            StepGameplayUpdates(24, Vector2.Zero, batched: batched);
            FailIf(actors.Any(a => !a.Visible || a.AnimationFrame != 0 || a.TransitionDrawOffset != new Vector2(160, 0)),
                "Incoming fountain state0 must render initial OAM and freeze animation during scrolling.");
            _entities.FinishScreenTransition();
            StepGameplayUpdates(6, Vector2.Zero, batched: batched);
            FailIf(actors[0].AnimationFrame != 0 || actors[1].AnimationFrame != 1,
                "Fountain must resume its separate clocks after scrolling.");
        }
        ReinitializeGameplayForValidation();
    }
}
