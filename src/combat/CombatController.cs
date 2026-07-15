using Godot;
using System;

namespace oracleofages;

public sealed class CombatController
{
    private readonly Node _worldRoot;
    private readonly RoomSession _rooms;
    private readonly RoomView _roomView;
    private readonly RoomEntityManager _entities;
    private readonly BreakableTileDatabase _breakables;
    private readonly OracleSoundEngine _sound;
    private readonly Func<long> _animationTick;

    internal int ClinkEffectsSpawned { get; private set; }
    internal ClinkEffect? LastClinkEffect { get; private set; }

    private static readonly Vector2[] SwordTileOffsets =
    {
        new(0, -14), new(13, -14), new(13, 0), new(13, 13),
        new(0, 13), new(-14, 13), new(-14, 0), new(-14, -14),
        Vector2.Zero
    };

    private static readonly byte[][] BombableWallClinkTiles =
    {
        new byte[] { 0xc1, 0xc2, 0xc4, 0xd1, 0xcf },
        new byte[] { 0x1f, 0x30, 0x31, 0x32, 0x33, 0x38, 0x39, 0x3a, 0x3b, 0x68, 0x69 },
        new byte[] { 0x1f, 0x30, 0x31, 0x32, 0x33, 0x38, 0x39, 0x3a, 0x3b, 0x68, 0x69 },
        new byte[] { 0x12 },
        new byte[] { 0xc1, 0xc2, 0xc4, 0xd1, 0xcf },
        new byte[] { 0x1f, 0x30, 0x31, 0x32, 0x33, 0x38, 0x39, 0x3a, 0x3b, 0x68, 0x69 }
    };

    private static readonly byte[][] SilentSwordClinkTiles =
    {
        new byte[] { 0xfd, 0xfe, 0xff },
        new byte[] { 0x0a, 0x0b },
        new byte[] { 0x0a, 0x0b },
        Array.Empty<byte>(),
        new byte[] { 0xfd, 0xfe, 0xff },
        new byte[] { 0x0a, 0x0b }
    };

    public CombatController(
        Node worldRoot,
        RoomSession rooms,
        RoomView roomView,
        RoomEntityManager entities,
        BreakableTileDatabase breakables,
        OracleSoundEngine sound,
        Func<long> animationTick)
    {
        _worldRoot = worldRoot;
        _rooms = rooms;
        _roomView = roomView;
        _entities = entities;
        _breakables = breakables;
        _sound = sound;
        _animationTick = animationTick;
    }

    public bool ApplySwordHit(Player player, Rect2 hitbox)
    {
        return _entities.ApplySwordHit(hitbox, player.Position);
    }

    public bool ApplySwordTileHit(Player player, int direction, bool swordPoke)
    {
        if ((uint)direction >= SwordTileOffsets.Length)
            throw new ArgumentOutOfRangeException(nameof(direction));

        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 point = player.Position + SwordTileOffsets[direction];
        byte tile = room.GetMetatile(point);
        int swordSource = player.Inventory.SwordLevel <= 1
            ? BreakableTileDatabase.SourceSwordLevel1
            : BreakableTileDatabase.SourceSwordLevel2;
        if (_breakables.TryGet(room.ActiveCollisions, tile, out BreakableTileDatabase.BreakableTileRecord record) &&
            record.AllowsSource(swordSource))
        {
            bool changed = record.Replacement == 0 ||
                room.ReplaceMetatile(point, tile, (byte)record.Replacement, _animationTick());
            if (!changed)
                return false;

            SpawnBreakEffect(point, record.Effect);
            _roomView.QueueRedraw();
            return true;
        }

        int collisionSet = Math.Clamp(room.ActiveCollisions, 0, BombableWallClinkTiles.Length - 1);
        if (Array.IndexOf(BombableWallClinkTiles[collisionSet], tile) >= 0)
        {
            SpawnClinkEffect(point, flickers: false);
            _sound.PlaySound(OracleSoundEngine.SndClink2);
            return true;
        }
        if (!swordPoke || Array.IndexOf(SilentSwordClinkTiles[collisionSet], tile) >= 0 ||
            room.GetTerrainInfo(point).Collision != 0x0f)
        {
            return false;
        }

        SpawnClinkEffect(point, flickers: true);
        _sound.PlaySound(OracleSoundEngine.SndClink);
        return true;
    }

    internal void ClearClinkEffectAudit()
    {
        ClinkEffectsSpawned = 0;
        LastClinkEffect = null;
    }

    private void SpawnClinkEffect(Vector2 position, bool flickers)
    {
        var effect = new ClinkEffect
        {
            Name = "Clink",
            ZIndex = 10
        };
        effect.Initialize(position, flickers);
        _worldRoot.AddChild(effect);
        LastClinkEffect = effect;
        ClinkEffectsSpawned++;
    }

    private void SpawnBreakEffect(Vector2 point, int effect)
    {
        // INTERAC_GRASSDEBRIS ($00) and INTERAC_BUSHLEAF ($01) share
        // SND_CUTGRASS. Other imported debris records remain owned by their
        // respective interaction slices, but their tile replacement is still
        // applied here.
        if ((effect & 0x1f) is not (0x00 or 0x01))
            return;

        int tileX = Mathf.FloorToInt(point.X / OracleRoomData.MetatileSize);
        int tileY = Mathf.FloorToInt(point.Y / OracleRoomData.MetatileSize);
        Rect2 tileBounds = new(
            tileX * OracleRoomData.MetatileSize,
            tileY * OracleRoomData.MetatileSize,
            OracleRoomData.MetatileSize,
            OracleRoomData.MetatileSize);

        _worldRoot.AddChild(new BushCutEffect
        {
            Position = tileBounds.GetCenter(),
            ZIndex = 12
        });
        _sound.PlaySound(OracleSoundEngine.SndCutGrass);
    }
}
