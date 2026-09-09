using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// Presentation only: never allocate an actor or run its initialization/AI.
internal sealed class DebugObjectPreview(EnemyDatabase enemies)
{
    private readonly ItemDropDatabase _drops = new();
    private readonly Dictionary<(bool Drop, int Id, int SubId), Texture2D?> _textures = new();

    internal Texture2D? GetTexture(DebugObjectEntry entry, bool isDrop)
    {
        var key = (isDrop, entry.Id, entry.SubId);
        if (_textures.TryGetValue(key, out Texture2D? texture))
            return texture;
        PreviewVisual? visual;
        if (isDrop)
        {
            ItemDropDatabaseVisualRecord drop = _drops.GetVisual(entry.SubId);
            visual = new(["spr_common_items"], [drop.Animation], drop.TileBase, drop.Palette);
        }
        else
            visual = EnemyVisual(entry.Id, entry.SubId);

        texture = visual is null ? null : BuildRepresentativeFrame(visual);
        if (texture is null && entry.Supported)
            throw new InvalidOperationException(
                $"Debug preview ${entry.Id:x2}:${entry.SubId:x2} has no visible imported sprite.");
        _textures.Add(key, texture);
        return texture;
    }

    private PreviewVisual? EnemyVisual(int id, int subId)
    {
        var source = new RoomObjectRecord(0, 0, 0, RoomObjectKind.FixedEnemy,
            id, subId, 0, 1, 0, 0, 0, 0xff);
        if (enemies.TryGetImportedEnemyDefinition(source, out var common))
            return new(common.Sprites, common.Animations, common.TileBase, common.Palette,
                common.SourceGrayscaleInverted,
                id == 0x47 ? enemies.ColorChangingGelPalettes : null);
        if (enemies.TryGetKeeseDefinition(source, out var keese))
            return new([keese.SpriteName], [keese.IdleAnimation, keese.FlyAnimation],
                keese.TileBase, keese.Palette);
        if (enemies.TryGetOctorokDefinition(source, out var octorok))
            return new([octorok.SpriteName], [octorok.DownAnimation], octorok.TileBase, octorok.Palette);
        if (enemies.TryGetStalfosDefinition(source, out var stalfos))
            return new([stalfos.SpriteName], [stalfos.WalkAnimation], stalfos.TileBase, stalfos.Palette);
        if (enemies.TryGetZolDefinition(source, out var zol))
            return new([zol.SpriteName], [subId == 0 ? zol.WaitAnimation : zol.RedIdleAnimation],
                zol.TileBase, zol.Palette);
        if (enemies.TryGetCrowDefinition(source, out var crow))
            return new([crow.SpriteName], [crow.PerchedRightAnimation], crow.TileBase, crow.Palette);
        if (id == enemies.Gel.Id && subId == enemies.Gel.SubId)
        {
            GelDefinition gel = enemies.Gel;
            return new([gel.SpriteName], [gel.NormalAnimation], gel.TileBase, gel.Palette);
        }
        if (id == enemies.MaskedMoblin.Id && subId is 0 or 1)
        {
            MaskedMoblinRecord moblin = enemies.MaskedMoblin;
            return new([moblin.SpriteName], [moblin.DownAnimation], moblin.TileBase, moblin.Palette);
        }
        return null;
    }

    private static Texture2D? BuildRepresentativeFrame(PreviewVisual visual)
    {
        Image source = EnemyVisualSource.LoadComposite(visual.Sprites);
        Texture2D? best = null;
        int bestArea = 0;
        // Hidden/emerging enemies can start with empty OAM. Use the fullest
        // imported pose, retaining its cells, flips, palette and source polarity.
        foreach (string animation in visual.Animations)
        {
            foreach (AnimationFrameDefinition frame in
                OracleGraphicsCache.GetAnimationDefinition(animation).Frames)
            {
                (Texture2D texture, _) = visual.PaletteOverrides is null
                    ? NpcCharacter.BuildPositionedOamTexture(source, frame.EncodedOam,
                        visual.TileBase, visual.Palette, null, visual.Inverted)
                    : NpcCharacter.BuildPositionedOamTextureWithPaletteOverrides(source,
                        frame.EncodedOam, visual.TileBase, visual.Palette,
                        visual.PaletteOverrides, visual.Inverted);
                using Image pixels = texture.GetImage();
                Rect2I bounds = pixels.GetUsedRect();
                int area = bounds.Size.X * bounds.Size.Y;
                if (area <= bestArea) continue;
                best = texture;
                bestArea = area;
            }
        }
        return best;
    }

    private sealed record PreviewVisual(string[] Sprites, string[] Animations,
        int TileBase, int Palette, bool Inverted = true,
        IReadOnlyDictionary<int, Color[]>? PaletteOverrides = null);
}
