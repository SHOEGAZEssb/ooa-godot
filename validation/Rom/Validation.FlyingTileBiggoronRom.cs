using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFlyingTileBiggoronRom() => CompareFlyingTileWeaponIdentityRom(false);
    private void ValidateFlyingTileBombIdentityRom() => CompareFlyingTileWeaponIdentityRom(true);

    private void CompareFlyingTileWeaponIdentityRom(bool bomb)
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (int layout in new[] { 0, 2 })
        foreach (bool batched in new[] { false, true })
        foreach (int scenario in bomb ? new[] { 0, 4 } : new[] { 0, 1, 2, 3, 4 })
        {
            bool eligible = scenario < 3;
            ReinitializeGameplayForValidation(); LoadValidationRoom(layout == 0 ? 4 : 5, layout == 0 ? 0x5b : 0x91);
            var parent = _entities.Entities<FlyingTileSpawnerRoomEntity>().Single();
            foreach (var other in _entities.EntityAdapters<IRoomEntity>().Where(a => !ReferenceEquals(a.Node, parent)).ToArray())
                _entities.RetireEntity(other);
            FailIf(((IRoomEntity)parent) is ISwordHittableRoomEntity, "The original Flying Tile spawner must remain outside the active child's combat route.");
            byte floor = Enumerable.Range(0, _currentRoom.WidthInTiles * _currentRoom.HeightInTiles)
                .Select(i => _currentRoom.GetTerrainInfo(new(i % _currentRoom.WidthInTiles * 16 + 8, i / _currentRoom.WidthInTiles * 16 + 8)))
                .First(info => info.Collision == 0 && info.Type == TerrainType.Normal && info.Hazard == HazardType.None).Tile;
            for (int y = 8; y < _currentRoom.Height; y += 16)
            for (int x = 8; x < _currentRoom.Width; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), floor, null, 0);
            _inventory.GiveTreasure(bomb ? TreasureId.Bombs : TreasureId.BiggoronSword, bomb ? 0x10 : 1);
            _inventory.EquipA(bomb ? TreasureId.Bombs : TreasureId.BiggoronSword); _inventory.EquipB(0);
            bool paused = false;
            var previousText = _entities.TextActiveSource;
            _entities.TextActiveSource = () => paused;
            try
            {
                for (int repeat = 0; repeat < (eligible ? 3 : 1); repeat++)
                {
                    _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0);
                    _player.WarpTo(new(16, 16)); _player.BeginCutsceneControl();
                    int wait = 0;
                    while (_entities.Entities<FlyingTileCharacter>().Count == 0 && wait++ < 256)
                        StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                    var tile = _entities.Entities<FlyingTileCharacter>().Single();
                    FailIf(tile.State != FlyingTileState.Preparing || tile.Visible,
                        "Original active Flying Tile birth must stop at hidden state $08 after common initialization.");
                    StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                    int expectedPacked = layout == 0 ? new[] { 0x57, 0x56, 0x46 }[repeat] : new[] { 0x67, 0x54, 0x5a }[repeat];
                    FailIf(tile.State != FlyingTileState.Rising || tile.ZFixed != 0 ||
                        tile.Position != new Vector2((expectedPacked & 15) * 16 + 8, (expectedPacked >> 4) * 16 + 8),
                        $"Layout ${layout:x2} must produce source child ${expectedPacked:x2} through the actual room spawner.");
                    var native = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
                    native[0xcc08] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x52 * 4];
                    native[0xd080] = 1; native[0xd081] = 0x52; native[0xd082] = 0x80;
                    native.Word(0xd08a, (int)(tile.Position.Y * 256)); native.Word(0xd08c, (int)(tile.Position.X * 256));
                    native.Update(_entities.FrameCounter, _player.Position);
                    FailIf(native[0xd084] != 8, "Original active Flying Tile state zero must enter state $08.");
                    native.Update(_entities.FrameCounter, _player.Position);
                    FailIf(native[0xd084] != 9 || native[0xd0a4] != 0xd2 || native[0xd0a5] != 0x3c ||
                        native[0xd0a6] != 6 || native[0xd0a7] != 6 || native[0xd0a9] != 2,
                        "Original child state $08 must enable identity $52, mode $3c, radii $06/$06 and HP $02.");
                    // Align the visible rising boundary after both distinct
                    // state0/state8 visits, then retain native AI.
                    int countBefore = _entities.RoomEnemyCount;
                    int defeatsBefore = _entities.ActiveRoomDefeatBitset;
                    native[0xcdd1] = (byte)countBefore;
                    bool cancelled = false;
                    int updates = 0, contacts = 0;
                    bool Pending() => (bool)typeof(FlyingTileCharacter).GetField("_collisionBreakPending", hidden)!.GetValue(tile)!;
                    void Compare()
                    {
                        native[0xcba0] = paused ? (byte)1 : (byte)0;
                        native[0xcc8a] = _runtimeState.ReadWramByte(WramAddress.wDisabledObjects);
                        native.Update(_entities.FrameCounter, _player.Position);
                        native[0xd624] = native[0xd724] = 0;
                        if (_entities.Biggoron?.Weapon is { Finished: false } sword)
                        {
                            native[0xd600] = 1; native[0xd601] = 0x0c; native[0xd604] = 1;
                            native[0xd60b] = (byte)sword.Position.Y; native[0xd60d] = (byte)sword.Position.X;
                            native[0xd60f] = unchecked((byte)sword.ZHigh); native[0xd624] = (byte)sword.Collision;
                            native[0xd626] = (byte)sword.Radius.Y; native[0xd627] = (byte)sword.Radius.X;
                            native[0xd628] = unchecked((byte)-sword.Damage);
                        }
                        if (_entities.Entities<BombEffect>().SingleOrDefault() is { ExplosionCollisionEnabled: true } explosion)
                        {
                            native[0xd700] = 1; native[0xd701] = 3; native[0xd704] = 0xff;
                            native[0xd70b] = (byte)explosion.Position.Y; native[0xd70d] = (byte)explosion.Position.X;
                            native[0xd70f] = unchecked((byte)explosion.CollisionZ); native[0xd724] = 0x98;
                            native[0xd726] = native[0xd727] = (byte)explosion.ExplosionRadius;
                            native[0xd728] = unchecked((byte)-explosion.Damage);
                        }
                        bool wasPending = (native[0xd0aa] & 0x80) != 0;
                        if (!paused) native.ResolveLinkCollisions();
                        if (!wasPending && (native[0xd0aa] & 0x80) != 0) contacts++;
                        string context = $"Flying Tile layout=${layout:x2}, {(bomb ? "Bomb" : "Biggoron")}, scenario={scenario}, repeat={repeat}, batch={batched}, update={++updates}";
                        if (cancelled) return;
                        FailIf(tile.IsDead != (native[0xd080] == 0) || _entities.RoomEnemyCount != native[0xcdd1] ||
                            _entities.ActiveRoomDefeatBitset != defeatsBefore, context + ": deletion/count or recent-defeat side effect differs.");
                        if (tile.IsDead) return;
                        FailIf(Pending() != ((native[0xd0aa] & 0x80) != 0) || tile.Health != native[0xd0a9] ||
                            tile.InvincibilityCounter != unchecked((sbyte)native[0xd0ab]) || (int)tile.State != native[0xd084] ||
                            tile.Counter != native[0xd086] || tile.ZFixed != unchecked((short)native.Word(0xd08e)) ||
                            tile.Position != new Vector2(native.Word(0xd08c)/256f, native.Word(0xd08a)/256f),
                            $"{context}: source status differs: pending={Pending()}/${native[0xd0aa]:x2}, HP={tile.Health}/{native[0xd0a9]}, identity=${_entities.EntityAdapters<FlyingTileRoomEntity>().Single().DimitriCollisionType:x}, state={tile.State}/${native[0xd084]:x2}.");
                    }
                    void Step(int count = 1, bool press = false, Vector2 movement = default) =>
                        StepGameplayUpdates(count, movement, press ? ["attack"] : [], press ? ["attack"] : [], batched, Compare);
                    if (scenario is 1 or 2) Step(scenario == 1 ? 7 : 22);
                    _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 4);
                    _player.EndCutsceneControl();
                    if (bomb)
                    {
                        _player.WarpTo(new(80, 64)); _player.Face(Vector2I.Up);
                        Step(1, press: true); Step(19); Step(1, press: true); Step(9); Step(32, movement: Vector2.Down);
                        var child = _entities.Entities<BombEffect>().Single();
                        tile.Position = child.Position + Vector2.Right * 8;
                        native.Word(0xd08a, (int)(tile.Position.Y * 256)); native.Word(0xd08c, (int)(tile.Position.X * 256));
                        typeof(BombEffect).GetField("_frameIndex", hidden)!.SetValue(child, 9);
                        typeof(BombEffect).GetField("_frameCounter", hidden)!.SetValue(child, 1);
                    }
                    else
                    {
                        _player.WarpTo(tile.Position - new Vector2(19, 2)); _player.Face(Vector2I.Up);
                    }
                    if (scenario == 3)
                    {
                        typeof(FlyingTileCharacter).GetField("_zFixed", hidden)!.SetValue(tile, -0x0a00);
                        native.Word(0xd08e, -0x0a00); // Distance 8 from weapon zh=-$02 exceeds source radius $07.
                    }
                    if (scenario == 4) { tile.InvincibilityCounter = 3; native[0xd0ab] = 3; }
                    Step(1, press: !bomb);
                    FailIf(contacts != (eligible ? 1 : 0) || Pending() != eligible || tile.IsDead || tile.Health != 2,
                        "The original $1c weapon effect queues one next-update break without HP damage; rejected contacts must leave the child alive.");
                    if (repeat == 2)
                    {
                        _entities.ClearPhysicalPlayerItems(); _entities.Clear(); native[0xd080] = native[0xd0a4] = 0;
                        cancelled = true; Step(2);
                        FailIf(_entities.Entities<RockDebrisEffect>().Count != 0,
                            "Cancelling a pending break must not create deferred debris after the room owner is retired.");
                        break;
                    }
                    paused = true; Step(2); paused = false; Step(2);
                    FailIf(tile.IsDead || Pending() != eligible, "Text/enemy freezes must preserve pending and rejected tile contacts.");
                    if (!eligible)
                    {
                        _entities.ClearPhysicalPlayerItems(); _entities.Clear(); native[0xd080] = native[0xd0a4] = 0;
                        cancelled = true; Step(2); break;
                    }
                    _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); Step(2);
                    FailIf(!tile.IsDead || _entities.RoomEnemyCount != countBefore - 1 || _entities.Entities<RockDebrisEffect>().Count != 1,
                        "The first eligible child update must create one debris owner and silently decrement the room count once.");
                    _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 4);
                    wait = 0;
                    while (_entities.Biggoron?.Active == true || _entities.Entities<BombEffect>().Count != 0 || _entities.Entities<RockDebrisEffect>().Count != 0)
                    {
                        FailIf(wait++ > 64, "Tile weapon/debris lifetime did not complete."); Step(2);
                    }
                    FailIf(_entities.RoomEnemyCount != countBefore - 1 || contacts != 1,
                        "Completed tile breaks must not repeat their contact or count decrement.");
                }
            }
            finally { _entities.TextActiveSource = previousText; _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); }
        }
        ReinitializeGameplayForValidation();
    }
}
