using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateUnderwaterSurfaceRom() => ValidateUnderwaterSurfaceRom(false);
    private void ValidateUnderwaterDungeonSurfaceRom() => ValidateUnderwaterSurfaceRom(true);
    private void ValidateUnderwaterWarpHoleRom() => ValidateUnderwaterSurfaceRom(true, down: true);
    private void ValidateUnderwaterWhirlpoolRom() => ValidateUnderwaterSurfaceRom(false, whirlpool: true);

    private void ValidateUnderwaterEquippedSurfaceRom()
    {
        // checkUseItems checks only A in top-down underwater rooms, before
        // Link consumes a fresh B edge for travel. Cover usable B items,
        // including selectors 2/3/4 and actions that immobilize Link.
        foreach (int item in new[] { TreasureId.SwitchHook, TreasureId.Sword,
            TreasureId.Shooter, TreasureId.Boomerang, TreasureId.Bombs,
            TreasureId.Bracelet, TreasureId.Feather, TreasureId.Shield, TreasureId.Shovel,
            TreasureId.CaneOfSomaria, TreasureId.SeedSatchel,
            TreasureId.BiggoronSword, TreasureId.Bombchus, TreasureId.Harp, TreasureId.Flute })
            ValidateUnderwaterSurfaceRom(false, equippedB: item);
    }

    private void ValidateUnderwaterSurfaceRom(bool dungeon, bool down = false, bool whirlpool = false,
        int equippedB = 0)
    {
        int hostCase1 = 0;
        foreach (int group in dungeon ? new[] { 5 } : equippedB != 0 ? new[] { 2 } : new[] { 2, 3 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            int sourceRoom = dungeon ? down ? 0x5c : 0x4c : 0xb0;
            int destinationRoom = dungeon ? down ? 0x4c : 0x5c : 0xb0;
            int destinationGroup = dungeon ? group : group - 2;
            int blockedTile = dungeon ? 0x5b : 0x63;
            int allowedTile = dungeon ? 0x5c : 0x64;
            if (dungeon) _saveData.WriteWramByte(WramAddress.wJabuWaterLevel, 0x22);
            LoadValidationRoom(group, sourceRoom); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            if (equippedB != 0)
            {
                _inventory.GiveTreasure(equippedB, equippedB is TreasureId.Bombs or TreasureId.Bombchus ? 0x10 : 1);
                _inventory.GiveTreasure(TreasureId.EmberSeeds, 0x10);
            }
            _inventory.EquipA(0); _inventory.EquipB(equippedB);
            byte[] ammo = Enumerable.Range(0xc6b0, 14).Select(_saveData.ReadWramByte).ToArray();
            for (int y = 0; y < _currentRoom.HeightInTiles; y++)
            for (int x = 0; x < _currentRoom.WidthInTiles; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0,
                    x == 0 || x >= (allowedTile & 15) + 1 || y == 0 || y == _currentRoom.HeightInTiles - 1
                        ? (byte)0x0f : (byte)0, 0);
            if (down)
                _currentRoom.SetPositionTileAndCollision(new((allowedTile & 15) * 16 + 8, (allowedTile >> 4) * 16 + 8), 0x49, 0, 0);
            if (whirlpool)
                _currentRoom.SetPositionTileAndCollision(new((allowedTile & 15) * 16 + 8, (allowedTile >> 4) * 16 + 8), 0xe9, 0, 0);
            Vector2 start = new((blockedTile & 15) * 16 + 8.25f, (blockedTile >> 4) * 16 - 3.5f);
            _player.WarpTo(start); _player.Face(Vector2I.Right);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 1, (int)start.X, (int)start.Y);
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40; rom.InitializeLinkGameplay();
            rom[0xd004] = 0; rom[0xd009] = 0;
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            if (dungeon)
            {
                rom.CreateMenuView().LoadDungeon(7);
                FailIf(rom[0xcc3b] != (down ? 1 : 0) || rom[0xcc3a] != 0x1b,
                    $"Jabu source 5:{sourceRoom:x2} must occupy floor ${(down ? 1 : 0):x2}/map cell $1b.");
            }
            var sounds = _sound.AttachPlayRequestAudit();
            int previousHeld = 0, update = 0, captured = -1, fadeUpdates = 0;
            bool loaded = false;
            void Lock(int mask)
            {
                _runtimeState.SetWramByte(WramAddress.wDisableScreenTransitions, (byte)mask);
                rom[0xcc91] = (byte)mask;
            }
            void Step(int count = 1, int held = 0)
            {
                int edge = held & ~previousHeld; previousHeld = held;
                Vector2 movement = (held & 0x10) != 0 ? Vector2.Right : (held & 0x20) != 0 ? Vector2.Left : Vector2.Zero;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.AdvanceWarpPalette();
                    if (rom[0xc2ef] == 3)
                    {
                        rom.UpdateFadeOutWarp(); fadeUpdates++;
                        loaded = rom[0xcc2d] != group || rom[0xcc30] != sourceRoom;
                    }
                    else
                    {
                        rom.UpdateGameplay(edge, held, movement == Vector2.Right ? 8 : movement == Vector2.Left ? 0x18 : 0xff,
                            _entities.FrameCounter);
                        if (!loaded && rom[0xcc4b] != 0)
                        {
                            captured = rom[0xcc4a];
                            FailIf(rom[0xcc47] != (0x80 | destinationGroup) || rom[0xcc48] != destinationRoom ||
                                rom[0xcc49] != 0 || rom[0xcc4b] != 3 || captured != allowedTile,
                                $"Native surfacing request ${rom[0xcc47]:x2}:${rom[0xcc48]:x2}/${rom[0xcc49]:x2}/${rom[0xcc4b]:x2}, tile=${captured:x2}, expected {destinationGroup:x1}:{destinationRoom:x2}, tile=${allowedTile:x2}.");
                            if (dungeon)
                                FailIf(rom[0xcc3b] != (down ? 0 : 1) || rom[0xcc3a] != 0x1b,
                                    $"Native Jabu travel down={down} must change only the floor, retaining map cell $1b.");
                            rom.ApplyRequestedWarp();
                        }
                    }
                    edge = 0;
                    string context = $"Surfacing {group:x1}:{sourceRoom:x2} B=${equippedB:x2} batched={batched} update={++update}";
                    FailIf(_rooms.ActiveGroup != rom[0xcc2d] || _currentRoom.Id != rom[0xcc30] ||
                        _player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f),
                        context + $": room/fixed XY differ: runtime={_rooms.ActiveGroup:x1}:{_currentRoom.Id:x2}/{_player.PrecisePosition}, native={rom[0xcc2d]:x1}:{rom[0xcc30]:x2}/{rom.Word(0xd00c):x4},{rom.Word(0xd00a):x4}; flags=${_currentRoom.TilesetFlags:x2}/${rom[0xcc34]:x2}, tile=${_terrain.GetActiveTerrain(_player.Position).Terrain.Tile:x2}/${rom[0xcf00 + rom[0xcc99]]:x2}, type={_terrain.GetActiveTerrain(_player.Position).Terrain.Type}, native velocity=${rom[0xd009]:x2}/${rom[0xd010]:x2}, held=${held:x2}.");
                    if (!loaded)
                    {
                        if (equippedB != 0)
                        {
                            FailIf(_player.NativeItemUseActive || _player.IsUsingShield ||
                                _entities.BombchuParent.Active || _bracelet.State != BraceletState.Idle || _bomb.Active ||
                                Enumerable.Range(0xd2, 10).Any(page => rom[page << 8] != 0),
                                context + ": B allocated an underwater parent/child instead of remaining reserved for travel.");
                            FailIf(!Enumerable.Range(0xc6b0, 14).Select(_saveData.ReadWramByte).SequenceEqual(ammo) ||
                                !Enumerable.Range(0xc6b0, 14).Select(address => rom[address]).SequenceEqual(ammo),
                                context + ": reserved B spent item ammunition.");
                            // The fixture injects text activity into the ROM;
                            // only the runtime renders this literal message.
                            FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(
                                rom.Sounds.Where(id => id != SoundId.SndText)),
                                context + $": reserved B changed native sound order: runtime={string.Join(',', sounds.Requests)}, ROM={string.Join(',', rom.Sounds)}.");
                        }
                        FailIf(_transitions.IsTransitioning != (rom[0xc2ef] == 3) || _player.TopDownSwimming,
                            context + ": source dry-underwater state or fade request differs.");
                        if (rom[0xc2ef] == 3)
                            FailIf(!Mathf.IsEqualApprox(_warpFade.Color.A, System.Math.Min((int)rom[0xc2ff], 31) / 31f),
                                context + ": native fade-out offset differs.");
                        var random = _random.CaptureState();
                        FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                            random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": source shared RNG differs.");
                        FailIf(!sounds.Requests.Where(id => id == SoundId.SndSplash).SequenceEqual(
                            rom.Sounds.Where(id => id == SoundId.SndSplash)), context + ": underwater splash sound order differs.");
                    }
                    else
                    {
                        FailIf(!Mathf.IsEqualApprox(_warpFade.Color.A, System.Math.Min((int)rom[0xc2ff], 31) / 31f),
                            context + ": arrival palette offset differs.");
                        if (rom[0xd004] == 1)
                            FailIf(CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008] ||
                                _player.TopDownSwimmingState != (rom[0xcc5d] & 15),
                                context + $": initialized destination facing/swimming differs: runtime={CarriedObjectMotion.DirectionIndex(_player.FacingVector)}/{_player.TopDownSwimmingState}, native={rom[0xd008]}/{rom[0xcc5d] & 15}, mode=${rom[0xc4ab]:x2}, Link=${rom[0xd004]:x2}/${rom[0xd005]:x2}, tile=${_currentRoom.GetMetatile(_player.Position):x2}.");
                    }
                    FailIf(sounds.Requests.Contains(SoundId.SndEnterCave) || rom.Sounds.Contains(SoundId.SndEnterCave),
                        context + ": direct surfacing incorrectly played entrance sound $6e.");
                });
            }
            Step(6);
            FailIf(_terrain.GetActiveTerrain(_player.Position).PackedPosition != blockedTile ||
                blockedTile == _currentRoom.GetPackedPosition(_player.Position),
                "Surfacing fixture must use a blocked foot tile distinct from Link's center tile.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(1, 2); Step(3, 2); Step(3);
                FailIf(_transitions.IsTransitioning, "Blocked surfacing tile retained a pending warp.");
            }
            int began = update;
            while (_terrain.GetActiveTerrain(_player.Position).PackedPosition != allowedTile && update - began < 240)
            { Step(); Step(5, 0x10); }
            FailIf(_terrain.GetActiveTerrain(_player.Position).PackedPosition != allowedTile,
                "Allowed surfacing tile was unreachable through the dry-floor room geometry.");
            Step(60);
            foreach (int mask in new[] { 1, 0x80 })
            {
                Lock(mask); Step(1, 2); Step(3);
                FailIf(_transitions.IsTransitioning, $"Screen-transition mask ${mask:x2} did not suppress surfacing.");
            }
            Lock(0);
            _dialogue.ShowMessage("Surfacing handoff pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(6, 2);
            _dialogue.Close(); rom[0xcba0] = 0;
            Step(3, 2);
            FailIf(_transitions.IsTransitioning, "Dialogue or a consumed B edge requested surfacing after text closed.");
            Step(2);
            if (whirlpool)
            {
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    // The underlying surfacing mask allows this tile. Native
                    // raw TILETYPE_WHIRLPOOL rejects B before that lookup.
                    Step(1, 0x12); Step(6, 0x12); Step(60);
                    FailIf(_transitions.IsTransitioning || captured != -1 || _player.TopDownSwimming || _player.TopDownDiving,
                        "Underwater whirlpool must reject fresh/held B without entering surface swimming or leaving a warp request.");
                }
                continue;
            }
            Vector2 sourcePosition = _player.PrecisePosition;
            int soundCount = sounds.Requests.Count;
            Step(1, 0x22); // Travel returns before the newly pressed left-direction impulse.
            FailIf(!_transitions.IsTransitioning || captured != allowedTile || _player.PrecisePosition != sourcePosition ||
                _warpFade.Color.A != 0 || sounds.Requests.Count != soundCount,
                $"Surfacing request transition={_transitions.IsTransitioning}, captured=${captured:x2}, XY={_player.PrecisePosition}/{sourcePosition}, fade={_warpFade.Color.A}, sounds={sounds.Requests.Count}/{soundCount}.");
            Step(31, 0x22);
            FailIf(loaded || _warpFade.Color.A != 1, "Surfacing loaded before the terminal fade-out update.");
            Step(1, 0x22);
            FailIf(!loaded || fadeUpdates != 32 ||
                _player.PrecisePosition != new Vector2((allowedTile & 15) * 16 + 8, (allowedTile >> 4) * 16 + 8),
                "Surfacing did not clear fractions and spawn at the captured foot tile's center.");
            Step(32);
            FailIf(!_transitions.PaletteFadeActive || rom[0xc4ab] == 0 || _warpFade.Color.A != 0,
                "Surfacing arrival must remain active on the visible-zero palette update.");
            Step();
            FailIf(_transitions.IsTransitioning, "Surfacing did not finish the arrival fade on update 33.");
            Step(4);
        }
        GD.Print($"Validated clean-US underwater travel dungeon={dungeon}, down={down}, whirlpool={whirlpool}, B=${equippedB:x2}: reachable foot tiles, B edge/dialogue/lock gates and split/batched native gameplay handoff.");
    }
}
