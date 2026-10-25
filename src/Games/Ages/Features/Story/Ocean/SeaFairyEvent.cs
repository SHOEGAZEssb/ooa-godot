using Godot;
using System;
using System.Linq;

namespace Oracles;

// fakeOctorok.s:greatFairyOctorokCode. Its script runs at the actual object slot.
internal sealed class SeaFairyEvent : InteractiveCutsceneCommandHost, IRoomEntryEvent
{
    private readonly CutsceneCommandRunner _runner;
    private readonly ScriptActorBinding<NpcCharacter> _binding = new("Octorok");
    private NpcCharacter? _actor;
    private bool _clean, _ended, _transformed;
    private int _z;
    internal SeaFairyDatabase Database { get; } = new();
    internal SeaFairyEvent(RoomEventContext context) { Context = context; _runner = new(this); }
    public override RoomEventContext Context { get; }
    public bool HasState => _actor is { Active: true } || _transformed;
    public bool BlocksGameplay => InputControlHeld || _transformed;
    public bool MenusDisabled => BlocksGameplay;
    public bool AllScreenTransitionsDisabled => BlocksGameplay;
    internal Action BeginCleanSeas { get; set; } = null!;
    internal Action BeginWhiteFade { get; set; } = null!;
    internal int Height => (sbyte)(_z >> 8);
    internal bool Rising => _ended;
    internal int Counter => _runner.Counter;
    public bool Matches(int group, OracleRoomData room) => group == 3 && room.Id == 0xef;
    public void Start(OracleRoomData room)
    {
        var slot = Context.Entities.EntityAdapters<SeaFairyNpcRoomEntity>().Single();
        slot.Owner = this; _actor = slot.Npc;
        _clean = Context.Rooms.SaveData.HasGlobalFlag(AgesGlobalFlag.WaterPollutionFixed);
        _z = 0; _ended = false; _transformed = false;
        _actor.TimeWarpSolidMarked = false; // $32 never calls objectMarkSolidPosition.
        _actor.SetDialogue(0, "", canFace: true);
        _actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
        _actor.SetScriptVisible(!_clean);
        _binding.Bind(_actor); _runner.Start(Database.Commands);
    }
    public void UpdateFrame() { }
    internal void UpdateOctorokSlot()
    {
        if (_actor is not { Active: true } actor) return;
        if (_clean)
        {
            // getFreeEnemySlot failure still deletes the disguise in the source.
            Context.Entities.TrySpawnEnemy(EnemyId.GreatFairy, 0, actor.Position,
                "fakeOctorok.s:@init2 $32:$02", out _);
            actor.SetActive(false); return;
        }
        actor.FaceLinkAndAnimateOneUpdate(Context.Player);
        if (!_ended) _runner.AdvanceFrame();
        if (!_ended) return;
        _z = (_z - 0x80) & 0xffff; // objectUpdateSpeedZ with gravity A=$00.
        actor.SetScriptDrawOffset(new Vector2(0, Height));
        if ((_z >> 8) != 0xf0) return;
        if (Context.Entities.InteractionSlotAvailable)
            Context.Entities.Spawn(new CleansedFairySpawn(Database, actor.Position, BeginWhiteFade, BeginCleanSeas));
        Context.Inventory.LoseTreasure(TreasureId.FairyPowder);
        _transformed = true;
        actor.SetActive(false);
        ReleaseInputControl(); // $11 remains live until CUTSCENE_CLEAN_SEAS takes over.
    }
    internal bool TryInteractNpc(NpcCharacter actor) => !_clean && !_ended && !InputControlHeld && _binding.QueueButton(actor);
    public override bool HasActorBinding(CutsceneActorId actor) => _binding.Matches(actor);
    public override bool TryConsumeActorButton(CutsceneActorId actor) => _binding.ConsumeButton();
    public override void SetActorButtonSensitive(string actor) => _binding.EnableButton();
    public override void InitializeActorCollisionRadii(string actor) => _actor!.InitializeCollisionRadii();
    public override bool MemoryEquals(string binding, int value) => value == 1 && binding switch
    {
        "Talked" => Context.Rooms.SaveData.HasGlobalFlag(AgesGlobalFlag.TalkedToOctorokFairy),
        "Powder" => Context.Inventory.HasTreasure(TreasureId.FairyPowder),
        _ => throw UnsupportedCommand(binding)
    };
    public override void SetDisabledObjects(int value)
    {
        if (value != 0x11) throw UnsupportedCommand($"wDisabledObjects=${value:x2}");
        Context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0x11);
        EventResources.LockInput(interruptBracelet: false);
    }
    public override void SetMenuEnabled(bool enabled) => Context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wMenuDisabled, (byte)(enabled ? 0 : 1));
    public override void ShowText(int id, string message, int? position) => Context.ShowDialogue(message, position, sourceTextId: id);
    public override void RunNativeHandler(string handler)
    {
        if (handler != "MagicPowder") throw UnsupportedCommand(handler);
        Context.Sound.PlaySound(SoundId.SndMagicPowder);
        for (int x = -8; x < 24 && Context.Entities.PartSlotAvailable; x += 8)
            Context.Entities.Spawn(new SeaPowderSpawn(Database, _actor!.Position + new Vector2(x, 0), true));
    }
    public override void ScriptEnded() => _ended = true;
    public void Cancel()
    {
        if (InputControlHeld || _transformed)
        {
            Context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
            Context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wMenuDisabled, 0);
        }
        ReleaseInputControl(); _binding.Clear(); _runner.Clear(); _actor = null; _ended = false; _transformed = false;
    }
}
