using Godot;
using System;

namespace Oracles;

// Ages' native Harp tune effects bind to the shared Flute/Harp playback owner.
public sealed class HarpController : InstrumentController
{
    private readonly AgesRoomTransitionController _transitions;
    private readonly InteractionController _interactions;
    internal HarpItemDatabase Database { get; }
    internal HarpController(RoomSession rooms,RoomEntityManager entities,AgesRoomTransitionController transitions,
        InteractionController interactions,OracleSoundEngine sound,HarpItemDatabase? database=null)
        : base(rooms,entities,sound,interactions.ShowRoomInteractionMessage)
    {
        _transitions=transitions; _interactions=interactions; Database=database ?? new();
    }
    internal override int TryStartHarp(Player player)
    {
        if (IsPlaying) return 0;
        int song=player.Inventory.SelectedHarpSong;
        Begin(Database.SoundForSong(song),song);
        return Database.FramesForSong(song);
    }
    protected override int HarpParameter(int update)=>Database.AnimationParameterAtUpdate(update);
    protected override void CompleteHarp(Player player,int song)
    {
        var record=Database.Record;
        byte flags=Rooms.CurrentRoom.TilesetFlags;
        if ((flags&record.ProhibitedTilesetMask)!=0) return;
        switch (song)
        {
            case 0: return;
            case 1:
                if (!Rooms.SaveData.HasRoomFlag(Rooms.ActiveGroup,Rooms.CurrentRoom.Id,(byte)record.PortalRoomFlag))
                    _interactions.ShowRoomInteractionMessage(record.NoEffectText,player);
                return;
            case 2:
                if ((flags&record.PastMask)==0)
                {
                    _interactions.ShowRoomInteractionMessage(record.NoEffectText,player);
                    return;
                }
                break;
            case 3: break;
            default: throw new InvalidOperationException($"ITEM_HARP completed unsupported song ${song:x2}.");
        }
        _transitions.ApplyHarpTimeWarp(player,player.Position);
    }
}
