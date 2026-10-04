using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static T DialoguePrivate<T>(DialogueBox dialogue, string name) =>
        (T)typeof(DialogueBox).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialogue)!;

    private void CompareDialogueLineRom(DialogueRom rom, string context)
    {
        if (rom.State is not (2 or 4 or 0x0a) || rom.Text(0xd0d0) != 0 || rom[0xcba1] != 0) return;
        int lineIndex = DialoguePrivate<int>(_dialogue, "_firstLineIndex") + (rom.State == 2 ? 0 : 1);
        var segments = DialoguePrivate<List<TextSegment>>(_dialogue, "_segments");
        var lines = segments[DialoguePrivate<int>(_dialogue, "_segmentIndex")].Lines;
        var glyphs = lineIndex < lines.Count ? lines[lineIndex].Glyphs : TextLine.Empty.Glyphs;
        int count = Enumerable.Range(0, 16).TakeWhile(column => rom.Text(0xd400 + column) != 0).Count();
        FailIf(glyphs.Count != count, $"{context}: prepared line length runtime={glyphs.Count}, ROM={count}.");
        for (int column = 0; column < count; column++)
        {
            TextGlyph glyph = glyphs[column];
            int attribute = glyph.Source == FontSource.TradeItem ? 0x84 : glyph.ColorIndex >= 2 ? 0x81 : 0x80;
            int code = glyph.Source == FontSource.Main ? glyph.Code : 0x06;
            FailIf(code != rom.Text(0xd400 + column) || attribute != rom.Text(0xd410 + column) ||
                glyph.CharacterSound != rom.Text(0xd430 + column) || glyph.SoundEffect != rom.Text(0xd440 + column),
                $"{context}: column ${column:x2}, runtime code/attr/sound/effect=${glyph.Code:x2}/${attribute:x2}/${glyph.CharacterSound:x2}/${glyph.SoundEffect:x2}, " +
                $"ROM=${rom.Text(0xd400 + column):x2}/${rom.Text(0xd410 + column):x2}/${rom.Text(0xd430 + column):x2}/${rom.Text(0xd440 + column):x2}.");
            if (glyph.Source == FontSource.TradeItem) continue;
            Texture2D font = DialoguePrivate<Texture2D>(_dialogue,
                glyph.Source == FontSource.Symbol ? "_symbolTexture" : "_fontTexture");
            using Image pixels = font.GetImage();
            int shade = glyph.Code == 0x14 && glyph.Source == FontSource.Main ? 1 : glyph.ColorIndex switch
            { 0 or 4 => 2, 1 or 3 => 1, 2 => 0, _ => throw new InvalidOperationException(context) };
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int address = 0xd200 + column * 32 + y * 2;
                int bit = 7 - x;
                int actual = ((rom.Text(address) >> bit) & 1) | (((rom.Text(address + 1) >> bit) & 1) << 1);
                int expected = pixels.GetPixel((glyph.Code & 15) * 8 + x, (glyph.Code >> 4) * 16 + y).A > 0 ? shade : 3;
                FailIf(actual != expected, $"{context}: glyph ${glyph.Code:x2} font/color pixel ({x},{y}) runtime shade={expected}, ROM={actual}.");
            }
        }
    }

    private void RunDialogueRom(int id, string message, int speed, bool batched, bool accelerate, int flags = 0)
    {
        ReinitializeGameplayForValidation();
        _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
        LoadValidationRoom(4, 0x91); _entities.Clear();
        _saveData.SetLinkName("Zelda");
        _dialogue.MessageSpeed = speed;
        _dialogue.ShowMessage(message, DialogueScreenContext.Gameplay(_player.Position.Y), textboxFlags: flags);
        var rom = new DialogueRom(_saveData);
        rom.Open(id, speed, (int)_player.Position.Y, flags);
        var sounds = _sound.AttachPlayRequestAudit();
        Vector2 position = _player.Position;
        int update = 0;
        string Context() => $"TX_{id:x4} speed={speed}, batch={batched}, fast={accelerate}, update={update}, ROM state=${rom.State:x2}";
        void Step(int count, int edge = 0, int held = 0)
        {
            string[] Actions(int mask) => new[] { "attack", "item", "map", "inventory", "move_right", "move_left", "move_up", "move_down" }
                .Where((_, bit) => (mask & (1 << bit)) != 0).ToArray();
            StepGameplayUpdates(count, Vector2.Zero, Actions(held), Actions(edge), batched, () =>
            {
                rom.AdvanceText(edge, held); edge = 0; update++;
                string context = Context();
                FailIf(_dialogue.IsOpen != rom.Active || _player.Position != position,
                    $"{context}: text lifetime or gameplay freeze differs, runtime={_dialogue.IsOpen}, ROM={rom.Active}.");
                if (rom.Active)
                {
                    int textboxY = rom[0xcbac] switch { 0 => 24, 1 => 56, 2 => 96, 3 => 112, _ => throw new InvalidOperationException(context) };
                    FailIf(_dialogue.Position.Y != textboxY, $"{context}: textbox position runtime={_dialogue.Position.Y}, ROM={textboxY}.");
                }
                // $10 has restored the old tilemap; the runtime is retaining
                // its final glyphs until the separate closing update.
                if (rom.Active && rom.State != 0x10)
                {
                    int glyphCount = _dialogue.VisibleGlyphCount;
                    if (_dialogue.IsScrollingText)
                    {
                        // GlyphCount counts upper character tiles. Scrolling
                        // clips that half of the outgoing top line immediately.
                        var segment = DialoguePrivate<List<TextSegment>>(_dialogue, "_segments")[DialoguePrivate<int>(_dialogue, "_segmentIndex")];
                        glyphCount -= segment.Lines[DialoguePrivate<int>(_dialogue, "_firstLineIndex")].Glyphs.Count;
                    }
                    FailIf(glyphCount != rom.GlyphCount,
                        $"{context}: displayed upper glyph tiles runtime={glyphCount}, ROM={rom.GlyphCount}.");
                    CompareDialogueLineRom(rom, context);
                    int scroll = rom.State is 6 or 7 or 0x0c or 0x0d ? 8 : rom.State is 8 or 0x0e ? 16 : 0;
                    FailIf(_dialogue.TextScrollOffset != scroll, $"{context}: scroll runtime={_dialogue.TextScrollOffset}, ROM={scroll}.");
                }
                FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"{context}: sound requests runtime=[{string.Join(',', sounds.Requests)}], ROM=[{string.Join(',', rom.Sounds)}].");
            });
        }
        Step(2, 3, 3); // Opening input is ignored; held A/B does not skip text.
        while (rom.Active && update < 3500)
        {
            bool lineDone = rom.Text(0xd0d0) >= 16 || rom.Text(0xd400 + rom.Text(0xd0d0)) == 0;
            bool waiting = rom.State is 5 or 0x0f || (rom.Text(0xd0c1) & 2) != 0 && lineDone;
            Step(batched && !waiting && !accelerate ? 4 : 1,
                waiting ? 1 : accelerate && update % 5 == 0 ? 2 : 0, held: 3);
        }
        FailIf(rom.Active, $"{Context()}: text never completed.");
        Step(3);
        FailIf(_dialogue.BlocksPlayerInput, $"{Context()}: modal ownership survived completion.");
    }

    private void ValidateDialoguePagingRom()
    {
        var birds = new KnowItAllBirdDatabase();
        foreach (bool batched in new[] { false, true })
        foreach (bool accelerate in new[] { false, true })
        foreach (int subid in new[] { 0, 2, 8 })
            RunDialogueRom(0x320a + subid, birds.Get(subid).Tutorial, 3, batched, accelerate);
        GD.Print("Validated clean-US dictionary-expanded text, two-line scrolling, automatic second lines, stop clearing, A/B reveal/continue, sounds and closing through split/batched gameplay.");
    }

    private void ValidateDialogueFormattingRom()
    {
        var impa = new ImpaIntroEventDatabase().Record;
        var birds = new KnowItAllBirdDatabase();
        var forest = new CompanionForestDatabase();
        string vision = ((CutsceneShowTextCommand)new HarpOfAgesEventDatabase().Commands[2]).Message;
        string essence = new CrownDungeonDatabase().Essence.Message;
        var advice = new MakuTreeAdviceDatabase();
        FailIf(!advice.TryGet(3, false, out var maku), "Maku TX_0500 fixture is missing.");
        int hostCase = 0;
        for (int speed = 0; speed < 5; speed++)
        foreach (bool batched in RomHostSchedules(hostCase++))
        {
            RunDialogueRom(0x0102, impa.Text, speed, batched, accelerate: true);
            // All formatting/control streams run at the fastest speed; the
            // canonical stream independently checks every speed's cadence.
            if (speed != 4) continue;
            RunDialogueRom(0x0103, impa.LinkedText, speed, batched, accelerate: false);
            foreach (int subid in new[] { 3, 4 })
                RunDialogueRom(0x320a + subid, birds.Get(subid).Tutorial, speed, batched, accelerate: true);
            RunDialogueRom(0x1133, forest.Text(0x1133), speed, batched, accelerate: true);
            RunDialogueRom(0x1133, forest.Text(0x1133), speed, batched, accelerate: false);
            RunDialogueRom(0x1d10, vision, speed, batched, accelerate: true, flags: 4);
            RunDialogueRom(0x0012, essence, speed, batched, accelerate: true);
            RunDialogueRom(0x0500, maku.Text, speed, batched, accelerate: true);
            RunDialogueRom(0x0102, impa.Text, speed, batched, accelerate: true, flags: 1);
            RunDialogueRom(0x0102, impa.Text, speed, batched, accelerate: true, flags: 4);
            RunDialogueRom(0x0102, impa.Text, speed, batched, accelerate: true, flags: 0x14);
        }
        GD.Print("Validated clean-US text calls, name substitution, normal/symbol/item glyphs, color/attribute composition, all five speeds and sound timing against generated messages through split/batched gameplay.");
    }

    private void ValidateDialogueChoicesRom()
    {
        string prompt = new NpcDatabase().GetRoomNpcs(3, 0xf7).Single(record => record.SubId == 0).Message;
        // Independent source text for two-row option layouts: text/ages/text.yaml.
        (int Id, string Text)[] cases =
        [
            (0x3200, prompt),
            (0x0c00, "Did you drop a\n\\col(1)Golden Bomb\\col(0)? Or\na \\col(1)Silver Bomb\\col(0)?\\stop\n \\opt()Golden \\opt()Silver\n \\opt()A regular one"),
            (0x440c, "Really? How much\ncan you lend us?\n \\opt()150 \\opt()50\n \\opt()10  \\opt()1")
        ];
        foreach (bool batched in new[] { false, true })
        foreach (var entry in cases)
        for (int speed = 0; speed < 5; speed++)
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x91); _entities.Clear();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            var rom = new DialogueRom(_saveData);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            bool ready = false;
            void Step(int count, int mask = 0)
            {
                string[] actions = new[] { "attack", "item", "map", "inventory", "move_right", "move_left", "move_up", "move_down" }
                    .Where((_, bit) => (mask & (1 << bit)) != 0).ToArray();
                int edge = mask;
                StepGameplayUpdates(count, Vector2.Zero, actions, actions, batched, () =>
                {
                    rom.AdvanceText(edge, mask); edge = 0; update++;
                    string context = $"Choice TX_{entry.Id:x4}, speed={speed}, batch={batched}, update={update}, mode/state=${rom[0xcba1]:x2}/${rom.State:x2}";
                    FailIf(_dialogue.IsOpen != rom.Active, $"{context}: closing lifetime differs.");
                    if (rom.Active)
                    {
                        bool input = rom[0xcba1] == 1 && rom.State == 2;
                        FailIf((DialoguePrivate<int>(_dialogue, "_choicePhase") == 2) != input,
                            $"{context}: option input/cursor delay boundary differs.");
                        if (rom[0xcba1] == 1)
                        {
                            FailIf(_dialogue.SelectedChoice != rom.Text(0xd0e8), $"{context}: cursor runtime={_dialogue.SelectedChoice}, ROM={rom.Text(0xd0e8)}.");
                            var segment = DialoguePrivate<List<TextSegment>>(_dialogue, "_segments")[DialoguePrivate<int>(_dialogue, "_segmentIndex")];
                            int firstLine = DialoguePrivate<int>(_dialogue, "_firstLineIndex"), option = 0;
                            for (int row = 0; row < 2; row++)
                            foreach (int column in segment.Lines[firstLine + row].OptionColumns)
                                FailIf(rom.Text(0xd0e0 + option++) != 0x40 + row * 0x20 + column * 2 + 1,
                                    $"{context}: source cursor column/row differs.");
                            FailIf(rom.Text(0xd0e0 + option) != 0, $"{context}: native option count differs.");
                        }
                        if (DialoguePrivate<int>(_dialogue, "_choicePhase") >= 0)
                        {
                            int[] cursor = Enumerable.Range(0, 0xc0).Where(offset => rom.Text(0xd000 + offset) == 4).ToArray();
                            FailIf(_dialogue.ChoiceCursorVisible != (cursor.Length == 1), $"{context}: visible cursor boundary differs.");
                            if (cursor.Length == 1)
                            {
                                int nativePosition = rom.Text(0xd0e9);
                                int expected = (nativePosition & 0x20) != 0 ? 0x60 : 0x20;
                                expected += ((nativePosition & 0x1e) >> 1) + 2;
                                FailIf(cursor[0] != expected, $"{context}: native cursor tile is outside its reserved glyph column.");
                            }
                        }
                        CompareDialogueLineRom(rom, context);
                        ready = input;
                    }
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), $"{context}: sound requests differ: [{string.Join(',', sounds.Requests)}] / [{string.Join(',', rom.Sounds)}].");
                });
            }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                _dialogue.MessageSpeed = speed;
                _dialogue.ShowGameplayChoiceMessage(entry.Text, _player.Position.Y);
                rom.Open(entry.Id, speed, (int)_player.Position.Y);
                ready = false;
                int limit = update + 1200;
                while (!ready && update < limit)
                {
                    // Presses during option initialization/delay must be ignored.
                    int mask = rom[0xcba1] == 1 ? 0xff : update % 3 == 0 ? 1 : 0;
                    Step(batched && rom[0xcba1] == 1 ? 3 : 1, mask);
                }
                FailIf(!ready, $"TX_{entry.Id:x4} never reached choice input.");
                Step(5, 0); // Holding without an edge never moves or confirms.
                for (int directions = 1; directions < 16; directions++) Step(1, directions << 4);
                Step(1, 0xfe); // B precedes every direction and Select/Start.
                Step(1, 0x03); // B also wins over A.
                Step(1, 0);
                int selected = rom.Text(0xd0e8);
                Step(1, 0xfd); // A precedes directions, confirms the selected result.
                FailIf(!_dialogue.IsOpen || rom[0xcba5] != selected,
                    "Choice confirmation must publish its result while retaining active text.");
                Step(3, 0xff); // Close input cannot change the result or open menus.
                FailIf(_dialogue.IsOpen || !_dialogue.TryTakeChoiceResult(out int result) || result != selected ||
                    _dialogue.TryTakeChoiceResult(out _) || _inventoryMenu.IsActive || _mapMenu.IsActive,
                    $"TX_{entry.Id:x4}: result/closing input escaped its owner.");
                Step(1);
            }
        }
        GD.Print("Validated clean-US option space columns, all five cursor delays, horizontal wrap/spatial vertical navigation, direction chords, A/B priority, result/close handoff and repeated split/batched use.");
    }
}
