namespace Oracles;

// INTERAC_USED_ROD_OF_SEASONS $15 is a one-visit consumer after ITEM $07.
internal sealed class SeasonsUsedRodRoomEntity(SeasonsOpeningSession session, Vector2 position, int angle)
    : INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private NativeInteractionState _state = null!;
    public Node2D? PresentationNode => null;
    public bool Finished { get; private set; }
    public bool NativeInitializationPending => !Finished;
    public void BindNativeInteractionSlot(int slot)
    {
        _state = new(session.Memory, slot); _state.Clear();
        _state[0x40] = 1; _state[0x41] = 0x15; _state[0x49] = angle; _state.Position = position;
    }
    public void ReleaseNativeInteractionSlot() { _state.Clear(); Finished = true; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (session.Memory.ReadField("wMenuDisabled") == 0 && session.Memory.ReadField("wLinkDeathTrigger") == 0 && session.Rooms.ActiveGroup == 0 &&
            (session.Inventory.ObtainedSeasons & 15) != 0)
        {
            int season = session.Memory.ReadField("wRoomStateModifier");
            do { season = (season + 1) & 3; } while ((session.Inventory.ObtainedSeasons & (1 << season)) == 0);
            // bank0.setSeason still leaves the cue/rate writes on rejected Natzu packs.
            if (session.Memory.ReadField("wRoomPack") < 0xf1)
            { session.Memory.SetField("wRoomStateModifier", (byte)season); session.Memory.SetField("wcc4c", 2); }
            session.Sound.PlaySound(SoundId.SndEnergyThing);
            session.Memory.SetField("wPaletteThread_updateRate", 2);
        }
        Finished = true;
    }
}
