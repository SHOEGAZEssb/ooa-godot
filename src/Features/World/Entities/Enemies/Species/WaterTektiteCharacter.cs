using Godot;

namespace oracleofages;

// object_code/common/enemies/waterTektite.s. This swimmer has no jump/hazard
// handler: raw layout tiles $f9-$fd are traversable regardless of solidity.
internal sealed partial class WaterTektiteCharacter : EnemyCharacter
{
    private readonly WaterTektiteBehaviorProfile _behavior = EnemyBehaviorTables.Shared.WaterTektite;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private int _stunCounter, _stunZ, _stunSpeedZ;
    internal int StunCounter => _stunCounter;
    internal override void ApplyBoomerangStun(int updates) => _stunCounter = updates;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Counter { get; set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal override bool InitializationPending => State == 0;

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room,
        Vector2 position, OracleRandom random)
    {
        Record = record;
        _room = room;
        _random = random;
        _movement = new(this, room);
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        ConfigureSwordKnockback(room, EnemyKnockbackMotion.Terrain);
        Visible = false;
    }

    internal void UpdateFrame(Vector2? scentTarget, int frameCounter = 0)
    {
        if (IsDead) return;
        bool stunned = State != 0 && !NativeHitPending && !HasActiveKnockback && Health > 0 && _stunCounter != 0;
        if (stunned)
            Position = EnemyStunMotion.Update(Position, State, frameCounter,
                ref _stunCounter, ref _stunZ, ref _stunSpeedZ);
        if (BeginFrame()) return;
        if (stunned) { QueueRedraw(); return; }
        switch (State)
        {
            case 0:
                // bank0.enemyStandardUpdate seeds var3d before native state 0.
                _random.Next();
                Visible = true;
                ZIndex = ObjectDrawPriority.BehindLinkZIndex;
                ChooseAngle(scentTarget);
                return;
            case 8:
                Counter = (Counter - 1) & 0xff;
                if (Counter == 0)
                {
                    State = 9;
                    Counter = _behavior.RestUpdates;
                }
                else
                {
                    Speed = _behavior.Speeds[Counter >> 2].Value;
                    var walls = EnemyAdjacentWallResolver.Shared.Probe(Position, Angle, IsWaterWall);
                    _movement.MoveGivenAdjacentWalls(Angle, Speed, walls);
                    // Only room collision-buffer $ff boundaries turn it around.
                    Angle = EnemyAdjacentWallResolver.Shared.BounceAngle(Position, Angle,
                        point => point.X < 0 || point.Y < 0 || point.X >= _room.Width || point.Y >= _room.Height);
                }
                AdvanceAnimation();
                return;
            case 9:
                Counter = (Counter - 1) & 0xff;
                if (Counter == 0) ChooseAngle(scentTarget);
                else AdvanceAnimation();
                return;
        }
    }

    internal bool TakeSwitchHookHit(Vector2 origin, int damage)
    {
        if (!TakeSwordHit(origin, damage)) return false;
        ApplySwordKnockback(origin, EnemyKnockbackStrength.Low);
        DeferNativeHitStatus();
        return true;
    }

    private void ChooseAngle(Vector2? scentTarget)
    {
        State = 8;
        Counter = _behavior.SwimUpdates;
        // @scentSeedActive puts the scent in hFF8F/hFF8E (origin) and self
        // in BC (target). Preserve this reversal of ordinary scent steering.
        Angle = scentTarget is { } target
            ? OracleObjectMovement.Shared.RelativeAngle(target, Position)
            : (_random.Next().Value & _behavior.AngleMask) + _behavior.AngleOffset;
        AdvanceAnimation();
    }

    private protected override bool UpdateKnockback()
    {
        if (!HasActiveKnockback) return false;
        KnockbackCounter--;
        // enemyCode3a supplies SPEED_200 even for high recoil, restores the
        // swimming speed afterward, and neither allocates dust nor cancels a
        // blocked recoil. Its counter/animation freeze still consumes this pass.
        var walls = EnemyAdjacentWallResolver.Shared.Probe(Position, KnockbackAngle, IsWaterWall);
        _movement.MoveGivenAdjacentWalls(KnockbackAngle, _behavior.KnockbackSpeed, walls);
        return true;
    }

    private bool IsWaterWall(Vector2I point)
    {
        // The original probes wrap bytes before forming the 16-byte-stride
        // storage position; do not substitute collision or room-boundary tests.
        int packed = (point.Y & 0xf0) | ((point.X & 0xff) >> 4);
        if ((packed & 15) >= _room.WidthInTiles || (packed >> 4) >= _room.HeightInTiles)
            return true; // cleared non-room wRoomLayout bytes are $00
        int tile = _room.GetPackedStorageMetatile((byte)packed);
        return ((tile - _behavior.FirstWaterTile) & 0xff) >= _behavior.WaterTileCount;
    }
}
