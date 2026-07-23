using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSigns()
    {
        LoadSignValidationRoom();
        if (_dialogue.MessageSpeed != _saveData.TextSpeed)
            throw new InvalidOperationException(
                "Gameplay dialogue did not consume the selected save's wTextSpeed value.");
        Color expectedDefaultText = new(0x1f / 31.0f, 0x1a / 31.0f, 0x11 / 31.0f);
        if (!DialogueBox.DefaultTextColorForValidation.IsEqualApprox(expectedDefaultText))
            throw new InvalidOperationException(
                "Default textbox text did not use paletteData48e0's white color 2.");
        if (DialogueBox.ContinueMarkerRectForValidation != new Rect2(144, 32, 8, 8) ||
            _dialogue.ContinueMarkerOpaquePixelCountForValidation() != 22)
        {
            throw new InvalidOperationException(
                "The textbox continue marker did not use gfx_hud tile $03 at its original tile position.");
        }
        if (_dialogue.VisibleLinesPerPage != 2 || _dialogue.TextLineSpacing != 16)
            throw new InvalidOperationException(
                "The textbox does not use the original two 8x16 text rows.");
        if (_currentRoom.GetMetatile(new Vector2(88, 58)) != 0xf2)
            throw new InvalidOperationException("Expected sign metatile $f2 in room 2a at $35.");
        if (!TryInteract(_player) || !_dialogue.IsOpen)
            throw new InvalidOperationException("The room 2a test sign did not open its dialogue.");

        _dialogue.RevealCurrentPageForValidation();
        if (!_dialogue.IsPageComplete || _dialogue.HasNextMessage || _dialogue.ArrowVisible)
        {
            throw new InvalidOperationException(
                "The final two-line sign message incorrectly displayed a continuation prompt.");
        }

        _dialogue.ShowMessage("First.\nSecond.\nThird.", _player.Position.Y);
        _dialogue.RevealCurrentPageForValidation();
        if (!_dialogue.HasNextMessage || _dialogue.ArrowVisible)
            throw new InvalidOperationException(
                "A multi-line textbox did not begin its continuation prompt on the blank phase.");
        _dialogue.AdvanceArrowClockForValidation(16.0 / 60.0);
        if (!_dialogue.ArrowVisible)
            throw new InvalidOperationException("The textbox arrow did not appear after 16 original-engine frames.");
        _dialogue.AdvanceArrowClockForValidation(16.0 / 60.0);
        if (_dialogue.ArrowVisible)
            throw new InvalidOperationException("The textbox arrow did not complete its 32-frame blink cycle.");

        int selectedSpeed = _dialogue.MessageSpeed;
        int[] expectedCharacterFrames = { 7, 5, 4, 3, 2 };
        for (int speed = 0; speed < expectedCharacterFrames.Length; speed++)
        {
            _dialogue.MessageSpeed = speed;
            if (_dialogue.CharacterDisplayFrameLength != expectedCharacterFrames[speed])
                throw new InvalidOperationException(
                    $"Message speed {speed} did not select {expectedCharacterFrames[speed]} updates per character.");
        }
        _dialogue.MessageSpeed = 0;
        _dialogue.ShowMessage("AB", _player.Position.Y);
        _dialogue.AdvanceCharacterClockForValidation(6.0 / 60.0);
        if (_dialogue.VisibleGlyphCount != 0)
            throw new InvalidOperationException("Message speed 0 displayed a character before update 7.");
        _dialogue.AdvanceCharacterClockForValidation(1.0 / 60.0);
        if (_dialogue.VisibleGlyphCount != 1)
            throw new InvalidOperationException("Message speed 0 did not display its first character on update 7.");
        _dialogue.MessageSpeed = 4;
        _dialogue.ShowMessage("AB", _player.Position.Y);
        _dialogue.AdvanceCharacterClockForValidation(1.0 / 60.0);
        if (_dialogue.VisibleGlyphCount != 0)
            throw new InvalidOperationException("Message speed 4 displayed a character before update 2.");
        _dialogue.AdvanceCharacterClockForValidation(1.0 / 60.0);
        if (_dialogue.VisibleGlyphCount != 1)
            throw new InvalidOperationException("Message speed 4 did not display its first character on update 2.");

        int textRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndText);
        _dialogue.ShowMessage("ABC", _player.Position.Y);
        _dialogue.AdvanceCharacterClockForValidation(1.0 / 60.0);
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndText) != textRequests)
            throw new InvalidOperationException("SND_TEXT played before the first glyph appeared.");
        _dialogue.AdvanceCharacterClockForValidation(1.0 / 60.0);
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndText) != textRequests + 1)
            throw new InvalidOperationException("The first visible non-space glyph did not request SND_TEXT $66.");
        _dialogue.AdvanceCharacterClockForValidation(2.0 / 60.0);
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndText) != textRequests + 1)
            throw new InvalidOperationException("SND_TEXT ignored its four-update cooldown.");
        _dialogue.AdvanceCharacterClockForValidation(2.0 / 60.0);
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndText) != textRequests + 2)
            throw new InvalidOperationException(
                "SND_TEXT did not become available on the original fourth cooldown update.");

        textRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndText);
        _dialogue.ShowMessage(" A", _player.Position.Y);
        _dialogue.AdvanceCharacterClockForValidation(4.0 / 60.0);
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndText) != textRequests + 1)
            throw new InvalidOperationException(
                "Textbox character audio did not suppress spaces or sound the following glyph.");

        const int tokayTextSound = 0xb6;
        textRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndText);
        int tokayRequests = _sound.PlayRequestsFor(tokayTextSound);
        _dialogue.ShowMessage("\\sfx(0xb6)A", _player.Position.Y);
        _dialogue.AdvanceCharacterClockForValidation(2.0 / 60.0);
        if (_dialogue.CurrentMessage != "A" ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndText) != textRequests + 1 ||
            _sound.PlayRequestsFor(tokayTextSound) != tokayRequests + 1)
        {
            throw new InvalidOperationException(
                "Inline \\sfx() did not remain hidden and play beside the next glyph's default cue.");
        }

        textRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndText);
        tokayRequests = _sound.PlayRequestsFor(tokayTextSound);
        _dialogue.ShowMessage("\\charsfx(0xb6)A", _player.Position.Y);
        _dialogue.AdvanceCharacterClockForValidation(2.0 / 60.0);
        if (_dialogue.CurrentMessage != "A" ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndText) != textRequests ||
            _sound.PlayRequestsFor(tokayTextSound) != tokayRequests + 1)
        {
            throw new InvalidOperationException(
                "Inline \\charsfx() did not replace the per-character SND_TEXT cue.");
        }

        int moveRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove);
        int selectRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndSelectItem);
        _dialogue.ShowChoiceMessage("\\opt()Yes \\opt()No", _player.Position.Y);
        _dialogue.RevealCurrentPageForValidation();
        _dialogue.MoveChoiceForValidation(1);
        if (_dialogue.SelectedChoice != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove) != moveRequests + 1)
        {
            throw new InvalidOperationException(
                "Moving the textbox option cursor did not request SND_MENU_MOVE $84.");
        }
        _dialogue.SubmitChoiceForValidation(1);
        if (_dialogue.IsOpen ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSelectItem) != selectRequests + 1)
        {
            throw new InvalidOperationException(
                "Confirming a textbox option did not request SND_SELECTITEM $56.");
        }

        _dialogue.ShowMessage(
            "\\col(1)R\\col(3)B\\col(0)N\n\\sym(0x57)♪\\heart\\abtn\\bbtn",
            _player.Position.Y);
        if (_dialogue.GlyphColorForValidation(0, 0, 0) != 1 ||
            _dialogue.GlyphColorForValidation(0, 0, 1) != 3 ||
            _dialogue.GlyphColorForValidation(0, 0, 2) != 0 ||
            !_dialogue.GlyphUsesSymbolFontForValidation(0, 1, 0) ||
            _dialogue.GlyphCodeForValidation(0, 1, 0) != 0x57 ||
            !_dialogue.GlyphUsesSymbolFontForValidation(0, 1, 1) ||
            _dialogue.GlyphCodeForValidation(0, 1, 1) != 0x1c ||
            _dialogue.GlyphUsesSymbolFontForValidation(0, 1, 2) ||
            _dialogue.GlyphCodeForValidation(0, 1, 2) != 0x14 ||
            _dialogue.GlyphCodeForValidation(0, 1, 3) != 0xb8 ||
            _dialogue.GlyphCodeForValidation(0, 1, 4) != 0xb9 ||
            _dialogue.GlyphCodeForValidation(0, 1, 5) != 0xba ||
            _dialogue.GlyphCodeForValidation(0, 1, 6) != 0xbb)
        {
            throw new InvalidOperationException(
                "Textbox color commands or main/symbol-font glyph selection were not preserved.");
        }
        _dialogue.MessageSpeed = selectedSpeed;

        _dialogue.ShowMessage("First.\nSecond.\nThird.\nFourth.", _player.Position.Y);
        _dialogue.RevealCurrentPageForValidation();
        int continuationRequests =
            _sound.PlayRequestsFor(OracleSoundEngine.SndText2);
        _dialogue.AdvanceOrClose();
        if (!_dialogue.IsScrollingText ||
            !Mathf.IsEqualApprox(_dialogue.TextScrollOffset, 8.0f) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndText2) !=
                continuationRequests + 1)
        {
            throw new InvalidOperationException(
                "The button frame did not request SND_TEXT_2 $89 and perform " +
                "standardTextStateb's first 8px shift.");
        }
        _dialogue.AdvanceTextScrollForValidation(1.0 / 60.0);
        if (!_dialogue.IsScrollingText ||
            !Mathf.IsEqualApprox(_dialogue.TextScrollOffset, 8.0f))
        {
            throw new InvalidOperationException(
                "The standard textbox DMA state did not hold the first 8px shift for one frame.");
        }
        _dialogue.AdvanceTextScrollForValidation(1.0 / 60.0);
        if (!Mathf.IsEqualApprox(_dialogue.TextScrollOffset, 16.0f))
            throw new InvalidOperationException("standardTextState7 did not perform the second 8px shift.");
        _dialogue.AdvanceTextScrollForValidation(5.0 / 60.0);
        if (_dialogue.IsScrollingText || !Mathf.IsZeroApprox(_dialogue.TextScrollOffset))
            throw new InvalidOperationException(
                "The two discrete tile-row shifts did not finish the one-line text scroll.");

        _dialogue.ShowMessage("Last line.", _player.Position.Y);
        _dialogue.RevealCurrentPageForValidation();
        _dialogue.AdvanceArrowClockForValidation(32.0 / 60.0);
        if (_dialogue.HasNextMessage || _dialogue.ArrowVisible)
            throw new InvalidOperationException("The final dialogue message displayed a continuation arrow.");
        _dialogue.AdvanceOrClose();
        if (_dialogue.IsOpen || !_dialogue.BlocksPlayerInput)
            throw new InvalidOperationException("Closing the final textbox did not consume its button press.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (_dialogue.IsOpen)
            throw new InvalidOperationException("The final textbox press immediately restarted the interaction.");

        GD.Print("Validated save-selected 7/5/4/3/2-update dialogue speed, white default " +
            "text, four-update SND_TEXT/inline voice cues, SND_TEXT_2 continuation, " +
            "choice sounds, colored and symbol-font glyphs, gfx_hud tile $03 continuation " +
            "marker, one-line tile-row scrolling, continuation-only 32-update blink, " +
            "and final-message input consumption.");
    }

    private void ValidateNpcs()
    {
        LoadNpcValidationRoom();
        NpcCharacter? villager = _npcNodes.Find(npc => npc.Record.Id == 0x3a && npc.Record.SubId == 0x03);
        if (villager is null)
            throw new InvalidOperationException($"Expected the room 0:48 villager among {_npcNodes.Count} extracted NPCs.");
        if (villager.TextId != 0x1420)
            throw new InvalidOperationException($"Expected room 0:48 villager to resolve TX_1420, got TX_{villager.TextId:x4}.");
        if (villager.Position != new Vector2(0x38, 0x48) ||
            villager.SpriteBounds.GetCenter() != villager.Position)
        {
            throw new InvalidOperationException("The room 0:48 villager sprite is not centered on its object tile.");
        }
        if (villager.ObjectCollisionBounds.Size != new Vector2(12.0f, 12.0f) ||
            villager.ObjectCollisionBounds.GetCenter() != villager.Position)
        {
            throw new InvalidOperationException("The room 0:48 villager object hitbox does not match the original $06/$06 collision radii.");
        }
        if (villager.LinkBlockingBounds.Size != new Vector2(24.0f, 24.0f) ||
            villager.LinkBlockingBounds.GetCenter() != villager.Position)
        {
            throw new InvalidOperationException("The room 0:48 villager does not combine NPC and Link $06 radii into a 24px blocking region.");
        }
        if (!villager.BlocksLinkCenter(villager.Position) ||
            villager.BlocksLinkCenter(villager.Position + new Vector2(0.0f, 12.0f)))
        {
            throw new InvalidOperationException("The room 0:48 villager's strict radius collision boundary is not centered correctly.");
        }
        if (!Collides(villager.Position + new Vector2(0.0f, 11.9f)) ||
            Collides(villager.Position + new Vector2(0.0f, 12.1f)))
        {
            throw new InvalidOperationException("The room 0:48 villager did not stop Link at the original bottom collision radius.");
        }
        if (!TryInteract(_player) || !_dialogue.IsOpen)
            throw new InvalidOperationException("The room 0:48 villager did not open dialogue.");
        int frameBase = villager.Record.TileBase / 4;
        if (villager.CurrentFrameColumn != frameBase)
            throw new InvalidOperationException("The room 0:48 villager did not face down toward Link after talking.");
        villager.UpdateNpc(16.0 / 60.0, _player.Position);
        if (villager.CurrentAnimationFrame != 1)
            throw new InvalidOperationException("The room 0:48 villager did not advance its original 16-frame idle animation.");
        villager.UpdateNpc(1.0 / 60.0, villager.Position + Vector2.Left * 20.0f);
        if (villager.FacingVector != Vector2I.Left || villager.CurrentAnimationFrame != 0)
            throw new InvalidOperationException("The room 0:48 villager did not face nearby Link and reset its animation.");
        villager.UpdateNpc(1.0 / 60.0, villager.Position + Vector2.Right * 20.0f);
        if (villager.FacingVector != Vector2I.Left)
            throw new InvalidOperationException("The NPC facing cooldown did not preserve the current direction.");
        villager.UpdateNpc(30.0 / 60.0, villager.Position + Vector2.Right * 20.0f);
        if (villager.FacingVector != Vector2I.Right || villager.CurrentFrameColumn != frameBase + 2)
            throw new InvalidOperationException("The villager did not use the mirrored side OAM after the 30-frame facing delay.");
        villager.UpdateNpc(30.0 / 60.0, villager.Position + Vector2.Right * 80.0f);
        if (villager.FacingVector != Vector2I.Down)
            throw new InvalidOperationException("The villager did not return to facing down when Link left the $28 awareness radius.");

        _dialogue.Close();
        _transitions.BeginScroll(_player, Vector2I.Down, 0x58);
        NpcCharacter? destinationNpc = _npcNodes.Find(npc =>
            npc.Record.Room == 0x58 && npc.Record.Id == 0x41 && npc.Record.SubId == 0x04);
        if (!_entities.ScreenTransitionActive || _currentRoom.Id != 0x58 ||
            _entities.OutgoingEntities<NpcCharacter>().Count != 2 || destinationNpc is null)
        {
            throw new InvalidOperationException(
                $"The 0:48 -> 0:58 scroll did not retain two outgoing NPCs and preload the destination NPC " +
                $"(active={_entities.ScreenTransitionActive}, room={_currentRoom.Id:x2}, " +
                $"outgoing={_entities.OutgoingEntities<NpcCharacter>().Count}, incoming={_npcNodes.Count}, " +
                $"destinationFound={destinationNpc is not null}).");
        }
        foreach (NpcCharacter outgoingNpc in _entities.OutgoingEntities<NpcCharacter>())
        {
            if (outgoingNpc.Record.Room != 0x48 ||
                !outgoingNpc.TransitionDrawOffset.IsEqualApprox(Vector2.Zero))
            {
                throw new InvalidOperationException(
                    "An outgoing room 0:48 NPC was not retained at its initial screen position.");
            }
        }
        if (!destinationNpc.TransitionDrawOffset.IsEqualApprox(
            Vector2.Down * OracleRoomData.ViewportHeight))
        {
            throw new InvalidOperationException(
                "The room 0:58 NPC was not staged one screen below the outgoing room.");
        }

        UpdateScrollingTransition(1.0 / 60.0);
        foreach (NpcCharacter outgoingNpc in _entities.OutgoingEntities<NpcCharacter>())
        {
            if (!outgoingNpc.TransitionDrawOffset.IsEqualApprox(Vector2.Up * 4.0f))
                throw new InvalidOperationException("An outgoing NPC did not move with its scrolling room.");
        }
        if (!destinationNpc.TransitionDrawOffset.IsEqualApprox(
            Vector2.Down * (OracleRoomData.ViewportHeight - 4.0f)))
        {
            throw new InvalidOperationException("The preloaded destination NPC did not scroll into view with room 0:58.");
        }

        FinishActiveScrollingTransitionForValidation();
        if (_entities.ScreenTransitionActive || _entities.OutgoingEntities<NpcCharacter>().Count != 0 ||
            !destinationNpc.TransitionDrawOffset.IsEqualApprox(Vector2.Zero) ||
            destinationNpc.GetParent() != _scene.WorldRoot)
        {
            throw new InvalidOperationException(
                "The destination NPC did not become the normal room NPC after the scroll completed.");
        }

        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(_activeGroup, 0x66);
        _roomView.SetRoom(_currentRoom.Texture);
        RefreshRoomObjects();
        NpcCharacter? woman = _npcNodes.Find(npc =>
            npc.Record.Id == 0x3b && npc.Record.SubId == 0x01);
        if (woman is null || woman.Position != new Vector2(0x7a, 0x44))
            throw new InvalidOperationException("Expected female villager $3b/$01 at 0:66 $44/$7a.");

        woman.UpdateDrawPriority(woman.Position - Vector2.Down * 12.0f);
        if (woman.ZIndex != NpcCharacter.InFrontOfLinkZIndex)
            throw new InvalidOperationException(
                "Room 0:66's woman did not cover Link when yh exceeded w1Link.yh+$0b.");
        woman.UpdateDrawPriority(woman.Position - Vector2.Down * 11.0f);
        if (woman.ZIndex != NpcCharacter.BehindLinkZIndex)
            throw new InvalidOperationException(
                "Room 0:66's woman covered Link at the strict w1Link.yh+$0b boundary.");

        Color linkBlack = Player.RecolorLinkPixel(new Color(0.25f, 0.25f, 0.25f));
        Color linkGreen = Player.RecolorLinkPixel(new Color(0.75f, 0.75f, 0.75f));
        Color linkSkin = Player.RecolorLinkPixel(Colors.White);
        if (!linkBlack.IsEqualApprox(Colors.Black) ||
            !linkGreen.IsEqualApprox(new Color(0x02 / 31.0f, 0x15 / 31.0f, 0x08 / 31.0f)) ||
            !linkSkin.IsEqualApprox(new Color(0x1f / 31.0f, 0x1a / 31.0f, 0x11 / 31.0f)))
        {
            throw new InvalidOperationException(
                "Link did not use standardSpritePaletteData palette 0 selected by OAM flags $08.");
        }

        GD.Print("Validated villager idle animation, $28 Link awareness, 30-frame facing delay, " +
            "TX_1420 dialogue, retained/preloaded NPC screen scrolling, room 0:66 " +
            "Link-relative draw priority, and Link sprite palette 0.");
    }

    private void ValidateRoom148NpcInteractions()
    {
        const double frame = 1.0 / 60.0;
        var validationRoot = new Node { Name = "Room148NpcValidation" };
        AddChild(validationRoot);
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var manager = new RoomEntityManager(
            validationRoot, new NpcDatabase(), new EnemyDatabase(), save);
        var pickaxe = new Room148PickaxeDatabase();
        var sounds = new List<int>();
        manager.SoundRequested += sounds.Add;
        OracleRoomData room148 = _world.LoadRoom(1, 0x48);
        manager.LoadRoom(1, room148);

        NpcCharacter Worker() => manager.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x57, SubId: 0x00 });
        NpcCharacter Villager() => manager.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x3a, SubId: 0x06 });
        NpcCharacter Girl() => manager.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x38, SubId: 0x00 });
        void SetEssences(byte value)
        {
            if (save.WriteWramByte(0xc6bf, value))
                save.CommitInventoryChange();
        }

        NpcCharacter worker = Worker();
        NpcCharacter villager = Villager();
        NpcCharacter girl = Girl();
        if (pickaxe.Record.DebrisSpriteName != "spr_common_sprites" ||
            pickaxe.Record.DebrisTileBase != 0x02 ||
            manager.Entities<NpcCharacter>().Count != 3 ||
            worker.Position != new Vector2(0x38, 0x58) ||
            villager.Position != new Vector2(0x88, 0x58) ||
            girl.Position != new Vector2(0x78, 0x38) ||
            worker.TextId != 0x1b00 || villager.TextId != 0x1400 ||
            girl.TextId != 0x1a00 ||
            worker.CurrentScriptAnimationSource != pickaxe.Record.WorkAnimation ||
            worker.CurrentAnimationParameter != 0 ||
            worker.CurrentAnimationOpaquePixels == 0 ||
            !worker.Active || !villager.Active || !girl.Active)
        {
            throw new InvalidOperationException(
                "Room 1:48 did not load ordered worker $57:$00, villager " +
                "$3a:$06, and girl $38:$00 with their imported positions, " +
                "work animation, and TX_1b00/TX_1400/TX_1a00 dialogue.");
        }

        _player.WarpTo(new Vector2(0x18, 0x70));
        for (int update = 0; update < 25; update++)
            manager.Update(frame, _player);
        if (sounds.Count != 0 ||
            manager.Entities<Room148PickaxeDebris>().Count != 0 ||
            worker.CurrentAnimationFrame != 0 ||
            worker.CurrentAnimationParameter != 0)
        {
            throw new InvalidOperationException(
                "Pickaxe worker $57:$00 struck before animation $02's initial 26-update frame.");
        }

        manager.Update(frame, _player);
        List<Room148PickaxeDebris> debris =
            manager.Entities<Room148PickaxeDebris>();
        if (sounds.Count != 1 || sounds[0] != OracleSoundEngine.SndClink ||
            worker.CurrentAnimationFrame != 1 ||
            worker.CurrentAnimationParameter != 1 || debris.Count != 2 ||
            debris[0].Position != new Vector2(0x2a, 0x5c) ||
            debris[1].Position != new Vector2(0x2a, 0x5c) ||
            debris[0].Angle != 0x18 || debris[1].Angle != 0x08 ||
            debris.Any(chip => chip.Palette != 1 || chip.ZFixed != 0 ||
                chip.SpeedZ != -0xc0 || !chip.StateInitialized))
        {
            throw new InvalidOperationException(
                "Animation $02's first parameter-$01 strike did not play " +
                "SND_CLINK and create two same-update $92:$06 chips at " +
                "$5c/$2a with angles $18/$08 and Z speed -$00c0.");
        }

        AnimationFrameDefinition debrisFrame =
            OracleGraphicsCache.GetAnimationDefinition(
                pickaxe.Record.DebrisAnimation).Frames[0];
        Image debrisSource = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{pickaxe.Record.DebrisSpriteName}.png");
        using Texture2D debrisTexture = NpcCharacter.BuildOamTextureUncachedForValidation(
            debrisSource,
            debrisFrame.EncodedOam,
            pickaxe.Record.DebrisTileBase,
            1);
        Image debrisImage = debrisTexture.GetImage();
        int debrisOpaquePixels = 0;
        for (int y = 0; y < debrisImage.GetHeight(); y++)
        for (int x = 0; x < debrisImage.GetWidth(); x++)
        {
            if (debrisImage.GetPixel(x, y).A > 0.1f)
                debrisOpaquePixels++;
        }
        if (debrisOpaquePixels != 52 ||
            debrisImage.GetPixel(12, 8).A > 0.1f ||
            debrisImage.GetPixel(14, 12).A <= 0.1f)
        {
            throw new InvalidOperationException(
                "INTERAC_FALLING_ROCK $92:$06 did not render fixed bank-1 " +
                "spr_common_sprites tile $02 as the 8x8 dirt chip.");
        }

        manager.Update(frame, _player);
        debris = manager.Entities<Room148PickaxeDebris>();
        if (debris.Count != 2 ||
            debris[0].PrecisePosition != new Vector2(0x29 + 0.5f, 0x5c) ||
            debris[1].PrecisePosition != new Vector2(0x2a + 0.5f, 0x5c) ||
            debris[0].Position != new Vector2(0x29, 0x5c) ||
            debris[1].Position != new Vector2(0x2a, 0x5c) ||
            debris.Any(chip => chip.ZFixed != -0xc0 || chip.SpeedZ != -0xa8))
        {
            throw new InvalidOperationException(
                "Pickaxe dirt chips did not begin their SPEED_80 cardinal " +
                "movement and -$00c0/$18 fixed-point Z flight one update after creation.");
        }
        for (int update = 0;
            update < 30 && manager.Entities<Room148PickaxeDebris>().Count != 0;
            update++)
        {
            manager.Update(frame, _player);
        }
        if (manager.Entities<Room148PickaxeDebris>().Count != 0)
            throw new InvalidOperationException(
                "Pickaxe dirt chips were not deleted on their first Z=0 landing update.");

        manager.LoadRoom(1, room148);
        sounds.Clear();
        worker = Worker();
        for (int update = 0; update < 130; update++)
            manager.Update(frame, _player);
        debris = manager.Entities<Room148PickaxeDebris>();
        if (sounds.Count != 3 || sounds.Any(sound =>
                sound != OracleSoundEngine.SndClink) ||
            worker.CurrentAnimationParameter != 2 || debris.Count != 2 ||
            debris.Any(chip => chip.Palette != 2 ||
                chip.Position != new Vector2(0x46, 0x5c)))
        {
            throw new InvalidOperationException(
                "Pickaxe animation $02 did not strike on updates 26/78/130 " +
                "or move its parameter-$02 dirt-chip origin to $5c/$46.");
        }

        manager.LoadRoom(1, room148);
        sounds.Clear();
        worker = Worker();
        if (!manager.BeginNpcTalk(worker) ||
            worker.CurrentScriptAnimationSource != pickaxe.Record.TalkAnimation)
        {
            throw new InvalidOperationException(
                "Talking to worker $57:$00 did not select static animation $03.");
        }
        for (int update = 0; update < 100; update++)
            manager.Update(frame, _player);
        if (sounds.Count != 0 ||
            manager.Entities<Room148PickaxeDebris>().Count != 0)
        {
            throw new InvalidOperationException(
                "Worker $57:$00 continued striking while TX_1b00 was active.");
        }
        manager.EndNpcTalk(worker);
        if (worker.CurrentScriptAnimationSource != pickaxe.Record.WorkAnimation ||
            worker.CurrentAnimationFrame != 0 ||
            worker.CurrentAnimationParameter != 0)
        {
            throw new InvalidOperationException(
                "Closing TX_1b00 did not jump back to work animation $02 " +
                "and run its same-update interactionAnimateAsNpc call.");
        }
        for (int update = 0; update < 24; update++)
            manager.Update(frame, _player);
        if (sounds.Count != 0)
            throw new InvalidOperationException(
                "Worker $57:$00 struck too early after closing TX_1b00.");
        manager.Update(frame, _player);
        if (sounds.Count != 1 || worker.CurrentAnimationParameter != 1)
            throw new InvalidOperationException(
                "Worker $57:$00 did not restart its 26-update strike cycle at the script boundary.");

        villager = Villager();
        girl = Girl();
        SetEssences(0x02);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 1 ||
            !villager.Active || villager.TextId != 0x1401 || girl.Active)
        {
            throw new InvalidOperationException(
                "getGameProgress_2 state $01 did not keep $3a:$06 with TX_1401 and delete $38:$00.");
        }
        SetEssences(0x08);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 2 ||
            !villager.Active || villager.TextId != 0x1402 || girl.Active)
        {
            throw new InvalidOperationException(
                "getGameProgress_2 state $02 did not keep $3a:$06 with TX_1402 and delete $38:$00.");
        }
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 3 ||
            villager.Active || !girl.Active || girl.TextId != 0x1a03)
        {
            throw new InvalidOperationException(
                "getGameProgress_2 state $03 did not replace $3a:$06 with $38:$00/TX_1a03.");
        }
        SetEssences(0x40);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 4 ||
            villager.Active || !girl.Active || girl.TextId != 0x1a04)
        {
            throw new InvalidOperationException(
                "getGameProgress_2 state $04 did not select $38:$00/TX_1a04 after D7.");
        }
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 5 ||
            villager.Active || !girl.Active || girl.TextId != 0x1a05)
        {
            throw new InvalidOperationException(
                "GLOBALFLAG_SAW_TWINROVA_BEFORE_ENDGAME did not take precedence as state $05.");
        }
        save.SetLinkedGame(linked: true);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 5 ||
            girl.TextId != 0x1a08)
        {
            throw new InvalidOperationException(
                "Linked getGameProgress_2 state $05 did not select the TX_1a08 branch.");
        }
        save.SetRoomFlag(4, 0xfb, OracleSaveData.RoomFlag80);
        save.SetRoomFlag(5, 0xfc, OracleSaveData.RoomFlag80);
        save.SetRoomFlag(4, 0xfc, 0x7f);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 5 ||
            girl.TextId != 0x1a08)
        {
            throw new InvalidOperationException(
                "Unrelated room/group flags or room 4:fc bits $01-$40 changed getGameProgress_2.");
        }
        save.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 6 ||
            villager.Active || !girl.Active || girl.TextId != 0x1a09)
        {
            throw new InvalidOperationException(
                "Linked room 4:fc flag $80 did not take precedence as state $06/TX_1a09.");
        }
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 7 ||
            villager.Active || !girl.Active || girl.TextId != 0x1a07)
        {
            throw new InvalidOperationException(
                "GLOBALFLAG_FINISHEDGAME did not take precedence as state $07/TX_1a07.");
        }
        manager.LoadRoom(1, room148);
        if (Villager().Active || !Girl().Active || Girl().TextId != 0x1a07)
            throw new InvalidOperationException(
                "Room 1:48 re-entry did not retain getGameProgress_2 state $07.");

        OracleSaveData state3Save = OracleSaveData.CreateStandardGame();
        if (state3Save.WriteWramByte(0xc6bf, 0x08))
            state3Save.CommitInventoryChange();
        state3Save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru);
        var state3Manager = new RoomEntityManager(
            validationRoot, new NpcDatabase(), new EnemyDatabase(), state3Save);
        state3Manager.LoadRoom(1, _world.LoadRoom(1, 0x47));
        NpcCharacter state3Villager = state3Manager.Entities<NpcCharacter>().Single(
            npc => npc.Record is { Id: 0x3a, SubId: 0x07 });
        if (!state3Villager.Active || state3Villager.TextId != 0x1403)
            throw new InvalidOperationException(
                "Villager $3a:$07 did not use unlinked state-$03 TX_1403.");
        state3Save.SetLinkedGame(linked: true);
        if (!state3Villager.Active || state3Villager.TextId != 0x1408)
            throw new InvalidOperationException(
                "Villager $3a:$07 did not switch live to linked state-$03 TX_1408.");
        state3Manager.Clear();

        manager.LoadRoom(1, room148);
        sounds.Clear();
        worker = Worker();
        for (int update = 0; update < 10; update++)
            manager.Update(frame, _player);
        int frozenFrame = worker.CurrentAnimationFrame;
        int frozenParameter = worker.CurrentAnimationParameter;
        manager.BeginScreenTransition(
            1, _world.LoadRoom(1, 0x47), new Vector2(160, 0));
        for (int update = 0; update < 120; update++)
            manager.Update(frame, _player);
        if (worker.CurrentAnimationFrame != frozenFrame ||
            worker.CurrentAnimationParameter != frozenParameter ||
            sounds.Count != 0)
        {
            throw new InvalidOperationException(
                "Room 1:48's outgoing worker advanced or struck during scrolling.");
        }

        manager.Clear();
        RemoveChild(validationRoot);
        validationRoot.QueueFree();
        GD.Print("Validated room 1:48's $57:$00 pickaxe strike/talk cycle, " +
            "$92:$06 dirt-chip spawn order and 8.8 flight, SND_CLINK, " +
            "transition freeze, and all eight getGameProgress_2 visibility/" +
            "dialogue states including linked branches and flag precedence.");
    }

    private void ValidateRoom149FamilyInteractions()
    {
        const double frame = 1.0 / 60.0;
        var validationRoot = new Node { Name = "Room149FamilyValidation" };
        AddChild(validationRoot);
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var manager = new RoomEntityManager(
            validationRoot, new NpcDatabase(), new EnemyDatabase(), save);
        var database = new Room149FamilyDatabase();
        manager.LoadRoom(1, _world.LoadRoom(1, 0x49));

        NpcCharacter? boy = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3c && npc.Record.SubId == 0x0e);
        NpcCharacter? father = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3a && npc.Record.SubId == 0x0c);
        NpcCharacter? observer = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x43 && npc.Record.SubId == 0x06);
        Room149Ball? ball = manager.Entities<Room149Ball>().SingleOrDefault();
        if (manager.Entities<NpcCharacter>().Count != 3 ||
            boy is null || father is null || observer is null || ball is null ||
            boy.Position != new Vector2(0x78, 0x48) || boy.TextId != 0x251d ||
            boy.TextPosition != 0 || father.Position != new Vector2(0x38, 0x48) ||
            father.TextId != 0x1442 || observer.Position != new Vector2(0x78, 0x28) ||
            observer.TextId != 0x1712 || !ball.Active || !ball.Idle ||
            ball.Position != new Vector2(0x75, 0x4a) ||
            boy.CurrentScriptAnimationSource != database.Visual("boy").Animation ||
            father.CurrentScriptAnimationSource !=
                database.Visual("father-default").Animation ||
            observer.CurrentScriptAnimationSource !=
                database.Visual("observer").Animation)
        {
            throw new InvalidOperationException(
                "Room 1:49 did not load the pre-D7 father/son catch interaction, " +
                "observer, imported animations, and TX_251d/TX_1442/TX_1712.");
        }

        ulong normalFatherHash = father.CurrentAnimationPixelHash;
        ulong normalObserverHash = observer.CurrentAnimationPixelHash;
        if (save.WriteWramByte(0xc6bf, 0xbf))
            save.CommitInventoryChange();
        save.SetRoomFlag(4, 0xfc, 0x7f);
        save.SetRoomFlag(4, 0xfb, OracleSaveData.RoomFlag80);
        save.SetRoomFlag(5, 0xfc, OracleSaveData.RoomFlag80);
        if (boy.Position != new Vector2(0x78, 0x48) || boy.TextId != 0x251d ||
            father.TextId != 0x1442 || observer.TextId != 0x1712 ||
            !ball.Active || !ball.Idle ||
            father.CurrentAnimationPixelHash != normalFatherHash ||
            observer.CurrentAnimationPixelHash != normalObserverHash)
        {
            throw new InvalidOperationException(
                "Unrelated wEssencesObtained bits, room 4:fc bits $01-$40, " +
                "room 4:fb bit $80, or group-5 room fc bit $80 changed room " +
                "1:49's pre-D7 family state.");
        }

        if (save.WriteWramByte(0xc6bf, 0xff))
            save.CommitInventoryChange();
        if (boy.Position != new Vector2(0x48, 0x48) ||
            boy.TextId != 0x251b || boy.TextPosition != 2 ||
            father.TextId != 0 || observer.TextId != 0 || ball.Active ||
            father.CurrentScriptAnimationSource !=
                database.Visual("father-stone").Animation ||
            father.CurrentAnimationPixelHash == normalFatherHash ||
            observer.CurrentAnimationPixelHash == normalObserverHash ||
            !father.CurrentAnimationUsesColor(database.StonePalette[1]) &&
            !father.CurrentAnimationUsesColor(database.StonePalette[2]) &&
            !father.CurrentAnimationUsesColor(database.StonePalette[3]))
        {
            throw new InvalidOperationException(
                "D7 essence bit 6 did not move the room 1:49 boy to $48/$48, " +
                "select TX_251b with \\pos(2), petrify the father/observer with " +
                "PALH_a2, suppress their dialogue, and remove INTERAC_BALL $95.");
        }

        save.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80);
        if (boy.Position != new Vector2(0x78, 0x48) || boy.TextId != 0x251e ||
            boy.TextPosition != 0 || father.TextId != 0x1443 ||
            observer.TextId != 0x1712 || !ball.Active || !ball.Idle ||
            ball.Position != new Vector2(0x75, 0x4a) ||
            father.CurrentScriptAnimationSource !=
                database.Visual("father-default").Animation ||
            father.CurrentAnimationPixelHash != normalFatherHash ||
            observer.CurrentAnimationPixelHash != normalObserverHash)
        {
            throw new InvalidOperationException(
                "Room 4:fc flag $80 did not restore room 1:49's family, ball, " +
                "normal palettes, positions, and TX_251e/TX_1443/TX_1712 live.");
        }

        if (save.WriteWramByte(0xc6bf, 0xbf))
            save.CommitInventoryChange();
        if (boy.TextId != 0x251e || father.TextId != 0x1443 ||
            observer.TextId != 0x1712 || !ball.Active)
        {
            throw new InvalidOperationException(
                "Room 4:fc flag $80 did not take precedence after D7 essence " +
                "bit 6 was cleared live.");
        }

        save.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80, value: false);
        if (boy.TextId != 0x251d || father.TextId != 0x1442 ||
            observer.TextId != 0x1712 || !ball.Active)
        {
            throw new InvalidOperationException(
                "Clearing room 4:fc flag $80 with D7 essence bit 6 clear did " +
                "not restore room 1:49's pre-D7 state live.");
        }

        if (save.WriteWramByte(0xc6bf, 0xff))
            save.CommitInventoryChange();
        if (boy.TextId != 0x251b || father.TextId != 0 || observer.TextId != 0 ||
            ball.Active)
        {
            throw new InvalidOperationException(
                "D7 essence bit 6 did not reselect room 1:49's stone state " +
                "after the Veran flag was cleared live.");
        }

        save.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80);
        if (boy.TextId != 0x251e || father.TextId != 0x1443 ||
            observer.TextId != 0x1712 || !ball.Active || !ball.Idle ||
            ball.Position != new Vector2(0x75, 0x4a))
        {
            throw new InvalidOperationException(
                "Reapplying room 4:fc flag $80 did not restore and reset room " +
                "1:49's post-Veran catch interaction live.");
        }

        _player.WarpTo(new Vector2(0x18, 0x70));
        for (int update = 0; update < 29; update++)
            manager.Update(frame, _player);
        if (!ball.Idle || ball.Position != new Vector2(0x75, 0x4a) ||
            boy.CurrentAnimationFrame != 0)
        {
            throw new InvalidOperationException(
                "The room 1:49 boy threw the ball before his initial 30-update wait.");
        }

        manager.Update(frame, _player);
        if (ball.Idle || ball.SubId != 1 || ball.Position != new Vector2(0x75, 0x4a) ||
            ball.ZFixed != 0 || ball.SpeedZ != -0x1c0 ||
            boy.CurrentAnimationFrame != 1)
        {
            throw new InvalidOperationException(
                "The boy's cfd3=$02 update did not force his throw frame and launch " +
                "INTERAC_BALL $95 left from $4a/$75 at Z speed -$01c0.");
        }

        for (int update = 0; update < 29; update++)
            manager.Update(frame, _player);
        if (!ball.Idle || ball.Position != new Vector2(0x3c, 0x4a) ||
            ball.ZFixed != 0 || ball.SpeedZ != 0)
        {
            throw new InvalidOperationException(
                "The boy's ball did not land at the father's original $4a/$3c " +
                "position after the exact SPEED_200/-$01c0/$20 flight.");
        }

        for (int update = 0; update < 30; update++)
            manager.Update(frame, _player);
        if (!ball.Idle)
            throw new InvalidOperationException(
                "The father threw before his initial 60+30 update script waits.");
        manager.Update(frame, _player);
        if (ball.Idle || ball.SubId != 0 ||
            ball.Position != new Vector2(0x3c, 0x4a) ||
            ball.SpeedZ != -0x1c0 || father.CurrentAnimationFrame != 1)
        {
            throw new InvalidOperationException(
                "The father's cfd3=$01 update did not force his throw frame and " +
                "launch INTERAC_BALL $95 right on update 90.");
        }

        manager.Clear();
        RemoveChild(validationRoot);
        validationRoot.QueueFree();
        GD.Print("Validated room 1:49's exact D7/Veran flag truth table and precedence, " +
            "six imported texts, PALH_a2 stone palette, exact actor positions, " +
            "30/60/90-update synchronized throw scripts, and INTERAC_BALL $95 " +
            "SPEED_200 8.8 parabolic flights.");
    }

    private void ValidateRoom157NpcInteractions()
    {
        const double frame = 1.0 / 60.0;
        var validationRoot = new Node { Name = "Room157NpcValidation" };
        AddChild(validationRoot);
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var manager = new RoomEntityManager(
            validationRoot, new NpcDatabase(), new EnemyDatabase(), save);
        manager.LoadRoom(1, _world.LoadRoom(1, 0x57));

        List<NpcCharacter> actors = manager.Entities<NpcCharacter>();
        if (actors.Count != 1 ||
            actors[0].Record is not { Id: 0x3b, SubId: 0x05 })
        {
            throw new InvalidOperationException(
                "Room 1:57 did not preserve its sole female villager $3b:$05.");
        }

        NpcCharacter villager = actors[0];
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 0 ||
            !villager.Active || villager.Position != new Vector2(0x48, 0x38) ||
            villager.Record.Palette != 1 || villager.Record.DefaultAnimation != 2 ||
            !villager.Record.CanFace || villager.TextId != 0x1510 ||
            string.IsNullOrEmpty(villager.Message))
        {
            throw new InvalidOperationException(
                "Room 1:57 state $00 did not create the palette-$01, down-facing " +
                "female villager at $38,$48 with TX_1510.");
        }

        bool CanTalkToVillager()
        {
            _player.WarpTo(villager.Position + Vector2.Down * 16.0f);
            _player.Face(Vector2I.Up);
            return villager.CanTalkTo(_player);
        }
        void SetEssences(byte value)
        {
            if (save.WriteWramByte(0xc6bf, value))
                save.CommitInventoryChange();
        }
        void SetLinked(bool value)
        {
            save.SetLinkedGame(value);
        }

        if (!CanTalkToVillager())
            throw new InvalidOperationException(
                "Room 1:57's state-$00 female villager was not talkable.");

        ulong downHash = villager.CurrentAnimationPixelHash;
        _player.WarpTo(new Vector2(0x21, 0x38));
        manager.Update(frame, _player);
        if (villager.CurrentAnimationPixelHash == downHash)
        {
            throw new InvalidOperationException(
                "Room 1:57's female villager did not use npcFaceLinkAndAnimate " +
                "inside the original $28 facing radius.");
        }

        SetEssences(0x02);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 1 ||
            !villager.Active || villager.TextId != 0x1511)
        {
            throw new InvalidOperationException(
                "Room 1:57 state $01 did not select TX_1511.");
        }

        SetEssences(0x08);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 2 ||
            !villager.Active || villager.TextId != 0x1512)
        {
            throw new InvalidOperationException(
                "Room 1:57 state $02 did not select TX_1512.");
        }

        save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 3 ||
            !villager.Active || villager.TextId != 0x1513)
        {
            throw new InvalidOperationException(
                "Room 1:57 state $03 did not select TX_1513.");
        }

        SetEssences(0x40);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 4 ||
            villager.Active || villager.TextId != 0x1515)
        {
            throw new InvalidOperationException(
                "Room 1:57's female villager was not deleted in state $04.");
        }

        save.SetGlobalFlag(OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 5 ||
            !villager.Active || villager.TextId != 0x1515)
        {
            throw new InvalidOperationException(
                "Room 1:57 state $05 did not restore the villager with TX_1515.");
        }

        SetLinked(value: true);
        save.SetRoomFlag(4, 0xfc, 0x7f);
        save.SetRoomFlag(5, 0xfc, OracleSaveData.RoomFlag80);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 5 ||
            !villager.Active || villager.TextId != 0x1515)
        {
            throw new InvalidOperationException(
                "Unrelated room 4:fc bits or group-5 room fc bit $80 " +
                "incorrectly selected room 1:57 state $06.");
        }

        save.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 6 ||
            !villager.Active || villager.TextId != 0x1518)
        {
            throw new InvalidOperationException(
                "Linked room 4:fc flag $80 did not select room 1:57 " +
                "state $06/TX_1518.");
        }

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 7 ||
            villager.Active || villager.TextId != 0x1518)
        {
            throw new InvalidOperationException(
                "Finished-game state $07 did not take precedence and delete " +
                "room 1:57's female villager.");
        }

        manager.LoadRoom(1, _world.LoadRoom(1, 0x57));
        villager = manager.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x3b, SubId: 0x05 });
        if (villager.Active || villager.Position != new Vector2(0x48, 0x38) ||
            villager.Record.Palette != 1 || villager.TextId != 0x1518)
        {
            throw new InvalidOperationException(
                "Room 1:57 finished-game re-entry did not retain the deleted " +
                "palette-$01 state-$07 actor record.");
        }

        manager.Clear();
        RemoveChild(validationRoot);
        validationRoot.QueueFree();
        GD.Print("Validated room 1:57's sole female villager, palette-$01 " +
            "override, six-state existence set, all eight getGameProgress_2 " +
            "dialogue entries, facing, exact flag precedence, negative " +
            "room-table cases, live refresh, and re-entry.");
    }

    private void ValidateRoom158NpcInteractions()
    {
        const double frame = 1.0 / 60.0;
        var validationRoot = new Node { Name = "Room158NpcValidation" };
        AddChild(validationRoot);
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var manager = new RoomEntityManager(
            validationRoot, new NpcDatabase(), new EnemyDatabase(), save);
        manager.LoadRoom(1, _world.LoadRoom(1, 0x58));

        List<NpcCharacter> actors = manager.Entities<NpcCharacter>();
        if (actors.Count != 3 ||
            actors[0].Record is not { Id: 0x44, SubId: 0x04 } ||
            actors[1].Record is not { Id: 0x4f, SubId: 0x02 } ||
            actors[2].Record is not { Id: 0x36, SubId: 0x0d })
        {
            throw new InvalidOperationException(
                "Room 1:58 did not preserve hobo, Impa, and Nayru object-data order.");
        }

        NpcCharacter hobo = actors[0];
        NpcCharacter impa = actors[1];
        NpcCharacter nayru = actors[2];
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 0 ||
            !hobo.Active || hobo.Position != new Vector2(0x48, 0x48) ||
            hobo.TextId != 0x1600 || string.IsNullOrEmpty(hobo.Message) ||
            hobo.Record.CanFace || impa.Active || nayru.Active)
        {
            throw new InvalidOperationException(
                "Room 1:58 state $00 did not contain only the static hobo " +
                "at $48,$48 with TX_1600.");
        }

        void SetEssences(byte value)
        {
            if (save.WriteWramByte(0xc6bf, value))
                save.CommitInventoryChange();
        }
        void SetLinked(bool value)
        {
            save.SetLinkedGame(value);
        }
        void SetTreasure(int treasure, bool value)
        {
            int address = 0xc69a + treasure / 8;
            byte mask = (byte)(1 << (treasure & 7));
            byte current = save.ReadWramByte(address);
            byte next = value ? (byte)(current | mask) : (byte)(current & ~mask);
            if (save.WriteWramByte(address, next))
                save.CommitInventoryChange();
        }
        bool CanTalkTo(NpcCharacter npc)
        {
            _player.WarpTo(npc.Position + Vector2.Down * 16.0f);
            _player.Face(Vector2I.Up);
            return npc.CanTalkTo(_player);
        }

        SetEssences(0x02);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 1 ||
            !hobo.Active || hobo.TextId != 0x1601 || !CanTalkTo(hobo))
        {
            throw new InvalidOperationException(
                "Room 1:58 state $01 did not select unlinked TX_1601.");
        }
        SetLinked(value: true);
        if (hobo.TextId != 0x1608 || !CanTalkTo(hobo))
            throw new InvalidOperationException(
                "Room 1:58 linked state $01 did not select TX_1608 live.");
        SetLinked(value: false);

        SetEssences(0x08);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 2 ||
            !hobo.Active || hobo.TextId != 0x1602)
        {
            throw new InvalidOperationException(
                "Room 1:58 state $02 did not select TX_1602.");
        }

        save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 3 || hobo.Active)
            throw new InvalidOperationException(
                "Room 1:58's hobo was not deleted only in saved-Nayru state $03.");

        SetEssences(0x40);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 4 ||
            !hobo.Active || hobo.Position != new Vector2(0x48, 0x48) ||
            hobo.TextId != 0x1604)
        {
            throw new InvalidOperationException(
                "Room 1:58 state $04 did not restore the hobo at $48,$48 with TX_1604.");
        }

        save.SetGlobalFlag(OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 5 ||
            hobo.TextId != 0x1605)
        {
            throw new InvalidOperationException(
                "Room 1:58 state $05 did not select TX_1605.");
        }

        SetLinked(value: true);
        save.SetRoomFlag(4, 0xfc, 0x7f);
        save.SetRoomFlag(5, 0xfc, OracleSaveData.RoomFlag80);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 5 ||
            hobo.Position != new Vector2(0x48, 0x48) || hobo.TextId != 0x1605)
        {
            throw new InvalidOperationException(
                "Unrelated room 4:fc bits or group-5 room fc bit $80 " +
                "incorrectly selected room 1:58 state $06.");
        }

        save.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 6 ||
            !hobo.Active || hobo.Position != new Vector2(0x78, 0x58) ||
            hobo.TextId != 0x1609 || impa.Active || nayru.Active)
        {
            throw new InvalidOperationException(
                "Linked room 4:fc flag $80 did not move the hobo to $58,$78, " +
                "select TX_1609, and retain the pre-flame actor set.");
        }

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFlameOfDespairLit);
        if (!nayru.Active || nayru.TextId != 0x1d17 || impa.Active ||
            !nayru.Record.CanFace || !CanTalkTo(nayru))
        {
            throw new InvalidOperationException(
                "GLOBALFLAG_FLAME_OF_DESPAIR_LIT did not reveal talkable " +
                "Nayru $36:$0d/TX_1d17 while retaining Impa's compound gate.");
        }

        SetTreasure(TreasureDatabase.TreasureHarp, value: true);
        SetTreasure(TreasureDatabase.TreasureMakuSeed, value: true);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagPreBlackTowerCutsceneDone);
        save.SetRoomFlag(1, 0x83, OracleSaveData.RoomFlag80);
        if (impa.Active)
            throw new InvalidOperationException(
                "Group-1 room 83 flag $80 incorrectly satisfied Impa's " +
                "wPresentRoomFlags+$83 predicate.");
        save.SetRoomFlag(0, 0x83, OracleSaveData.RoomFlag80);
        if (!impa.Active || impa.TextId != 0x012f || !impa.Record.CanFace ||
            !CanTalkTo(impa))
        {
            throw new InvalidOperationException(
                "getImpaNpcState $08 did not reveal talkable Impa " +
                "$4f:$02/TX_012f after every exact prerequisite was met.");
        }

        ulong impaDownHash = impa.CurrentAnimationPixelHash;
        ulong nayruDownHash = nayru.CurrentAnimationPixelHash;
        _player.WarpTo(new Vector2(0x21, 0x48));
        manager.Update(frame, _player);
        if (impa.CurrentAnimationPixelHash == impaDownHash ||
            nayru.CurrentAnimationPixelHash == nayruDownHash)
        {
            throw new InvalidOperationException(
                "Room 1:58 Impa or Nayru did not use the imported directional " +
                "animation when Link entered the original $28 facing radius.");
        }

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        if (NpcVisibilityRuleDatabase.GetGameProgress2(save) != 7 ||
            !hobo.Active || hobo.Position != new Vector2(0x48, 0x48) ||
            hobo.TextId != 0x160a || impa.Active || nayru.Active)
        {
            throw new InvalidOperationException(
                "Finished-game state $07 did not override linked state $06, " +
                "restore the hobo position, select linked TX_160a, and remove " +
                "the Flame of Despair actors.");
        }
        SetLinked(value: false);
        if (hobo.TextId != 0x1607)
            throw new InvalidOperationException(
                "Unlinked room 1:58 state $07 did not select TX_1607 live.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x58));
        hobo = manager.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x44, SubId: 0x04 });
        if (!hobo.Active || hobo.Position != new Vector2(0x48, 0x48) ||
            hobo.TextId != 0x1607)
        {
            throw new InvalidOperationException(
                "Room 1:58 re-entry did not retain unlinked state-$07 hobo state.");
        }

        manager.Clear();
        RemoveChild(validationRoot);
        validationRoot.QueueFree();
        GD.Print("Validated room 1:58's ordered hobo/Impa/Nayru composition, " +
            "all eight getGameProgress_2 hobo states and linked texts, exact " +
            "state-$06 relocation, Flame of Despair predicates, directional " +
            "facing, flag precedence, negative room-table cases, and re-entry.");
    }

    private void ValidateRoom175NpcInteractions()
    {
        var validationRoot = new Node { Name = "Room175NpcValidation" };
        AddChild(validationRoot);
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var manager = new RoomEntityManager(
            validationRoot, new NpcDatabase(), new EnemyDatabase(), save);

        void SetTreasure(OracleSaveData target, int treasure, bool value)
        {
            int address = 0xc69a + treasure / 8;
            byte mask = (byte)(1 << (treasure & 7));
            byte current = target.ReadWramByte(address);
            byte next = value ? (byte)(current | mask) : (byte)(current & ~mask);
            if (target.WriteWramByte(address, next))
                target.CommitInventoryChange();
        }

        void SetLinked(OracleSaveData target, bool value)
        {
            target.SetLinkedGame(value);
        }

        manager.LoadRoom(1, _world.LoadRoom(1, 0x75));
        List<NpcCharacter> actors = manager.Entities<NpcCharacter>();
        if (actors.Count != 7 ||
            actors[0].Record is not { Id: 0x37, SubId: 0x0a } ||
            actors[1].Record is not { Id: 0x31, SubId: 0x04 } ||
            actors[2].Record is not { Id: 0x31, SubId: 0x05 } ||
            actors[3].Record is not { Id: 0x36, SubId: 0x0a } ||
            actors[4].Record is not { Id: 0xad, SubId: 0x04 } ||
            actors[5].Record is not { Id: 0x58, SubId: 0x01, Var03: 0x00 } ||
            actors[6].Record is not { Id: 0x58, SubId: 0x01, Var03: 0x01 })
        {
            throw new InvalidOperationException(
                "Room 1:75 did not preserve its seven pre-Black Tower placements in object order.");
        }

        NpcCharacter ralph = actors[0];
        NpcCharacter impaUnlinked = actors[1];
        NpcCharacter impaLinked = actors[2];
        NpcCharacter nayru = actors[3];
        NpcCharacter zelda = actors[4];
        NpcCharacter earlyWorker = actors[5];
        NpcCharacter lateWorker = actors[6];
        if (ralph.Active || impaUnlinked.Active || impaLinked.Active ||
            nayru.Active || zelda.Active || !earlyWorker.Active || lateWorker.Active ||
            earlyWorker.TextId != 0x1007 || lateWorker.TextId != 0x1008 ||
            !earlyWorker.Record.CanFace || !lateWorker.Record.CanFace)
        {
            throw new InvalidOperationException(
                "Room 1:75's initial hardhat/story actor selection or TX_1007/TX_1008 records were wrong.");
        }

        save.SetRoomFlag(0, 0xba, OracleSaveData.RoomFlag40);
        if (earlyWorker.Active || !lateWorker.Active)
            throw new InvalidOperationException(
                "getBlackTowerProgress $01 did not swap room 1:75 to hardhat var03 $01.");

        save.SetRoomFlag(5, 0x90, OracleSaveData.RoomFlag40);
        save.SetRoomFlag(1, 0x90, OracleSaveData.RoomFlag40);
        if (earlyWorker.Active || !lateWorker.Active)
            throw new InvalidOperationException(
                "Non-present room-table $90 bits incorrectly changed room 1:75's hardhat phase.");

        save.SetRoomFlag(0, 0x90, OracleSaveData.RoomFlag40);
        if (earlyWorker.Active || lateWorker.Active)
            throw new InvalidOperationException(
                "Room 0:90 flag $40 did not take precedence as getBlackTowerProgress $02.");

        save.SetRoomFlag(0, 0x90, OracleSaveData.RoomFlag40, value: false);
        save.SetRoomFlag(0, 0xba, OracleSaveData.RoomFlag40, value: false);
        if (!earlyWorker.Active || lateWorker.Active)
            throw new InvalidOperationException(
                "Clearing both Black Tower progress bits did not restore hardhat var03 $00 live.");

        SetTreasure(save, TreasureDatabase.TreasureMakuSeed, value: true);
        if (!ralph.Active || !impaUnlinked.Active || impaLinked.Active ||
            nayru.Active || zelda.Active)
        {
            throw new InvalidOperationException(
                "Unlinked Maku Seed state did not reveal only Ralph and Impa $31:$04 in room 1:75.");
        }
        save.SetGlobalFlag(OracleSaveData.GlobalFlagRalphEnteredBlackTower);
        if (ralph.Active || !impaUnlinked.Active)
            throw new InvalidOperationException(
                "GLOBALFLAG_RALPH_ENTERED_BLACK_TOWER did not remove only Ralph before Impa's scene.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagPreBlackTowerCutsceneDone);
        if (impaUnlinked.Active)
            throw new InvalidOperationException(
                "GLOBALFLAG_PRE_BLACK_TOWER_CUTSCENE_DONE did not remove unlinked Impa.");

        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagPreBlackTowerCutsceneDone, value: false);
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagRalphEnteredBlackTower, value: false);
        SetLinked(save, value: true);
        if (!ralph.Active || impaUnlinked.Active || !impaLinked.Active ||
            !nayru.Active || !zelda.Active ||
            impaLinked.Record.DefaultAnimation != 3 ||
            nayru.Record.DefaultAnimation != 1)
        {
            throw new InvalidOperationException(
                "Linked room 1:75 did not reveal Ralph/Impa $05/Nayru/Zelda with animations $03/$01.");
        }

        manager.Clear();
        RemoveChild(validationRoot);
        validationRoot.QueueFree();

        PreBlackTowerEvent roomEvent = _roomEvents.PreBlackTower;
        ValidationCutsceneTrace trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        SetTreasure(_saveData, TreasureDatabase.TreasureMakuSeed, value: true);
        SetLinked(_saveData, value: false);
        _saveData.SetGlobalFlag(
            OracleSaveData.GlobalFlagPreBlackTowerCutsceneDone, value: false);
        _saveData.SetGlobalFlag(
            OracleSaveData.GlobalFlagRalphEnteredBlackTower, value: false);
        _player.WarpTo(new Vector2(0x50, 0x10));
        LoadValidationRoom(1, 0x75);
        _player.WarpTo(new Vector2(0x50, 0x10));
        if (roomEvent.Stage != PreBlackTowerEventEventStage.RalphUnlinkedNative ||
            !_player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "Unlinked room 1:75 did not begin Ralph's native entrance choreography.");
        }

        for (int frame = 0; frame < 1600 && roomEvent.HasState; frame++)
        {
            StepRoomEventFrames(1);
            if (_dialogue.IsOpen)
                _dialogue.Close();
        }
        int[] unlinkedTexts = trace.Observations
            .Where(entry => entry.Observation == "Dialogue")
            .Select(entry => entry.Value)
            .ToArray();
        int[] expectedUnlinked = [0x2a19, 0x0124, 0x0125, 0x1d12, 0x0126, 0x1d13];
        if (roomEvent.HasState || _player.CutsceneControlled ||
            !_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagRalphEnteredBlackTower) ||
            !_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagPreBlackTowerCutsceneDone) ||
            !unlinkedTexts.SequenceEqual(expectedUnlinked))
        {
            throw new InvalidOperationException(
                "Unlinked room 1:75 did not complete its exact Ralph/Impa/Nayru dialogue and flag sequence.");
        }

        trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        SetLinked(_saveData, value: true);
        _saveData.SetGlobalFlag(
            OracleSaveData.GlobalFlagPreBlackTowerCutsceneDone, value: false);
        _saveData.SetGlobalFlag(
            OracleSaveData.GlobalFlagRalphEnteredBlackTower, value: false);
        LoadValidationRoom(1, 0x75);
        _player.WarpTo(new Vector2(0x50, 0x20));
        if (roomEvent.Stage != PreBlackTowerEventEventStage.Linked ||
            !_player.CutsceneControlled || roomEvent.SharedSignal != 0)
        {
            throw new InvalidOperationException(
                "Linked room 1:75 did not start its four ordered actor lanes at signal $00.");
        }
        for (int frame = 0; frame < 1600 && roomEvent.HasState; frame++)
        {
            StepRoomEventFrames(1);
            if (_dialogue.IsOpen)
                _dialogue.Close();
        }
        int[] linkedTexts = trace.Observations
            .Where(entry => entry.Observation == "Dialogue")
            .Select(entry => entry.Value)
            .ToArray();
        int[] expectedLinked = [0x2a19, 0x0125, 0x1d12, 0x0607];
        if (roomEvent.HasState || _player.CutsceneControlled ||
            roomEvent.SharedSignal != 0x08 ||
            !_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagPreBlackTowerCutsceneDone) ||
            _saveData.HasGlobalFlag(OracleSaveData.GlobalFlagRalphEnteredBlackTower) ||
            !linkedTexts.SequenceEqual(expectedLinked))
        {
            throw new InvalidOperationException(
                "Linked room 1:75 did not complete its ordered $cfd0 actor lanes and dialogue sequence.");
        }
        _roomEvents.CommandTraceSink = null;

        GD.Print("Validated room 1:75's ordered ensemble, exact hardhat getBlackTowerProgress " +
            "predicates/texts, linked/unlinked actor gates, forced movement, shared signals, " +
            "spawned Nayru, gravity, dialogue order, and completion flags.");
    }

    private void ValidateRoom176NpcInteractions()
    {
        const int group = 1;
        const int roomId = 0x76;
        BlackTowerDoorwayEvent doorway = _roomEvents.BlackTowerDoorway;
        BlackTowerDoorwayEventDatabaseRecord record = doorway.Database.Data;
        if (record is not
            { InteractionId: 0xdc, SubId: 0x10, Y: 0x42, X: 0x50,
              ClearPositionA: 0x44, ClearPositionB: 0x45,
              ObjectRadiusY: 0x04, ObjectRadiusX: 0x10,
              LinkRadiusY: 0x06, LinkRadiusX: 0x06,
              RoomFlagMask: OracleSaveData.RoomFlagLayoutSwap,
              ClearDestinationGroup: 4, ClearDestinationRoom: 0xe7,
              SetDestinationGroup: 4, SetDestinationRoom: 0xf3,
              WarpTransition: 0x93, DestinationPosition: 0xff,
              WarpTransition2: 0x01, Sound: OracleSoundEngine.SndEnterCave })
        {
            throw new InvalidOperationException(
                "Room 1:76 did not import the exact $dc:$10 doorway record.");
        }

        Vector2 firstDoorTile = new(0x48, 0x48);
        Vector2 secondDoorTile = new(0x58, 0x48);
        (bool FlagSet, int FirstTile, int SecondTile, int Destination)[] branches =
        [
            (false, 0xdf, 0xed, 0xe7),
            (true, 0xee, 0xef, 0xf3)
        ];

        foreach ((bool flagSet, int firstTile, int secondTile, int destination) in branches)
        {
            _saveData.SetRoomFlag(
                group, roomId, OracleSaveData.RoomFlagLayoutSwap, flagSet);
            LoadValidationRoom(group, roomId);
            if (_currentRoom.GetOriginalMetatile(firstDoorTile) != firstTile ||
                _currentRoom.GetOriginalMetatile(secondDoorTile) != secondTile ||
                doorway.Stage != BlackTowerDoorwayEventEventStage.Initialize)
            {
                throw new InvalidOperationException(
                    $"Room 1:76 flag ${record.RoomFlagMask:x2}={flagSet} did not load its expected entrance layout or $dc:$10 initializer.");
            }
            var doorwayPixelsBefore = new Color[0x20 * 0x10];
            for (int pixelY = 0x40; pixelY < 0x50; pixelY++)
            for (int pixelX = 0x40; pixelX < 0x60; pixelX++)
            {
                doorwayPixelsBefore[(pixelY - 0x40) * 0x20 + pixelX - 0x40] =
                    _currentRoom.GetRenderedPixelForValidation(
                        new Vector2I(pixelX, pixelY));
            }

            _player.WarpTo(new Vector2(record.X, record.Y));
            _sound.ClearPlayRequestAudit();
            StepRoomEventFrames(1);
            if (_currentRoom.GetMetatile(firstDoorTile) != 0x00 ||
                _currentRoom.GetMetatile(secondDoorTile) != 0x00 ||
                doorway.Stage != BlackTowerDoorwayEventEventStage.WaitForExit ||
                _transitions.IsTransitioning)
            {
                throw new InvalidOperationException(
                    "Room 1:76 state 0 did not clear $44/$45 and retain an initially overlapping Link in state 1.");
            }
            for (int pixelY = 0x40; pixelY < 0x50; pixelY++)
            for (int pixelX = 0x40; pixelX < 0x60; pixelX++)
            {
                Color clearedPixel = _currentRoom.GetRenderedPixelForValidation(
                    new Vector2I(pixelX, pixelY));
                Color pixelBefore = doorwayPixelsBefore[
                    (pixelY - 0x40) * 0x20 + pixelX - 0x40];
                if (clearedPixel != pixelBefore)
                {
                    throw new InvalidOperationException(
                        $"Room 1:76 logical doorway clear redrew pixel ({pixelX:x2},{pixelY:x2}).");
                }
            }

            StepRoomEventFrames(1);
            if (doorway.Stage != BlackTowerDoorwayEventEventStage.WaitForExit ||
                _transitions.IsTransitioning)
            {
                throw new InvalidOperationException(
                    "Room 1:76 state 1 warped before Link left the entrance rectangle.");
            }

            // Combined X radius is $10+$06=$16 and collision is strict. At
            // exactly +$16 the interaction arms without triggering.
            _player.WarpTo(new Vector2(
                record.X + record.ObjectRadiusX + record.LinkRadiusX,
                record.Y));
            StepRoomEventFrames(1);
            if (doorway.Stage != BlackTowerDoorwayEventEventStage.Armed ||
                _transitions.IsTransitioning)
            {
                throw new InvalidOperationException(
                    "Room 1:76 did not use the original strict combined collision-radius boundary.");
            }

            _player.WarpTo(new Vector2(
                record.X + record.ObjectRadiusX + record.LinkRadiusX - 1,
                record.Y));
            _dialogue.ShowMessage("Doorway vulnerability gate", record.Y);
            StepRoomEventFrames(1);
            if (doorway.Stage != BlackTowerDoorwayEventEventStage.Armed ||
                _transitions.IsTransitioning)
            {
                throw new InvalidOperationException(
                    "Room 1:76 ignored checkLinkVulnerable's active-text gate.");
            }
            _dialogue.Close();
            StepRoomEventFrames(1);
            if (_activeGroup != 4 || _currentRoom.Id != destination ||
                !_transitions.IsTransitioning || doorway.HasState ||
                _player.Position != new Vector2(0x78, _currentRoom.Height) ||
                _player.FacingVector != Vector2I.Up ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != 1)
            {
                throw new InvalidOperationException(
                    $"Room 1:76 flag ${record.RoomFlagMask:x2}={flagSet} did not enter 4:{destination:x2} through $93/$ff/$01 with SND_ENTERCAVE.");
            }

            UpdateRoomWarpTransition(RoomTransitionController.WarpFadeFrames / 60.0);
            if (_transitions.IsTransitioning ||
                _player.Position != new Vector2(
                    0x78, _currentRoom.Height - RoomTransitionController.WarpEnterFrames) ||
                _player.FacingVector != Vector2I.Up)
            {
                throw new InvalidOperationException(
                    "The room 1:76 destination did not complete its 28-update middle-bottom entrance within the 32-update fade.");
            }
        }

        _saveData.SetRoomFlag(
            group, roomId, OracleSaveData.RoomFlagLayoutSwap, value: false);
        GD.Print("Validated room 1:76 INTERAC_MISCELLANEOUS_2 $dc:$10 nonvisual layout clears, " +
            "initial-overlap exit latch, strict $04/$10+$06/$06 collision, current-room " +
            "flag $01 destinations 4:e7/4:f3, $93/$ff/$01 entrance, and SND_ENTERCAVE.");
    }

    private void ValidateLynnaShopInteractions()
    {
        const int group = 2;
        const int roomId = 0x5e;
        var database = new LynnaShopDatabase();

        static void SetTreasure(OracleSaveData save, int treasure, bool value)
        {
            int address = 0xc69a + treasure / 8;
            byte mask = (byte)(1 << (treasure & 7));
            byte previous = save.ReadWramByte(address);
            byte next = value ? (byte)(previous | mask) : (byte)(previous & ~mask);
            if (save.WriteWramByte(address, next))
                save.CommitInventoryChange();
        }

        // Exercise each stock predicate independently of accumulated gameplay
        // validation state.
        OracleSaveData predicateSave = OracleSaveData.CreateStandardGame();
        SetTreasure(predicateSave, TreasureDatabase.TreasureBombs, value: false);
        SetTreasure(predicateSave, 0x0b, value: false);
        SetTreasure(predicateSave, 0x0e, value: false);
        predicateSave.SetGlobalFlag(database.GlobalCanBuyFlute, value: false);
        predicateSave.SetLinkedGame(false);
        predicateSave.WriteWramByte(database.BoughtItems1Address, 0);
        predicateSave.WriteWramByte(database.BoughtItems2Address, 0);
        predicateSave.WriteWramByte(0xc6af, 0);
        IReadOnlyList<StockRecord> stock =
            database.ResolveStock(predicateSave);
        if (stock.Count != 2 || stock[0].Item.SubId != 0x01 ||
            stock[1].Item.SubId != 0x03 ||
            (predicateSave.ReadWramByte(database.BoughtItems2Address) &
                database.BombchuMissingMask) == 0)
        {
            throw new InvalidOperationException(
                "Room 2:5e base stock did not hide bombs or record missing Bombchus.");
        }

        SetTreasure(predicateSave, TreasureDatabase.TreasureBombs, value: true);
        stock = database.ResolveStock(predicateSave);
        if (stock.Count != 3 || stock[2].Item.SubId != 0x04)
            throw new InvalidOperationException(
                "Owning TREASURE_BOMBS did not reveal $47:$04 in room 2:5e.");

        predicateSave.SetGlobalFlag(database.GlobalCanBuyFlute);
        stock = database.ResolveStock(predicateSave);
        if (stock[0].Item.SubId != 0x0d || stock[0].X != 0x84 ||
            (predicateSave.ReadWramByte(database.BoughtItems2Address) &
                database.FluteStockMask) == 0)
        {
            throw new InvalidOperationException(
                "GLOBALFLAG_CAN_BUY_FLUTE did not replace hearts with the X+4 Strange Flute.");
        }
        SetTreasure(predicateSave, 0x0e, value: true);
        stock = database.ResolveStock(predicateSave);
        if (stock[0].Item.SubId != 0x01 ||
            (predicateSave.ReadWramByte(database.BoughtItems2Address) &
                database.FluteStockMask) != 0)
        {
            throw new InvalidOperationException(
                "Obtaining TREASURE_FLUTE did not restore the recurring heart stock.");
        }

        predicateSave.SetLinkedGame(true);
        stock = database.ResolveStock(predicateSave);
        if (stock[1].Item.SubId != 0x13)
            throw new InvalidOperationException(
                "Linked-game state did not replace the normal shield with Gasha Seed $13.");
        predicateSave.WriteWramByte(
            database.BoughtItems1Address, (byte)database.NormalGashaBoughtMask);
        stock = database.ResolveStock(predicateSave);
        if (stock[1].Item.SubId != 0x03)
            throw new InvalidOperationException(
                "Bought normal-shop Gasha bit $20 did not restore shield stock.");

        predicateSave.SetLinkedGame(false);
        predicateSave.WriteWramByte(0xc6af, 0x02);
        stock = database.ResolveStock(predicateSave);
        if (stock[1].Item.SubId != 0x11)
            throw new InvalidOperationException(
                "wShieldLevel bit 1 did not replace L1 with the L2 shield.");
        predicateSave.WriteWramByte(0xc6af, 0x03);
        stock = database.ResolveStock(predicateSave);
        if (stock[1].Item.SubId != 0x12)
            throw new InvalidOperationException(
                "wShieldLevel bits 1+0 did not follow the L1 -> L2 -> L3 chain.");

        predicateSave.WriteWramByte(
            database.DimitriStateAddress, (byte)database.DimitriSavedMask);
        database.ApplyCompanionEntryState(predicateSave);
        if (predicateSave.ReadWramByte(database.DimitriStateAddress) !=
            (database.DimitriSavedMask | database.DimitriDisappearMask))
        {
            throw new InvalidOperationException(
                "$71:$0c did not promote wDimitriState bit 5 to bit 6 on shop entry.");
        }

        // Instantiate the real room and drive the native lift, theft, reject,
        // and successful recurring-heart purchase paths.
        bool linkedBefore = _saveData.IsLinkedGame;
        bool fluteFlagBefore = _saveData.HasGlobalFlag(database.GlobalCanBuyFlute);
        byte bought1Before = _saveData.ReadWramByte(database.BoughtItems1Address);
        byte bought2Before = _saveData.ReadWramByte(database.BoughtItems2Address);
        byte dimitriBefore = _saveData.ReadWramByte(database.DimitriStateAddress);
        byte shieldBefore = _saveData.ReadWramByte(0xc6af);
        int bombsFlagAddress = 0xc69a + TreasureDatabase.TreasureBombs / 8;
        byte bombsFlagsBefore = _saveData.ReadWramByte(bombsFlagAddress);

        _saveData.SetLinkedGame(false);
        _saveData.SetGlobalFlag(database.GlobalCanBuyFlute, value: false);
        _saveData.WriteWramByte(database.BoughtItems1Address, 0);
        _saveData.WriteWramByte(database.BoughtItems2Address, 0);
        _saveData.WriteWramByte(database.DimitriStateAddress, 0);
        _saveData.WriteWramByte(0xc6af, 0);
        SetTreasure(_saveData, TreasureDatabase.TreasureBombs, value: false);
        LoadValidationRoom(group, roomId);

        LynnaShopEvent shop = _roomEvents.LynnaShop;
        List<LynnaShopItem> products = _entities.Entities<LynnaShopItem>();
        NpcCharacter shopkeeper = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x46, SubId: 0x00 });
        if (products.Count != 2 ||
            products[0].Record.SubId != 0x01 || products[0].Position != new Vector2(0x80, 0x28) ||
            products[0].PricePosition != new Vector2(0x78, 0x18) ||
            products[1].Record.SubId != 0x03 || products[1].Position != new Vector2(0x68, 0x28) ||
            products[1].PricePosition != new Vector2(0x60, 0x18) ||
            products.Any(item => item.CurrentPixelHash == 0 ||
                item.DigitPixelHash == 0 || item.DigitColorCount != 2) ||
            shopkeeper.CurrentScriptAnimationSource != database.Animation(0x46, 3) ||
            shopkeeper.TextId != 0 || !_entities.PlayerItemUsageDisabled ||
            !_entities.PlayerRingTransformationsDisabled ||
            _entities.PlayerSwordDisabled)
        {
            throw new InvalidOperationException(
                "Room 2:5e did not instantiate its surviving $47 stock, BG prices, " +
                "$46:$00 shopkeeper, or retail shop input restrictions in source order.");
        }
        StepRoomEventFrames(130);
        if (products.Any(item => item.AnimationFrame != 0))
        {
            throw new InvalidOperationException(
                "INTERAC_SHOP_ITEM advanced its initialized graphics even though states 1/2 " +
                "never call interactionAnimate.");
        }

        if (!shop.TryInteractNpc(shopkeeper) ||
            shop.Stage != LynnaShopEventEventStage.ShopkeeperText ||
            !_dialogue.CurrentMessage.StartsWith("Welcome, sir!", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Lynna shopkeeper did not run the empty-handed TX_0e00 welcome script.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1);

        LynnaShopItem hearts = products[0];
        _player.WarpTo(hearts.ShelfPosition + Vector2.Down * 20, recordSafe: false);
        _player.Face(Vector2I.Up);
        if (shop.TryInteractPlayer(_player))
        {
            throw new InvalidOperationException(
                "$07+$06 shop-item collision accepted the first point beyond its source boundary.");
        }
        _player.WarpTo(hearts.ShelfPosition + Vector2.Down * 19, recordSafe: false);
        if (!_playerWorld.TryInteract(_player))
        {
            throw new InvalidOperationException(
                "PlayerWorld's normal A-button interaction route did not accept shop stock.");
        }
        if (shop.Stage != LynnaShopEventEventStage.Holding ||
            !hearts.Held || !_player.IsCarryingObject ||
            _player.IsHoldingItemTwoHands ||
            hearts.Position != _player.Position + new Vector2(0, -13))
        {
            throw new InvalidOperationException(
                "Normal A-button input did not lift $47:$01 with wLinkGrabState=$83, " +
                "wLinkGrabState2=$08, and the held-walk pose at the source boundary.");
        }
        _player.Face(Vector2I.Right);
        hearts.UpdateFrame(_player);
        if (hearts.Position != _player.Position + new Vector2(0, -14))
        {
            throw new InvalidOperationException(
                "Held shop stock did not use the source's side-facing walk-phase-2 Z=-$0e.");
        }
        _player.Face(Vector2I.Up);
        hearts.UpdateFrame(_player);

        Vector2 shopkeeperStart = shopkeeper.Position;
        _player.WarpTo(new Vector2(_player.Position.X, database.TheftLinkY + 1),
            recordSafe: false);
        StepRoomEventFrames(1);
        if (shop.Stage != LynnaShopEventEventStage.TheftDown ||
            _player.Position.Y != database.TheftLinkY || !_player.CutsceneControlled ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) == 0)
        {
            throw new InvalidOperationException(
                "Crossing shop Y=$69 with held stock did not clamp Link and start theft prevention.");
        }
        StepRoomEventFrames(4 + 12);
        if (shop.Stage != LynnaShopEventEventStage.TheftText ||
            shopkeeper.Position != shopkeeperStart + new Vector2(-24, 8) ||
            !_dialogue.CurrentMessage.StartsWith("Hey! Don't just", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Shopkeeper did not complete the SPEED_200 down-8/left-24 theft approach.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1 + 12 + 4);
        if (shop.Stage != LynnaShopEventEventStage.Holding ||
            shopkeeper.Position != shopkeeperStart || _player.CutsceneControlled || !hearts.Held)
        {
            throw new InvalidOperationException(
                "Shopkeeper theft script did not return right-24/up-8 and restore Link's held item.");
        }

        _player.WarpTo(hearts.ShelfPosition + Vector2.Down * 16, recordSafe: false);
        _player.Face(Vector2I.Up);
        if (!_playerWorld.TrySecondaryInteract(_player))
        {
            throw new InvalidOperationException(
                "PlayerWorld's normal B-button interaction route did not return shop stock.");
        }
        if (shop.HasState || hearts.Held ||
            _player.IsCarryingObject || _player.IsHoldingItemTwoHands)
        {
            throw new InvalidOperationException(
                "Normal B-button input did not return held shop stock at the strict " +
                "Y<$3d/X+-$0d/up-facing boundary.");
        }

        // Full health maps to shopkeeperCantBuy/TX_0e05 and returns the item.
        _inventory.RefillHealth();
        if (!shop.TryInteractPlayer(_player) || !shop.TryInteractNpc(shopkeeper))
            throw new InvalidOperationException("Could not lift and present the heart stock.");
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        if (shop.Stage != LynnaShopEventEventStage.PurchaseRejected ||
            !_dialogue.CurrentMessage.StartsWith("You have it.", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Full-health 3-Hearts purchase did not select shopkeeperCantBuy TX_0e05.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1);
        if (shop.HasState || hearts.Held || _player.IsCarryingObject)
            throw new InvalidOperationException("Rejected shop stock did not return to its shelf.");

        int rupeesBefore = _inventory.Rupees;
        _inventory.AddRupees(200);
        _inventory.ApplyDamage(12);
        int damagedHealth = _inventory.HealthQuarters;
        _player.WarpTo(hearts.ShelfPosition + Vector2.Down * 16, recordSafe: false);
        _player.Face(Vector2I.Up);
        if (!shop.TryInteractPlayer(_player) || !shop.TryInteractNpc(shopkeeper))
            throw new InvalidOperationException("Could not retry the heart purchase.");
        if (!_dialogue.CurrentMessage.Contains("10 Rupees", StringComparison.Ordinal) ||
            _dialogue.CurrentMessage.Contains("\\num1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "TX_0e02 did not substitute the imported 10-Rupee price.");
        }
        int purchaseRupees = _inventory.Rupees;
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        if (shop.Stage != LynnaShopEventEventStage.ItemText ||
            _inventory.Rupees != purchaseRupees - 10 ||
            _inventory.HealthQuarters != Math.Min(
                _inventory.MaxHealthQuarters, damagedHealth + 12) ||
            !_dialogue.CurrentMessage.StartsWith("You got three", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Successful $47:$01 purchase did not deduct 10, heal 12 quarters, and show TX_004c.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1);
        if (shop.HasState || !hearts.Removed || hearts.Visible ||
            _player.IsCarryingObject || _player.IsHoldingItemTwoHands)
        {
            throw new InvalidOperationException(
                "Purchased shop stock did not delete after its item textbox closed.");
        }

        _inventory.AddRupees(rupeesBefore - _inventory.Rupees);
        _saveData.SetLinkedGame(linkedBefore);
        _saveData.SetGlobalFlag(database.GlobalCanBuyFlute, fluteFlagBefore);
        _saveData.WriteWramByte(database.BoughtItems1Address, bought1Before);
        _saveData.WriteWramByte(database.BoughtItems2Address, bought2Before);
        _saveData.WriteWramByte(database.DimitriStateAddress, dimitriBefore);
        _saveData.WriteWramByte(0xc6af, shieldBefore);
        _saveData.WriteWramByte(bombsFlagAddress, bombsFlagsBefore);
        _saveData.CommitInventoryChange();

        GD.Print("Validated room 2:5e exact stock predicates/replacements, Dimitri $20->$60 " +
            "entry helper, source OAM/TREE_GFXH_03 BG prices, static stock graphics, " +
            "$5c-$5f/$88-$8b held-walk pose, A/B lift/return, full-item denial, " +
            "10-Rupee heart purchase, and SPEED_200 theft-prevention route.");
    }

    private void ValidateVasuShopInteractions()
    {
        const int group = 2;
        const int roomId = 0xee;
        VasuShopEvent shop = _roomEvents.VasuShop;
        VasuShopDatabase database = shop.Database;

        _saveData.SetGlobalFlag(database.GlobalObtainedRingBox, value: false);
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame, value: false);
        _saveData.SetLinkedGame(false);
        if (_saveData.WriteWramByte(database.ObtainedRingBoxAddress, 0))
            _saveData.CommitInventoryChange();
        LoadValidationRoom(group, roomId);

        List<NpcCharacter> actors = _entities.Entities<NpcCharacter>();
        if (actors.Count != 5 ||
            actors[0].Record is not { Id: 0x89, SubId: 0x00, Y: 0x28, X: 0x50 } ||
            actors[1].Record is not { Id: 0x89, SubId: 0x01, Y: 0x38, X: 0x38 } ||
            actors[2].Record is not { Id: 0x89, SubId: 0x06, Y: 0x38, X: 0x68 } ||
            actors[3].Record is not { Id: 0xe5, SubId: 0x00, Y: 0x48, X: 0x28,
                Palette: 1 } ||
            actors[4].Record is not { Id: 0xe5, SubId: 0x01, Y: 0x48, X: 0x78,
                Palette: 2 } ||
            actors.Any(actor => actor.TextId != 0 || actor.TextPosition != 2 ||
                actor.CurrentAnimationOpaquePixels == 0))
        {
            throw new InvalidOperationException(
                "Room 2:ee did not instantiate Vasu, both snakes, and both palette-distinct " +
                "ring-help books in original object order.");
        }

        NpcCharacter vasu = actors[0];
        NpcCharacter blue = actors[1];
        NpcCharacter red = actors[2];
        NpcCharacter basicsBook = actors[3];
        NpcCharacter secretsBook = actors[4];
        if (vasu.CurrentScriptAnimationSource != database.Animation(0x89, 0) ||
            blue.CurrentScriptAnimationSource != database.Animation(0x89, 1) ||
            red.CurrentScriptAnimationSource != database.Animation(0x89, 6) ||
            basicsBook.CurrentScriptAnimationSource != database.Animation(0xe5, 0) ||
            secretsBook.CurrentScriptAnimationSource != database.Animation(0xe5, 0) ||
            database.Text(0x3000).Contains("\\jump", StringComparison.Ordinal) ||
            database.Text(0x300b).Contains("\\jump", StringComparison.Ordinal) ||
            !database.Text(0x300b).Contains("Do you want\nto hear more?", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Room 2:ee animations or assembler-time TX_30XX call/jump expansion diverged.");
        }

        // Vasu is behind the counter. The generic sprite-grown talk box ends
        // too early here; the retail A-sensitive path probes ten pixels ahead
        // and tests his strict $12/$06 collision radii.
        _player.WarpTo(new Vector2(vasu.Position.X, vasu.Position.Y + 27), recordSafe: false);
        _player.Face(Vector2I.Up);
        if (!TryInteract(_player) ||
            shop.Stage != EventStage.VasuFirstExplanation)
        {
            throw new InvalidOperationException(
                "Vasu could not be talked to across his counter at the final valid $12 boundary.");
        }
        shop.Cancel();
        _dialogue.Close();
        _player.WarpTo(new Vector2(vasu.Position.X, vasu.Position.Y + 28), recordSafe: false);
        _player.Face(Vector2I.Up);
        if (_entities.FindTalkTarget(_player) is not null)
        {
            throw new InvalidOperationException(
                "Vasu's strict $12 A-sensitive boundary accepted the first outside point.");
        }

        // The snake's state-1 NC branch resets its idle animation every update
        // outside the strict $18 Manhattan radius, then permits it to emerge
        // and animate when Link enters that radius.
        _player.WarpTo(new Vector2(0x78, 0x70), recordSafe: false);
        StepRoomEventFrames(20);
        if (blue.CurrentAnimationFrame != 0)
            throw new InvalidOperationException(
                "Blue Snake was not pinned to its hidden first frame outside distance $18.");
        _player.WarpTo(blue.Position + new Vector2(0, 0x10), recordSafe: false);
        StepRoomEventFrames(18);
        if (blue.CurrentAnimationFrame == 0)
            throw new InvalidOperationException(
                "Blue Snake did not emerge and animate inside distance $18.");

        // Both books retain their source loops and choices. Subid $00 can
        // revisit either topic; subid $01 is a single read/don't-read prompt.
        if (!shop.TryInteractNpc(basicsBook) ||
            shop.Stage != EventStage.BookBasicsInitial ||
            !_dialogue.CurrentMessage.Contains("Ring Link\nBasics", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The blue-snake help book did not open TX_3020.");
        }
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        if (shop.Stage != EventStage.BookBasicsFortune ||
            !_dialogue.CurrentMessage.Contains("Ring Fortunes", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Ring Link Basics book did not select its Fortune topic.");
        }
        _dialogue.SubmitChoiceForValidation(1);
        StepRoomEventFrames(1);
        if (shop.HasState)
            throw new InvalidOperationException("The Ring Link Basics book did not reset on Don't.");

        if (!shop.TryInteractNpc(secretsBook))
            throw new InvalidOperationException("The ring-secret help book was not button-sensitive.");
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        if (shop.Stage != EventStage.BookSecretsText ||
            !_dialogue.CurrentMessage.Contains("Red\nSnake", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The ring-secret help book did not show TX_301a after Read.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1);

        // Red Snake's nonlinked tutorial waits exactly 30 updates between the
        // outer and inner choices, expands the TX_3016 jump, and uses its talk
        // and parameter-terminated retreat animations.
        if (!shop.TryInteractNpc(red) ||
            shop.Stage != EventStage.RedInitial ||
            red.CurrentScriptAnimationSource != database.Animation(0x89, 7))
        {
            throw new InvalidOperationException(
                "Red Snake did not select the prelinked TX_3009/talk-animation path.");
        }
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        StepRoomEventFrames(database.RedSnakeWait - 1);
        if (_dialogue.IsOpen || shop.Counter != 1)
            throw new InvalidOperationException("Red Snake's wait completed before update 30.");
        StepRoomEventFrames(1);
        if (!_dialogue.IsOpen || shop.Stage != EventStage.RedTopic)
            throw new InvalidOperationException("Red Snake did not open TX_300a on update 30.");
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        if (!_dialogue.CurrentMessage.Contains("Do you want\nto hear more?", StringComparison.Ordinal))
            throw new InvalidOperationException("Red Snake did not jump from TX_300b to TX_3016.");
        _dialogue.SubmitChoiceForValidation(1);
        StepRoomEventFrames(1);
        if (shop.Stage != EventStage.SnakeRetreat ||
            red.CurrentScriptAnimationSource != database.Animation(0x89, 8))
        {
            throw new InvalidOperationException("Red Snake did not begin animation $08 cleanup.");
        }
        StepRoomEventFrames(16);
        if (shop.HasState || red.CurrentScriptAnimationSource != database.Animation(0x89, 6))
            throw new InvalidOperationException(
                "Red Snake did not return to idle when animation $08 set animParameter.");

        // Without a Ring Box, FINISHEDGAME alone must not select the linked
        // snake table. The blue fortune then performs the original 16-bit
        // $0200 counter before reporting the absent cable.
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        if (!shop.TryInteractNpc(blue) ||
            shop.Stage != EventStage.BlueInitial)
        {
            throw new InvalidOperationException(
                "FINISHEDGAME incorrectly selected Blue Snake's linked script without a Ring Box.");
        }
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        StepRoomEventFrames(database.BlueSnakeCableTimeout - 1);
        if (_dialogue.IsOpen || shop.Counter != 1)
            throw new InvalidOperationException(
                "Blue Snake reported a missing cable before 512 serial-wait updates.");
        StepRoomEventFrames(1);
        if (!_dialogue.IsOpen ||
            !_dialogue.CurrentMessage.Contains("Is your cable\nconnected?", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Blue Snake did not show TX_300f on the 512th no-cable update.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1 + 16);
        if (shop.HasState || blue.CurrentScriptAnimationSource != database.Animation(0x89, 1))
            throw new InvalidOperationException("Blue Snake did not complete animation $03 cleanup.");
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame, value: false);

        // Exercise the ordinary first-time Vasu reward chain through both
        // mandatory ring menus, including the free appraisal and box move.
        int unappraisedBefore = _inventory.UnappraisedRingCount;
        _sound.ClearPlayRequestAudit();
        if (!shop.TryInteractNpc(vasu) ||
            shop.Stage != EventStage.VasuFirstExplanation ||
            !_dialogue.CurrentMessage.Contains("Understood?", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Vasu did not open expanded TX_3000/TX_303a.");
        }
        _dialogue.SubmitChoiceForValidation(1);
        StepRoomEventFrames(1);
        if (shop.Stage != EventStage.VasuFirstExplanation ||
            !_dialogue.CurrentMessage.StartsWith("Rings made from", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Vasu's No answer did not repeat TX_303a.");
        }
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        _dialogue.Close();
        StepRoomEventFrames(1);
        if (shop.Stage != EventStage.VasuRingBoxReward ||
            _inventory.RingBoxLevel != 1 || !_player.IsHoldingItemTwoHands ||
            _entities.Entities<GroundTreasurePickup>().Count == 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 2)
        {
            throw new InvalidOperationException(
                "vasu_giveRingBox did not grant/show the L-1 Ring Box with two-hand audio.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1);
        _dialogue.Close();
        StepRoomEventFrames(1);
        if (shop.Stage != EventStage.VasuFriendshipReward ||
            _inventory.UnappraisedRingCount != unappraisedBefore + 1 ||
            _inventory.UnappraisedRingAt(unappraisedBefore) != 0x40 ||
            !_player.IsHoldingItemOneHand ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetSeed) != 1)
        {
            throw new InvalidOperationException(
                "vasu_giveFriendshipRing did not grant ring $00 through one-hand TREASURE_RING.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1);
        if (shop.Stage != EventStage.VasuAppraisalHandoff ||
            !_dialogue.CurrentMessage.Contains("Let's appraise", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Vasu did not reach the forced appraisal prompt TX_3033.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1);
        if (shop.Stage != EventStage.VasuFirstAppraisalMenu ||
            !_ringMenu.IsActive)
        {
            throw new InvalidOperationException(
                "Vasu did not open the mandatory first appraisal menu.");
        }
        _ringMenu.Update(22.0 / 60.0);
        if (!_ringMenuScreen.Visible ||
            _ringMenuScreen.Mode != RingMenuMode.Appraisal ||
            _ringMenuScreen.BackgroundHashForValidation == 0 ||
            !_inventory.TryBeginRingAppraisal(unappraisedBefore, 0, out int friendship) ||
            friendship != database.RingFriendship)
        {
            throw new InvalidOperationException(
                "The mandatory appraisal menu did not render or begin the free Friendship Ring appraisal.");
        }
        RingAppraisalResult firstResult =
            _inventory.CompleteRingAppraisal(
                unappraisedBefore, database.DuplicateRefund);
        if (firstResult.Duplicate ||
            !_inventory.HasAppraisedRing(database.RingFriendship) ||
            _inventory.RingsAppraised != 1)
        {
            throw new InvalidOperationException(
                "The free Friendship Ring appraisal did not update c616/c6ce.");
        }
        _ringMenu.CloseImmediatelyForValidation();
        StepRoomEventFrames(database.MenuCloseWait);
        if (shop.Stage != EventStage.VasuFirstListIntroduction ||
            !_dialogue.CurrentMessage.Contains("Now, the List!", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Vasu did not resume at TX_3013 ten updates after appraisal close " +
                $"(stage {shop.Stage}, counter {shop.Counter}, text '{_dialogue.CurrentMessage}').");
        }
        _dialogue.Close();
        StepRoomEventFrames(1);
        _ringMenu.Update(22.0 / 60.0);
        if (shop.Stage != EventStage.VasuFirstListMenu ||
            _ringMenuScreen.Mode != RingMenuMode.List ||
            _ringMenuScreen.BackgroundHashForValidation == 0 ||
            !_inventory.SetRingBoxSlotFromList(0, database.RingFriendship))
        {
            throw new InvalidOperationException(
                "Vasu's mandatory ring list did not render or accept the Friendship Ring into slot 0.");
        }
        _ringMenu.CloseImmediatelyForValidation();
        StepRoomEventFrames(database.MenuCloseWait);
        if (shop.Stage != EventStage.VasuFirstFinalText ||
            !_dialogue.CurrentMessage.Contains("do nothing", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Vasu did not resume at TX_3008 ten updates after ring-list close.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1);
        if (shop.HasState ||
            !_saveData.HasGlobalFlag(database.GlobalObtainedRingBox) ||
            (_saveData.ReadWramByte(database.ObtainedRingBoxAddress) &
                database.LinkedFirstMask) == 0 ||
            _inventory.RingAt(0) != database.RingFriendship)
        {
            throw new InvalidOperationException(
                "The completed mandatory ring menus did not commit Vasu's flag, bit, and box contents.");
        }

        // Re-entry recreates Vasu. A linked save with bit 0 clear uses the
        // short TX_303e branch, skips the Friendship Ring, then commits both
        // redundant completion indicators after its Ring Box check.
        _saveData.SetGlobalFlag(database.GlobalObtainedRingBox, value: false);
        _saveData.WriteWramByte(database.ObtainedRingBoxAddress, 0);
        _saveData.SetLinkedGame(true);
        LoadValidationRoom(group, roomId);
        vasu = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x89, SubId: 0x00 });
        if (!shop.TryInteractNpc(vasu) ||
            shop.Stage != EventStage.VasuLinkedGreeting ||
            !_dialogue.CurrentMessage.StartsWith("Good to see you", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Linked wObtainedRingBox bit-$01-clear state did not select TX_303e.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1);
        if (shop.HasState ||
            !_saveData.HasGlobalFlag(database.GlobalObtainedRingBox) ||
            (_saveData.ReadWramByte(database.ObtainedRingBoxAddress) &
                database.LinkedFirstMask) == 0 ||
            _inventory.UnappraisedRingCount != unappraisedBefore)
        {
            throw new InvalidOperationException(
                "Vasu's linked first-time branch did not commit its flag/bit without another ring.");
        }

        // Ring Box plus linked/completed state selects the linked snake table;
        // removing both linked predicates returns to the tutorial table.
        blue = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x89, SubId: 0x01 });
        if (!shop.TryInteractNpc(blue) ||
            shop.Stage != EventStage.BlueLinkedMenu)
        {
            throw new InvalidOperationException(
                "Ring Box + linked save did not select Blue Snake's TX_3024 table.");
        }
        shop.Cancel();
        _dialogue.Close();
        _saveData.SetLinkedGame(false);
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame, value: false);
        if (!shop.TryInteractNpc(blue) ||
            shop.Stage != EventStage.BlueInitial)
        {
            throw new InvalidOperationException(
                "Clearing both linked predicates did not restore Blue Snake's prelinked script.");
        }
        shop.Cancel();
        _dialogue.Close();

        // With Vasu initialized and no unappraised rings, Appraise reaches
        // TX_3014; earned special-ring flags are claimed in source priority and grant
        // the concrete ring index before returning to the NPC loop.
        if (!shop.TryInteractNpc(vasu))
            throw new InvalidOperationException("Initialized Vasu was no longer talkable.");
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        if (shop.Stage != EventStage.VasuFinalText ||
            !_dialogue.CurrentMessage.Contains("have been\nappraised", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Vasu's empty unappraised-ring list did not select TX_3014.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1);

        _saveData.SetGlobalFlag(database.GlobalEarnedSlayer);
        _saveData.SetGlobalFlag(database.GlobalGotSlayer, value: false);
        int ringsBeforeSpecial = _inventory.UnappraisedRingCount;
        if (!shop.TryInteractNpc(vasu) ||
            shop.Stage != EventStage.VasuSpecialText ||
            !_saveData.HasGlobalFlag(database.GlobalGotSlayer))
        {
            throw new InvalidOperationException(
                "Vasu did not claim the first earned unreceived special ring before TX_3036.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1);
        if (_inventory.UnappraisedRingCount != ringsBeforeSpecial + 1 ||
            _inventory.UnappraisedRingAt(ringsBeforeSpecial) !=
                (database.RingSlayer | 0x40))
        {
            throw new InvalidOperationException(
                "Vasu did not pass SLAYERS_RING $34 through giveRingToLink.");
        }
        _dialogue.Close();
        StepRoomEventFrames(2);
        if (shop.HasState)
            throw new InvalidOperationException("Vasu's special-ring reward did not return to idle.");

        GD.Print("Validated room 2:ee's five ordered actors, exact book palettes/visuals, " +
            "snake $18 proximity/talk/retreat animations, 30-update red tutorial loop, " +
            "512-update blue no-cable result, Vasu first/linked introductions, Ring Box/" +
            "Friendship/special-ring rewards, forced appraisal/list UI, and Ring Box + " +
            "finished/linked predicates; only Game Link remains a deferred boundary.");
    }

    private void ValidateRoom186NpcInteractions()
    {
        const int group = 1;
        const int roomId = 0x86;
        var groundDatabase = new GroundTreasureDatabase();
        IReadOnlyList<GroundTreasureDatabaseRecord> groundRecords =
            groundDatabase.GetRoomRecords(group, roomId);
        if (groundRecords.Count != 1 ||
            groundRecords[0] is not
            { Order: 1, Y: 0x28, X: 0x78,
              TreasureObject: "TREASURE_OBJECT_HEART_PIECE_00",
              Sprite: "spr_quest_items_5", TileBase: 0x10,
              Palette: 0x02, CompletionTextId: 0x0049 } ||
            string.IsNullOrWhiteSpace(groundRecords[0].CompletionMessage))
        {
            throw new InvalidOperationException(
                "Room 1:86 did not import ordered $dc:$07 Heart Piece data.");
        }

        var completionInventory = new InventoryState(_treasures);
        TreasureObjectRecord heartPieceObject =
            _treasures.GetObject("TREASURE_OBJECT_HEART_PIECE_00");
        for (int piece = 0; piece < 4; piece++)
            completionInventory.GiveTreasure(heartPieceObject);
        int maxHealthBeforeCompletion = completionInventory.MaxHealthQuarters;
        completionInventory.CompleteHeartPieceSet(
            _treasures.GetObject("TREASURE_OBJECT_HEART_CONTAINER_00"));
        if (completionInventory.HeartPieces != 0 ||
            completionInventory.MaxHealthQuarters != maxHealthBeforeCompletion + 4 ||
            completionInventory.HealthQuarters !=
                completionInventory.MaxHealthQuarters)
        {
            throw new InvalidOperationException(
                "The fourth Heart Piece did not clear its counter and grant/refill a four-quarter Heart Container.");
        }

        var heartDialogue = new DialogueBox { Name = "HeartPieceTextboxValidation" };
        AddChild(heartDialogue);
        var heartDialogueInventory = new InventoryState(_treasures);
        for (int piece = 0; piece < 4; piece++)
            heartDialogueInventory.GiveTreasure(heartPieceObject);
        var heartDialogueSounds = new List<int>();
        int heartFilledEvents = 0;
        int heartAcceptedEvents = 0;
        heartDialogue.SetSoundPlayer(heartDialogueSounds.Add);
        heartDialogue.SetHeartPieceCountProvider(
            () => heartDialogueInventory.HeartPieces);
        heartDialogue.HeartPieceSetFilled += () =>
        {
            heartFilledEvents++;
            heartDialogueInventory.ResetCompletedHeartPieceSet();
        };
        heartDialogue.HeartPieceSetAccepted += () =>
        {
            heartAcceptedEvents++;
            heartDialogueInventory.GiveCompletedHeartContainer(
                _treasures.GetObject("TREASURE_OBJECT_HEART_CONTAINER_00"));
            heartDialogueSounds.Add(OracleSoundEngine.SndFilledHeartContainer);
            heartDialogue.ShowMessage(groundRecords[0].CompletionMessage, 0x48);
        };
        heartDialogue.ShowMessage("Heart!\\heartpiece\nAfter", 0x48);
        heartDialogue.RevealCurrentPageForValidation();
        ulong threeQuarterHash = heartDialogue.HeartPiecePixelHashForValidation();
        if (!heartDialogue.HeartPieceDisplayActive ||
            heartDialogue.HeartPieceDisplayCount != 3 ||
            heartDialogue.HeartPieceDisplayTimer != 30 || threeQuarterHash == 0)
        {
            throw new InvalidOperationException(
                "The inline Heart Piece control did not begin with its previous three-quarter graphic.");
        }
        heartDialogue.AdvanceHeartPieceClockForValidation(29.0 / 60.0);
        if (heartDialogue.HeartPieceDisplayTimer != 1 ||
            heartDialogueInventory.HeartPieces != 4 || heartFilledEvents != 0)
        {
            throw new InvalidOperationException(
                "The inline Heart Piece control completed before its 30th update.");
        }
        heartDialogue.AdvanceHeartPieceClockForValidation(1.0 / 60.0);
        ulong fullHeartHash = heartDialogue.HeartPiecePixelHashForValidation();
        if (heartDialogue.HeartPieceDisplayTimer != 0 ||
            heartDialogue.HeartPieceDisplayCount != 4 ||
            fullHeartHash == 0 || fullHeartHash == threeQuarterHash ||
            heartDialogueInventory.HeartPieces != 0 || heartFilledEvents != 1 ||
            heartDialogueSounds.Count(sound => sound == OracleSoundEngine.SndText2) != 1)
        {
            throw new InvalidOperationException(
                "The inline Heart Piece control did not fill/reset/sound on update 30.");
        }
        heartDialogue.AdvanceOrClose();
        if (heartAcceptedEvents != 1 ||
            heartDialogueInventory.MaxHealthQuarters != maxHealthBeforeCompletion + 4 ||
            heartDialogueInventory.HealthQuarters !=
                heartDialogueInventory.MaxHealthQuarters ||
            heartDialogue.CurrentMessage !=
                DialogueBox.PlainText(groundRecords[0].CompletionMessage) ||
            heartDialogueSounds.Count(sound =>
                sound == OracleSoundEngine.SndFilledHeartContainer) != 1)
        {
            throw new InvalidOperationException(
                "Accepting the full inline Heart did not grant/refill and hand off to TX_0049.");
        }
        heartDialogue.Close();
        RemoveChild(heartDialogue);
        heartDialogue.QueueFree();

        var validationRoot = new Node { Name = "Room186PredicateValidation" };
        AddChild(validationRoot);
        OracleSaveData isolatedSave = OracleSaveData.CreateStandardGame();
        var isolatedManager = new RoomEntityManager(
            validationRoot, new NpcDatabase(), new EnemyDatabase(), isolatedSave);
        isolatedManager.LoadRoom(group, _world.LoadRoom(group, roomId));
        NpcCharacter isolatedGuard = isolatedManager.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x58, SubId: 0x02 });
        GroundTreasurePickup isolatedHeart =
            isolatedManager.Entities<GroundTreasurePickup>().Single();
        if (!isolatedGuard.Active || isolatedGuard.TextId != 0x1003 ||
            isolatedGuard.Position != new Vector2(0x48, 0x38) ||
            isolatedHeart.Position != new Vector2(0x78, 0x28) ||
            isolatedHeart.PixelHash == 0)
        {
            throw new InvalidOperationException(
                "Room 1:86 did not create its initial guard and static Heart Piece visuals.");
        }

        isolatedSave.SetRoomFlag(
            group, roomId, OracleSaveData.RoomFlag80);
        if (isolatedGuard.TextId != 0x1004 ||
            isolatedGuard.Position != new Vector2(0x58, 0x38))
        {
            throw new InvalidOperationException(
                "Room flag $80 did not select the moved guard and TX_1004 phase live.");
        }
        isolatedSave.SetRoomFlag(
            group, roomId, OracleSaveData.RoomFlagItem);
        isolatedManager.LoadRoom(group, _world.LoadRoom(group, roomId));
        if (isolatedManager.Entities<GroundTreasurePickup>().Count != 0)
            throw new InvalidOperationException(
                "Room flag $20 did not suppress the $dc:$07 Heart Piece on re-entry.");
        isolatedSave.WriteWramByte(0xc6bf, 0x08);
        isolatedSave.CommitInventoryChange();
        NpcCharacter essenceGuard = isolatedManager.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x58, SubId: 0x02 });
        if (essenceGuard.Active || essenceGuard.Visible)
            throw new InvalidOperationException(
                "Essence bit $08 did not delete hardhat worker $58:$02.");
        isolatedManager.Clear();
        RemoveChild(validationRoot);
        validationRoot.QueueFree();

        // Exercise the reusable ground-treasure interaction before setting
        // the story bits. State 0/1 make it collectible on the second update.
        _saveData.WriteWramByte(0xc6bf, 0x00);
        _saveData.CommitInventoryChange();
        _saveData.SetRoomFlag(
            group, roomId, OracleSaveData.RoomFlagItem, value: false);
        _saveData.SetRoomFlag(
            group, roomId, OracleSaveData.RoomFlag40, value: false);
        _saveData.SetRoomFlag(
            group, roomId, OracleSaveData.RoomFlag80, value: false);
        LoadValidationRoom(group, roomId);
        GroundTreasurePickup heart = _entities.Entities<GroundTreasurePickup>().Single();
        int heartPiecesBefore = _inventory.HeartPieces;
        _sound.ClearPlayRequestAudit();
        _player.WarpTo(heart.Position);
        _entities.Update(1.0 / 60.0, _player);
        if (heart.State != PickupState.Spawning)
            throw new InvalidOperationException(
                "Ground Heart Piece did not retain its state-0 initialization update.");
        _entities.Update(1.0 / 60.0, _player);
        if (_interactions.GroundTreasureForValidation != heart ||
            heart.State != PickupState.Collected ||
            _inventory.HeartPieces != (heartPiecesBefore + 1) % 4 ||
            !_saveData.HasRoomFlag(
                group, roomId, OracleSaveData.RoomFlagItem) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 1 ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage.Contains("\\heartpiece", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Ground Heart Piece contact did not give the treasure, set $20, play SND_GETITEM, and open TX_0017.");
        }
        _interactions.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);
        if (!heart.Held || !_player.IsHoldingItemTwoHands ||
            heart.Position != _player.Position + new Vector2(0, -14) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 2)
        {
            throw new InvalidOperationException(
                "Ground Heart Piece did not enter its next-update two-hand pose with the second SND_GETITEM.");
        }
        _dialogue.Close();
        _interactions.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);
        if (_player.IsHoldingItemTwoHands ||
            _entities.Entities<GroundTreasurePickup>().Count != 0)
        {
            throw new InvalidOperationException(
                "Closing TX_0017 did not clear Link's two-hand pose and delete the ground treasure.");
        }
        LoadValidationRoom(group, roomId);
        if (_entities.Entities<GroundTreasurePickup>().Count != 0)
            throw new InvalidOperationException(
                "Collected room 1:86 Heart Piece respawned despite room flag $20.");

        // Run the guard's first lane, imported tower scene, same-room return,
        // and aftermath lane as one continuous flow.
        _saveData.SetRoomFlag(
            group, roomId, OracleSaveData.RoomFlag40, value: false);
        _saveData.SetRoomFlag(
            group, roomId, OracleSaveData.RoomFlag80, value: false);
        LoadValidationRoom(group, roomId);
        BlackTowerEntranceEvent roomEvent = _roomEvents.BlackTowerEntrance;
        ValidationCutsceneTrace trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        NpcCharacter guard = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x58, SubId: 0x02 });
        _player.WarpTo(new Vector2(0x5a, 0x38));
        _player.Face(Vector2I.Left);
        _sound.ClearPlayRequestAudit();
        if (!_interactions.TryInteract(_player) ||
            roomEvent.Stage != BlackTowerEntranceEventEventStage.FirstScript)
        {
            throw new InvalidOperationException(
                "A-button contact did not start hardhatWorkerSubid02Script's first lane.");
        }

        bool sawExplanation = false;
        bool sawExplanationText = false;
        bool sawAftermath = false;
        for (int frame = 0; frame < 1200 &&
            (roomEvent.HasState || _transitions.IsTransitioning); frame++)
        {
            _transitions.Update(1.0 / 60.0);
            if (!_transitions.TimeWarpActive)
                _entities.Update(1.0 / 60.0, _player);
            _roomEvents.Update(1.0 / 60.0);
            _interactions.Update(1.0 / 60.0, _player);
            _sound.Tick();

            if (roomEvent.Screen is { } screen)
            {
                sawExplanation = true;
                if (screen.BackgroundPixelHash == 0 || _hud.Visible ||
                    _sound.ActiveMusic != OracleSoundEngine.MusDisaster ||
                    _warpFade.Position != Vector2.Zero ||
                    _warpFade.Size != new Vector2(
                        OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight) ||
                    screen.Size != new Vector2(
                        OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight) ||
                    _warpFade.ZIndex <= _hud.ZIndex)
                {
                    throw new InvalidOperationException(
                        "Black Tower stage-0 screen, full-screen fade priority, hidden HUD, or MUS_DISASTER presentation was missing.");
                }
            }
            if (roomEvent.Stage ==
                    BlackTowerEntranceEventEventStage.ExplanationDialogue &&
                _dialogue.IsOpen)
            {
                sawExplanationText =
                    _dialogue.CurrentMessage == DialogueBox.PlainText(
                        roomEvent.Database.Record.ExplanationText);
            }
            if (roomEvent.Stage == BlackTowerEntranceEventEventStage.Aftermath)
            {
                if (!sawAftermath &&
                    _sound.ActiveMusic != OracleSoundEngine.MusBlackTowerEntrance)
                {
                    throw new InvalidOperationException(
                        "Room 1:86 did not restore MUS_BLACK_TOWER_ENTRANCE on its same-room return.");
                }
                sawAftermath = true;
            }
            if (_dialogue.IsOpen)
                _dialogue.Close();
        }

        guard = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x58, SubId: 0x02 });
        int[] scriptTexts = trace.Observations
            .Where(entry => entry.Observation == "Dialogue")
            .Select(entry => entry.Value)
            .ToArray();
        if (roomEvent.HasState || _transitions.IsTransitioning ||
            !sawExplanation || !sawExplanationText || !sawAftermath ||
            !_saveData.HasRoomFlag(group, roomId, OracleSaveData.RoomFlag40) ||
            !_saveData.HasRoomFlag(group, roomId, OracleSaveData.RoomFlag80) ||
            guard.Position != new Vector2(0x58, 0x38) ||
            guard.TextId != 0x1004 || _player.CutsceneControlled ||
            _warpFade.Position !=
                new Vector2(0, OracleRoomData.GameplayScreenTop) ||
            _warpFade.Size != new Vector2(
                OracleRoomData.ViewportWidth, OracleRoomData.ViewportHeight) ||
            _warpFade.ZIndex != 15 ||
            !scriptTexts.SequenceEqual(new[] { 0x1003, 0x1006 }) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCtrlMediumFadeOut) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.MusBlackTowerEntrance) != 1 ||
            _sound.ActiveMusic != OracleSoundEngine.MusBlackTowerEntrance)
        {
            throw new InvalidOperationException(
                "Room 1:86 did not complete its $40 explanation/$0c return/$80 aftermath sequence exactly.");
        }

        _player.WarpTo(new Vector2(0x58, 0x48));
        _player.Face(Vector2I.Up);
        if (!_interactions.TryInteract(_player) ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(guard.Message))
        {
            throw new InvalidOperationException(
                "Completed hardhat worker did not enter the ordinary TX_1004 A-button loop.");
        }
        _dialogue.Close();
        _saveData.WriteWramByte(0xc6bf, 0x08);
        _saveData.CommitInventoryChange();
        LoadValidationRoom(group, roomId);
        guard = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x58, SubId: 0x02 });
        if (guard.Active || guard.Visible || roomEvent.HasState)
            throw new InvalidOperationException(
                "Essence bit $08 did not suppress the guard and its entry event after completion.");
        _saveData.WriteWramByte(0xc6bf, 0x00);
        _saveData.CommitInventoryChange();
        _roomEvents.CommandTraceSink = null;

        GD.Print("Validated room 1:86's $58:$02 guard, essence $08 and room $40/$80 " +
            "predicates, typed first/aftermath lanes, full-screen fade above the HUD, stage-0 " +
            "Black Tower screen and same-room $0c return with restored room-fade bounds, plus " +
            "reusable $dc:$07 Heart Piece collection and room flag $20.");
    }

    private void ValidateLowerBlackTowerInteractions()
    {
        const double frame = 1.0 / 60.0;
        var data = new BlackTowerWorkerDatabase();
        var dungeonEntries = new DungeonEntranceInteractionDatabase();
        var strike = new Room148PickaxeDatabase();

        (int Room, (int Id, int SubId, int Var03, int Y, int X)[] Actors,
            int InitialRandomCalls)[] rooms =
        {
            (0xe0, new[] { (0x3a, 0x02, 0, 0x98, 0x38) }, 256),
            (0xe1, new[]
            {
                (0x58, 0x00, 0, 0x98, 0x48),
                (0x40, 0x0c, 0, 0x68, 0x58),
                (0x57, 0x03, 0, 0x38, 0x48),
                (0x57, 0x03, 1, 0x58, 0x88)
            }, 256),
            (0xe2, new[]
            {
                (0x40, 0x0c, 0, 0x98, 0xd8),
                (0x58, 0x00, 1, 0x58, 0x88),
                (0x58, 0x03, 3, 0x68, 0x28),
                (0x57, 0x03, 2, 0x48, 0x78),
                (0x57, 0x03, 3, 0x58, 0x98)
            }, 257),
            (0xe7, new[]
            {
                (0x40, 0x0c, 0, 0x78, 0xa8),
                (0x58, 0x03, 0, 0x58, 0x28),
                (0x58, 0x03, 1, 0x48, 0x38),
                (0x57, 0x03, 4, 0x38, 0x28),
                (0x57, 0x03, 5, 0x88, 0xc8)
            }, 258),
            (0xe8, new[]
            {
                (0x58, 0x03, 2, 0x48, 0x28),
                (0x57, 0x03, 6, 0x68, 0x78),
                (0x57, 0x03, 7, 0x58, 0x98)
            }, 257)
        };

        foreach (var expectedRoom in rooms)
        {
            var root = new Node { Name = $"BlackTower{expectedRoom.Room:x2}Validation" };
            AddChild(root);
            OracleSaveData save = OracleSaveData.CreateStandardGame();
            var manager = new RoomEntityManager(
                root, new NpcDatabase(), new EnemyDatabase(), save);
            manager.LoadRoom(4, _world.LoadRoom(4, expectedRoom.Room));
            List<NpcCharacter> actors = manager.Entities<NpcCharacter>();
            if (actors.Count != expectedRoom.Actors.Length ||
                manager.RandomCalls != expectedRoom.InitialRandomCalls)
            {
                throw new InvalidOperationException(
                    $"Room 4:{expectedRoom.Room:x2} did not preserve its actor count or " +
                    "initialization-only RNG consumption.");
            }
            for (int index = 0; index < actors.Count; index++)
            {
                var expected = expectedRoom.Actors[index];
                NpcCharacter actor = actors[index];
                if (actor.Record.Id != expected.Id ||
                    actor.Record.SubId != expected.SubId ||
                    actor.Record.Var03 != expected.Var03 ||
                    actor.Position != new Vector2(expected.X, expected.Y) ||
                    actor.CurrentAnimationOpaquePixels == 0)
                {
                    throw new InvalidOperationException(
                        $"Room 4:{expectedRoom.Room:x2} actor {index} did not match " +
                        "the ordered mainData.s stream and imported visual.");
                }
            }
            manager.Clear();
            RemoveChild(root);
            root.QueueFree();
        }

        // $40:$0c enters soldierSubid0c directly. The deletion predicates in
        // soldierSubid00/01 must never leak onto these placed construction guards.
        var predicateRoot = new Node { Name = "BlackTowerSoldierPredicateValidation" };
        AddChild(predicateRoot);
        OracleSaveData predicateSave = OracleSaveData.CreateStandardGame();
        var predicateManager = new RoomEntityManager(
            predicateRoot, new NpcDatabase(), new EnemyDatabase(), predicateSave);
        predicateManager.LoadRoom(4, _world.LoadRoom(4, 0xe1));
        NpcCharacter soldier = predicateManager.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x40, SubId: 0x0c });
        predicateSave.SetGlobalFlag(OracleSaveData.GlobalFlag0b);
        predicateSave.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        if (!soldier.Active || !soldier.Visible)
            throw new InvalidOperationException(
                "GLOBALFLAG_0b/FINISHEDGAME incorrectly hid soldier $40:$0c.");

        var referenceRandom = new OracleRandom();
        referenceRandom.BeginRoomParse();
        int[] soldierTexts = { 0x590d, 0x590e, 0x590f, 0x590d };
        int expectedSoldierText = soldierTexts[referenceRandom.Next().Value & 3];
        if (!predicateManager.BeginNpcTalk(soldier) ||
            soldier.TextId != expectedSoldierText ||
            predicateManager.RandomCalls != 257)
        {
            throw new InvalidOperationException(
                "Soldier $40:$0c did not consume one global RNG value and choose its per-talk text.");
        }
        predicateManager.EndNpcTalk(soldier);
        NpcCharacter pickaxe = predicateManager.Entities<NpcCharacter>().First(npc =>
            npc.Record is { Id: 0x57, SubId: 0x03 });
        int[] pickaxeTexts =
        {
            0x1b01, 0x1b02, 0x1b03, 0x1b04,
            0x1b05, 0x1b01, 0x1b02, 0x1b03
        };
        int expectedPickaxeText = pickaxeTexts[referenceRandom.Next().Value & 7];
        if (!predicateManager.BeginNpcTalk(pickaxe) ||
            pickaxe.TextId != expectedPickaxeText ||
            predicateManager.RandomCalls != 258)
        {
            throw new InvalidOperationException(
                "Pickaxe worker $57:$03 did not consume one global RNG value and choose its per-talk text.");
        }
        predicateManager.EndNpcTalk(pickaxe);

        var sounds = new List<int>();
        predicateManager.SoundRequested += sounds.Add;
        _player.WarpTo(new Vector2(0x18, 0x18));
        for (int update = 0; update < 26; update++)
            predicateManager.Update(frame, _player);
        List<Room148PickaxeDebris> chips =
            predicateManager.Entities<Room148PickaxeDebris>();
        if (sounds.Count != 2 || sounds.Any(sound => sound != strike.Record.Sound) ||
            chips.Count != 4 ||
            chips.Count(chip => chip.Palette == 1 &&
                chip.Position == new Vector2(0x3a, 0x3c)) != 2 ||
            chips.Count(chip => chip.Palette == 2 &&
                chip.Position == new Vector2(0x96, 0x5c)) != 2)
        {
            throw new InvalidOperationException(
                "Black Tower pickaxe animations $00/$01 did not strike left/right with exact dirt-chip origins.");
        }
        predicateManager.Clear();
        RemoveChild(predicateRoot);
        predicateRoot.QueueFree();

        // var03=$03 begins down for $40, moves on the first 63 updates at
        // SPEED_80, skips movement when the counter reaches zero, then waits 20.
        var patrolRoot = new Node { Name = "BlackTowerPatrolValidation" };
        AddChild(patrolRoot);
        var patrolManager = new RoomEntityManager(
            patrolRoot, new NpcDatabase(), new EnemyDatabase(),
            OracleSaveData.CreateStandardGame());
        patrolManager.LoadRoom(4, _world.LoadRoom(4, 0xe2));
        NpcCharacter patroller = patrolManager.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x58, SubId: 0x03 });
        _player.WarpTo(new Vector2(0x100, 0x20));
        for (int update = 0; update < 63; update++)
            patrolManager.Update(frame, _player);
        if (patroller.Position != new Vector2(0x28, 0x87))
            throw new InvalidOperationException(
                "Hardhat var03=$03 did not apply 63 half-pixel down movements for counter $40.");
        patrolManager.Update(frame, _player);
        Vector2 stoppedPosition = patroller.Position;
        _player.WarpTo(stoppedPosition + Vector2.Down * 12);
        _player.Face(Vector2I.Up);
        if (patrolManager.FindTalkTarget(_player) is not null)
            throw new InvalidOperationException(
                "Hardhat patrol accepted A-button interaction inside its 20-update between-leg wait.");
        _player.WarpTo(new Vector2(0x100, 0x20));
        for (int update = 0; update < data.PatrolWait; update++)
            patrolManager.Update(frame, _player);
        if (patroller.Position != stoppedPosition ||
            patroller.CurrentScriptAnimationSource.Length != 0)
        {
            throw new InvalidOperationException(
                "Hardhat patrol moved on its counter-zero/20-wait boundary or retained a script animation.");
        }
        patrolManager.Update(frame, _player);
        patrolManager.Update(frame, _player);
        if (patroller.Position != stoppedPosition + Vector2.Right)
            throw new InvalidOperationException(
                "Hardhat patrol did not resume its next rightward half-pixel leg after the exact wait.");
        patrolManager.Clear();
        RemoveChild(patrolRoot);
        patrolRoot.QueueFree();

        var blockerRoot = new Node { Name = "BlackTowerBlockerValidation" };
        AddChild(blockerRoot);
        var blockerManager = new RoomEntityManager(
            blockerRoot, new NpcDatabase(), new EnemyDatabase(),
            OracleSaveData.CreateStandardGame());
        blockerManager.LoadRoom(4, _world.LoadRoom(4, 0xe0));
        NpcCharacter blocker = blockerManager.Entities<NpcCharacter>().Single();
        _player.WarpTo(new Vector2(0x49, 0x98));
        blockerManager.Update(frame, _player);
        if (blocker.Position != new Vector2(0x38, 0x98) ||
            !blockerManager.PlayerMovementDisabled)
        {
            throw new InvalidOperationException(
                "Villager $3a:$02 did not arm part 2 at saved-X+$11 without moving before its next script update.");
        }
        for (int update = 0; update < data.BlockerDistance; update++)
            blockerManager.Update(frame, _player);
        if (blocker.Position != new Vector2(0x48, 0x98) ||
            _player.Position.Y != 0x98 || !blockerManager.PlayerMovementDisabled)
        {
            throw new InvalidOperationException(
                "Villager $3a:$02 did not move exactly 16 pixels while restoring Link's saved Y.");
        }
        for (int update = 0; update < data.BlockerWait; update++)
            blockerManager.Update(frame, _player);
        if (blockerManager.PlayerMovementDisabled ||
            _player.Position != new Vector2(0x54, 0x98) ||
            blockerManager.BlocksLink(_player.Position) ||
            blockerManager.BlocksLink(_player.Position + Vector2.Right))
        {
            throw new InvalidOperationException(
                "Villager $3a:$02 did not separate Link before enabling input after its exact 10-update wait.");
        }
        _player.WarpTo(new Vector2(0x37, 0x98));
        blockerManager.Update(frame, _player);
        if (blocker.Position != new Vector2(0x48, 0x98))
            throw new InvalidOperationException(
                "Villager $3a:$02 moved before reversed part 2's next script update.");
        blockerManager.Update(frame, _player);
        if (blocker.Position != new Vector2(0x47, 0x98))
            throw new InvalidOperationException(
                "Villager $3a:$02 did not reverse and move left on the next open-side collision.");
        blockerManager.Clear();
        RemoveChild(blockerRoot);
        blockerRoot.QueueFree();

        // Dungeon-stuff $12:$00 exists only for the $ff screen-entry warp. It
        // shows TX_020f once and records the live position as the checkpoint.
        var entranceRoot = new Node { Name = "BlackTowerEntranceValidation" };
        AddChild(entranceRoot);
        OracleSaveData entranceSave = OracleSaveData.CreateStandardGame();
        long entranceTick = 0;
        var entranceRooms = new RoomSession(
            4, 0xe7, () => entranceTick, () => entranceTick = 0, entranceSave);
        var entranceManager = new RoomEntityManager(
            entranceRoot, new NpcDatabase(), new EnemyDatabase(), entranceSave);
        var respawn = new DeathRespawnPointController(entranceRooms, _player);
        int entranceText = 0;
        string entranceMessage = string.Empty;
        entranceManager.DungeonEntranceTriggered += (textId, message) =>
        {
            entranceText = textId;
            entranceMessage = message;
            respawn.RecordCurrentPoint();
        };
        _player.WarpTo(new Vector2(0x78, 0x88));
        entranceManager.LoadRoom(
            4, entranceRooms.CurrentRoom,
            EnemyPlacementContext.FromWarpDestination(0xff));
        if (entranceManager.Entities<Node2D>().Count != 6)
            throw new InvalidOperationException(
                "Room 4:e7's screen warp did not insert dungeon-stuff second in its object stream.");
        entranceManager.Update(frame, _player);
        if (entranceText != 0x020f ||
            entranceMessage != dungeonEntries.Entry(15).Message ||
            entranceManager.Entities<Node2D>().Count != 5 ||
            entranceSave.RespawnGroup != 4 || entranceSave.RespawnRoom != 0xe7 ||
            entranceSave.RespawnY != 0x88 || entranceSave.RespawnX != 0x78)
        {
            throw new InvalidOperationException(
                "Dungeon-stuff $12:$00 did not show TX_020f, delete, and record the 4:e7 checkpoint.");
        }
        entranceManager.LoadRoom(4, entranceRooms.CurrentRoom);
        if (entranceManager.Entities<Node2D>().Count != 6)
            throw new InvalidOperationException(
                "Direct room 4:e7 load did not preserve dungeon-stuff's source interaction slot.");
        entranceManager.Update(frame, _player);
        if (entranceManager.Entities<Node2D>().Count != 5)
            throw new InvalidOperationException(
                "Direct room 4:e7 load did not delete dungeon-stuff on its first non-whiteout update.");
        entranceText = 0;
        _player.WarpTo(new Vector2(0x78, 0x77));
        entranceManager.LoadRoom(
            4, entranceRooms.CurrentRoom,
            EnemyPlacementContext.FromWarpDestination(0xff));
        entranceManager.Update(frame, _player);
        if (entranceText != 0 || entranceManager.Entities<Node2D>().Count != 5)
            throw new InvalidOperationException(
                "Dungeon-stuff $12:$00 did not delete below the strict entry-side Y=$78 gate.");
        entranceManager.Clear();
        RemoveChild(entranceRoot);
        entranceRoot.QueueFree();

        // Run hardhatWorkerSubid00Script through a completely isolated room,
        // inventory, textbox, and sound sink so the grant cannot affect later scenarios.
        var shovelRoot = new Node { Name = "BlackTowerShovelValidation" };
        var shovelWorldRoot = new Node { Name = "World" };
        var shovelInterface = new Node { Name = "Interface" };
        var shovelView = new RoomView { Name = "RoomView" };
        var shovelDialogue = new DialogueBox { Name = "Dialogue" };
        shovelRoot.AddChild(shovelWorldRoot);
        shovelRoot.AddChild(shovelInterface);
        shovelRoot.AddChild(shovelView);
        shovelRoot.AddChild(shovelDialogue);
        AddChild(shovelRoot);
        OracleSaveData shovelSave = OracleSaveData.CreateStandardGame();
        long shovelTick = 0;
        var shovelRooms = new RoomSession(
            4, 0xe1, () => shovelTick, () => shovelTick = 0, shovelSave);
        var shovelManager = new RoomEntityManager(
            shovelWorldRoot, new NpcDatabase(), new EnemyDatabase(), shovelSave);
        var shovelTreasures = new TreasureDatabase();
        var shovelInventory = new InventoryState(
            shovelTreasures, shovelSave, () => shovelRooms.CurrentDungeonIndex);
        var shovelSounds = new List<int>();
        var shovelInteractions = new InteractionController(
            shovelRooms, shovelManager, new SignDatabase(), new ChestDatabase(),
            shovelTreasures, shovelDialogue, shovelWorldRoot, shovelView,
            static position => position, () => shovelTick, shovelInventory,
            shovelInterface, shovelSounds.Add);
        shovelManager.LoadRoom(4, shovelRooms.CurrentRoom);
        NpcCharacter shovelWorker = shovelManager.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x58, SubId: 0x00 });
        _player.WarpTo(shovelWorker.Position + Vector2.Down * 12);
        _player.Face(Vector2I.Up);
        if (!shovelInteractions.TryInteract(_player) ||
            DialogueBox.PlainText(shovelDialogue.CurrentMessage) !=
                DialogueBox.PlainText(data.Text(0x1001)))
        {
            throw new InvalidOperationException(
                "Hardhat var03=$00 did not begin the uncollected TX_1001 shovel branch.");
        }
        shovelDialogue.Close();
        shovelInteractions.Update(frame, _player);
        for (int update = 0; update < data.TalkWait - 1; update++)
            shovelInteractions.Update(frame, _player);
        if (shovelInventory.HasTreasure(TreasureDatabase.TreasureShovel) ||
            shovelSave.HasRoomFlag(4, 0xe1, OracleSaveData.RoomFlagItem))
        {
            throw new InvalidOperationException(
                "Hardhat giveitem ran before the exact first 30-update wait.");
        }
        shovelInteractions.Update(frame, _player);
        GroundTreasurePickup heldShovel = shovelWorldRoot.GetChildren()
            .OfType<GroundTreasurePickup>().Single();
        if (!shovelInventory.HasTreasure(TreasureDatabase.TreasureShovel) ||
            !shovelSave.HasRoomFlag(4, 0xe1, OracleSaveData.RoomFlagItem) ||
            !heldShovel.Held || heldShovel.PixelHash == 0 ||
            heldShovel.Position != _player.Position + Vector2.Up * 14 ||
            !_player.IsHoldingItemTwoHands ||
            shovelSounds.Count(sound => sound == OracleSoundEngine.SndGetItem) != 2 ||
            DialogueBox.PlainText(shovelDialogue.CurrentMessage) !=
                DialogueBox.PlainText(data.Text(0x0025)))
        {
            throw new InvalidOperationException(
                "Hardhat giveitem did not grant/set $20, play both SND_GETITEM calls, and hold the exact Shovel visual for TX_0025.");
        }
        shovelDialogue.Close();
        shovelInteractions.Update(frame, _player);
        for (int update = 0; update < data.TalkWait - 1; update++)
            shovelInteractions.Update(frame, _player);
        if (shovelDialogue.IsOpen || _player.IsHoldingItemTwoHands)
            throw new InvalidOperationException(
                "Hardhat shovel branch did not remove the held object during its second 30-update wait.");
        shovelInteractions.Update(frame, _player);
        if (DialogueBox.PlainText(shovelDialogue.CurrentMessage) !=
            DialogueBox.PlainText(data.Text(0x1002)))
        {
            throw new InvalidOperationException(
                "Hardhat shovel branch did not finish with TX_1002 after the second exact wait.");
        }
        shovelDialogue.Close();
        shovelInteractions.Update(frame, _player);
        int soundsAfterGrant = shovelSounds.Count;
        if (!shovelInteractions.TryInteract(_player) ||
            DialogueBox.PlainText(shovelDialogue.CurrentMessage) !=
                DialogueBox.PlainText(data.Text(0x1002)))
        {
            throw new InvalidOperationException(
                "Room flag $20 did not select hardhat worker's already-gave TX_1002 branch.");
        }
        shovelDialogue.Close();
        shovelInteractions.Update(frame, _player);
        if (shovelSounds.Count != soundsAfterGrant)
            throw new InvalidOperationException(
                "Hardhat worker replayed giveitem after room flag $20 was set.");

        shovelRooms.Load(4, 0xe2);
        shovelManager.LoadRoom(4, shovelRooms.CurrentRoom);
        NpcCharacter genericHardhat = shovelManager.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x58, SubId: 0x00 });
        _player.WarpTo(genericHardhat.Position + Vector2.Down * 12);
        _player.Face(Vector2I.Up);
        if (!shovelInteractions.TryInteract(_player) ||
            DialogueBox.PlainText(shovelDialogue.CurrentMessage) !=
                DialogueBox.PlainText(data.Text(0x1000)))
        {
            throw new InvalidOperationException(
                "Hardhat var03=$01 did not take its unconditional generic TX_1000 branch.");
        }
        shovelDialogue.Close();
        shovelInteractions.Update(frame, _player);
        if (shovelSave.HasRoomFlag(4, 0xe2, OracleSaveData.RoomFlagItem))
            throw new InvalidOperationException(
                "Hardhat var03=$01 incorrectly set the shovel room flag in 4:e2.");
        shovelManager.Clear();
        RemoveChild(shovelRoot);
        shovelRoot.QueueFree();

        GD.Print("Validated rooms 4:e0/e1/e2/e7/e8 ordered construction actors, unconditional " +
            "$40:$0c flag truth table, per-talk and initialization RNG, pickaxe debris, " +
            "SPEED_80 patrols, path blocker, exact Shovel giveitem sequence, and TX_020f checkpoint entry.");
    }

    private void ValidateNpcFlagVisibility()
    {
        var validationRoot = new Node { Name = "NpcFlagVisibilityValidation" };
        AddChild(validationRoot);
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var manager = new RoomEntityManager(
            validationRoot, new NpcDatabase(), new EnemyDatabase(), save);

        if (new NpcVisibilityRuleDatabase().RuleCount != 330 ||
            new NpcDialogueRuleDatabase().RuleCount != 100 ||
            new NpcPositionRuleDatabase().RuleCount != 2)
            throw new InvalidOperationException(
                "Expected 330 NPC visibility, 100 NPC dialogue, and two NPC " +
                "position state predicates.");

        manager.LoadRoom(0, _world.LoadRoom(0, 0x5a));
        List<NpcCharacter> introMonkeys = manager.Entities<NpcCharacter>().Where(npc =>
            npc.Record.Id == 0x39 && npc.Record.SubId is 0x02 or 0x03).ToList();
        if (introMonkeys.Count != 2 ||
            introMonkeys[0].TextId != 0x5700 || introMonkeys[1].TextId != 0x5701 ||
            introMonkeys[0].Record.DefaultAnimation != 0x06 ||
            introMonkeys[1].Record.DefaultAnimation != 0x07 ||
            introMonkeys.Any(monkey => !monkey.Active || string.IsNullOrEmpty(monkey.Message)))
        {
            throw new InvalidOperationException(
                "Room 0:5a did not load its two active intro monkeys with TX_5700/TX_5701 and animations $06/$07.");
        }
        foreach (NpcCharacter monkey in introMonkeys)
        {
            _player.WarpTo(monkey.Position + Vector2.Down * 16.0f);
            _player.Face(Vector2I.Up);
            if (!monkey.CanTalkTo(_player))
                throw new InvalidOperationException(
                    $"Room 0:5a monkey ${monkey.Record.Id:x2}:${monkey.Record.SubId:x2} was not talkable from below.");
        }
        ulong upperMonkeyFirstFrame = introMonkeys[0].CurrentAnimationPixelHash;
        ulong lowerMonkeyFirstFrame = introMonkeys[1].CurrentAnimationPixelHash;
        foreach (NpcCharacter monkey in introMonkeys)
            monkey.UpdateNpc(31.0 / 60.0, _player.Position);
        if (introMonkeys.Any(monkey => monkey.CurrentAnimationFrame != 0))
            throw new InvalidOperationException(
                "A room 0:5a monkey advanced before animation $06/$07's original $20-frame duration.");
        foreach (NpcCharacter monkey in introMonkeys)
            monkey.UpdateNpc(1.0 / 60.0, _player.Position);
        if (introMonkeys.Any(monkey => monkey.CurrentAnimationFrame != 1) ||
            introMonkeys[0].CurrentAnimationPixelHash != lowerMonkeyFirstFrame ||
            introMonkeys[1].CurrentAnimationPixelHash != upperMonkeyFirstFrame)
        {
            throw new InvalidOperationException(
                "Room 0:5a's animation $06/$07 monkeys did not swap their two original poses after $20 frames.");
        }
        foreach (NpcCharacter monkey in introMonkeys)
            monkey.UpdateNpc(32.0 / 60.0, _player.Position);
        if (introMonkeys.Any(monkey => monkey.CurrentAnimationFrame != 0))
            throw new InvalidOperationException(
                "Room 0:5a's two-pose monkey animation did not loop after another $20 frames.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone);
        if (introMonkeys.Any(monkey => monkey.Active || monkey.Visible))
            throw new InvalidOperationException(
                "GLOBALFLAG_INTRO_DONE $0a did not remove room 0:5a's intro monkeys.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone, value: false);
        if (introMonkeys.Any(monkey => !monkey.Active || !monkey.Visible))
            throw new InvalidOperationException(
                "Clearing GLOBALFLAG_INTRO_DONE $0a did not restore room 0:5a's intro monkeys.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone);
        manager.LoadRoom(2, _world.LoadRoom(2, 0xea));
        List<NpcCharacter> newbornLeftFamily = manager.Entities<NpcCharacter>();
        NpcCharacter? newbornBipin = newbornLeftFamily.Find(npc =>
            npc.Record.Id == 0x28 && npc.Record.SubId == 0x00);
        NpcCharacter? newbornBlossom = newbornLeftFamily.Find(npc =>
            npc.Record.Id == 0x2b && npc.Record.SubId == 0x00);
        if (newbornLeftFamily.Count != 2 ||
            newbornBipin is not { TextId: 0x4300 } ||
            newbornBipin.Position != new Vector2(0x48, 0x48) ||
            newbornBipin.Record.DefaultAnimation != 0x04 ||
            !CanTalkTo(newbornBipin) ||
            newbornBlossom is not { TextId: 0x4400 } ||
            newbornBlossom.Position != new Vector2(0x78, 0x38) ||
            !CanTalkTo(newbornBlossom))
        {
            throw new InvalidOperationException(
                "Room 2:ea did not expand family stage $00 into talkable Bipin/Blossom actors.");
        }

        string bipinRunningLeft = newbornBipin.Record.DownAnimation;
        string bipinRunningRight = newbornBipin.Record.RightAnimation;
        if (string.IsNullOrEmpty(bipinRunningLeft) ||
            string.IsNullOrEmpty(bipinRunningRight) ||
            bipinRunningLeft == bipinRunningRight)
        {
            throw new InvalidOperationException(
                "Bipin $28:$00 did not import distinct animation $04/$05 running records.");
        }
        for (int frame = 0; frame < 32; frame++)
            manager.Update(1.0 / 60.0, _player);
        if (newbornBipin.Position != new Vector2(0x28, 0x48))
            throw new InvalidOperationException(
                "Bipin $28:$00 did not move left at SPEED_100 to X=$28 after 32 updates.");
        manager.Update(1.0 / 60.0, _player);
        if (newbornBipin.Position != new Vector2(0x27, 0x48) ||
            newbornBipin.CurrentScriptAnimationSource != bipinRunningRight)
        {
            throw new InvalidOperationException(
                "Bipin $28:$00 did not reverse and toggle animation $04->$05 after leaving X=$28.");
        }
        for (int frame = 0; frame < 49; frame++)
            manager.Update(1.0 / 60.0, _player);
        if (newbornBipin.Position != new Vector2(0x58, 0x48) ||
            newbornBipin.CurrentScriptAnimationSource != bipinRunningLeft)
        {
            throw new InvalidOperationException(
                "Bipin $28:$00 did not reverse and toggle animation $05->$04 at X=$58.");
        }

        // Link begins exactly at the legal left edge of Bipin's collision box.
        // Bipin's next leftward update creates a one-pixel overlap, which the
        // original objectPreventLinkFromPassing immediately resolves leftward.
        _player.WarpTo(new Vector2(0x4c, 0x48));
        manager.Update(1.0 / 60.0, _player);
        if (newbornBipin.Position != new Vector2(0x57, 0x48) ||
            _player.Position != new Vector2(0x4b, 0x48))
        {
            throw new InvalidOperationException(
                "Running Bipin entered Link from the side without resolving their collision.");
        }
        manager.LoadRoom(2, _world.LoadRoom(2, 0xeb));
        if (manager.Entities<NpcCharacter>().Count != 0)
            throw new InvalidOperationException(
                "Room 2:eb was not empty during family stage $00.");

        bool familySaveChanged = save.WriteWramByte(
            OracleSaveData.ChildStageAddress, 0x04);
        familySaveChanged |= save.WriteWramByte(
            OracleSaveData.ChildPersonalityAddress, 0x01);
        if (familySaveChanged)
            save.CommitInventoryChange();
        manager.LoadRoom(2, _world.LoadRoom(2, 0xea));
        List<NpcCharacter> shyStage4Left = manager.Entities<NpcCharacter>();
        if (shyStage4Left.Count != 2 ||
            shyStage4Left.Find(npc => npc.Record.Id == 0x2b) is not
                { Record.SubId: 0x04, TextId: 0x4417 } ||
            shyStage4Left.Find(npc => npc.Record.Id == 0x35) is not
                { Record.SubId: 0x01, Record.Var03: 0x02, TextId: 0x4200 })
        {
            throw new InvalidOperationException(
                "Room 2:ea did not select the shy family stage-$04 actors and dialogue.");
        }
        manager.LoadRoom(2, _world.LoadRoom(2, 0xeb));
        List<NpcCharacter> shyStage4Right = manager.Entities<NpcCharacter>();
        if (shyStage4Right.Count != 1 ||
            shyStage4Right[0].Record is not { Id: 0x28, SubId: 0x04 } ||
            shyStage4Right[0].TextId != 0x4304)
        {
            throw new InvalidOperationException(
                "Room 2:eb did not select Bipin for shy family stage $04.");
        }

        familySaveChanged = save.WriteWramByte(
            OracleSaveData.ChildStageAddress, 0x06);
        familySaveChanged |= save.WriteWramByte(
            OracleSaveData.NextChildStageAddress, 0x07);
        familySaveChanged |= save.WriteWramByte(
            OracleSaveData.ChildPersonalityAddress, 0x02);
        familySaveChanged |= save.WriteWramByte(
            OracleSaveData.ChildStatusAddress, 0x0e);
        familySaveChanged |= save.WriteWramByte(0xc6bf, 0x03);
        if (familySaveChanged)
            save.CommitInventoryChange();
        manager.RuntimeState.SetWramByte(
            OracleRuntimeState.SeedTreeRefilledBitsetAddress, 0x02);
        manager.LoadRoom(2, _world.LoadRoom(2, 0xeb));
        List<NpcCharacter> warriorStage7Right = manager.Entities<NpcCharacter>();
        if (save.ReadWramByte(OracleSaveData.ChildStageAddress) != 0x07 ||
            save.ReadWramByte(OracleSaveData.ChildPersonalityAddress) != 0x01 ||
            manager.RuntimeState.ReadWramByte(
                OracleRuntimeState.SeedTreeRefilledBitsetAddress) != 0 ||
            warriorStage7Right.Count != 2 ||
            warriorStage7Right.Find(npc => npc.Record.Id == 0x2b) is not
                { Record.SubId: 0x07, Record.Var03: 0x01, TextId: 0x4426 } ||
            warriorStage7Right.Find(npc => npc.Record.Id == 0x28) is not
                { Record.SubId: 0x07, TextId: 0x4307 })
        {
            throw new InvalidOperationException(
                "The family spawner did not advance curious stage $06 to warrior stage $07 " +
                "after two essences and seed-tree refill bit 1.");
        }
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        manager.LoadRoom(2, _world.LoadRoom(2, 0xea));
        if (manager.Entities<NpcCharacter>().Count != 0)
            throw new InvalidOperationException(
                "GLOBALFLAG_FINISHEDGAME $14 did not delete the Bipin/Blossom family spawner.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame, value: false);
        familySaveChanged = save.WriteWramByte(
            OracleSaveData.ChildStageAddress, 0x00);
        familySaveChanged |= save.WriteWramByte(
            OracleSaveData.NextChildStageAddress, 0x00);
        familySaveChanged |= save.WriteWramByte(
            OracleSaveData.ChildPersonalityAddress, 0x00);
        familySaveChanged |= save.WriteWramByte(
            OracleSaveData.ChildStatusAddress, 0x00);
        familySaveChanged |= save.WriteWramByte(0xc6bf, 0x00);
        if (familySaveChanged)
            save.CommitInventoryChange();

        manager.LoadRoom(0, _world.LoadRoom(0, 0x3a));
        NpcCharacter? finishedGameBoy = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3c && npc.Record.SubId == 0x10);
        NpcCharacter? postgameBear = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x5d && npc.Record.SubId == 0x02 && npc.Record.Var03 == 0x01);
        NpcCharacter? postgameMonkey = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x39 && npc.Record.SubId == 0x07 && npc.Record.Var03 == 0x01);
        if (finishedGameBoy is not { Active: false } ||
            postgameBear is not { Active: false } ||
            postgameMonkey is not { Active: false })
            throw new InvalidOperationException(
                "Room 0:3a's finished-game NPC variants appeared before GLOBALFLAG_FINISHEDGAME $14.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        if (!finishedGameBoy.Active || !postgameBear.Active || !postgameMonkey.Active)
            throw new InvalidOperationException(
                "Room 0:3a did not reveal its boy, bear, and monkey finished-game variants.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame, value: false);
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame, value: false);
        manager.LoadRoom(0, _world.LoadRoom(0, 0x7b));
        List<NpcCharacter> graveyardBoys = manager.Entities<NpcCharacter>().Where(npc =>
            (npc.Record.Id == 0x3c && npc.Record.SubId is 0x03 or 0x04) ||
            (npc.Record.Id == 0x3f && npc.Record.SubId == 0x02)).ToList();
        if (graveyardBoys.Count != 3 || graveyardBoys.Any(npc => !npc.Active))
            throw new InvalidOperationException(
                "Room 0:7b did not begin with all three room-flag-gated children visible.");
        save.SetRoomFlag(0, 0x7b, OracleSaveData.RoomFlag40);
        if (graveyardBoys.Any(npc => npc.Active || npc.Visible))
            throw new InvalidOperationException(
                "Room 0:7b flag $40 did not hide all three completed-event children immediately.");
        save.SetRoomFlag(0, 0x7b, OracleSaveData.RoomFlag40, value: false);
        if (graveyardBoys.Any(npc => !npc.Active || !npc.Visible))
            throw new InvalidOperationException(
                "Clearing room 0:7b flag $40 did not restore its room-placed children.");
        graveyardBoys[0].SetActive(false);
        save.SetRoomFlag(0, 0x7b, OracleSaveData.RoomFlag40);
        save.SetRoomFlag(0, 0x7b, OracleSaveData.RoomFlag40, value: false);
        if (graveyardBoys[0].Active || graveyardBoys[0].Visible)
            throw new InvalidOperationException(
                "A live flag refresh revived an NPC already retired by its interaction lifecycle.");

        manager.LoadRoom(0, _world.LoadRoom(0, 0x82));
        List<NpcCharacter> forestFairies = manager.Entities<NpcCharacter>().Where(npc =>
            npc.Record.Id == 0x49 && npc.Record.SubId == 0x0a).ToList();
        if (forestFairies.Count != 2 || forestFairies.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 0:82's $49:$0a fairies ignored their initial compound flag gate.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagWonFairyHidingGame);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagForestUnscrambled);
        if (forestFairies.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 0:82's fairies appeared before specific room 0:90 flag $40 was set.");
        save.SetRoomFlag(0, 0x90, OracleSaveData.RoomFlag40);
        if (forestFairies.Any(npc => !npc.Active))
            throw new InvalidOperationException(
                "The global-and-specific-room predicate did not reveal room 0:82's fairies.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        if (forestFairies.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "GLOBALFLAG_FINISHEDGAME $14 did not hide the pre-ending forest fairies.");

        manager.LoadRoom(2, _world.LoadRoom(2, 0xe7));
        NpcCharacter? dog = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x54 && npc.Record.SubId == 0x00);
        if (dog is null || !dog.Active)
            throw new InvalidOperationException("Room 2:e7's Mamamu dog did not satisfy its room-item alternative.");
        save.SetRoomFlag(2, 0xe7, OracleSaveData.RoomFlagItem);
        if (dog.Active)
            throw new InvalidOperationException(
                "Mamamu's dog remained visible after every initialization alternative failed.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagReturnedDog);
        if (!dog.Active)
            throw new InvalidOperationException(
                "GLOBALFLAG_RETURNED_DOG $3b did not satisfy Mamamu's alternative visibility branch.");

        void SetTreasure(int treasure, bool value)
        {
            int address = 0xc69a + treasure / 8;
            byte mask = (byte)(1 << (treasure & 7));
            byte current = save.ReadWramByte(address);
            byte next = value ? (byte)(current | mask) : (byte)(current & ~mask);
            if (save.WriteWramByte(address, next))
                save.CommitInventoryChange();
        }

        // Restore a coherent immediate-post-intro state before checking all
        // placed members of the Impa/Nayru/Zelda story-state family.
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame, value: false);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru, value: false);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagPreBlackTowerCutsceneDone, value: false);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagGotRingFromZelda, value: false);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFlameOfDespairLit, value: false);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagReturnedDog, value: false);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone);
        save.SetRoomFlag(0, 0x83, OracleSaveData.RoomFlag80, value: false);
        save.SetRoomFlag(0, 0xe7, OracleSaveData.RoomFlag80, value: false);
        SetTreasure(TreasureDatabase.TreasureHarp, value: false);
        SetTreasure(TreasureDatabase.TreasureMakuSeed, value: false);
        save.SetLinkedGame(linked: false);
        if (save.WriteWramByte(0xc6bf, 0))
            save.CommitInventoryChange();

        manager.RuntimeState.SetWramByte(
            OracleRuntimeState.MamamuDogLocationAddress, 0x03);
        manager.LoadRoom(0, _world.LoadRoom(0, 0x48));
        NpcCharacter? roamingDog = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x54 && npc.Record.SubId == 0x01 && npc.Record.Var03 == 0x03);
        if (roamingDog is not { Active: false })
            throw new InvalidOperationException(
                "Room 0:48's roaming dog appeared before Mamamu's search began.");
        save.SetRoomFlag(0, 0xe7, OracleSaveData.RoomFlag80);
        if (!roamingDog.Active)
            throw new InvalidOperationException(
                "Room 0:e7 flag $80 did not reveal location-$03 Mamamu dog in room 0:48.");
        manager.RuntimeState.SetWramByte(
            OracleRuntimeState.MamamuDogLocationAddress, 0x01);
        if (roamingDog.Active)
            throw new InvalidOperationException(
                "Changing wMamamuDogLocation away from $03 did not hide room 0:48's dog.");

        manager.LoadRoom(0, _world.LoadRoom(0, 0x55));
        NpcCharacter? relocatedDog = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x54 && npc.Record.SubId == 0x01 && npc.Record.Var03 == 0x01);
        if (relocatedDog is not { Active: true })
            throw new InvalidOperationException(
                "wMamamuDogLocation $01 did not select the roaming dog in room 0:55.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagReturnedDog);
        if (relocatedDog.Active)
            throw new InvalidOperationException(
                "GLOBALFLAG_RETURNED_DOG $3b did not remove the selected roaming dog.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagReturnedDog, value: false);
        save.SetRoomFlag(0, 0xe7, OracleSaveData.RoomFlag80, value: false);
        manager.RuntimeState.SetWramByte(
            OracleRuntimeState.MamamuDogLocationAddress, 0x00);

        manager.LoadRoom(0, _world.LoadRoom(0, 0x57));
        NpcCharacter? earlyLynnaOldMan = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x41 && npc.Record.SubId == 0x01);
        if (earlyLynnaOldMan is not { Active: true } ||
            earlyLynnaOldMan.TextId != 0x2600 || !CanTalkTo(earlyLynnaOldMan))
            throw new InvalidOperationException(
                "Room 0:57 did not load its talkable state-$00 old man with TX_2600.");
        if (save.WriteWramByte(0xc6bf, 0x04))
            save.CommitInventoryChange();
        if (earlyLynnaOldMan.Active)
            throw new InvalidOperationException(
                "Room 0:57's $41:$01 old man remained after getGameProgress_1 state $00.");
        if (save.WriteWramByte(0xc6bf, 0))
            save.CommitInventoryChange();

        manager.LoadRoom(0, _world.LoadRoom(0, 0x58));
        NpcCharacter? rollingRidgeMan = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x41 && npc.Record.SubId == 0x04);
        if (rollingRidgeMan is not { Active: false })
            throw new InvalidOperationException(
                "Room 0:58's $41:$04 man appeared before getGameProgress_1 state $03.");
        if (save.WriteWramByte(0xc6bf, 0x40))
            save.CommitInventoryChange();
        if (!rollingRidgeMan.Active || rollingRidgeMan.TextId != 0x2603 ||
            !CanTalkTo(rollingRidgeMan))
            throw new InvalidOperationException(
                "Beating D7 did not reveal room 0:58's talkable TX_2603 state-$03 man.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame);
        if (rollingRidgeMan.Active)
            throw new InvalidOperationException(
                "The Maku-seed/Twinrova phase did not retire room 0:58's state-$03 man.");
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame, value: false);
        if (!rollingRidgeMan.Active)
            throw new InvalidOperationException(
                "Clearing the later-phase flag did not restore room 0:58's D7-phase man.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        if (rollingRidgeMan.Active)
            throw new InvalidOperationException(
                "Finished-game state did not retire room 0:58's state-$03 man.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame, value: false);
        if (save.WriteWramByte(0xc6bf, 0))
            save.CommitInventoryChange();

        manager.LoadRoom(0, _world.LoadRoom(0, 0x65));
        List<NpcCharacter> kidnappedZeldaActors = manager.Entities<NpcCharacter>().Where(npc =>
            (npc.Record.Id == 0x31 && npc.Record.SubId == 0x07) ||
            (npc.Record.Id == 0x4c && npc.Record.SubId == 0x04)).ToList();
        if (kidnappedZeldaActors.Count != 2 || kidnappedZeldaActors.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 0:65's linked kidnapped-Zelda Impa and bird appeared immediately after the intro.");

        manager.LoadRoom(0, _world.LoadRoom(0, 0x68));
        NpcCharacter? earlyLynnaMan = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x44 && npc.Record.SubId == 0x02);
        NpcCharacter? lateLynnaWoman = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3b && npc.Record.SubId == 0x02);
        NpcCharacter? makuSeedVillager = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3a && npc.Record.SubId == 0x05);
        NpcCharacter? seedSatchelBoy = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3c && npc.Record.SubId == 0x02);
        if (earlyLynnaMan is not { Active: true } ||
            earlyLynnaMan.TextId != 0x1610 ||
            lateLynnaWoman is not { Active: false } ||
            makuSeedVillager is not { Active: false } ||
            seedSatchelBoy is not { Active: false })
            throw new InvalidOperationException(
                "Room 0:68 did not select its talkable TX_1610 pre-D3 man after the intro.");

        bool CanTalkTo(NpcCharacter npc)
        {
            _player.WarpTo(npc.Position + Vector2.Down * 16.0f);
            _player.Face(Vector2I.Up);
            return manager.FindTalkTarget(_player) == npc;
        }
        if (!CanTalkTo(earlyLynnaMan))
            throw new InvalidOperationException(
                "Room 0:68's $44:$02 man was visible with TX_1610 but not talkable.");

        if (save.WriteWramByte(0xc6bf, 0x04))
            save.CommitInventoryChange();
        if (!earlyLynnaMan.Active || earlyLynnaMan.TextId != 0x1611 ||
            !CanTalkTo(earlyLynnaMan))
            throw new InvalidOperationException(
                "Room 0:68's $44:$02 man did not switch live to D3 dialogue TX_1611.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru);
        if (!earlyLynnaMan.Active || earlyLynnaMan.TextId != 0x1612 ||
            !CanTalkTo(earlyLynnaMan))
            throw new InvalidOperationException(
                "Room 0:68's $44:$02 man did not switch live to saved-Nayru dialogue TX_1612.");

        if (save.WriteWramByte(0xc6bf, 0x40))
            save.CommitInventoryChange();
        if (earlyLynnaMan.Active || !lateLynnaWoman.Active ||
            lateLynnaWoman.TextId != 0x1523 ||
            makuSeedVillager.Active || !seedSatchelBoy.Active ||
            seedSatchelBoy.TextId != 0x2503 ||
            !CanTalkTo(lateLynnaWoman) || !CanTalkTo(seedSatchelBoy))
            throw new InvalidOperationException(
                "Room 0:68 did not switch to talkable TX_1523/TX_2503 actors in state $03.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame);
        if (earlyLynnaMan.Active || !lateLynnaWoman.Active ||
            lateLynnaWoman.TextId != 0x1524 ||
            !makuSeedVillager.Active || makuSeedVillager.TextId != 0x1434 ||
            !seedSatchelBoy.Active || seedSatchelBoy.TextId != 0x2504 ||
            !CanTalkTo(makuSeedVillager))
            throw new InvalidOperationException(
                "Room 0:68 did not select its state-$04 dialogue and talkable villager cast.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        if (earlyLynnaMan.Active || !lateLynnaWoman.Active ||
            lateLynnaWoman.TextId != 0x1525 ||
            makuSeedVillager.Active || !seedSatchelBoy.Active ||
            seedSatchelBoy.TextId != 0x2505)
            throw new InvalidOperationException(
                "Room 0:68 did not select finished-game dialogue and retire its state-$04 villager.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame, value: false);
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame, value: false);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru, value: false);
        if (save.WriteWramByte(0xc6bf, 0))
            save.CommitInventoryChange();

        manager.LoadRoom(0, _world.LoadRoom(0, 0x78));
        NpcCharacter? clockSecretLady = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3d && npc.Record.SubId == 0x04);
        if (clockSecretLady is not { Active: false } ||
            clockSecretLady.TextId != 0x4d00)
            throw new InvalidOperationException(
                "Room 0:78's TX_4d00 linked-secret old lady appeared in an unlinked game.");
        if (save.WriteWramByte(0xc6bf, 0x08))
            save.CommitInventoryChange();
        if (clockSecretLady.Active)
            throw new InvalidOperationException(
                "Room 0:78's old lady ignored the linked-game requirement after D4.");
        save.SetLinkedGame(linked: true);
        if (!clockSecretLady.Active || !CanTalkTo(clockSecretLady))
            throw new InvalidOperationException(
                "Linked-game plus D4 state did not reveal room 0:78's talkable TX_4d00 old lady.");

        if (save.WriteWramByte(0xc6bf, 0x02))
            save.CommitInventoryChange();
        manager.LoadRoom(3, _world.LoadRoom(3, 0xf8));
        NpcCharacter? ruulSecretLady = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x3d && npc.Record.SubId == 0x05);
        if (ruulSecretLady is not { Active: true } ||
            ruulSecretLady.TextId != 0x4d2d || !CanTalkTo(ruulSecretLady))
            throw new InvalidOperationException(
                "Linked-game plus D2 state did not reveal the paired talkable TX_4d2d old lady.");
        save.SetLinkedGame(linked: false);
        if (save.WriteWramByte(0xc6bf, 0))
            save.CommitInventoryChange();

        manager.LoadRoom(0, _world.LoadRoom(0, 0x25));
        List<NpcCharacter> bridgeCarpenters = manager.Entities<NpcCharacter>().Where(npc =>
            npc.Record.Id == 0x9a).ToList();
        if (bridgeCarpenters.Count != 5 || bridgeCarpenters.Any(npc => !npc.Active))
            throw new InvalidOperationException(
                "Room 0:25 did not begin with its five unlinked pre-bridge carpenters.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSymmetryBridgeBuilt);
        if (bridgeCarpenters.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "GLOBALFLAG_SYMMETRY_BRIDGE_BUILT $25 did not hide room 0:25's carpenters.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSymmetryBridgeBuilt, value: false);

        manager.LoadRoom(0, _world.LoadRoom(0, 0xaa));
        NpcCharacter? dimitriTokay = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x48 && npc.Record.SubId == 0x10);
        if (dimitriTokay is not { Active: false })
            throw new InvalidOperationException(
                "Room 0:aa's Dimitri-event Tokay appeared before D3.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x46));
        List<NpcCharacter> palaceActors = manager.Entities<NpcCharacter>().Where(npc =>
            (npc.Record.Id == 0x37 && npc.Record.SubId == 0x09) ||
            (npc.Record.Id == 0x40 && npc.Record.SubId == 0x0b)).ToList();
        if (palaceActors.Count != 2 || palaceActors.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 1:46's essence/Mystery-Seed-gated Ralph and soldier appeared immediately after the intro.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x65));
        List<NpcCharacter> linkedFinaleActors = manager.Entities<NpcCharacter>().Where(npc =>
            (npc.Record.Id == 0x4d && npc.Record.SubId == 0x0a) ||
            (npc.Record.Id == 0x37 && npc.Record.SubId == 0x12)).ToList();
        if (linkedFinaleActors.Count != 2 || linkedFinaleActors.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 1:65's linked finale Ambi and Ralph appeared in a standard post-intro game.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x68));
        NpcCharacter? linkedSubrosian = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x4e && npc.Record.SubId == 0x00);
        if (linkedSubrosian is not { Active: false })
            throw new InvalidOperationException(
                "Room 1:68's late linked-game Subrosian appeared immediately after the intro.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x75));
        List<NpcCharacter> linkedEndingActors = manager.Entities<NpcCharacter>().Where(npc =>
            (npc.Record.Id == 0x37 && npc.Record.SubId == 0x0a) ||
            (npc.Record.Id == 0x31 && npc.Record.SubId is 0x04 or 0x05) ||
            (npc.Record.Id == 0x36 && npc.Record.SubId == 0x0a) ||
            (npc.Record.Id == 0xad && npc.Record.SubId == 0x04)).ToList();
        if (linkedEndingActors.Count != 5 || linkedEndingActors.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 1:75's pre-tower/linked Impa, Ralph, Nayru, and Zelda variants appeared immediately after the intro.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x47));
        List<NpcCharacter> heritageActors = manager.Entities<NpcCharacter>().Where(npc =>
            (npc.Record.Id == 0x4f && npc.Record.SubId == 0x01) ||
            (npc.Record.Id == 0xad && npc.Record.SubId == 0x08)).ToList();
        if (heritageActors.Count != 2 || heritageActors.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 1:47's late-story Impa and linked Zelda variants appeared immediately after the intro.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x58));
        List<NpcCharacter> flameActors = manager.Entities<NpcCharacter>().Where(npc =>
            (npc.Record.Id == 0x4f && npc.Record.SubId == 0x02) ||
            (npc.Record.Id == 0x36 && npc.Record.SubId == 0x0d)).ToList();
        if (flameActors.Count != 2 || flameActors.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "Room 1:58's flame-of-despair Impa and Nayru variants appeared immediately after the intro.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0xcb));
        NpcCharacter? rosa = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x68 && npc.Record.SubId == 0x00);
        if (rosa is not { Active: false })
            throw new InvalidOperationException(
                "Room 1:cb's linked pre-D3 Rosa appeared immediately after the intro.");

        manager.LoadRoom(2, _world.LoadRoom(2, 0xa0));
        NpcCharacter? d7Zora = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0xab && npc.Record.SubId == 0x10);
        if (d7Zora is not { Active: false })
            throw new InvalidOperationException(
                "Room 2:a0's D7-gated Zora appeared immediately after the intro.");

        manager.LoadRoom(2, _world.LoadRoom(2, 0xd7));
        NpcCharacter? linkedZora = manager.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0xab && npc.Record.SubId == 0x12);
        if (linkedZora is not { Active: false })
            throw new InvalidOperationException(
                "Room 2:d7's linked-game Zora appeared in a standard post-intro game.");

        manager.LoadRoom(3, _world.LoadRoom(3, 0x9e));
        List<NpcCharacter> nayruHouseActors = manager.Entities<NpcCharacter>().Where(npc =>
            npc.Record.Id is 0x36 or 0x4f or 0xad).ToList();
        List<NpcCharacter> activeHouseActors = nayruHouseActors.Where(npc => npc.Active).ToList();
        if (nayruHouseActors.Count != 11 || activeHouseActors.Count != 1 ||
            activeHouseActors[0].Record.Id != 0x4f ||
            activeHouseActors[0].Record.SubId != 0x00 ||
            activeHouseActors[0].Record.Var03 != 0x00 ||
            activeHouseActors[0].Position != new Vector2(0x38, 0x38) ||
            activeHouseActors[0].TextId != 0x0120 ||
            string.IsNullOrEmpty(activeHouseActors[0].Message))
        {
            throw new InvalidOperationException(
                "Immediate-post-intro room 3:9e did not contain only talkable Impa $4f:$00 state $00 at $38,$38.");
        }

        save.SetRoomFlag(0, 0x83, OracleSaveData.RoomFlag80);
        NpcCharacter? passageImpa = nayruHouseActors.Find(npc =>
            npc.Record.Id == 0x4f && npc.Record.Var03 == 0x01);
        if (passageImpa is not { Active: true } ||
            nayruHouseActors.Count(npc => npc.Active) != 1 ||
            passageImpa.Position != new Vector2(0x28, 0x48) ||
            passageImpa.TextId != 0x0121)
        {
            throw new InvalidOperationException(
                "Opening D2's passage did not live-swap Nayru's-house Impa to state $01 and TX_0121.");
        }

        SetTreasure(TreasureDatabase.TreasureHarp, value: true);
        NpcCharacter? harpImpa = nayruHouseActors.Find(npc =>
            npc.Record.Id == 0x4f && npc.Record.Var03 == 0x02);
        if (harpImpa is not { Active: true } || nayruHouseActors.Count(npc => npc.Active) != 1 ||
            harpImpa.Position != new Vector2(0x68, 0x28) || harpImpa.TextId != 0x0122)
        {
            throw new InvalidOperationException(
                "Obtaining the harp did not live-swap Nayru's-house Impa to state $02 and TX_0122.");
        }

        save.SetLinkedGame(linked: true);
        NpcCharacter? linkedD3Impa = nayruHouseActors.Find(npc =>
            npc.Record.Id == 0x4f && npc.Record.Var03 == 0x0b);
        if (linkedD3Impa is not { Active: true } || nayruHouseActors.Count(npc => npc.Active) != 1)
            throw new InvalidOperationException(
                "Linked-game state did not select Nayru's-house Impa behavior $0b before D3.");
        if (save.WriteWramByte(0xc6bf, 0x04))
            save.CommitInventoryChange();
        if (nayruHouseActors.Any(npc => npc.Active))
            throw new InvalidOperationException(
                "D3 essence bit 2 did not delete linked Nayru's-house Impa state $0c.");

        save.SetLinkedGame(linked: false);
        if (save.WriteWramByte(0xc6bf, 0))
            save.CommitInventoryChange();
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru);
        NpcCharacter? houseNayru = nayruHouseActors.Find(npc =>
            npc.Record.Id == 0x36 && npc.Record.SubId == 0x0b);
        if (houseNayru is not { Active: true })
            throw new InvalidOperationException(
                "GLOBALFLAG_SAVED_NAYRU $11 did not reveal room 3:9e's pre-Maku-seed Nayru.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagGotRingFromZelda);
        NpcCharacter? houseZelda = nayruHouseActors.Find(npc =>
            npc.Record.Id == 0xad && npc.Record.SubId == 0x07);
        if (houseZelda is not { Active: true })
            throw new InvalidOperationException(
                "GLOBALFLAG_GOT_RING_FROM_ZELDA $38 did not reveal room 3:9e's pre-Maku-seed Zelda.");
        SetTreasure(TreasureDatabase.TreasureMakuSeed, value: true);
        if (houseNayru.Active || houseZelda.Active)
            throw new InvalidOperationException(
                "Obtaining the Maku Seed did not remove Nayru and Zelda from room 3:9e.");

        manager.Clear();
        RemoveChild(validationRoot);
        validationRoot.QueueFree();
        GD.Print("Validated room 0:5a's TX_5700/TX_5701 intro monkeys, opposing $06/$07 " +
            "$20-frame animation loops, rooms 2:ea/2:eb's 72-record family spawner, " +
            "Bipin $28:$00's SPEED_100 X=$28/$58 patrol, $04/$05 animation reversal, " +
            "and moving objectPreventLinkFromPassing collision, " +
            "330 visibility, 100 dialogue, and two position predicates, roaming-dog " +
            "location selection, rooms 0:68/0:78's phased and linked talkable cast, " +
            "room 3:9e's post-intro Impa, var03 selection, compound and alternative gates, " +
            "live refresh, and lifecycle-safe hiding.");
    }

    private void ValidateBipinBlossomNaming()
    {
        Span<byte> emptyName = stackalloc byte[6];
        emptyName.Clear();
        bool changed = _saveData.WriteWramBytes(
            OracleSaveData.ChildNameAddress, emptyName);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildStatusAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildStageAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.NextChildStageAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildFlagsAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildPersonalityAddress, 0x00);
        if (changed)
            _saveData.CommitInventoryChange();
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame, value: false);
        _entities.RuntimeState.SetWramByte(
            OracleRuntimeState.SeedTreeRefilledBitsetAddress, 0x00);

        LoadValidationRoom(2, 0xea);
        NpcCharacter? blossom = _entities.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x2b && npc.Record.SubId == 0x00);
        if (blossom is null)
            throw new InvalidOperationException(
                "Room 2:ea did not provide Blossom $2b:$00 for child-name validation.");

        _player.WarpTo(blossom.Position + Vector2.Down * 16.0f);
        _player.Face(Vector2I.Up);
        if (!_interactions.TryInteract(_player) ||
            !_dialogue.CurrentMessage.Contains("would you call", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Blossom $2b:$00 did not begin TX_4400's child-naming interaction.");
        }
        _dialogue.Close();
        _interactions.UpdateFamilyNamingForValidation(0.0);
        if (!_interactions.GameplayMenuActive ||
            _interactions.KidNameScreenForValidation is not { EnteredName.Length: 0 })
        {
            throw new InvalidOperationException(
                "Closing TX_4400 did not open MENU_KIDNAME $07 with an empty five-character field.");
        }

        _interactions.CommitKidNameForValidation(string.Empty);
        _interactions.UpdateFamilyNamingForValidation(0.0);
        if (!_dialogue.CurrentMessage.Contains("more thought", StringComparison.Ordinal) ||
            _saveData.ChildNamed)
        {
            throw new InvalidOperationException(
                "An empty child name did not show TX_440a without advancing family state.");
        }
        _dialogue.Close();
        _interactions.UpdateFamilyNamingForValidation(0.0);
        if (_interactions.FamilyNamingActive)
            throw new InvalidOperationException(
                "Blossom's empty-name response did not return to her ordinary talk loop.");

        if (!_interactions.TryInteract(_player))
            throw new InvalidOperationException(
                "Blossom could not restart child naming after an empty name.");
        _dialogue.Close();
        _interactions.UpdateFamilyNamingForValidation(0.0);
        _interactions.CommitKidNameForValidation("Pip");
        _interactions.UpdateFamilyNamingForValidation(0.0);
        if (!_dialogue.ChoiceActive ||
            !_dialogue.CurrentMessage.Contains("Pip", StringComparison.Ordinal) ||
            !_dialogue.CurrentMessage.Contains("Yes", StringComparison.Ordinal) ||
            !_dialogue.CurrentMessage.Contains("No", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MENU_KIDNAME did not pass the candidate name into TX_4407's Yes/No confirmation.");
        }

        _dialogue.SubmitChoiceForValidation(1);
        _interactions.UpdateFamilyNamingForValidation(0.0);
        if (!_interactions.GameplayMenuActive ||
            _interactions.KidNameScreenForValidation?.EnteredName != "Pip" ||
            _saveData.ChildNamed)
        {
            throw new InvalidOperationException(
                "Choosing No in TX_4407 did not reopen MENU_KIDNAME with the candidate preserved.");
        }
        _interactions.CommitKidNameForValidation("Pip");
        _interactions.UpdateFamilyNamingForValidation(0.0);
        _dialogue.SubmitChoiceForValidation(0);
        _interactions.UpdateFamilyNamingForValidation(0.0);

        // blossom_decideInitialChildStatus sums the encoded name's low
        // nibbles: P=$0, i=$9, p=$0, so Pip selects status $01.
        if (_saveData.ChildName != "Pip" || !_saveData.ChildNamed ||
            _saveData.ReadWramByte(OracleSaveData.ChildStatusAddress) != 0x01 ||
            _saveData.ReadWramByte(OracleSaveData.ChildStageAddress) != 0x00 ||
            _saveData.ReadWramByte(OracleSaveData.NextChildStageAddress) != 0x01)
        {
            throw new InvalidOperationException(
                "Confirming Pip did not reproduce wKidName/wChildStatus/wc6e2/wNextChildStage writes.");
        }

        NpcCharacter? bipin = _entities.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x28 && npc.Record.SubId == 0x00);
        if (bipin is not { TextId: 0x4301 } || blossom.TextId != 0x4409 ||
            !bipin.Message.Contains("Pip", StringComparison.Ordinal) ||
            !blossom.Message.Contains("Pip", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Naming the child did not live-switch Bipin/Blossom to TX_4301/TX_4409 with \\Child expanded.");
        }

        for (int frame = 0; frame < 29; frame++)
            _interactions.UpdateFamilyNamingForValidation(1.0 / 60.0);
        if (_dialogue.IsOpen)
            throw new InvalidOperationException(
                "Blossom showed TX_4408 before the original 30-update delay elapsed.");
        _interactions.UpdateFamilyNamingForValidation(1.0 / 60.0);
        if (!_dialogue.CurrentMessage.Contains("It's a fine", StringComparison.Ordinal) ||
            !_dialogue.CurrentMessage.Contains("Come visit us", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Blossom did not show TX_4408 after the original 30-update delay.");
        }
        _dialogue.Close();
        _interactions.UpdateFamilyNamingForValidation(0.0);
        if (_interactions.FamilyNamingActive)
            throw new InvalidOperationException(
                "Blossom's child-naming interaction did not finish after TX_4408 closed.");

        LoadValidationRoom(2, 0xea);
        blossom = _entities.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x2b && npc.Record.SubId == 0x00);
        bipin = _entities.Entities<NpcCharacter>().Find(npc =>
            npc.Record.Id == 0x28 && npc.Record.SubId == 0x00);
        if (blossom is not { TextId: 0x4409 } || bipin is not { TextId: 0x4301 } ||
            !blossom.Message.Contains("Pip", StringComparison.Ordinal) ||
            !bipin.Message.Contains("Pip", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Reloading room 2:ea lost the named stage-$00 family dialogue state.");
        }

        changed = _saveData.WriteWramBytes(OracleSaveData.ChildNameAddress, emptyName);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildStatusAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildStageAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.NextChildStageAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildFlagsAddress, 0x00);
        changed |= _saveData.WriteWramByte(OracleSaveData.ChildPersonalityAddress, 0x00);
        if (changed)
            _saveData.CommitInventoryChange();

        GD.Print("Validated Bipin/Blossom stage-$00 movement and MENU_KIDNAME $07: empty-name " +
            "handling, No/re-edit, Yes confirmation, original child-status/state writes, " +
            "30-update TX_4408 delay, and persistent TX_4301/TX_4409 \\Child dialogue.");
    }
}
