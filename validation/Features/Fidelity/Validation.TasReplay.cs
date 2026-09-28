using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private bool _tasReplay;
    private int _tasUpdate;
    private int _tasBatchSize = 1;
    private readonly OracleSaveData?[] _tasSlots = new OracleSaveData?[3];
    private const BindingFlags TasFields = BindingFlags.Instance | BindingFlags.NonPublic;

    private static T TasRead<T>(object owner, string name) =>
        (T)(owner.GetType().GetField(name, TasFields)
            ?? throw new InvalidOperationException($"Missing TAS owner field {owner.GetType().Name}.{name}"))
        .GetValue(owner)!;

    private T? TasRootField<T>(string name) => (T?)typeof(GameRoot).GetField(name, TasFields)!.GetValue(this);
    private void TasSetRoot(string name, object value) => typeof(GameRoot).GetField(name, TasFields)!.SetValue(this, value);
    private void TasRootCall(string name, params object[] args) => typeof(GameRoot).GetMethod(name, TasFields)!.Invoke(this, args);

    private void BeginTasReplay()
    {
        try
        {
            _tasReplay = true;
            string? batch = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--tas-batch-size=", StringComparison.Ordinal));
            if (batch is not null && (!int.TryParse(batch[17..], out _tasBatchSize) || _tasBatchSize is < 1 or > 1024))
                throw new ArgumentException("TAS batch size must be 1..1024.");
            TasSetRoot("_launchOptions", new LaunchOptions());
            TasSetRoot("_persistSaveData", true);
            _sound = GetNode<OracleSoundEngine>("SoundEngine");
            _sound.ApplicationUpdateOwned = true;
            _random = new OracleRandom();
            // Asset loading is host work. The first source _mainLoop boundary
            // precedes runIntro, so no original update is consumed here.
            TasRootCall("StartFrontend", false);
            GD.Print("TAS_READY");
        }
        catch (Exception exception) { FailTasReplay(exception); }
    }

    protected override OracleSaveData? LoadFileSlot(int slot)
    {
        if (!_tasReplay) return base.LoadFileSlot(slot);
        if (_tasSlots[slot] is not { } saved) return null;
        if (!OracleSaveData.TryDeserialize(saved.Serialize(), out var copy))
            throw new InvalidOperationException("TAS isolated load failed validation.");
        return copy;
    }

    protected override SaveResult StoreFileSlot(int slot, OracleSaveData save)
    {
        if (!_tasReplay) return base.StoreFileSlot(slot, save);
        if (!OracleSaveData.TryDeserialize(save.Serialize(), out var copy))
            throw new InvalidOperationException("TAS isolated save failed validation.");
        _tasSlots[slot] = copy;
        return SaveResult.Succeeded;
    }

    protected override void EraseFileSlot(int slot)
    {
        if (_tasReplay) _tasSlots[slot] = null;
        else base.EraseFileSlot(slot);
    }

    private void AdvanceTasReplay()
    {
        for (int i = 0; i < _tasBatchSize && _tasReplay; i++) AdvanceTasReplayStep();
    }

    private void AdvanceTasReplayStep()
    {
        try
        {
            string? line = Console.ReadLine();
            if (line is null || line == "stop") { _tasReplay = false; SetProcess(false); GetTree().Quit(0); return; }
            using var command = JsonDocument.Parse(line);
            int update = command.RootElement.GetProperty("update").GetInt32();
            int buttons = command.RootElement.GetProperty("input").GetInt32();
            int pressed = command.RootElement.GetProperty("pressed").GetInt32();
            if (update != _tasUpdate || buttons is < 0 or > 255 || pressed is < 0 or > 255)
                throw new InvalidOperationException("Invalid TAS update/input sequence.");
            if (update > 0)
            {
                var map = new (int Bit, string Action)[] { (1,"attack"), (2,"item"), (4,"map"),
                    (8,"inventory"), (16,"move_right"), (32,"move_left"), (64,"move_up"), (128,"move_down") };
                Vector2 movement = new(((buttons & 16) != 0 ? 1 : 0) - ((buttons & 32) != 0 ? 1 : 0),
                    ((buttons & 128) != 0 ? 1 : 0) - ((buttons & 64) != 0 ? 1 : 0));
                AdvanceGameplayPreparation();
                StepGameplayUpdates(1, movement,
                    map.Where(x => (buttons & x.Bit) != 0).Select(x => x.Action).ToArray(),
                    map.Where(x => (pressed & x.Bit) != 0).Select(x => x.Action).ToArray());
            }
            GD.Print("TAS_SNAPSHOT " + JsonSerializer.Serialize(new { update, input = buttons, pressed,
                state = CaptureTasSharedState(), diagnostics = new {
                    frontend = TasRootField<FrontendIntroController>("_frontendIntro")?.Stage.ToString(),
                    menu = TasRootField<MainMenuController>("_mainMenu")?.CurrentPage.ToString(),
                    link = _scene is null || !GodotObject.IsInstanceValid(_scene) ? null : new { _player.IsDying, _player.CutsceneControlled,
                        _player.IsAttacking, _player.IsFallingInHole }
                } }));
            _tasUpdate++;
        }
        catch (Exception exception) { FailTasReplay(exception); }
    }

    private Dictionary<string, int> CaptureTasSharedState()
    {
        var rng = _random.CaptureState();
        var state = new Dictionary<string, int> { ["rng.hRng1"] = rng.Rng1, ["rng.hRng2"] = rng.Rng2 };
        var intro = TasRootField<FrontendIntroController>("_frontendIntro");
        state["frontend.active"] = intro is not null ? 1 : 0;
        if (intro is not null)
        {
            state["frontend.stage"] = intro.Stage switch {
                FrontendIntroStage.Boot => 0, FrontendIntroStage.Capcom => 1,
                FrontendIntroStage.Horse or FrontendIntroStage.Temple or FrontendIntroStage.PreTitle => 2,
                FrontendIntroStage.Title => 3, FrontendIntroStage.Restart => 4,
                _ => throw new NotSupportedException("Unmapped frontend stage.") };
            // Cinematic State uses per-scene dispatch; it is not wIntroVar.
            if (state["frontend.stage"] != 2) state["frontend.state"] = intro.State;
        }
        // Compare live semantic driver fields, not scratch bytes or CPU/APU
        // clocks. Inactive channel counters are not initialized by stopSound.
        foreach (int address in new[] { 0xc014, 0xc015, 0xc01b, 0xc022, 0xc023, 0xc024 })
            state[$"audio.${address:x4}"] = _sound.Driver.ReadState(address);
        for (int channel = 0; channel < 8; channel++)
        {
            int enabled = _sound.Driver.ReadState(0xc06d + channel);
            state[$"audio.channel{channel}.enabled"] = enabled;
            if (enabled != 0) state[$"audio.channel{channel}.wait"] = _sound.Driver.ReadState(0xc075 + channel);
        }
        bool gameplay = intro is null && TasRootField<MainMenuController>("_mainMenu") is null &&
            _scene is not null && GodotObject.IsInstanceValid(_scene) && _transitions is not null && !GameplayPrepared &&
            TasRootField<NewGameIntroController>("_newGameIntro") is null;
        state["room.active"] = gameplay ? 1 : 0;
        if (!gameplay) return state;
        state["room.group"] = _activeGroup;
        state["room.id"] = _currentRoom.Id;
        Vector2 point = TasRead<Vector2>(_player, "_precisePosition");
        state["link.x8_8"] = (int)(point.X * 256) & 0xffff;
        state["link.y8_8"] = (int)(point.Y * 256) & 0xffff;
        state["link.direction"] = _player.FacingVector == Vector2I.Up ? 0 :
            _player.FacingVector == Vector2I.Right ? 1 : _player.FacingVector == Vector2I.Down ? 2 : 3;
        state["link.controlled"] = _player.CutsceneControlled ? 1 : 0;
        state["link.health"] = _player.HealthQuarters;
        var slots = TasRead<HashSet<int>>(_entities, "_reservedEnemySlots");
        for (int slot = 0; slot < 16; slot++) state[$"enemy.${0xd080 + slot * 256:x4}.occupied"] = slots.Contains(slot) ? 1 : 0;
        // Original persistent fields, including inventory and room flags.
        // Signature/checksum are committed-file metadata, not live gameplay.
        for (int address = 0xc5ba; address <= 0xcaff; address++)
            state[$"save.${address:x4}"] = _saveData.ReadWramByte(address);
        return state;
    }

    private void FailTasReplay(Exception exception)
    {
        _tasReplay = false;
        GD.Print("TAS_ERROR " + JsonSerializer.Serialize(new { update = _tasUpdate, message = exception.ToString() }));
        SetProcess(false);
        GetTree().Quit(2);
    }

    private void ValidateTasSaveBoundary()
    {
        bool persist = TasRootField<bool>("_persistSaveData");
        try
        {
            _tasReplay = true;
            TasSetRoot("_persistSaveData", true);
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone, true);
            TasRootCall("SaveActiveFile");
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false);
            var saved = LoadFileSlot(0)!;
            FailIf(!saved.HasGlobalFlag(GlobalFlag.IntroDone),
                "TAS explicit save did not survive a subsequent uncommitted live mutation.");
            saved.SetGlobalFlag(GlobalFlag.IntroDone, false);
            FailIf(!LoadFileSlot(0)!.HasGlobalFlag(GlobalFlag.IntroDone),
                "TAS loaded file aliases the committed generation.");
            StoreFileSlot(2, _saveData);
            EraseFileSlot(0);
            FailIf(LoadFileSlot(0) is not null || LoadFileSlot(2) is null,
                "TAS slot erase changed another slot or retained the erased slot.");
        }
        finally
        {
            _tasReplay = false;
            Array.Clear(_tasSlots);
            TasSetRoot("_persistSaveData", persist);
        }
    }
}
