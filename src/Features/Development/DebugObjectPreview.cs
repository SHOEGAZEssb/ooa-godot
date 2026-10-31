using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// Presentation only: never allocate an actor or run its initialization/AI.
internal sealed class DebugObjectPreview(string assetRoot)
{
    private string AssetRoot => assetRoot;
    private EnemyDatabase? _enemies;
    private EnemyDatabase Enemies => _enemies ??= EnemyDatabase.For(AssetRoot);
    private ItemDropDatabase? _dropsSourceCache;
    private ItemDropDatabase _drops => _dropsSourceCache ??= new(AssetRoot);
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
            visual = entry.HasImportedDefinition ? EnemyVisual(entry.Id, entry.SubId) : null;

        texture = visual is null ? null : BuildRepresentativeFrame(visual);
        if (texture is null && entry.Supported)
            throw new InvalidOperationException(
                $"Debug preview ${entry.Id:x2}:${entry.SubId:x2} has no visible imported sprite.");
        _textures.Add(key, texture);
        return texture;
    }

    private PreviewVisual? EnemyVisual(int id, int subId)
    {
        if (id == 3 && subId == 0)
        {
            var actor = new TwinrovaDatabase(AssetRoot).Actor(id);
            return new(actor.Sprites, actor.Animations, actor.TileBase, actor.Palette, actor.SourceGrayscaleInverted);
        }
        if(id==0x02&&subId==0)
        {
            var veran=new VeranFinalDatabase(AssetRoot);var actor=veran.Actor(id);
            return new(actor.Sprites,actor.Animations,actor.TileBase,actor.Palette,
                actor.SourceGrayscaleInverted,veran.Palettes);
        }
        if (id == 0x7f && subId == 0)
        {
            var king = new KingMoblinDatabase(AssetRoot);
            var actor = king.Actor(id);
            return new(actor.Sprites, actor.Animations, actor.TileBase, actor.Palette,
                actor.SourceGrayscaleInverted, king.Palettes);
        }
        var source = new RoomObjectRecord(0, 0, 0, RoomObjectKind.FixedEnemy,
            id, subId, 0, 1, 0, 0, 0, 0xff);
        if (id == EnemyId.Keese && KeeseDatabase.For(AssetRoot).TryGetDefinition(source,out var keese))
            return new([keese.SpriteName],[keese.IdleAnimation,keese.FlyAnimation],keese.TileBase,keese.Palette);
        if (EnemyDefinitionDatabase.For(AssetRoot).Definitions.TryGetValue(
            (id,id == EnemyId.PodobooTower ? 0 : subId),out var record))
        {
            return new(record.Sprites,record.Animations,record.TileBase,record.Palette,record.SourceGrayscaleInverted);
        }
        if (id == EnemyId.Octorok && OctorokDatabase.For(AssetRoot).TryGetDefinition(source, out var octorok))
            return new([octorok.SpriteName], [octorok.DownAnimation], octorok.TileBase, octorok.Palette);
        if (Enemies.TryGetImportedEnemyDefinition(source, out var common))
            return new(common.Sprites, common.Animations, common.TileBase, common.Palette,
                common.SourceGrayscaleInverted,
                id == 0x47 ? Enemies.ColorChangingGelPalettes : null);
        if (Enemies.TryGetStalfosDefinition(source, out var stalfos))
            return new([stalfos.SpriteName], [stalfos.WalkAnimation], stalfos.TileBase, stalfos.Palette);
        if (Enemies.TryGetZolDefinition(source, out var zol))
            return new([zol.SpriteName], [subId == 0 ? zol.WaitAnimation : zol.RedIdleAnimation],
                zol.TileBase, zol.Palette);
        if (Enemies.TryGetCrowDefinition(source, out var crow))
            return new([crow.SpriteName], [crow.PerchedRightAnimation], crow.TileBase, crow.Palette);
        if (id == Enemies.Gel.Id && subId == Enemies.Gel.SubId)
        {
            GelDefinition gel = Enemies.Gel;
            return new([gel.SpriteName], [gel.NormalAnimation], gel.TileBase, gel.Palette);
        }
        return null;
    }

    private Texture2D? BuildRepresentativeFrame(PreviewVisual visual)
    {
        Image source = EnemyVisualSource.LoadComposite(visual.Sprites, AssetRoot);
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
                    ? OracleOamCompositor.BuildPositionedOamTexture(source, frame.EncodedOam,
                        visual.TileBase, visual.Palette, null, visual.Inverted)
                    : OracleOamCompositor.BuildPositionedOamTextureWithPaletteOverrides(source,
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
