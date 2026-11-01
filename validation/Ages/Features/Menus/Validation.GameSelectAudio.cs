using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateGameSelectAudio()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"oracle-selector-audio-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var traces = new Dictionary<OracleGame, int[]>();
        try
        {
            foreach (bool batched in new[] { false, true })
            foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
            {
                var root = new FrontendValidationRoot();
                AddChild(root);
                try
                {
                    GameSelectScreen screen = root.InitializeGameSelect();
                    var audit = root._sound.AttachPlayRequestAudit();
                    var trace = new List<int>();
                    void Expect(Action action, params int[] sounds)
                    {
                        audit.Clear();
                        action();
                        FailIf(!audit.Requests.SequenceEqual(sounds),
                            $"{game}, batch={batched}: selector sounds [{string.Join(",", audit.Requests.Select(id => $"${id:x2}"))}] differed from [{string.Join(",", sounds.Select(id => $"${id:x2}"))}].");
                        trace.AddRange(audit.Requests);
                    }

                    Expect(() => root.Step(1, batched));
                    FailIf(root._sound.Channel(2).Active, "Opening the selector must not play a cursor sound.");
                    Expect(() => root.Step(1, batched, "move_up", "move_down"));
                    Expect(() => root.Step(1, batched, "move_down"), 0x84);
                    FailIf(root._sound.Data.Game != OracleGame.Seasons, "Seasons cursor audio used Ages sound data.");
                    Expect(() => root.Step(4, batched, "move_down"));
                    Expect(() => { screen.RequestPointerSelection(OracleGame.Ages, false); root.Step(4, batched); }, 0x84);
                    // audio/common/sfx/menuMove.s: $07a0 for three updates at
                    // volume $08, then five updates at volume $01 on logical $02.
                    FailIf(root._sound.Data.Game != OracleGame.Ages || !root._sound.Channel(2).Active ||
                        root._sound.Apu.Frequency(0) != 0x7a0 || root._sound.Apu.Voice(0).Volume != 1,
                        "The selector did not execute the original $84 cursor program through the audio owner.");
                    Expect(() => { screen.RequestPointerSelection(game, false); root.Step(1, batched); },
                        game == OracleGame.Seasons ? new[] { 0x84 } : Array.Empty<int>());
                    Expect(() => root.Step(1, batched, "move_right"), 0x84);
                    Expect(() =>
                    {
                        screen.Preview(game).GetNode<TextureButton>("Options").EmitSignal(Control.SignalName.MouseEntered);
                        root.Step(4, batched);
                    });
                    Expect(() => root.Step(4, batched, "attack", "inventory"), 0x56);
                    var options = screen.Options;
                    var files = options.GetNode<FileDialog>("Files");
                    files.UseNativeDialog = false;
                    var import = options.GetNode<TextureButton>("Board/Import");
                    var replace = options.GetNode<Control>("Board/Replace");
                    var notice = options.GetNode<Control>("Board/Notice");
                    Expect(() => root.Step(1, batched, "move_right"), 0x84);
                    Expect(() =>
                    {
                        options.GetNode<TextureButton>("Board/Slot").EmitSignal(BaseButton.SignalName.Pressed);
                        root.Step(4, batched);
                    }, 0x84);
                    FailIf(options.SelectedSlot != 2, "Slot audio changed the selected save slot incorrectly.");
                    Expect(() => { import.EmitSignal(Control.SignalName.MouseEntered); root.Step(4, batched); }, 0x84);
                    Expect(() => { import.EmitSignal(Control.SignalName.MouseEntered); root.Step(4, batched); });
                    Expect(() => root.Step(4, batched, "attack", "inventory"), 0x56);
                    FailIf(!files.Visible, "Import confirmation must still open its file picker.");
                    Expect(() => root.Step(4, batched, "move_up", "move_down", "attack"));
                    Expect(() =>
                    {
                        files.Hide(); files.EmitSignal(FileDialog.SignalName.Canceled); root.Step(1, batched);
                    });

                    var incoming = OracleSaveData.CreateStandardGame(game);
                    incoming.SetLinkName("LINK");
                    string path = Path.Combine(directory, $"{game}.sav");
                    File.WriteAllBytes(path, incoming.Serialize());
                    void PickImport()
                    {
                        Expect(() => { import.EmitSignal(BaseButton.SignalName.Pressed); root.Step(1, batched); }, 0x56);
                        Expect(() =>
                        {
                            files.Hide(); files.EmitSignal(FileDialog.SignalName.FileSelected, path); root.Step(1, batched);
                        });
                        FailIf(!replace.Visible, "Reading a valid import must retain the replacement confirmation.");
                    }
                    PickImport();
                    Expect(() => root.Step(4, batched, "move_right"), 0x84);
                    Expect(() =>
                    {
                        replace.GetNode<TextureButton>("Cancel").EmitSignal(Control.SignalName.MouseEntered);
                        root.Step(4, batched);
                    });
                    Expect(() =>
                    {
                        replace.GetNode<TextureButton>("Import").EmitSignal(Control.SignalName.MouseEntered);
                        root.Step(4, batched);
                    }, 0x84);
                    Expect(() =>
                    {
                        replace.GetNode<TextureButton>("Cancel").EmitSignal(BaseButton.SignalName.Pressed);
                        root.Step(1, batched);
                    }, 0x56);
                    FailIf(root.FileSlotExists(2, game), "Canceling an audible confirmation committed a save.");
                    PickImport();
                    Expect(() => root.Step(4, batched, "attack", "inventory"), 0x56);
                    FailIf(!notice.Visible || !root.FileSlotSnapshot(2, game).SequenceEqual(incoming.Serialize()),
                        "Confirming import must commit once and show its result.");
                    Expect(() => root.Step(4, batched, "attack", "inventory"), 0x56);
                    FailIf(notice.Visible || !options.Visible, "Dismissing a result also closed Options.");
                    Expect(() =>
                    {
                        options.GetNode<TextureButton>("Board/Back").EmitSignal(BaseButton.SignalName.Pressed);
                        root.Step(1, batched);
                    }, 0x56);
                    Expect(() => root.Step(1, batched, "item", "attack"), 0x84);
                    Expect(() =>
                    {
                        screen.Preview(game).GetNode<TextureButton>("Options").EmitSignal(BaseButton.SignalName.Pressed);
                        root.Step(1, batched);
                    }, 0x84, 0x56);
                    Expect(() => root.Step(4, batched, "item"));
                    Expect(() => root.Step(1, batched, "move_left"), 0x84);
                    Expect(() => root.Step(1, batched, "attack", "inventory"), 0x56);
                    FailIf(root._gameSelect is not null || root._sound.Data.Game != game || !root._sound.Channel(2).Active,
                        "Launching either game discarded its $56 confirmation or bound the wrong audio pack.");
                    long clocks = root._sound.Apu.Clocks;
                    long updates = root.ApplicationUpdates.UpdateCount;
                    Expect(() =>
                    {
                        if (batched) root.AdvanceHost(4.0 / 60);
                        else for (int update = 0; update < 4; update++) root.AdvanceHost(1.0 / 60);
                    });
                    // selectItem.s: b5 lasts four updates, then e6 has volume $06.
                    FailIf(root.ApplicationUpdates.UpdateCount != updates || root._random.Calls != 0 ||
                        Math.Abs(root._sound.Apu.Clocks - clocks - 4.0 * OracleApu.ClockRate / 60) > 1 ||
                        !root._sound.Channel(2).Active || root._sound.Apu.Voice(0).Volume != 6,
                        "Boot loading must advance the original confirm sound at 60 Hz without game dispatch or RNG.");
                    if (traces.TryGetValue(game, out int[]? previous))
                        FailIf(!previous.SequenceEqual(trace), "Selector and Options audio depends on host batching.");
                    else traces.Add(game, trace.ToArray());
                }
                finally { root.Free(); }
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
        GD.Print("Validated native $84/$56 selector and Options cues, pointer/keyboard input, modal silence, selected-game audio and boot continuation.");
    }
}
