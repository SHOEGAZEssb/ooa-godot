using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// INTERAC_WILD_TOKAY_CONTROLLER $70. Allocation, RNG initialization and
// palette-gated setup occur before its source manager releases its own slot.
internal sealed class WildTokayControllerRoomEntity : RoomEntityAdapter<Node2D>,IFixedRoomEntity,IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly WildTokayGameEvent _owner;
    private readonly NpcRecord _record;
    private readonly WildTokaySpawnSchedule _schedule;
    private RoomEventContext Context=>_owner.Context;
    private WildTokayGameDatabase Data=>_owner.Database;
    private int _savedB,_savedA;
    private bool _inventorySaved;
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter { get; private set; }
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    private bool Present=>Context.Entities.RuntimeState.ReadWramByte(0xcfc0)!=0;
    private int Read(int address)=>Context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address,int value)=>Context.Entities.RuntimeState.SetWramByte(address,(byte)value);
    internal WildTokayControllerRoomEntity(WildTokayGameEvent owner,NpcRecord record)
        :base(new Node2D{Name="WildTokayController_70",Visible=false},static _=>{})
    {_owner=owner;_record=record;_schedule=new(owner.Database,()=>Context.Entities.NextRandomValue());}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;Write(0xcfde,0);Write(0xcfdf,0);
            int level=Present?2:Context.Rooms.SaveData.ReadWramByte(Data.WildLevelAddress);
            Write(0xcfdc,level);_schedule.Begin(level);return;
        }
        switch(Substate)
        {
            case 0:
                if(Context.Transitions.NativePaletteFadeActive)return;
                _savedB=Context.Inventory.EquippedB;_savedA=Context.Inventory.EquippedA;_inventorySaved=true;
                Context.Inventory.SetScriptedEquippedItems(TreasureId.None,TreasureId.Bracelet);
                var tiles=new Dictionary<int,byte>();foreach(var tile in Data.StartTiles)tiles.Add(tile.PackedPosition,(byte)tile.Tile);
                Context.Rooms.CurrentRoom.ApplyRoomInitializationChanges(tiles,Context.AnimationTick());
                Context.Player.SetScriptedCoordinateHigh(false,Data.GameLinkY);Context.Player.SetScriptedCoordinateHigh(true,Data.GameLinkX);Context.Player.Face(Vector2I.Up);
                Write(0xcbe9,0xff);Substate=1;Counter=30;return;
            case 1:
                if(--Counter!=0)return;Substate=2;Counter=10;Context.Sound.PlaySound(SoundId.MusMinigame);_owner.BeginFade(false);return;
            case 2:
                if(Context.Transitions.NativePaletteFadeActive||--Counter!=0)return;
                Substate=3;Write(0xcc8a,0);Context.ShowDialogue(_owner.Interactions.Text(0x0a16),sourceTextId:0x0a16);return;
            case 3:
                if(Context.DialogueOpen)return;Substate=4;Counter=60;
                if(Context.Entities.InteractionSlotAvailable)
                {Context.Entities.TryCreateInteraction(new WildTokayMeatSpawn());Context.Sound.PlaySound(Data.SoundWhistle);}return;
            case 4:
                int result=Read(0xcfde);
                if(result!=0){Write(0xcc8a,1);Substate=5;Counter=30;Context.Sound.PlaySound(result==0xff?Data.SoundError:Data.SoundSuccess);return;}
                Counter=(Counter-1)&255;if(Counter!=0)return;Counter=60;
                var instruction=_schedule.Advance();
                if(instruction.Final)Write(0xcfdf,_schedule.Slot);
                if(instruction.Code is 1 or 3)SpawnParticipant(false);
                if(instruction.Code is 2 or 3)SpawnParticipant(true);
                return;
            case 5:
                if(--Counter!=0)return;Substate=6;Counter=Present?60:20;
                if(!Present)Context.ShowDialogue(_owner.Interactions.Text(Read(0xcfde)==0xff?0x0a17:0x0a18),sourceTextId:Read(0xcfde)==0xff?0x0a17:0x0a18);return;
            case 6:
                if(!Present&&Context.DialogueOpen||--Counter!=0)return;
                RestoreInventory();Context.Rooms.SaveData.SetRoomFlag(_record.Group,_record.Room,0x40);
                Context.Sound.SetNativeActiveMusic(0xff);Finished=true;
                Context.Entities.OnRoomWarpRequested(new(_record.Group,_record.Room,-1,0,WarpSourceTransition.FadeOut,2,Present?0xe5:0xde,0x57,0,3,DirectFadeOut:true));return;
            default:throw new InvalidOperationException($"INTERAC$70: unsupported substate${Substate:x2}.");
        }
    }
    private void SpawnParticipant(bool right)
    {
        if(!Context.Entities.InteractionSlotAvailable)return;
        var record=_record with{SubId=0x0c,Y=Data.ParticipantStartY,X=right?Data.ParticipantRightX:Data.ParticipantLeftX,TextId=0,Message="",CanFace=false,Implementation=NpcImplementationClassification.EventOwned};
        Context.Entities.AddEntity(new WildTokayParticipantRoomEntity(_owner,record));
    }
    private void RestoreInventory(){if(!_inventorySaved)return;Context.Inventory.SetScriptedEquippedItems(_savedB,_savedA);_inventorySaved=false;}
    internal void Cancel()=>RestoreInventory();
}
