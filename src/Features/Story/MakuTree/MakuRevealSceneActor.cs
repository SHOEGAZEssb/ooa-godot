using Godot;
using System.Collections.Generic;

namespace oracleofages;

// objectData777c: boy $3c:$0a, stone villagers, Zelda $ad:$05 and Impa $31:$06.
internal sealed class MakuRevealSceneActor : RoomCutsceneCommandHost, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    public override RoomEventContext Context { get; }
    private readonly MakuSeedDatabase _data;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private Vector2 _precise;
    private bool _initialized;
    internal int Id { get; }
    public Node2D Node => _actor;
    public bool Finished => false;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    internal MakuRevealSceneActor(RoomEventContext context, MakuSeedDatabase data, int id, int subid, Vector2 position)
    {
        Context = context; _data = data; Id = id; _precise = position;
        _actor = data.CreateActor(id, subid, position); _runner = new(this);
        _actor.SetScriptAnimation(_actor.Record.UpAnimation);
    }
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            _initialized = true;
            _actor.ShowInitializedPresentation();
            _actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
            switch (Id)
            {
                case 0x3c:
                    _actor.SetScriptAnimation(_data.Animation(Id, 3));
                    _runner.Start(_data.Commands("boy"));
                    // loadStoneNpcPalette changes slot $06 for the other two actors too.
                    var palette = OracleGraphicsData.LoadPalette("res://assets/oracle/cutscenes/nayru_stone_sprite_palette.bin", 1, 6);
                    var stone = new Color[1, 4];
                    for (int shade = 0; shade < 4; shade++) stone[0, shade] = palette[6, shade];
                    Context.Entities.LoadNativeObjectPaletteSlots(6, stone);
                    return;
                case 0x3a:
                case 0x43:
                    _actor.SetBasePalette(6);
                    _actor.SetScriptPaletteOverride(Context.Entities.NativeObjectPalettes?[6] ??
                        throw new System.InvalidOperationException("objectData777c: boy $3c:$0a must load PALH_a2 slot $06 before the stone actors initialize."));
                    if (Id == 0x3a) _actor.SetScriptAnimation(_data.Animation(Id, 13));
                    return;
                case 0xad: _runner.Start(_data.Commands("zelda")); break;
                case 0x31: _runner.Start(_data.Commands("impa")); return;
                default: throw UnsupportedCommand($"reveal INTERAC ${Id:x2}");
            }
        }
        if (Id is 0x3a or 0x43) return;
        _actor.AdvanceAnimationUpdates(Id == 0x3c ? 2 : 1);
        _runner.AdvanceFrame();
    }
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Actor";
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) => _actor.SetScriptAnimation(encodedAnimation);
    public override void SetActorMovementAnimation(string actor, int angle, string animation) => _actor.SetScriptAnimation(animation);
    public override void MoveActorAtSpeed(string actor, int speed, int angle) =>
        _actor.Position = NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState, ref _precise, speed, angle);
    public override bool GateOpen(string gate) => gate == "PaletteDone" ? !Context.Transitions.NativePaletteFadeActive : throw UnsupportedCommand(gate);
    private static int Address(string binding) => binding switch
    {
        "SceneSignal" => 0xcfd1, "SceneDone" => 0xcfdf,
        _ => throw new System.InvalidOperationException($"Unknown Maku reveal signal '{binding}'.")
    };
    public override bool MemoryEquals(string binding, int value) => Context.Entities.RuntimeState.ReadWramByte(Address(binding)) == value;
    public override void WriteMemory(string binding, int value) => Context.Entities.RuntimeState.SetWramByte(Address(binding), (byte)value);
    public override void ShowText(int id, string message) => Context.ShowDialogue(message, sourceTextId: id);
    public override void ShowText(int id, string message, int? position) => Context.ShowDialogue(message, position, sourceTextId: id);
    public override void RunNativeHandler(string handler)
    {
        if (handler != "Exclamation") throw UnsupportedCommand(handler);
        if (Context.Entities.InteractionSlotAvailable) Context.Entities.CreatePalaceExclamation(_actor.Position + new Vector2(0, -13), 40);
    }
    public override void ScriptEnded() { }
}
