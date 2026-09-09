using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom034Interactions()
    {
        FailIf(_inventory.AnimalCompanion != 0, "Room 0:34 default-companion fixture must start unassigned.");
        bool hadFlute = _inventory.HasTreasure(0x0e);
        _saveData.SetGlobalFlag(0x22, false);
        _saveData.SetGlobalFlag(0x1d);
        for (int address = 0xcfd0; address < 0xcfe0; address++)
            _entities.RuntimeState.SetWramByte(address, 0xff);
        LoadValidationRoom(0, 0x34);
        FailIf(_inventory.AnimalCompanion != 0x0d || _saveData.ReadWramByte(0xc610) != 0x0d ||
            _inventory.HasTreasure(0x0e) != hadFlute || _saveData.HasGlobalFlag(0x1d) ||
            _roomEvents.Get<CompanionForestEvent>().HasState,
            "Room 0:34 must assign Moosh without a flute and clear CAN_BUY_FLUTE before its progress guard.");
        for (int address = 0xcfd0; address < 0xcfe0; address++)
            FailIf(_entities.RuntimeState.ReadWramByte(address) != 0,
                $"Room 0:34 did not clear forest scratch ${address:x4}.");

        var data = new CompanionForestDatabase();
        // Unterminated source records supply their own newline; their next
        // record starts at column zero, with no added blank line or space.
        foreach (var boundary in new[] { (0x1137, 6), (0x113e, 9), (0x1145, 9) })
        {
            _dialogue.ShowMessage(data.Text(boundary.Item1), _player.Position.Y);
            FailIf(_dialogue.GlyphCodeForValidation(1, boundary.Item2, 0) != 'P',
                $"TX_{boundary.Item1:x4} flute instructions did not start at column zero after fallthrough.");
            _dialogue.Close();
        }
        foreach (int companion in new[] { 0x0b, 0x0c, 0x0d })
        {
            _inventory.AssignAnimalCompanion(companion);
            _saveData.SetGlobalFlag(0x22);
            _saveData.SetGlobalFlag(0x42, false);
            _saveData.SetGlobalFlag(0x2b);
            _saveData.SetRoomFlag(0, 0x34, 0x40, false);
            LoadValidationRoom(0, 0x35);
            _player.WarpTo(new Vector2(1, 0x48), recordSafe: false);
            _transitions.BeginScroll(_player, Vector2I.Left, 0x34);
            for (int frame = 0; frame < 60 && _transitions.ScrollActive; frame++)
            {
                FailIf(_dialogue.IsOpen || _roomEvents.Get<CompanionForestEvent>().Flights.Count != 0,
                    "Room 0:34 fairy advanced during scrolling.");
                _transitions.UpdateScroll(1.0 / 60.0);
                _entities.Update(1.0 / 60.0, _player);
                _roomEvents.Update(1.0 / 60.0);
            }
            _player.WarpTo(new Vector2(0x50, 0x48), recordSafe: false);
            StepRoomEventFrames(1);
            FailIf(_roomEvents.Get<CompanionForestEvent>().Flights.Count != 0, "Room 0:34 triggered at Link.xh == $50.");
            _player.WarpTo(new Vector2(0x4f, 0x48), recordSafe: false);
            int choices = 0;
            int messages = 0;
            string description = DialogueBox.PlainText(data.Text(0x1123 + companion - 0x0b));
            for (int frame = 0; frame < 1200 && !_saveData.HasGlobalFlag(0x42); frame++)
            {
                if (_dialogue.IsOpen)
                {
                    messages++;
                    if (_dialogue.CurrentMessage.Contains("Can you help"))
                    {
                        FailIf(!_dialogue.CurrentMessage.Contains(description),
                            $"Room 0:34 selected the wrong description for companion ${companion:x2}.");
                        FailIf(_dialogue.GlyphCodeForValidation(0, 2, 0) != description[0],
                            "TX_1121 -> TX_1122 inserted whitespace before the companion description.");
                        _dialogue.SubmitChoiceForValidation(choices++ == 0 ? 1 : 0);
                    }
                    else _dialogue.Close();
                }
                StepRoomEventFrames(1);
            }
            FailIf(messages != 4 || choices != 2 || !_saveData.HasGlobalFlag(0x42) ||
                _saveData.HasGlobalFlag(0x2b) || !_saveData.HasRoomFlag(0, 0x34, 0x40) ||
                _inventory.AnimalCompanion != companion || _roomEvents.Get<CompanionForestEvent>().MenusDisabled,
                $"Room 0:34 failed its fairy dialogue/departure for companion ${companion:x2}.");
            LoadValidationRoom(0, 0x34);
            FailIf(_roomEvents.Get<CompanionForestEvent>().HasState, "Completed room 0:34 introduction restarted.");
        }
        GD.Print("Validated room 0:34 default assignment, all companion descriptions, fairy sequence and persistent flags.");
    }

    private void ValidateDimitriForestRescue()
        => ValidateDimitriForestRescueRoute(linked: false);

    private void ValidateDimitriForestRescueLinked()
        => ValidateDimitriForestRescueRoute(linked: true);

    private void ValidateRickyForestQuest() => ValidateDimitriForestRescueRoute(false, 0x0b, true);
    private void ValidateRickyForestQuestLinked() => ValidateDimitriForestRescueRoute(true, 0x0b, true);
    private void ValidateMooshForestQuest() => ValidateDimitriForestRescueRoute(false, 0x0d, true);
    private void ValidateMooshForestQuestLinked() => ValidateDimitriForestRescueRoute(true, 0x0d, true);
    private void ValidateDimitriForestQuest() => ValidateDimitriForestRescueRoute(false, 0x0c, true);
    private void ValidateDimitriForestQuestLinked() => ValidateDimitriForestRescueRoute(true, 0x0c, true);

    private void ValidateDimitriForestRescueRoute(bool linked, int companion = 0x0c, bool fullQuest = false)
    {
        _saveData.SetLinkedGame(linked);
        _saveData.SetGlobalFlag(0x22);
        _saveData.SetGlobalFlag(0x42);
        _saveData.SetGlobalFlag(0x23, false);
        _saveData.SetGlobalFlag(0x24, false);
        int stateAddress = 0xc646 + companion - 0x0b;
        _saveData.WriteWramByte(stateAddress, 0x60);
        if (companion == 0x0d && fullQuest) _inventory.AssignAnimalCompanion(companion);
        else _inventory.GiveTreasure(0x0e, companion);
        bool hadFlute = _inventory.HasTreasure(0x0e);
        if (fullQuest) ValidateForestQuestIntroduction();
        LoadValidationRoom(0, 0x81);
        var animal = _entities.EntityAdapters<IRoomEntity>().OfType<IForestCompanion>().Single();
        FailIf(((IRoomEntity)animal).Node.Position != new Vector2(0x50, 0x58),
            "Forest companion spawner $67:$04 did not use preset $58/$50.");
        _player.WarpTo(new Vector2(0x50, 0x68), recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(!animal.TryInteract(_player), $"Forest companion ${companion:x2} did not accept the source A-button handoff.");
        var messages = new List<string>();
        bool reward = false;
        for (int update = 0; update < 2400 && !_saveData.HasGlobalFlag(0x23); update++)
        {
            if (_dialogue.IsOpen)
            {
                messages.Add(_dialogue.CurrentMessage);
                var frozen = _roomEvents.Get<CompanionForestEvent>().Flights.Select(flight =>
                    (flight.XFixed, flight.YFixed, flight.Angle, flight.Counter1, flight.Counter2)).ToArray();
                for (int paused = 0; paused < 12; paused++) _roomEvents.Update(1.0 / 60.0);
                FailIf(!frozen.SequenceEqual(_roomEvents.Get<CompanionForestEvent>().Flights.Select(flight =>
                    (flight.XFixed, flight.YFixed, flight.Angle, flight.Counter1, flight.Counter2))),
                    "Companion forest fairy movement advanced while text was active.");
                if (_saveData.ReadWramByte(0xc6b5) == companion - 0x0a && _player.IsHoldingItemTwoHands)
                {
                    reward = true;
                    FailIf((_saveData.ReadWramByte(stateAddress) & 0x80) == 0 ||
                        !_entities.Entities<NpcCharacter>().Any(npc => npc.Name == "CompanionFluteReward" && npc.Visible),
                        "Dimitri flute reward lost its native companion bit or overhead graphic.");
                }
                _dialogue.Close();
            }
            if (IsTransitioning) UpdateRoomWarpTransition(1.0 / 60.0);
            else
            {
                _player.AdvanceApplicationUpdate();
                StepRoomEventFrames(1);
            }
        }
        FailIf(!_saveData.HasGlobalFlag(0x24) || !_saveData.HasGlobalFlag(0x23) ||
            _rooms.CurrentRoom.Id != 0x63 || !reward,
            $"Forest rescue/flute handoff failed: room 0:{_rooms.CurrentRoom.Id:x2}, command {_roomEvents.Get<CompanionForestEvent>().Instruction}, signal {_roomEvents.Get<CompanionForestEvent>().Signal}, messages {messages.Count}.");
        for (int update = 0; update < 180 && !_saveData.HasGlobalFlag(0x2b); update++)
        {
            if (_dialogue.IsOpen) _dialogue.Close();
            _player.AdvanceApplicationUpdate(); StepRoomEventFrames(1);
        }
        FailIf(!_saveData.HasGlobalFlag(0x2b) || !_player.CompanionRideActive ||
            _roomEvents.Get<CompanionForestEvent>().MenusDisabled,
            "Dimitri flute script did not wait for mounting before unscrambling the forest and releasing menus.");
        var text = new CompanionForestDatabase();
        int shift = (companion - 0x0b) * 7;
        int[] expectedText = [0x1133 + shift, (linked ? 0x1135 : 0x1134) + shift,
            0x112a, 0x112b, 0x112c, 0x112d, 0x112e, 0x112f,
            (linked ? 0x1137 : 0x1136) + shift,
            (hadFlute ? 0x0069 : 0x0038) + companion - 0x0b, 0x1139 + shift];
        FailIf(!messages.SequenceEqual(expectedText.Select(id => DialogueBox.PlainText(text.Text(id)))),
            $"Companion ${companion:x2} forest rescue/reward diverged from its eleven source texts (observed {messages.Count}).");
        FailIf(!OracleSaveData.TryDeserialize(_saveData.Serialize(), out var restored) || restored is null ||
            !restored.HasGlobalFlag(0x23) || !restored.HasGlobalFlag(0x24) || !restored.HasGlobalFlag(0x2b) ||
            restored.ReadWramByte(0xc610) != companion || restored.ReadWramByte(0xc6b5) != companion - 0x0a ||
            (restored.ReadWramByte(stateAddress) & 0x80) == 0,
            $"Companion ${companion:x2} forest completion did not survive save-image serialization.");
        if (fullQuest)
        {
            foreach (int room in new[] { 0x63, 0x81, 0x82 })
            {
                LoadValidationRoom(0, room);
                FailIf(_roomEvents.Get<CompanionForestEvent>().HasState ||
                    _entities.Entities<NpcCharacter>().Any(npc => npc.Active && npc.Record.Id == 0x49),
                    $"Completed forest quest restarted an interaction in room 0:{room:x2}.");
            }
            ValidateAwardedCompanionFlute(companion);
        }
        GD.Print($"Validated companion ${companion:x2} forest quest (linked={linked}), fairy signals, flute reward, mount-gated reset and persistence.");
    }

    private void ValidateDebugRickyFlute() => ValidateDebugCompanionFlute(0x0b, primaryButton: true);
    private void ValidateFlutePresentation()
    {
        // treasureDisplayData_flute: Strange, Ricky, Dimitri, Moosh.
        int[] palettes = [0, 3, 2, 1];
        for (int icon = 0; icon < 4; icon++)
        {
            _saveData.WriteWramByte(0xc6b5, (byte)icon);
            DisplayRecord display = _treasures.GetButtonDisplay(InventoryState.ItemFlute, _inventory);
            FailIf(display.LeftSprite != 0x8b || display.RightSprite != 0x8c + icon ||
                display.LeftPalette != palettes[icon] || display.RightPalette != palettes[icon] ||
                display.TextLow != 0x2e + icon || display.ExtraMode != 0xff ||
                string.IsNullOrWhiteSpace(_treasures.GetInventoryText(display.TextLow).Message),
                $"Flute icon ${icon:x2} did not select its imported inventory/HUD graphics, palette and TX_09{0x2e + icon:x2} text.");
        }

        IntroSpriteFrame[] frames = new NewGameIntroDatabase().SpriteFrames("link-flute-item");
        int[] durations = [35, 60, 35, 35, 60, 30, 127];
        int[] firstTiles = [0x118, 0x11c, 0x118, 0x11a, 0x11e, 0x11a, 0x11a];
        var renderer = new CutsceneSpriteRenderer();
        Image source = OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/spr_link.png");
        Color[] colors = [Colors.Transparent, Colors.Black,
            new Color(2 / 31f, 21 / 31f, 8 / 31f), new Color(1, 26 / 31f, 17 / 31f)];
        for (int index = 0; index < frames.Length; index++)
        {
            IntroSpriteFrame frame = frames[index];
            FailIf(frame.Duration != durations[index] || frame.Parts[0].Tile != firstTiles[index],
                $"Flute animation frame ${index:x2} lost its source timing or absolute graphics tile.");
            foreach (IntroOamPart part in frame.Parts)
            {
                Image rendered = renderer.CellTexture(frame, part).GetImage();
                for (int y = 0; y < 16; y++)
                for (int x = 0; x < 8; x++)
                {
                    int cell = part.Tile / 2;
                    int sx = cell % 16 * 8 + ((part.Flags & 0x20) != 0 ? 7 - x : x);
                    int sy = cell / 16 * 16 + y;
                    float shade = source.GetPixel(sx, sy).R;
                    Color expected = colors[shade < 0.1f ? 0 : shade < 0.5f ? 1 : shade < 0.9f ? 2 : 3];
                    Color actual = rendered.GetPixel(x, y);
                    FailIf((actual - expected).R * (actual - expected).R +
                        (actual - expected).G * (actual - expected).G +
                        (actual - expected).B * (actual - expected).B +
                        (actual - expected).A * (actual - expected).A > 0.0001f,
                        $"Flute frame ${index:x2}, tile ${part.Tile:x3}, pixel {x},{y} did not render spr_link's original source pixels.");
                }
            }
        }
        var high = renderer.CellTexture(frames[0], frames[0].Parts[0]);
        var low = renderer.CellTexture(frames[0] with { BasePalette = 1 },
            frames[0].Parts[0] with { Tile = 0x18 });
        FailIf(ReferenceEquals(high, low), "Absolute tile $118 collided with tile $18/palette $01 in the sprite cache.");
        GD.Print("Validated all four flute inventory/HUD variants and every pixel of seven playing frames, including absolute source tiles and cache isolation.");
    }
    private void ValidateDebugDimitriFlute() => ValidateDebugCompanionFlute(0x0c, primaryButton: false);
    private void ValidateDebugMooshFlute() => ValidateDebugCompanionFlute(0x0d, primaryButton: true);

    private void ValidateDebugCompanionFlute(int companion, bool primaryButton)
    {
        // The retail treasure selects an animal but must remain a Strange Flute
        // until the quest writes wFluteIcon. F1 intentionally grants the upgrade.
        _inventory.GiveTreasure(InventoryState.ItemFlute, companion);
        FailIf(_saveData.ReadWramByte(0xc6b5) != 0,
            "Retail Strange Flute collection bypassed the forest unlock.");
        LoadValidationRoom(0, 0x2a);
        bool questBefore = _saveData.HasGlobalFlag(0x23);
        _debugFlagMenu.OpenImmediatelyForValidation();
        _debugFlagScreen.SelectTreasureForValidation($"TREASURE_OBJECT_FLUTE_{companion - 0x0b:x2}");
        _debugFlagScreen.ActivateSelection();
        _debugFlagMenu.CloseImmediatelyForValidation();
        FailIf(!_inventory.HasTreasure(InventoryState.ItemFlute) ||
            _inventory.AnimalCompanion != companion ||
            _saveData.ReadWramByte(0xc6b5) != companion - 0x0a ||
            _saveData.HasGlobalFlag(0x23) != questBefore,
            $"F1 did not upgrade the owned Strange Flute to callable companion ${companion:x2} without quest progress.");
        FailIf(!OracleSaveData.TryDeserialize(_saveData.Serialize(), out var restored) ||
            restored!.ReadWramByte(0xc6b5) != companion - 0x0a ||
            restored.ReadWramByte(0xc610) != companion,
            $"Callable flute ${companion:x2} did not survive save serialization.");
        ValidateAwardedCompanionFlute(companion, primaryButton);
        GD.Print($"Validated F1 callable flute ${companion:x2}, equipped {(primaryButton ? "A" : "B")} input, entrance, waiting, mounting and save state.");
    }

    private void ValidateAwardedCompanionFlute(int companion, bool primaryButton = false)
    {
        LoadValidationRoom(0, 0x2a);
        for (int y = 8; y < 128; y += 16)
        for (int x = 8; x < 160; x += 16)
            _rooms.CurrentRoom.SetPositionTileAndCollision(new Vector2(x, y), 0, 0, 0);
        _player.WarpTo(new Vector2(72, 100), recordSafe: false);
        if (primaryButton) _inventory.EquipA(InventoryState.ItemFlute);
        else _inventory.EquipB(InventoryState.ItemFlute);
        string button = primaryButton ? "attack" : "item";
        Input.BeginOriginalUpdate(new ApplicationInputSnapshot(pressed: [button], justPressed: [button], movement: Vector2.Zero));
        try { _player.AdvanceApplicationUpdate(); }
        finally { Input.EndOriginalUpdate(); }
        int duration = new FluteDatabase().Duration(companion - 0x0a);
        for (int update = 2; update <= duration; update++) _player.AdvanceHarpForValidation(1);
        var summoned = _entities.EntityAdapters<IRoomEntity>().OfType<IForestCompanion>().Single();
        var node = ((IRoomEntity)summoned).Node;
        FailIf(node.Position != new Vector2(72, -8), $"Awarded companion ${companion:x2} flute lost its source top-edge entrance.");
        _entities.Update(1.0 / 60.0, _player);
        FailIf(node.Position != new Vector2(72, -8), "Flute entrance moved on the initialization update.");
        for (int frame = 0; frame < 150; frame++) _entities.Update(1.0 / 60.0, _player);
        FailIf(node.Position.Y <= 0 || node.Position.Y >= 100 || summoned.LinkRiding,
            $"Companion ${companion:x2} failed to enter and wait after the awarded flute call: position={node.Position}, riding={summoned.LinkRiding}.");
        Vector2 arrived = node.Position;
        for (int frame = 0; frame < 20; frame++) _entities.Update(1.0 / 60.0, _player);
        FailIf(node.Position != arrived, $"Companion ${companion:x2} continued moving after its flute entrance.");
        _player.WarpTo(arrived, recordSafe: false);
        for (int frame = 0; frame < 100 && !summoned.LinkRiding; frame++)
        {
            // The suite shares one host frame. Do not replay another case's
            // Godot just-pressed A edge and start a second flute during mounting.
            Input.BeginOriginalUpdate(new ApplicationInputSnapshot(pressed: [], justPressed: [], movement: Vector2.Zero));
            try
            {
                _player.AdvanceApplicationUpdate();
                _entities.Update(1.0 / 60.0, _player);
            }
            finally { Input.EndOriginalUpdate(); }
        }
        FailIf(!summoned.LinkRiding, $"Companion ${companion:x2} could not be mounted after the flute call: " +
            $"canMount={_player.CanMountCompanion}, invincibility={_player.InvincibilityFrames}, " +
            $"swimming={_player.TopDownSwimming}, carrying={_player.IsCarryingObject}, " +
            $"airborne={_player.TopDownAirborne}, Link={_player.Position}, animal={node.Position}, health={_player.HealthQuarters}.");
    }

    private void ValidateForestHintFairies()
    {
        for (int bits = 0; bits < 8; bits++)
        {
            _saveData.SetGlobalFlag(0x23, (bits & 1) != 0);
            _saveData.SetGlobalFlag(0x2b, (bits & 2) != 0);
            _saveData.SetGlobalFlag(0x42, (bits & 4) != 0);
            LoadValidationRoom(0, 0x82);
            var visible = _entities.Entities<NpcCharacter>().Where(npc => npc.Active && npc.Record.Id == 0x49).ToArray();
            FailIf(visible.Length != (bits == 4 ? 3 : 0), $"Forest hint visibility diverged for flags ${bits:x2}.");
            if (bits != 4) continue;
            for (int index = 0; index < visible.Length; index++)
            {
                var npc = visible[index];
                FailIf(npc.Record.SubId != 0x0e + index || npc.Record.Palette != index + 1 ||
                    npc.Record.DefaultAnimation != (index == 0 ? 0 : 1) || npc.ScriptDrawOffset != new Vector2(0, -4),
                    $"Forest hint ${npc.Record.SubId:x2} lost its source graphic/palette/Z.");
                _player.WarpTo(npc.Position + new Vector2(0, 12), recordSafe: false);
                _player.Face(Vector2I.Up);
                FailIf(!npc.CanTalkTo(_player), "Forest hint failed its A-button proximity test.");
            }
        }
        GD.Print("Validated forest hint fairies $49:$0e-$10, all eight flag combinations, graphics, Z and talk geometry.");
    }

    private void ValidateForestQuestIntroduction()
    {
        _saveData.SetGlobalFlag(0x42, false);
        _saveData.SetGlobalFlag(0x2b);
        LoadValidationRoom(0, 0x35);
        _player.WarpTo(new Vector2(1, 0x48), recordSafe: false);
        _transitions.BeginScroll(_player, Vector2I.Left, 0x34);
        FinishForestScroll();
        _player.WarpTo(new Vector2(0x4f, 0x48), recordSafe: false);
        for (int frame = 0; frame < 1200 && !_saveData.HasGlobalFlag(0x42); frame++)
        {
            if (_dialogue.IsOpen)
            {
                if (_dialogue.CurrentMessage.Contains("Can you help")) _dialogue.SubmitChoiceForValidation(0);
                else _dialogue.Close();
            }
            StepRoomEventFrames(1);
        }
        FailIf(!_saveData.HasGlobalFlag(0x42) || _saveData.HasGlobalFlag(0x2b), "Forest quest introduction did not enable the search.");
        LoadValidationRoom(0, 0x63);
        _player.WarpTo(new Vector2(0x48, 0x7f), recordSafe: false);
        _transitions.BeginScroll(_player, Vector2I.Down, 0x73);
        FinishForestScroll();
        int guideTexts = 0;
        for (int frame = 0; frame < 1000 && _roomEvents.Get<CompanionForestEvent>().HasState; frame++)
        {
            if (_dialogue.IsOpen) { guideTexts++; _dialogue.Close(); }
            StepRoomEventFrames(1);
        }
        FailIf(guideTexts != 1 || _roomEvents.Get<CompanionForestEvent>().MenusDisabled, "Forest guide failed to lead Link into the search.");
        LoadValidationRoom(0, 0x82);
        var hints = _entities.Entities<NpcCharacter>().Where(npc => npc.Active && npc.Record.Id == 0x49).ToArray();
        FailIf(hints.Length != 3 || !hints.Select(npc => npc.Record.TextId).SequenceEqual(new[] { 0x1127, 0x1128, 0x1129 }),
            "Forest search did not supply its three source-ordered hint fairies in room 0:82.");
        foreach (var hint in hints)
        {
            _player.WarpTo(hint.Position + new Vector2(0, 12), recordSafe: false);
            _player.Face(Vector2I.Up);
            FailIf(!hint.CanTalkTo(_player), $"Forest hint ${hint.Record.SubId:x2} is not talkable.");
        }
    }

    private void FinishForestScroll()
    {
        for (int frame = 0; frame < 60 && _transitions.ScrollActive; frame++)
        {
            _transitions.UpdateScroll(1.0 / 60.0);
            _entities.Update(1.0 / 60.0, _player);
            _roomEvents.Update(1.0 / 60.0);
        }
        FailIf(_transitions.ScrollActive, "Forest quest scroll did not complete.");
    }

    private void ValidateDimitriForestEntry()
    {
        _inventory.GiveTreasure(0x0e, 0x0c);
        _saveData.SetGlobalFlag(0x22);
        _saveData.SetGlobalFlag(0x23, false);
        _saveData.SetGlobalFlag(0x42, false);
        _saveData.SetGlobalFlag(0x2b);
        LoadValidationRoom(0, 0x34);
        FailIf(_roomEvents.Get<CompanionForestEvent>().HasState, "Forest $71:$08 triggered without a leftward scroll.");
        LoadValidationRoom(0, 0x35);
        _player.WarpTo(new Vector2(1, 0x48), recordSafe: false);
        _transitions.BeginScroll(_player, Vector2I.Left, 0x34);
        for (int frame = 0; frame < 60 && _transitions.ScrollActive; frame++)
        {
            FailIf(_dialogue.IsOpen || _roomEvents.Get<CompanionForestEvent>().Flights.Count != 0,
                "Forest $71:$08 began dialogue or flight during scrolling.");
            _transitions.UpdateScroll(1.0 / 60.0);
            _entities.Update(1.0 / 60.0, _player);
            _roomEvents.Update(1.0 / 60.0);
        }
        _player.WarpTo(new Vector2(0x50, 0x48), recordSafe: false);
        StepRoomEventFrames(1);
        FailIf(_dialogue.IsOpen || _roomEvents.Get<CompanionForestEvent>().Flights.Count != 0,
            "Forest introduction ignored the Link.xh < $50 trigger.");
        _player.WarpTo(new Vector2(0x4f, 0x48), recordSafe: false);
        StepRoomEventFrames(1);
        FailIf(_roomEvents.Get<CompanionForestEvent>().Flights.Count != 1,
            "Forest introduction did not spawn fairy $49:$03 preset $0f.");
        // Exercise the native circular flight separately from the text command
        // stream: $20 angle steps, two updates each, then one signal increment.
        var flight = _roomEvents.Get<CompanionForestEvent>().Flights.Single();
        for (int frame = 0; frame < 500 && flight.Stage != (int)FairyFlightStage.Circle; frame++)
            flight.UpdateFrame(frame);
        FailIf(flight.Stage != (int)FairyFlightStage.Circle || _roomEvents.Get<CompanionForestEvent>().Signal != 1,
            "Forest fairy did not signal arrival before beginning its circle.");
        for (int frame = 0; frame < 63; frame++) flight.UpdateFrame(frame);
        FailIf(_roomEvents.Get<CompanionForestEvent>().Signal != 1,
            "Forest fairy circle completed before its 64th update.");
        flight.UpdateFrame(63);
        FailIf(_roomEvents.Get<CompanionForestEvent>().Signal != 2 || flight.Stage != (int)FairyFlightStage.WaitForSignal,
            "Forest fairy circle did not signal exactly on update 64.");
        int choices = 0;
        for (int frame = 0; frame < 600 && !_saveData.HasGlobalFlag(0x42); frame++)
        {
            if (_dialogue.IsOpen)
            {
                if (_dialogue.CurrentMessage.Contains("Can you help"))
                    _dialogue.SubmitChoiceForValidation(choices++ == 0 ? 1 : 0);
                else _dialogue.Close();
            }
            StepRoomEventFrames(1);
        }
        FailIf(choices != 2 || !_saveData.HasGlobalFlag(0x42) || _saveData.HasGlobalFlag(0x2b) ||
            !_saveData.HasRoomFlag(0, 0x34, 0x40),
            "Forest introduction failed its repeat-choice loop or persistent lost/scrambled flags.");
        _roomEvents.Get<CompanionForestEvent>().Cancel();
        FailIf(_roomEvents.Get<CompanionForestEvent>().HasState || _roomEvents.Get<CompanionForestEvent>().MenusDisabled,
            "Cancelling forest introduction retained actors, effects or menu ownership.");
        LoadValidationRoom(0, 0x63);
        _player.WarpTo(new Vector2(0x48, 0x7f), recordSafe: false);
        _transitions.BeginScroll(_player, Vector2I.Down, 0x73);
        for (int frame = 0; frame < 60 && _transitions.ScrollActive; frame++)
        {
            FailIf(_dialogue.IsOpen, "Forest $71:$0b guide opened text during scrolling.");
            _transitions.UpdateScroll(1.0 / 60.0);
            _entities.Update(1.0 / 60.0, _player);
            _roomEvents.Update(1.0 / 60.0);
        }
        int guideTexts = 0;
        for (int frame = 0; frame < 700 && _roomEvents.Get<CompanionForestEvent>().HasState; frame++)
        {
            if (_dialogue.IsOpen)
            {
                guideTexts++;
                FailIf(_dialogue.CurrentMessage != DialogueBox.PlainText(new CompanionForestDatabase().Text(0x1126)),
                    "Forest $71:$0b guide selected the wrong source text.");
                _dialogue.Close();
            }
            StepRoomEventFrames(1);
        }
        FailIf(guideTexts != 1 || _roomEvents.Get<CompanionForestEvent>().HasState || _roomEvents.Get<CompanionForestEvent>().MenusDisabled,
            "Forest guide did not wait for its fairy to depart before releasing control.");
        GD.Print("Validated forest entry direction, scrolling/text freeze, Link coordinate guard, 64-update circle and cancellation.");
    }
}
