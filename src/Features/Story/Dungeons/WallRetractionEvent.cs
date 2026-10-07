using Godot;
using System;

namespace oracleofages;

// CUTSCENE$0b calls its handler BEFORE updateAllObjects, including release.
internal sealed class WallRetractionEvent(RoomEventContext context) : IRoomEvent
{
    private WallRetractionDatabase? _database;
    private WallRetractionDatabase Data => _database ??= new();
    internal int Phase { get; private set; }
    internal int Counter { get; private set; }
    internal int Retractions { get; private set; }
    private bool _pending;
    public bool HasState { get; private set; }
    public bool BlocksGameplay => HasState;
    public bool OwnsGameLogic => HasState;
    public bool MenusDisabled => HasState;
    public bool AllScreenTransitionsDisabled => HasState;
    internal bool CameraUpdatesDisabled => HasState && !_pending && Retractions == 0;

    internal void Begin()
    {
        if (HasState || context.Rooms.ActiveGroup != 5 || context.Rooms.CurrentRoom.Id != 0x43)
            throw new InvalidOperationException($"{Data.Source}: wall retraction requires one handoff in room$5:$43.");
        HasState=true; _pending=true; Phase=0; Counter=0; Retractions=0;
    }
    internal void AdvanceBeforeObjects()
    {
        if (!HasState) return;
        if (_pending)
        {
            _pending=false;
            OracleRoomData room=context.Rooms.CurrentRoom;
            context.Transitions.ResetCamera();
            context.Rooms.SaveData.SetRoomFlag(context.Rooms.ActiveGroup,room.Id,0x40);
            context.Rooms.ReloadCurrentRoomLayout();
            // loadRoomLayout uses CE as compressed-data scratch. It remains
            // live until func_7098 explicitly calls loadRoomCollisions.
            for (int position=0;position<Data.LayoutScratch.Length;position++)
                room.SetPackedTileCollision((byte)position,Data.LayoutScratch[position]);
            Counter=Data.InitialWait;
            return;
        }
        if (Phase == 0)
        {
            if (--Counter == 0) { Phase=1; Counter=Data.SecondWait; }
            return;
        }
        // The initial wait has scrollMode0. The first rectangle's reloadTileMap
        // sets scrollMode1, admitting camera/shake updates from that point.
        context.Entities.BeginScreenShake(Data.Shake);
        if (--Counter != 0) return;
        Counter=Data.Interval;
        context.Rooms.CurrentRoom.RegenerateBackgroundMappings(context.AnimationTick());
        int row=((Data.Rectangle>>12)&15)*2+Retractions;
        int column=((Data.Rectangle>>8)&15)*2;
        int width=Data.Rectangle&255, height=20-row;
        byte[] pairs=new byte[height*width*2];
        for (int y=0;y<height;y++) for (int x=0;x<width;x++)
        {
            int destination=(y*width+x)*2, source=y*32+x;
            pairs[destination]=Data.Map[source]; pairs[destination+1]=Data.Attributes[source];
        }
        context.Rooms.CurrentRoom.SetBackgroundMappingRectangle(new(column*8,row*8),width,pairs,context.AnimationTick());
        context.RoomView.QueueRedraw(); context.Sound.PlaySound(SoundId.SndDoorClose);
        if (++Retractions < Data.Steps) return;
        context.Sound.PlaySound(SoundId.SndSolvePuzzle);
        context.Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects,0);
        context.Entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled,0);
        context.Rooms.ReloadCurrentRoomLayout();
        context.Rooms.CurrentRoom.SetPackedTileCollision(0x5d,Data.FinalCollision);
        HasState=false;
    }
    // The event scheduler's later interaction phase must not advance it twice.
    public void UpdateFrame() { }
    public void Cancel()
    {
        if (HasState)
        {
            context.Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects,0);
            context.Entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled,0);
        }
        HasState=false; _pending=false;
    }
}
