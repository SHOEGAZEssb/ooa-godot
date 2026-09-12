using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>interactionCode83 and scriptHelp.bombUpgradeFairyScript_body.</summary>
internal sealed class BombUpgradeFairyEvent : InteractiveCutsceneCommandHost, IRoomEntryEvent,
    IUpdatesDuringDialogueRoomEvent
{
    private readonly CutsceneCommandRunner _runner;
    private readonly List<BombFairyActor> _effects = [];
    private NpcCharacter? _fairy;
    private BombFairyActor? _appearancePuff;
    private LightningEffect? _lightning;
    private int _fadeDirection, _fadeCounter, _fadeDelay, _fadeOffset;
    private bool _collapsed;
    internal BombUpgradeFairyEvent(RoomEventContext context) { Context = context; _runner = new(this); }
    public override RoomEventContext Context { get; }
    internal BombUpgradeFairyDatabase Database { get; } = new();
    public bool HasState => State != 0 || _effects.Count != 0 || _lightning is { Finished: false };
    public bool BlocksGameplay => InputControlHeld;
    public bool MenusDisabled => BlocksGameplay;
    public bool AllScreenTransitionsDisabled => BlocksGameplay;
    internal int State { get; private set; }
    internal int ScriptIndex => _runner.Instruction;
    internal int ScriptCounter => _runner.Counter;
    internal bool Fading => _fadeDirection != 0;
    private byte Signal { get => Read(0xcfc0); set => Write(0xcfc0, value); }
    private byte Read(int address) => Context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address, int value) => Context.Entities.RuntimeState.SetWramByte(address, unchecked((byte)value));
    public bool Matches(int group, OracleRoomData room) => group == Database.Group && room.Id == Database.Room;
    public void Start(OracleRoomData room)
    {
        Cancel();
        var slot = Context.Entities.EntityAdapters<BombUpgradeFairyRoomEntity>().Single();
        slot.Bind(this);
        _fairy = slot.Npc;
        if (Context.Rooms.SaveData.HasGlobalFlag(Database.GlobalFlag) ||
            !Context.Rooms.SaveData.HasRoomFlag(Database.Group, Database.Room, 1))
        {
            _fairy.SetActive(false); _fairy = null; return;
        }
        _fairy.SetCollisionRadii(1, 0x12);
        Write(0xcba8, Database.Capacity(Context.Inventory.MaxBombs)); Write(0xcba9, 0);
        Signal = 0; Write(0xcfd0, 0);
        State = 1;
    }
    public void UpdateFrame()
    {
        UpdateFade();
        // PART_LIGHTNING's enabled bit 7 keeps it running under $80 and text.
        // Parts precede interactions, so a helper-created part starts next update.
        _lightning?.UpdateFrame();
        bool ordinary = !Context.DialogueOpen && !Context.Player.IsUsingHarp;
        switch (State)
        {
            case 1:
                if (Signal == 1 && !Context.DialogueOpen && Database.Contains(Context.Player.Position) &&
                    Context.Player.BombFairyVulnerable)
                {
                    _appearancePuff = Spawn("puff", _fairy!.Position, alwaysUpdate: false);
                    // clearAllParentItems/dropLinkHeldItem: release the held child,
                    // preserving other existing bombs, which freeze under $80.
                    Context.Player.DropHeldItemsForScript();
                    EventResources.LockInput(interruptBracelet: false);
                    Context.Player.Face(Vector2I.Up); Signal = 0; State = 2;
                }
                break;
            case 2:
                if ((_appearancePuff!.Parameter & 0x80) == 0) break;
                State = 3;
                _fairy!.Position = new Vector2(_fairy.Position.X, 0x28);
                Spawn("sparkle", _fairy.Position, alwaysUpdate: true);
                _fairy.SetFixedDrawPriority(NpcCharacter.InFrontOfLinkZIndex); // objectSetVisible81
                _fairy.SetScriptVisible(true);
                _runner.Start(Database.Commands);
                Spawn("silver", new Vector2(0x5a, 0x3c));
                BombFairyActor gold = Spawn("silver", new Vector2(0x76, 0x3c), variant: 1);
                gold.Actor.SetBasePalette(6); gold.Actor.SetScriptPaletteOverride(Database.GoldPalette);
                break;
            case 3:
                _fairy!.AdvanceAnimationUpdates(1);
                if (Context.DialogueOpen || Fading) break;
                _runner.AdvanceFrame();
                if (!_runner.Active)
                {
                    ReleaseInputControl(); Signal = 1;
                    if (_collapsed) Context.Player.SetScriptedLinkAnimationMode(null);
                    _collapsed = false;
                    Spawn("puff", _fairy.Position, alwaysUpdate: true);
                    _fairy.SetActive(false); _fairy = null; State = 0;
                }
                break;
        }
        // updateInteractions chooses text eligibility once, before its slot pass.
        // New state-0 objects still initialize if a textbox was already active.
        for (int i = 0; i < _effects.Count; i++)
        {
            BombFairyActor effect = _effects[i];
            if (ordinary || effect.AlwaysUpdate || effect.State == 0) UpdateEffect(effect);
            if (effect.Finished) _effects.RemoveAt(i--);
        }
    }
    public void UpdateDuringDialogueFrame() => UpdateFrame();
    private BombFairyActor Spawn(string kind, Vector2 position, bool alwaysUpdate = false, int delay = 0, int variant = 0)
    {
        NpcRecord record = Database.Visual(kind, position) with { Var03 = variant };
        NpcCharacter actor = Context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(record,
            $"BombFairy_{kind}"));
        actor.SetAnimationRate(0); actor.SetScriptAnimation(record.DownAnimation);
        actor.SetGraphicsSourceOffset(Database.SourceOffset(kind));
        actor.SetSourceGrayscaleInverted(Database.SourceGrayscaleInverted(kind));
        actor.SetScriptVisible(false);
        actor.SetFixedDrawPriority(kind is "silver" or "bomb" ? NpcCharacter.BehindLinkZIndex : NpcCharacter.FixedHighPriorityZIndex);
        var effect = new BombFairyActor(actor, kind, alwaysUpdate, delay);
        _effects.Add(effect);
        return effect;
    }
    private void UpdateEffect(BombFairyActor effect)
    {
        NpcCharacter actor = effect.Actor;
        if (effect.State == 0)
        {
            effect.State = 1;
            switch (effect.Kind)
            {
                case "silver": Spawn("puff", actor.Position); return;
                case "bomb": return;
                case "puff": Context.Sound.PlaySound(Database.PoofSound); break;
                case "debris": Context.Sound.PlaySound(OracleSoundEngine.SndKillEnemy); break;
            }
            actor.SetScriptVisible(true); return;
        }
        switch (effect.Kind)
        {
            case "silver":
                if (effect.State == 1)
                {
                    // The source initializes relatedObj2 but reads relatedObj1.
                    // Unset pointer+$21 reads the clean ROM's zero-filled vector gap.
                    actor.SetScriptVisible(true); effect.State = 2; return;
                }
                if (Read(0xcfd0) == 0) return;
                Spawn("puff", actor.Position, alwaysUpdate: true); effect.Finish(); return;
            case "bomb":
                if (effect.State == 1)
                {
                    if (--effect.Counter != 0) return;
                    effect.State = 2;
                    Spawn("puff", actor.Position, alwaysUpdate: true); actor.SetScriptVisible(true); return;
                }
                if (Read(0xcfd0) == 0xff) { effect.Finish(); return; }
                effect.Counter = (effect.Counter - 1) & 0xff;
                if ((effect.Counter & 3) == 0) actor.SetBasePalette(actor.Record.Palette ^ 1);
                return;
            case "sparkle":
                if ((Signal & 1) != 0) { effect.Finish(); return; }
                actor.AdvanceAnimationUpdates(1);
                actor.SetScriptVisible((Context.Entities.FrameCounter & 1) == 0); return;
            case "puff": case "debris":
                if ((effect.Parameter & 0x80) != 0) { effect.Finish(); return; }
                actor.AdvanceAnimationUpdates(1); effect.Parameter = actor.CurrentAnimationParameter; return;
            default: throw new InvalidOperationException($"$83 unknown effect {effect.Kind}.");
        }
    }
    public override void ShowText(int textId, string message, int? textboxPosition)
    {
        int bcd = Read(0xcba8);
        message = message.Replace("\\num1", ((bcd >> 4) * 10 + (bcd & 15)).ToString(), StringComparison.Ordinal);
        if (message.Contains("\\opt(", StringComparison.Ordinal)) Context.ShowChoiceDialogue(message, textboxPosition: textboxPosition);
        else Context.ShowDialogue(message, textboxPosition);
    }
    private int TakeChoice() => EventResources.RequireDialogueChoice("$83 bombUpgradeFairyScript_body read an absent text option.");
    public override int ReadMemory(string binding) => binding == "wSelectedTextOption" ? TakeChoice() : throw UnsupportedCommand($"read {binding}");
    public override bool TextOptionEquals(int value) => TakeChoice() == value;
    public override void WriteMemory(string binding, int value)
    {
        if (binding != "wTmpcfc0.genericCutscene.cfd0") throw UnsupportedCommand($"write {binding}");
        Write(0xcfd0, value);
    }
    public override void ScriptEnded() { } // Native state 3 consumes the script-end carry.
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "bombUpgradeFairy_spawnBombsAroundLink":
                // The helper decrements B before each allocation: var03=3,2,1,0.
                for (int i = Database.Bombs.Length - 1; i >= 0; i--)
                    Spawn("bomb", Context.Player.Position + Database.Bombs[i].Offset, delay: Database.Bombs[i].Delay, variant: i);
                break;
            case "bombUpgradeFairy_lightningStrikesLink":
                EffectRecord visual = new SharedEffectDatabase().Effect("Lightning");
                Vector2 position = Context.Player.Position;
                NpcCharacter actor = Context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
                    visual.ToNpcRecord(Database.Group, Database.Room, (int)position.Y, (int)position.X), "BombFairy_Lightning"));
                _lightning = new LightningEffect(actor, () => Context.Entities.NextRandomValue(), Context.Sound.PlaySound,
                    Context.Entities.BeginScreenShake, p => Spawn("debris", p, alwaysUpdate: true));
                break;
            case "bombUpgradeFairy_decreaseLinkHealth":
                if (Context.Inventory.ApplyFairyHealthPenalty()) Collapse();
                break;
            case "bombUpgradeFairy_loseAllBombs": Context.Inventory.ConfiscateBombs(); Collapse(); break;
            case "bombUpgradeFairy_giveBombUpgrade": Context.Inventory.ApplyFairyBombCapacityUpgrade(Read(0xcba8)); break;
            case "bombUpgradeFairy_setGlobalFlag": Context.Rooms.SaveData.SetGlobalFlag(Database.GlobalFlag); break;
            case "fadeoutToWhite": BeginFade(1, 1); break;
            case "bombUpgradeFairy_fadeinFromWhite": Write(0xcfd0, 0xff); BeginFade(-1, 4); break;
            default: throw UnsupportedCommand($"run {handler}");
        }
    }
    private void Collapse() { _collapsed = true; Context.Player.SetScriptedLinkAnimationMode(0x02); }
    private void BeginFade(int direction, int delay)
    {
        EventResources.CaptureFullScreenFade();
        _fadeDirection = direction; _fadeDelay = delay; _fadeCounter = 1; _fadeOffset = direction > 0 ? 0 : 32;
    }
    private void UpdateFade()
    {
        if (!Fading || --_fadeCounter != 0) return;
        _fadeCounter = _fadeDelay; _fadeOffset += _fadeDirection;
        if (_fadeOffset >= 32 || _fadeOffset < 0) { _fadeDirection = 0; return; }
        Context.Fade.Color = new Color(1, 1, 1, Math.Min(_fadeOffset, 31) / 31f);
    }
    public void Cancel()
    {
        ReleaseInputControl(); EventResources.ReleaseFullScreenFade();
        if (_collapsed) Context.Player.SetScriptedLinkAnimationMode(null);
        _collapsed = false;
        foreach (BombFairyActor effect in _effects) effect.Finish();
        _effects.Clear(); _lightning?.Cancel(); _lightning = null;
        if (_fairy is not null && GodotObject.IsInstanceValid(_fairy)) _fairy.SetActive(false);
        _fairy = null; _appearancePuff = null; _runner.Clear(); State = 0; _fadeDirection = 0;
    }
}
