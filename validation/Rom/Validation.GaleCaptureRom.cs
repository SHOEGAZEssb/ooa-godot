using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGaleCaptureRom() => ValidateGaleCaptureRom(false);
    private void ValidateGaleCaptureGatesRom() => ValidateGaleCaptureRom(true);
    private void ValidateGaleMenuAcceptRom() => ValidateGaleCaptureRom(false, true);
    private void ValidateGaleAcceptedArrivalRom() => ValidateGaleCaptureRom(false, true, true);
    private void ValidateGaleAllDestinationsRom() => ValidateGaleCaptureRom(false, true, true, true);
    private void ValidateGaleArrivalMenuGatesRom()
    {
        foreach (int elapsed in new[] { 33, 34, 64, 65, 66, 98, 99, 100 })
        foreach (int key in new[] { 4, 8, 12 })
            ValidateGaleCaptureRom(false, true, true, arrivalMenuProbe: elapsed, probeKey: key);
    }
    private void ValidateGaleArrivalMenuResumeRom()
    {
        foreach (int elapsed in new[] { 34, 64, 100 })
        foreach (int key in new[] { 4, 8, 12 })
            ValidateGaleCaptureRom(false, true, true, arrivalMenuProbe: elapsed, probeKey: key, resumeMenu: true);
    }

    private void ValidateGaleCaptureRom(bool warpGate, bool acceptDestination = false, bool completeArrival = false,
        bool allDestinations = false, int arrivalMenuProbe = 0, int probeKey = 0, bool resumeMenu = false)
    {
        int hostCase1 = 0;
        foreach (int warpMask in warpGate ? new[] { 1, 0x80, 0xff } : new[] { 0 })
        foreach (bool primary in allDestinations || arrivalMenuProbe != 0 ? new[] { true } : new[] { false, true })
        foreach (int group in new[] { 0, 1 })
        foreach (int direction in allDestinations || arrivalMenuProbe != 0 ? new[] { 0 } : Enumerable.Range(0, 4))
        // Independent literals from data/ages/treeWarps.s, including the
        // planted present-era entry. Each fixture visits only its target.
        foreach (var destination in allDestinations
            ? group == 0 ? new (int Room, int Position)[] { (0xac, 0x54), (0x13, 0x55), (0x78, 0x55), (0xc1, 0x34) }
                : new (int Room, int Position)[] { (0x08, 0x43), (0x25, 0x44), (0x2d, 0x36), (0x78, 0x55), (0x80, 0x36), (0xc1, 0x34) }
            : new[] { (Room: group == 0 ? 0x13 : 0x08, Position: group == 0 ? 0x55 : 0x43) })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            foreach (int room in new[] { 0xac, 0x13, 0x08, 0x25, 0x2d, 0x78, 0x80, 0xc1 })
                _saveData.SetRoomFlag(group, room, 0xff, false);
            if (group == 0 && destination.Room == 0xac) _saveData.SetRoomFlag(0, 0xac, 0x80);
            _saveData.SetRoomFlag(group, destination.Room, OracleSaveData.RoomFlagVisited);
            LoadValidationRoom(group, 0x33); _entities.Clear();
            FailIf((_currentRoom.TilesetFlags & 1) == 0, "Gale capture requires the original outdoor tileset flag.");
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            _inventory.GiveTreasure(0x23, 0x20);
            _inventory.SelectSatchelSeeds(3);
            _inventory.EquipA(primary ? TreasureId.SeedSatchel : 0);
            _inventory.EquipB(primary ? 0 : TreasureId.SeedSatchel);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0, 0, 0);
            Vector2 facing = OracleObjectMovement.Shared.Direction(direction * 8);
            _player.WarpTo(new(80.25f, 64.5f)); _player.Face(new((int)facing.X, (int)facing.Y));
            var randomSeed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, randomSeed, _currentRoom, direction, 80, 64);
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40; rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            var menus = rom.CreateMenuView();
            int button = primary ? 1 : 2, update = 0, holdUpdates = 0, ascentUpdates = 0;
            bool returning = false;
            int acceptedAt = 0;
            bool destinationLoaded = false;
            bool arrivalMenuStarted = false;
            var fallZ = typeof(Player).GetField("_roomWarpFallZFixed", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var fallSpeed = typeof(Player).GetField("_roomWarpFallSpeedZ", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var fallCounter = typeof(Player).GetField("_roomWarpFallCollapsedCounter", BindingFlags.Instance | BindingFlags.NonPublic)!;
            void Step(int count = 1, int press = 0)
            {
                int edge = press;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(press), MenuRomActions(press), batched, () =>
                {
                    if (acceptedAt != 0)
                    {
                        rom.AdvanceWarpPalette();
                        if (destinationLoaded)
                        {
                            if (arrivalMenuProbe != 0 && rom[0xc2ef] == 1)
                                menus.Update(edge, edge, _saveData.ReadWramByte(0xc622));
                            if (rom[0xcbcb] == 0)
                            {
                                rom.UpdateGameplay(edge, edge, 0xff, _entities.FrameCounter - 1);
                                if (allDestinations || arrivalMenuProbe != 0) rom.AdvanceArrivalRoomControl();
                            }
                        }
                        else rom.UpdateFadeOutWarp();
                    }
                    else if (rom[0xcbcb] != 0)
                    {
                        menus.AdvancePalette();
                        menus.Update(edge, edge, _saveData.ReadWramByte(0xc622));
                        menus.AdvanceText();
                        if (rom[0xcbcb] == 0)
                        {
                            if (acceptDestination && rom[0xcc4b] == 3)
                            {
                                rom.CompleteGaleMenuHandoff();
                                acceptedAt = update + 1;
                            }
                            else rom.UpdateGaleCancelGameplay(edge, edge, _entities.FrameCounter);
                        }
                    }
                    else rom.UpdateGameplay(edge, edge, 0xff, _entities.FrameCounter);
                    edge = 0;
                    string context = $"Gale capture group=${group:x2} target=${destination.Room:x2}/${destination.Position:x2} A={primary} dir={direction} warpMask=${warpMask:x2} update={++update}";
                    if (acceptedAt != 0)
                    {
                        int elapsed = update - acceptedAt;
                        if (arrivalMenuProbe != 0 && arrivalMenuProbe == elapsed)
                        {
                            // func_131f already selected state2/$01 during
                            // loading. cutscene00 omits menus on its first
                            // object update, then promotes the caller to $01.
                            bool expectedOpen = elapsed >= 34;
                            int expectedMenu = !expectedOpen ? 0 : probeKey == 12 ? 3 : probeKey == 8 ? 1 : 2;
                            FailIf(rom[0xcbcb] != expectedMenu,
                                context + $": native arrival caller gate differs at elapsed={elapsed}, key=${probeKey:x2}, menu=${rom[0xcbcb]:x2}, scroll=${rom[0xcd00]:x2}, screen=${rom[0xcd04]:x2}/${rom[0xcd05]:x2}/{rom[0xcd03]}, palette=${rom[0xc4ab]:x2}/${rom[0xc4ae]:x2}, caller=${rom[0xc2ef]:x2}, locks=${rom[0xcc02]:x2}/${rom[0xcbca]:x2}.");
                            FailIf(_menuLifecycle.IsActive != expectedOpen ||
                                _menuLifecycle.IsActive && _menuLifecycle.CurrentPhase != Phase.OpeningFadeOut ||
                                _inventoryMenu.IsActive != (expectedMenu is 1 or 3) ||
                                _mapMenu.IsActive != (expectedMenu == 2) ||
                                _gameplayPause.IsLeased != expectedOpen,
                                context + $": runtime arrival menu gate differs at elapsed={elapsed}, key=${probeKey:x2}, expectedMenu=${expectedMenu:x2}, active={_menuLifecycle.IsActive}.");
                            FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                                _player.ObjectZHigh != rom[0xd00f], context + ": arrival menu opening advanced the frozen Link object.");
                            arrivalMenuStarted = expectedOpen;
                            return;
                        }
                        if (resumeMenu && arrivalMenuStarted && rom[0xcbcb] != 0)
                        {
                            Phase expectedPhase = rom[0xcbcc] switch
                            {
                                0 => Phase.OpeningFadeOut,
                                1 => rom[0xc4ab] != 0 ? Phase.OpeningFadeIn : Phase.Open,
                                2 => Phase.ClosingFadeOut,
                                3 => Phase.ClosingFadeIn,
                                _ => throw new System.InvalidOperationException(context + ": unexpected arrival menu phase.")
                            };
                            FailIf(_menuLifecycle.CurrentPhase != expectedPhase || !_gameplayPause.IsLeased ||
                                _player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                                _player.ObjectZHigh != rom[0xd00f] ||
                                _player.IsRoomWarpFalling && (((int)fallZ.GetValue(_player)! & 0xffff) != rom.Word(0xd00e) ||
                                    ((int)fallSpeed.GetValue(_player)! & 0xffff) != rom.Word(0xd014)) ||
                                _player.RoomWarpFallCollapsed && (int)fallCounter.GetValue(_player)! != rom[0xd006],
                                context + ": arrival menu phase/pause/fixed fall/collapse retention differs.");
                            if (_menuLifecycle.FadeUpdate > 0 && expectedPhase is Phase.OpeningFadeOut or Phase.OpeningFadeIn or Phase.ClosingFadeOut or Phase.ClosingFadeIn)
                                FailIf(Mathf.Clamp(Mathf.RoundToInt((_warpFade.Color.A + _scene.MenuFade.Color.A) * 31), 0, 31) != Mathf.Clamp(rom[0xc2ff], 0, 31),
                                    context + $": retained warp overlay conflicts with the menu palette: warp={_warpFade.Color.A}, menu={_scene.MenuFade.Color.A}, native=${rom[0xc2ff]:x2}.");
                            return;
                        }
                        if (resumeMenu && arrivalMenuStarted)
                            FailIf(_transitions.PaletteFadeActive || _warpFade.Color.A != 0 || _scene.MenuFade.Color.A != 0,
                                context + ": closing the arrival menu resumed its superseded warp palette.");
                        if (elapsed < 32)
                        {
                            FailIf(_rooms.ActiveGroup != group || _rooms.CurrentRoom.Id != 0x33 ||
                                _player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                                _player.ObjectZHigh != rom[0xd00f], context + ": acceptance moved Link or loaded the destination before white.");
                            FailIf(Mathf.RoundToInt(_scene.MenuFade.Color.A * 31) != rom[0xc2ff],
                                context + ": accepted warp palette offset differs.");
                        }
                        else
                        {
                            destinationLoaded = true;
                            FailIf(_rooms.ActiveGroup != rom[0xcc2d] || _rooms.CurrentRoom.Id != rom[0xcc30] ||
                                elapsed == 32 && _transitions.ActiveWarpDestinationPosition != rom[0xcc4a] ||
                                _mapMenu.IsActive || _gameplayPause.IsLeased,
                                context + $": accepted destination/white handoff differs, runtime=${_rooms.ActiveGroup:x1}:${_rooms.CurrentRoom.Id:x2}/${_transitions.ActiveWarpDestinationPosition:x2}, native=${rom[0xcc2d]:x1}:${rom[0xcc30]:x2}/${rom[0xcc4a]:x2}.");
                            if (completeArrival)
                            {
                                FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                                    _player.ObjectZHigh != rom[0xd00f] ||
                                    _player.IsRoomWarpFalling != (rom[0xd004] == 0x0a) ||
                                    CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008],
                                    context + $": arrival motion differs at elapsed={elapsed}, runtime={_player.PrecisePosition}/${_player.ObjectZHigh:x2}, native=({rom.Word(0xd00c) / 256f},{rom.Word(0xd00a) / 256f})/${rom[0xd00f]:x2}, Link=${rom[0xd004]:x2}/${rom[0xd005]:x2}/force=${rom[0xcc4f]:x2}.");
                                if (_player.IsRoomWarpFalling)
                                    FailIf(((int)fallZ.GetValue(_player)! & 0xffff) != rom.Word(0xd00e) ||
                                        ((int)fallSpeed.GetValue(_player)! & 0xffff) != rom.Word(0xd014) ||
                                        _player.RoomWarpFallCollapsed != (rom[0xd005] == 2) ||
                                        _player.RoomWarpFallCollapsed && (int)fallCounter.GetValue(_player)! != rom[0xd006],
                                        context + $": fixed fall/collapse differs at elapsed={elapsed}: Z=${(int)fallZ.GetValue(_player)! & 0xffff:x4}/${rom.Word(0xd00e):x4}, speed=${(int)fallSpeed.GetValue(_player)! & 0xffff:x4}/${rom.Word(0xd014):x4}, collapse={_player.RoomWarpFallCollapsed}, counter={(int)fallCounter.GetValue(_player)!}/${rom[0xd006]:x2}.");
                            }
                        }
                        return;
                    }
                    FailIf(_player.IsUsingSeedSatchel != Enumerable.Range(0xd2, 4).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x19),
                        context + ": Satchel parent retirement differs.");
                    FailIf(_player.GaleActive != (rom[0xcc4f] == 7 || rom[0xd004] == 7 || returning && rom[0xd004] == 0x0a), context + $": capture differs: runtime={_player.GaleActive}, native state={rom[0xd004]:x2}/{rom[0xd005]:x2}, force={rom[0xcc4f]:x2}, locks={rom[0xcc02]:x2}/{rom[0xcc8a]:x2}, collision={rom[0xd024]:x2}, air={rom[0xcc61]:x2}.");
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                        _player.ObjectZHigh != rom[0xd00f], context + $": Link position/height differs: runtime={_player.PrecisePosition}/${_player.ObjectZHigh:x2}, native=({rom.Word(0xd00c) / 256f},{rom.Word(0xd00a) / 256f})/${rom[0xd00f]:x2}, state={rom[0xd004]:x2}/{rom[0xd005]:x2}, menu={rom[0xcbcb]:x2}/{rom[0xcbcc]:x2}.");
                    if (rom[0xd004] == 7 && rom[0xd005] == 1)
                    {
                        int ticks = (int)typeof(Player).GetField("_galeFrameTicks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)!;
                        FailIf(ticks != rom[0xd020], context + $": Link spinning animation timing differs: runtime={ticks}, native={rom[0xd020]}.");
                    }
                    var items = _entities.Entities<EmberSeedEffect>();
                    int[] children = Enumerable.Range(0xd7, 5).Select(page => page << 8).Where(slot => rom[slot] != 0 && rom[slot + 1] == 0x23).ToArray();
                    FailIf(items.Count != children.Length, context + ": Gale child allocation/deletion differs.");
                    if (items.Count != 0)
                    {
                        var item = items.Single(); int slot = children.Single();
                        FailIf((item.State == EmberState.Flying ? 1 : 2) != rom[slot + 4] || item.PrecisePosition != new Vector2(rom.Word(slot + 0x0c) / 256f, rom.Word(slot + 0x0a) / 256f) ||
                            (item.ZFixed & 0xffff) != rom.Word(slot + 0x0e) || (item.SpeedZ & 0xffff) != rom.Word(slot + 0x14) ||
                            item.NativeAngle != rom[slot + 9] || item.Visible != ((rom[slot + 0x1a] & 0x80) != 0) || item.CollisionEnabled != ((rom[slot + 0x24] & 0x80) != 0),
                            context + ": Gale fixed flight/capture/ascent state differs.");
                        int counter = (int)typeof(EmberSeedEffect).GetField("_frameCounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(item)!;
                        FailIf(counter != rom[slot + 0x20] || item.State == EmberState.Gale &&
                            (item.FlameCounter != rom[slot + 6] || item.GaleSubstate != rom[slot + 5] || item.GaleCounter2 != rom[slot + 7] || item.GalePalette != rom[slot + 0x1c]),
                            context + ": Gale animation/palette/hold counter differs.");
                        if (item.GaleSubstate == 1) holdUpdates++;
                        if (item.GaleSubstate == 2) ascentUpdates++;
                    }
                    FailIf(_mapMenu.IsActive != (rom[0xcbcb] == 5) || _gameplayPause.IsLeased != (rom[0xcbcb] == 5),
                        context + ": MENU_GALE_SEED handoff/pause differs.");
                    if (rom[0xcbcb] == 5)
                    {
                        var expected = rom[0xcbcc] switch
                        {
                            0 => Phase.OpeningFadeOut,
                            1 => rom[0xc4ab] != 0 ? Phase.OpeningFadeIn : Phase.Open,
                            2 => Phase.ClosingFadeOut,
                            3 => Phase.ClosingFadeIn,
                            _ => throw new System.InvalidOperationException(context + ": unknown Gale menu load state.")
                        };
                        FailIf(_menuLifecycle.CurrentPhase != expected, context + ": Gale menu fade phase differs.");
                        if (_mapMenu.IsOpen)
                            FailIf(_mapMenu.GaleState != rom[0xcbcd] || _mapScreen.CursorRoom != rom[0xcbb6],
                                context + $": Gale selection/prompt state differs: runtime={_mapMenu.GaleState}/${_mapScreen.CursorRoom:x2}/text={_dialogue.BlocksPlayerInput}, native={rom[0xcbcd]}/${rom[0xcbb6]:x2}/text={rom[0xcba0]:x2}/state={menus.TextState:x2}/option={rom[0xcba5]}.");
                    }
                    for (int address = 0xc6b9; address <= 0xc6bd; address++)
                        FailIf(_saveData.ReadWramByte(address) != rom[address], context + $": ammo ${address:x4} differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - randomSeed.Calls != rom.RandomCalls, context + ": RNG differs.");
                    FailIf(!sounds.Requests.Where(id => id is 0x52 or 0x90).SequenceEqual(rom.Sounds.Where(id => id is 0x52 or 0x90)), context + ": Gale sounds differ.");
                });
            }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                returning = false;
                holdUpdates = ascentUpdates = 0;
                int attemptBegan = update;
                _entities.RuntimeState.SetWramByte(WramAddress.wWarpsDisabled, (byte)warpMask);
                rom[0xcc6e] = (byte)warpMask;
                Step(1, button); Step(3);
                _dialogue.ShowMessage("Gale flight pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6); _dialogue.Close(); rom[0xcba0] = 0;
                if (warpGate)
                {
                    while (!_entities.Entities<EmberSeedEffect>().Any(item => item.State == EmberState.Gale) && update - attemptBegan < 40) Step();
                    Step(20);
                    FailIf(_player.GaleActive || _mapMenu.IsActive || _entities.Entities<EmberSeedEffect>().Single().GaleSubstate != 0,
                        $"wWarpsDisabled ${warpMask:x2} did not retain the outdoor Gale's waiting state.");
                    _entities.RuntimeState.SetWramByte(WramAddress.wWarpsDisabled, 0);
                    rom[0xcc6e] = 0;
                }
                while (!_player.GaleActive && update - attemptBegan < 80) Step();
                FailIf(!_player.GaleActive, "Outdoor Gale did not capture Link after landing.");
                Step(3);
                _dialogue.ShowMessage("Gale hold pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6); _dialogue.Close(); rom[0xcba0] = 0;
                while (!_mapMenu.IsActive && update - attemptBegan < 220) Step(1);
                FailIf(!_mapMenu.IsActive || holdUpdates - 6 != 60 || ascentUpdates != 64 || rom[0xcc04] != 0x16 || rom[0xd005] != 2,
                    $"Outdoor Gale handoff missed the $3c hold/64 negative Z steps/CUTSCENE$16: hold={holdUpdates}, ascent={ascentUpdates}.");
                Step(22);
                FailIf(!_mapMenu.IsOpen, "Gale menu did not finish its native opening fades.");
                if (acceptDestination)
                {
                    Step(1, 1);
                    int acceptPromptBegan = update;
                    while ((!_dialogue.ChoiceCursorVisible || menus.TextState != 2) && update - acceptPromptBegan < 300) Step();
                    FailIf(!_dialogue.ChoiceCursorVisible || menus.TextState != 2, "Gale destination prompt did not reach its native input gate.");
                    Step(1, 1);
                    while (acceptedAt == 0 && update - acceptPromptBegan < 320) Step();
                    FailIf(acceptedAt == 0 || rom[0xc2ef] != 3 || rom[0xcc49] != 5 || rom[0xcc47] != (group | 0x80) ||
                        rom[0xcc48] != destination.Room || rom[0xcc4a] != destination.Position,
                        "Accepted Gale destination did not publish its original warp/cutscene request.");
                    Step(31);
                    FailIf(destinationLoaded, "Accepted Gale destination loaded before the terminal palette update.");
                    Step();
                    FailIf(!destinationLoaded, "Accepted Gale destination did not load at full white.");
                    if (completeArrival)
                    {
                        if (arrivalMenuProbe != 0)
                        {
                            while (update - acceptedAt < arrivalMenuProbe - 1) Step();
                            Step(1, probeKey);
                            if (!resumeMenu) break;
                            Step(22);
                            FailIf(_menuLifecycle.CurrentPhase != Phase.Open, "Arrival pause menu did not finish opening.");
                            Step(1, probeKey == 8 ? 8 : 2);
                            Step(22);
                            FailIf(_menuLifecycle.IsActive || _gameplayPause.IsLeased,
                                "Arrival pause menu did not release its closing-update ownership.");
                        }
                        int arrivalBegan = update;
                        while (_transitions.IsTransitioning && update - arrivalBegan < 240) Step();
                        FailIf(_transitions.IsTransitioning || rom[0xd004] != 1,
                            "Accepted Gale arrival did not complete its native fall and collapse.");
                        Step(4);
                    }
                    break;
                }
                Step(1, 2);
                int readyBegan = update;
                while ((!_dialogue.ChoiceCursorVisible || menus.TextState != 2) && update - readyBegan < 180) Step();
                FailIf(!_dialogue.ChoiceCursorVisible || menus.TextState != 2, "Gale reselect options did not become input-ready.");
                Step(1, 1); Step(4);
                FailIf(_dialogue.BlocksPlayerInput || rom[0xcba0] != 0, "Gale reselect prompt did not close after confirming its initial option.");
                Step();
                FailIf(_mapMenu.GaleState != 1, "Gale reselect confirmation did not return to navigation.");
                Step(1, 2);
                readyBegan = update;
                while ((!_dialogue.ChoiceCursorVisible || menus.TextState != 2) && update - readyBegan < 180) Step();
                FailIf(!_dialogue.ChoiceCursorVisible || menus.TextState != 2, "Gale cancel options did not become input-ready.");
                Step(1, 2); Step(); Step(1, 1); Step(4);
                FailIf(_dialogue.BlocksPlayerInput || rom[0xcba0] != 0, "Gale cancel prompt did not close through native B selection.");
                returning = true;
                int began = update;
                while (_mapMenu.IsActive && update - began < 220) Step();
                while (_player.GaleActive && update - began < 320) Step(3);
                FailIf(_mapMenu.IsActive || _player.GaleActive || _player.PrecisePosition != new Vector2(80.25f, 64.5f),
                    "Gale cancel did not release the menu and fall to the original world position.");
                Step(4);
            }
        }
        GD.Print(acceptDestination
            ? $"Validated {(allDestinations ? 11 : arrivalMenuProbe != 0 ? 3 : 17)} clean-US Gale accepted-travel fixtures: both eras, actual capture/menu confirmation, independently asserted treeWarps.s room/position, source warp request, 32-update fade, frozen Link and destination loading at white; allDestinations={allDestinations}, completeArrival={completeArrival}, menuProbe={arrivalMenuProbe}/${probeKey:x2}, resumeMenu={resumeMenu}."
            : "Validated clean-US outdoor Gale A/B/four facings, present/past capture, Link dispatch/spinning, fixed capture/hold/ascent counters/animation/palette/visibility, dialogue, ammo/RNG/sounds, MENU_GALE_SEED fades/reselect/cancel, exact return fall and repeat through split/batched gameplay.");
    }
}
