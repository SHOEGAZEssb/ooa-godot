using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom3cfRom()
    {
        // Unlike subid $00, $01 has neither GLOBALFLAG_0b dialogue selection
        // nor GLOBALFLAG_FINISHEDGAME deletion. Exercise each input separately.
        for (int storyCase = 0; storyCase < 3; storyCase++)
        foreach (bool batch in RomHostSchedules(storyCase))
        {
            ReinitializeGameplayForValidation();
            if (storyCase == 1) _saveData.SetGlobalFlag(GlobalFlag.Flag0b);
            if (storyCase == 2) _saveData.SetGlobalFlag(GlobalFlag.FinishedGame);
            LoadValidationRoom(3, 0xcf);
            _inventory.EquipA(0); _inventory.EquipB(0);
            _dialogue.MessageSpeed = 4; _saveData.SetTextSpeed(4);
            var npc = _entities.Entities<NpcCharacter>().Single();
            const string text = "I began my life\nas a fisherman\non this island,\nbut now the\nisland drifts\ntoward the\nhorizon. In my\ngrandson's day,\nthis island will\nbe farther west.";
            FailIf(npc.Record is not { Id: 0x42, SubId: 1, Var03: 0, TextId: 0x0f02,
                X: 0x50, Y: 0x40, CanFace: false, DefaultAnimation: 4,
                Palette: 1, TileBase: 0x1c,
                Implementation: NpcImplementationClassification.OrdinaryGeneric } ||
                npc.Message != text || !npc.Visible || !npc.Active ||
                npc.CurrentAnimationOpaquePixels == 0 ||
                npc.ObjectCollisionBounds.Size != new Vector2(12, 12) ||
                npc.LinkBlockingBounds.Size != new Vector2(24, 24),
                "Room3:cf must retain mustacheMan$42:$01 at $4050, fixed animation$04/palette$01, $06 radii and complete TX_0f02.");
            _player.WarpTo(new(80, 104)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position) || _entities.BlocksLink(_player.Position),
                "Room3:cf conversation must start in actual open house geometry.");
            var seed = _random.CaptureState();
            byte roomFlags = _saveData.GetRoomFlags(3, 0xcf);
            var rom = new DialogueRom(_saveData);
            rom.InitializeGameplay(_currentRoom, seed, 80, 104);
            rom[0xd008] = 0; rom[0xd009] = 0;
            int slot = _entities.InteractionSlot(npc);
            int address = 0xd040 + slot * 0x100;
            rom.AddNpc(slot, 0x42, 1, 80, 64);
            // Leave ROM state zero for the first complete gameplay update:
            // initialization falls through to interactionAnimateAsNpc.
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, lastHeld = 0, animatedUpdates = 0;
            int pages = 0;
            int previousTextState = -1;
            byte objectMask = 0;
            void SetObjectMask(byte mask)
            {
                objectMask = mask;
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, mask);
                rom[0xcc8a] = mask;
            }
            void Step(int count = 1, int held = 0)
            {
                int edge = held & ~lastHeld; lastHeld = held;
                Vector2 move = (held & 0x40) != 0 ? Vector2.Up : Vector2.Zero;
                StepGameplayUpdates(count, move, MenuRomActions(held), MenuRomActions(edge), batch, () =>
                {
                    // updateInteractions freezes initialized ordinary objects
                    // while wTextIsActive is nonzero. Opening text still runs
                    // this object's final animation update in the same pass.
                    bool wasOpen = rom.Active;
                    rom.AdvanceGameplay(edge, held, move == Vector2.Up ? 0 : 0xff, _entities.FrameCounter);
                    edge = 0;
                    if (!wasOpen && (objectMask & 2) == 0) animatedUpdates++;
                    string context = $"Room3:cf $42:$01 story={storyCase}, batch={batch}, update{++update}";
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                        _dialogue.IsOpen != rom.Active || npc.Position != new Vector2(80, 64) ||
                        npc.FacingVector != Vector2I.Down || !npc.Active || !npc.Visible,
                        context + $": XY={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}; text={_dialogue.IsOpen}/{rom.Active}.");
                    // interactionAnimation5a468: two $10-update frames, OAM
                    // indices $00/$02 -> $420e/$42a7, no facing or RNG.
                    int frame = animatedUpdates / 16 % 2;
                    FailIf(npc.CurrentAnimationFrame != frame ||
                        SomariaPrivate<double>(npc, "_animationTicks") != animatedUpdates % 16 ||
                        rom[address + 0x20] != 16 - animatedUpdates % 16 ||
                        rom.Word(address + 0x1e) != (frame == 0 ? 0x420e : 0x42a7),
                        context + $": animation frame={npc.CurrentAnimationFrame}/{frame}, ROM counter=${rom[address+0x20]:x2}, OAM=${rom.Word(address+0x1e):x4}.");
                    if (rom.Active && rom.State != 0x10)
                    {
                        CompareDialogueLineRom(rom, context);
                        int glyphCount = _dialogue.VisibleGlyphCount;
                        if (_dialogue.IsScrollingText)
                        {
                            var segment = DialoguePrivate<System.Collections.Generic.List<TextSegment>>(_dialogue, "_segments")
                                [DialoguePrivate<int>(_dialogue, "_segmentIndex")];
                            glyphCount -= segment.Lines[DialoguePrivate<int>(_dialogue, "_firstLineIndex")].Glyphs.Count;
                        }
                        int scroll = rom.State is 6 or 7 or 0x0c or 0x0d ? 8 : rom.State is 8 or 0x0e ? 16 : 0;
                        FailIf(glyphCount != rom.GlyphCount || _dialogue.TextScrollOffset != scroll,
                            context + ": displayed glyph count or two-line scroll position differs.");
                    }
                    if (rom.State == 5 && previousTextState != 5) pages++;
                    previousTextState = rom.State;
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), context + ": text sound order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": NPC/text shared RNG differs.");
                });
            }
            Step(32);
            FailIf(rom.Word(address + 0x32) != 0x0f02 || rom[address + 4] != 1 ||
                rom[address + 0x26] != 6 || rom[address + 0x27] != 6,
                "Clean-US mustacheMan$01 initializer must select TX_0f02/state$01 and generic $06 radii.");
            for (int wait = 0; _player.Position.Y > 78 && wait < 40; wait++) Step(held: 0x40);
            Step();
            Step(12, held: 0x40);
            Step();
            FailIf(_player.Position.Y > 78 || _currentRoom.IsSolid(_player.Position) || _entities.BlocksLink(_player.Position),
                "Room3:cf mustache man must be reachable from the actual entrance without occupying his hitbox.");
            FailIf(_player.Position.Y != 76,
                "Room3:cf $06 NPC + $06 Link radii must stop the entrance approach at Y=$4c.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                pages = 0;
                if (storyCase == 0 && repeat == 0)
                {
                    // DISABLE_INTERACTIONS ($02) leaves Link's $81 gate
                    // clear: selection must survive until the actor can run.
                    SetObjectMask(2);
                    Step(held: 1); Step(3);
                    FailIf(_dialogue.IsOpen || rom[address + 0x31] != 1 ||
                        _entities.FindTalkTarget(_player) is not null,
                        "Room3:cf pressed-A selection must wait under mask$02 and be unavailable for duplicate selection.");
                    SetObjectMask(0);
                    Step(); Step(2);
                }
                else
                {
                    Step(held: 1); Step(2);
                }
                FailIf(!_dialogue.IsOpen, "Reachable A-button contact must open TX_0f02.");
                int limit = update + 600;
                while (rom.Active && update < limit)
                {
                    bool waiting = rom.State is 5 or 0x0f;
                    bool pressA = waiting ? (lastHeld & 1) == 0 : update % 4 == 0;
                    Step(batch && !waiting ? 3 : 1, pressA ? 1 : 0);
                }
                // The native two-line window waits four times while scrolling
                // through the remaining eight lines of this ten-line message.
                FailIf(rom.Active || pages != 4,
                    $"TX_0f02 must finish all ten source lines and four scroll advances: active={rom.Active}, pages={pages}, state=${rom.State:x2}.");
                Step(3);
                FailIf(_dialogue.BlocksPlayerInput, "TX_0f02 must release closing input before repeated conversation.");
                FailIf(_saveData.GetRoomFlags(3, 0xcf) != roomFlags ||
                    _saveData.HasGlobalFlag(GlobalFlag.Flag0b) != (storyCase == 1) ||
                    _saveData.HasGlobalFlag(GlobalFlag.FinishedGame) != (storyCase == 2),
                    "Mustache man$01's generic conversation must not write room or story flags.");
            }
            Step(held: 1); Step(2);
            FailIf(!_dialogue.IsOpen, "Room3:cf cancellation fixture must have an active conversation.");
            LoadValidationRoom(3, 0xce);
            FailIf(_dialogue.IsOpen || _entities.Entities<NpcCharacter>().Contains(npc),
                "Leaving room3:cf must close text and retire its talk target.");
            LoadValidationRoom(3, 0xcf);
            var returned = _entities.Entities<NpcCharacter>().Single();
            FailIf(returned.TextId != 0x0f02 || returned.Message != text || !returned.Visible ||
                returned.Position != new Vector2(80, 64),
                "Room3:cf re-entry must restore its source conversation independent of story flags$0b/$14.");
            if (storyCase == 0)
            {
                // Cancellation also retires an unconsumed pressed-A signal.
                // Approach through open geometry again before queuing it.
                _player.WarpTo(new(80, 104)); _player.Face(Vector2I.Up);
                StepGameplayUpdates(3, Vector2.Zero, batched: batch);
                StepGameplayUpdates(48, Vector2.Up, batched: batch);
                FailIf(_collision.Collides(_player.Position) || _entities.BlocksLink(_player.Position) ||
                    !returned.CanTalkTo(_player),
                    $"Room3:cf pending-cancellation approach must remain reachable: XY={_player.Position}, facing={_player.FacingVector}.");
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 2);
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batch);
                FailIf(_dialogue.IsOpen || _entities.FindTalkTarget(_player) is not null,
                    "Room3:cf cancellation must arrange an unconsumed selection under mask$02.");
                LoadValidationRoom(3, 0xce);
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0);
                StepGameplayUpdates(3, Vector2.Zero, batched: batch);
                FailIf(_dialogue.IsOpen || _entities.Entities<NpcCharacter>().Contains(returned),
                    "A discarded room3:cf selection must not open text after room replacement.");
            }
        }
        GD.Print("Validated room3:cf mustacheMan$42:$01 source initialization, exact animation/collision, reachable repeated TX_0f02 paging/sound/RNG, story flags, cancellation and re-entry with clean-US gameplay in split/batched updates.");
    }
}
