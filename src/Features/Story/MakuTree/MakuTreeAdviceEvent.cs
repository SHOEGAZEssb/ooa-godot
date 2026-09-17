using System;

namespace oracleofages;

/// <summary>INTERAC_MAKU_TREE subid $00, the repeating story advice lanes.</summary>
internal sealed class MakuTreeAdviceEvent : InteractiveInfiniteScriptHost<NpcCharacter>,
    IRoomEntryEvent, ICutsceneCommandHost, IUpdatesDuringDialogueRoomEvent
{
    private readonly MakuTreeAdviceDatabase _database = new();
    private MakuTreeAdviceRecord _record;
    private NpcCharacter? _flower;
    private int _animation;

    internal MakuTreeAdviceEvent(RoomEventContext context) : base(context, "MakuTree") { }

    public bool Matches(int group, OracleRoomData room) =>
        group == _database.Graphics.Group && room.Id == _database.Graphics.Room &&
        _database.TryGet(Context.Rooms.SaveData.MakuTreeState,
            Context.Rooms.SaveData.IsLinkedGame, out _);

    public void ReleaseOutgoingActors(int group, OracleRoomData room)
    {
        // Room-entry cutscenes can claim the destination before CancelAll.
        // The old tree and its A-button script belong only to their room.
        if (HasState && !Matches(group, room))
            Cancel();
    }

    public void Start(OracleRoomData room)
    {
        OracleSaveData save = Context.Rooms.SaveData;
        if (!_database.TryGet(save.MakuTreeState, save.IsLinkedGame, out _record))
            throw new InvalidOperationException($"makuTree.s:@initSubid00 unsupported state ${save.MakuTreeState:x2}.");
        SavedEventRecord graphics = _database.Graphics;
        NpcCharacter tree = Context.RequireNpc(graphics.Group, room.Id,
            graphics.InteractionId, graphics.SubId, "INTERAC_MAKU_TREE");
        tree.AppendScriptGraphics(graphics.ExtraSprite);
        tree.SetScriptAnimation(graphics.Animation0);
        tree.SetAnimationRate(0);
        _animation = 0;
        _flower = Context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            _database.Flower, "MakuTreeFlower"));
        _flower.SetBlocksLink(false);
        _flower.SetAnimationRate(0);
        StartInfiniteScript(tree, _database.Commands(_record.Mode));
    }

    public override void UpdateFrame()
    {
        AdvanceInfiniteScript();
        UpdateDuringDialogueFrame();
    }

    public void UpdateDuringDialogueFrame()
    {
        ScriptActor?.AdvanceAnimationUpdates(1);
        // makuFlower.s resets its animation each update, after reading the
        // parent's var3b. Only tree animation $04 selects flower animation 1.
        _flower?.SetScriptAnimation(_animation == 4
            ? _database.FrowningFlower : _database.Flower.UpAnimation);
    }

    void ICutsceneCommandHost.SetActorAnimation(string actor, int animation, string encodedAnimation)
    {
        if (encodedAnimation != _database.Graphics.Animation(animation))
            throw new InvalidOperationException($"Maku Tree animation ${animation:x2} disagrees with its import.");
        _animation = animation;
        RequireScriptActor(actor).SetScriptAnimation(encodedAnimation);
    }

    void ICutsceneCommandHost.SetActorCollisionRadii(string actor, int radiusY, int radiusX) =>
        RequireScriptActor(actor).SetCollisionRadii(radiusY, radiusX);

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        string[] parts = handler.Split(':');
        if (parts.Length != 2 || parts[1] is not ("00" or "01") ||
            parts[0] is not ("makuTree_showTextWithOffset" or "makuTree_showTextWithOffsetAndUpdateMapText"))
            throw new InvalidOperationException($"Unsupported Maku Tree advice helper '{handler}'.");
        bool second = parts[1] == "01";
        if (parts[0] == "makuTree_showTextWithOffsetAndUpdateMapText")
            Context.Rooms.SaveData.SetMakuMapTextPresent((second ? _record.SecondTextId : _record.TextId) & 0xff);
        Context.ShowDialogue(second ? _record.SecondText : _record.Text,
            second ? _record.SecondPosition : _record.Position);
    }

    protected override void ResetEventState()
    {
        _flower?.SetActive(false);
        _flower = null;
    }
}
