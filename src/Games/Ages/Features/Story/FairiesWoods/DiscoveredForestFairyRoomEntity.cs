using Godot;
using System.Collections.Generic;
using System.Numerics;

namespace Oracles;

// $49:$01 allocates before checking its found bit. State0 chooses its script
// once; initialized actors watch the live active byte for deletion.
internal sealed class DiscoveredForestFairyRoomEntity : NpcCharacterRoomEntityAdapter,
    INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IRoomBlocker, ITalkTarget
{
    private readonly RoomEventContext _context;
    private readonly FairiesWoodsDatabase _data;
    private readonly int _index;
    private bool _initialized;
    private bool _pressedA;
    internal NpcCharacter Actor => Entity;
    internal bool ButtonSensitive => Actor.ScriptButtonSensitive;
    public bool Finished => !Actor.Active;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal DiscoveredForestFairyRoomEntity(RoomEventContext context, FairiesWoodsDatabase data, int index)
        : this(context, data, index, CreateActor(context, data, index)) { }
    private DiscoveredForestFairyRoomEntity(RoomEventContext context, FairiesWoodsDatabase data, int index, NpcCharacter actor)
        : base(actor, actor.SetTransitionDrawOffset)
    { _context = context; _data = data; _index = index; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Advance(frame.Player);
    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized && !Finished) Advance();
        return Actor.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    public bool BlocksLink(Godot.Vector2 center) => _initialized && !Finished && Actor.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => ButtonSensitive && !_pressedA && Actor.CanTalkTo(player) ? Actor : null;
    internal bool TryQueueAButton()
    {
        if (!ButtonSensitive || Finished || _pressedA) return false;
        _pressedA = true;
        return true;
    }
    private void Advance(Player? player = null)
    {
        if (Finished) return;
        if (!_initialized)
        {
            _initialized = true;
            int found = _context.Entities.RuntimeState.ReadWramByte(_data.Event.FoundAddress);
            if ((found & (1 << _index)) == 0) { Actor.SetActive(false); return; }
            int count = BitOperations.PopCount((uint)found);
            int text = count == 1 ? 0x1108 : count == 2 ? 0x1109 : 0;
            Actor.SetDialogue(text, text == 0 ? string.Empty : _data.Text(text).Message, false);
            var position = _data.DiscoveredFairies[_index];
            Actor.Position = new(position.X, position.Y);
            Actor.SetBasePalette(position.Palette); Actor.SetScriptAnimation(position.Animation);
            Actor.SetScriptDrawOffset(new(0, -4)); Actor.SetCollisionRadii(4, 4);
            Actor.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex);
            return;
        }
        if (_context.Entities.RuntimeState.ReadWramByte(_data.Event.ActiveAddress) == 0)
        { Actor.SetActive(false); return; }
        if (player is null) throw new System.InvalidOperationException("forestFairy_subid01 state1 requires its live Link object.");
        Actor.PreventPlayerPassing(player);
        Actor.AdvanceAnimationUpdates(1);
        Actor.SetScriptButtonSensitive(Actor.Record.TextId != 0);
        if (_pressedA && !_context.ScriptDialogueOpen && !player.IsDying)
        {
            _pressedA = false;
            player.ApplyInteractionInvincibility(0x80); // scriptCmd_checkAButton -> func_0c_4177.
            _context.ShowDialogue(_data.Text(Actor.Record.TextId).Message);
        }
    }
    private static NpcCharacter CreateActor(RoomEventContext context, FairiesWoodsDatabase data, int index)
    {
        var visual = data.Event;
        var record = new NpcRecord(context.Rooms.ActiveGroup, context.Rooms.CurrentRoom.Id,
            AgesInteractionId.ForestFairy, 1, 0, 0, index, 0, visual.FairySprite, visual.FairyTileBase, 0, 0, false,
            visual.Animation0, visual.Animation0, visual.Animation0, visual.Animation0, string.Empty,
            NpcImplementationClassification.EventOwned);
        var actor = NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot);
        actor.Name = $"FairiesWoods_01_{index:x2}"; actor.SetAnimationRate(0);
        return actor;
    }
}
