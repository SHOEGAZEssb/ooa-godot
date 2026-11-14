namespace Oracles;

internal sealed partial class SeasonsRoomTransitionController
{
    private bool _subrosiaWarp;
    internal bool SubrosiaWarpActive => _subrosiaWarp;
    // include/wram.s: wCutsceneIndex is the main thread's state byte.
    private const int CutsceneIndexAddress = 0xc2ef;
    private protected override bool ExtendedSequenceActive => _subrosiaWarp;
    public override void ApplyWarp(Player player, Warp warp)
    {
        if (_warps.HandlerFor(warp.SourceTransition) != WarpTransitionHandler.Subrosia || warp.DirectFadeOut || warp.NativeFadeDelay != 1)
        { base.ApplyWarp(player, warp); return; }
        RequireActive();
        if (IsTransitioning) return;
        if (!CanBeginWarp(player, warp, sourceAlreadyHandled: true)) return;
        _pendingWarp = warp; _subrosiaWarp = _warpActive = true; _suppressDestinationMusic = true;
        _dialogue.Close(); ClearRoomLoadColumnReveal(); SetFade(0);
        _entities.RuntimeState.SetField("wWarpTransition", 8);
        player.RequestSubrosiaWarp();
    }
    public override void UpdateWarp(double delta)
    {
        if (!_subrosiaWarp) { base.UpdateWarp(delta); return; }
        if (_player.SubrosiaWarpActive) return;
        _sound.SetNativeActiveMusic(_sound.NativeRoomMusic);
        _sound.PlayNativeSound(_sound.NativeRoomMusic);
        FinishWarp();
    }
    // bank1.cutscene15 updates its state before the special-object pass.
    internal void AdvanceSubrosiaBeforeObjects()
    {
        if (!_subrosiaWarp) return;
        var memory = _entities.RuntimeState;
        AdvanceNativeWhiteFade();
        if (memory.ReadField("wCutsceneTrigger") == 0x15)
        {
            memory.SetField("wCutsceneTrigger", 0); memory.SetField("wCutsceneState", 0);
            memory.SetWramByte(CutsceneIndexAddress, 0x15);
        }
        if (memory.ReadWramByte(CutsceneIndexAddress) != 0x15) return;
        int state = memory.ReadField("wCutsceneState"), substate = memory.ReadField("wGenericCutscene.cbb3");
        if (state == 0)
        {
            memory.SetField("wCutsceneState", 1);
            for (int offset = 0; offset < 4; offset++) memory.SetField("wGenericCutscene", 0, offset);
        }
        else if (state == 1 && substate == 0)
        {
            memory.SetField("wGenericCutscene.cbbb", 4); memory.SetField("wGenericCutscene.cbbc", 0);
            memory.SetField("wGenericCutscene.cbb3", 1); memory.SetField("wGfxRegs2.LYC", 0x10);
        }
        else if (state == 1 && substate == 2)
        {
            LoadWarpDestination();
            _sound.SelectRoomMusic(_rooms.ActiveGroup, _rooms.CurrentRoom.Id, _rooms.SaveData);
            memory.SetField("wCutsceneState", 2); memory.SetField("wGenericCutscene.cbb3", 0);
            memory.SetField("wLinkForceState", 0); memory.SetField("wWarpTransition", 8);
            _player.SetSubrosiaWarpObjectMask(0x81);
            _entities.ClearRecentEnemyDefeats();
            memory.SetField("wGfxRegs2.LYC", 0x10); memory.SetField("wGfxRegs2.SCY", 0xf0);
            SetFade(1);
            BeginNativeWhiteFade(false, 2);
        }
        else if (state == 2 && substate == 1)
        {
            memory.SetWramByte(CutsceneIndexAddress, 0); memory.SetField("wGfxRegs2.LYC", 0xc7);
            _player.ReleaseSubrosiaWarpInput(); memory.SetField("wScrollMode", 1);
            _roomView.ClearHorizontalWave(); _sound.PlayNativeSound(SoundId.SndCtrlStopSfx);
        }
        else if ((state == 1 && substate == 1) || (state == 2 && substate == 0))
        {
            int amplitude = memory.ReadField("wGenericCutscene.cbbc");
            memory.SetField("wGenericCutscene.cbbd", (byte)amplitude);
            int phase = memory.FrameCounter & 0x7f, step = memory.ReadField("wGenericCutscene.cbbb");
            for (int line = 0; line < 256; line++)
            {
                int index = phase + line * step;
                int value = ScreenWaveData.Shared.Offset(amplitude, index);
                if ((index & 1) == 0) value = -value;
                memory.SetWramByte(memory.FieldAddress("wBigBuffer") + line, unchecked((byte)value));
            }
            _roomView.SetHorizontalWave(amplitude, phase, step, invertEvenSamples: true);
        }
        else throw new NotSupportedException($"bank1.cutscene15: unsupported state ${state:x2}/${substate:x2}.");
    }
    private protected override void PlaceStandingWarpDestination(Vector2 spawn)
    {
        if (_subrosiaWarp) _player.SetScriptedPosition(spawn);
        else base.PlaceStandingWarpDestination(spawn);
    }
    private protected override void InitializeDestinationEntities(OracleRoomData room, Warp warp)
    {
        base.InitializeDestinationEntities(room, warp);
        if (_subrosiaWarp) _entities.PrepareNativeInteractionsForRoomLoad(_player);
    }
    private protected override void BeginDestinationPresentation(Vector2 spawn)
    { if (!_subrosiaWarp) base.BeginDestinationPresentation(spawn); }
    private protected override void OnWarpFinished()
    {
        if (!_subrosiaWarp) return;
        _subrosiaWarp = false; _suppressDestinationMusic = false;
        _roomView.ClearHorizontalWave(); CancelNativeWhiteFade(clearPresentation: true);
    }
    public override void Dispose()
    {
        if (_subrosiaWarp)
        {
            var memory = _entities.RuntimeState;
            if (memory.ReadField("wCutsceneTrigger") == 0x15) memory.SetField("wCutsceneTrigger", 0);
            if (memory.ReadWramByte(CutsceneIndexAddress) == 0x15) memory.SetWramByte(CutsceneIndexAddress, 0);
            _subrosiaWarp = false;
        }
        base.Dispose();
    }
}
