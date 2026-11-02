
namespace Oracles;

// harpFluteParent.s: one playback owner; native Harp effects bind through Ages.
public class InstrumentController
{
    protected readonly RoomSession Rooms;
    protected readonly RoomEntityManager Entities;
    protected readonly OracleSoundEngine Sound;
    private readonly Action<string,Player> _showText;
    private FluteDatabase? _flute;
    private FluteDatabase Flute => _flute ??= new(Rooms.AssetRoot);
    private OracleRuntimeState Memory => Entities.RuntimeState;
    internal bool IsPlaying => Memory.ReadWramByte(0xd500)!=0 && Memory.ReadWramByte(0xd504)!=0 &&
        Memory.ReadWramByte(0xd501) is TreasureId.Flute or TreasureId.Harp;
    internal bool IsPlayingFlute => IsPlaying && Memory.ReadWramByte(0xd501)==TreasureId.Flute;
    internal int PlayingSong => !IsPlaying ? 0 : IsPlayingFlute ? 0xff : Entities.Inventory!.SelectedHarpSong;
    internal int PlayingInstrument => Memory.ReadField("wLinkPlayingInstrument");
    internal int NoteSpawnCount { get; private set; }
    internal int ActiveMusicNoteCount => Entities.EntityAdapters<FloatingMusicNoteRoomEntity>()
        .Count(note=>!note.Finished && note.Node.Name.ToString().StartsWith("PlayableHarpMusicNote",StringComparison.Ordinal));

    internal InstrumentController(RoomSession rooms,RoomEntityManager entities,OracleSoundEngine sound,Action<string,Player> showText)
    {
        if (rooms.AssetRoot!=entities.AssetRoot || !ReferenceEquals(rooms.RuntimeState,entities.RuntimeState))
            throw new ArgumentException("Instrument playback requires the active room and interaction pool owners.");
        Rooms=rooms; Entities=entities; Sound=sound; _showText=showText;
    }
    internal virtual int TryStartHarp(Player player)=>throw new NotSupportedException(
        "harpFluteParent.s: ITEM_HARP has no selected-game effect binding.");
    protected void Begin(int sound,int publication)
    {
        Memory.SetField("wDisabledObjects",0x7e);
        Memory.SetField("wcc95",0x80);
        Memory.SetField("wLinkPlayingInstrument",(byte)publication);
        Sound.PlaySound(sound);
    }
    internal int TryStartFlute(Player player)
    {
        if (IsPlaying) return 0;
        int icon=player.Inventory.FluteIcon;
        Begin(Flute.Sound(icon),0xff);
        return Flute.Duration(icon);
    }
    protected virtual int HarpParameter(int update)=>throw new NotSupportedException(
        "harpFluteParent.s: ITEM_HARP has no selected-game animation binding.");
    internal void Advance(Player player,int actionUpdate)
    {
        if (!IsPlaying || actionUpdate<=0) return;
        Memory.SetField("wLinkPlayingInstrument",(byte)PlayingSong);
        if ((actionUpdate&0x1f)!=0) return;
        int parameter=IsPlayingFlute ? Flute.Parameter(actionUpdate) : HarpParameter(actionUpdate);
        bool right=(Entities.NextRandomValue()&1)!=0;
        if (FloatingMusicNoteRoomEntity.Spawn(Entities,
            player.Position+new Vector2((parameter&1)!=0?8:-8,-4),!right,$"PlayableHarpMusicNote{NoteSpawnCount}") is not null)
            NoteSpawnCount++;
    }
    internal void Complete(Player player,int song)
    {
        if (song==0xff) CompanionFluteSpawner.TryCreate(Rooms,Entities,Flute,_showText);
        Memory.SetField("wDisabledObjects",0); Memory.SetField("wcc95",0);
        if (song!=0xff) CompleteHarp(player,song);
    }
    protected virtual void CompleteHarp(Player player,int song)=>throw new NotSupportedException(
        "harpFluteParent.s: ITEM_HARP completion has no selected-game effect binding.");
    internal void Cancel()
    {
        if (Memory.ReadField("wDisabledObjects")==0x7e) Memory.SetField("wDisabledObjects",0);
        Memory.SetField("wLinkPlayingInstrument",0); Memory.SetField("wcc95",0);
    }
}
