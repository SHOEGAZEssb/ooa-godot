using Godot;

namespace oracleofages;

/// <summary>
/// INTERAC_TROY $ca:$01. The native handler is otherwise an ordinary NPC;
/// this adapter preserves its per-talk RNG substitution and room-$40 write.
/// </summary>
internal sealed class TroyHouseRoomEntity
    : RoomEntityAdapter<NpcCharacter>,
        IVariableRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity,
        INpcTalkLifecycle
{
    private readonly TroyHouseDatabase _database;
    private readonly OracleSaveData _save;
    private readonly OracleRandom _random;
    private bool _setFirstTalkFlagOnClose;

    internal TroyHouseRoomEntity(
        NpcCharacter troy,
        TroyHouseDatabase database,
        OracleSaveData save,
        OracleRandom random)
        : base(troy, troy.SetTransitionDrawOffset)
    {
        _database = database;
        _save = save;
        _random = random;
        // troySubid1Script reaches initcollisions before its A-button loop.
        // Its text ID is loaded only after the press, so use the script-sensitive
        // point test rather than installing a clone-side placeholder message.
        troy.SetScriptButtonSensitive(true);
    }

    public NpcCharacter Npc => Entity;
    public NpcCharacter TalkNpc => Entity;

    public void Update(double delta, Player player) =>
        Entity.UpdateNpc(delta, player.Position);

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanScriptTalkTo(
            player,
            NpcCharacter.CollisionRadius,
            NpcCharacter.CollisionRadius,
            NpcCharacter.AButtonPointOffset)
            ? Entity
            : null;

    public void OnNpcTalkStarted()
    {
        TroyHouseRecord record = _database.Record;
        bool firstTalk = !_save.HasRoomFlag(
            record.Group, record.Room, (byte)record.FirstTalkFlag);
        int choice = _random.Next().Value & record.RandomMask;
        Entity.SetDialogue(
            _database.TextId(firstTalk),
            _database.ComposeMessage(firstTalk, choice),
            canFace: false);
        _setFirstTalkFlagOnClose = firstTalk;
    }

    public void OnNpcTalkEnded()
    {
        if (!_setFirstTalkFlagOnClose)
            return;
        _setFirstTalkFlagOnClose = false;
        TroyHouseRecord record = _database.Record;
        _save.SetRoomFlag(
            record.Group, record.Room, (byte)record.FirstTalkFlag);
    }
}
