using Godot;
using System;

namespace oracleofages;

internal sealed class WildTokayManagerScriptHost : CutsceneCommandHost
{
    private readonly WildTokayGameEvent _owner;
    private readonly TokayNpcRoomEntity _slot;
    private readonly CutsceneCommandRunner _runner;
    private RoomEventContext Context=>_owner.Context;
    private NpcCharacter Npc=>_slot.Npc;
    private bool _initialized,_button,_ended,_starting,_ownsInput,_secretPending;
    private int _cannotAfford,_secretResult,_generation;
    private string _secret="";
    private TokayAttachedVisualRoomEntity? _prize;
    internal int Counter=>_runner.NativeCounter1;
    internal int CurrentCommandIndex=>_runner.CurrentCommand?.Source.CommandIndex??-1;
    internal bool LoweredPrize { get; private set; }
    internal WildTokayManagerScriptHost(WildTokayGameEvent owner,TokayNpcRoomEntity slot)
    {
        _owner=owner;_slot=slot;_runner=new(this);
        _runner.Start(CutsceneCommandCatalog.Load($"res://assets/oracle/cutscenes/wild_tokay_{(Npc.Record.SubId==0x19?"present":"past")}_commands.tsv"));
    }
    internal bool QueueButton(NpcCharacter npc)
    {
        if(!ReferenceEquals(npc,Npc)||!Npc.Active||_button||!Npc.ScriptButtonSensitive)return false;
        _button=true;Context.Player.ApplyObjectInteractionGrace();return true;
    }
    internal void Advance()
    {
        if(!_initialized)
        {
            _initialized=true;
            if(RoomFlagSet(0x40))
            {
                Context.Entities.RuntimeState.SetWramByte(0xcc8a,0x81);Context.Entities.RuntimeState.SetWramByte(0xcc02,0x81);
                Context.Player.SetScriptedCoordinateHigh(false,0x48);Context.Player.SetScriptedCoordinateHigh(true,0x50);Context.Player.Face(Vector2I.Up);
            }
            return;
        }
        if(Context.Transitions.PaletteFadeActive)return;
        if(_starting)
        {
            Context.InterruptBracelet(true);Context.Player.ClearNativeItemParents();Context.Player.DropHeldItemsForScript();
            if(Npc.Record.SubId==0x19)Context.Entities.RuntimeState.SetWramByte(0xcfc0,1);
            _ownsInput=false;_slot.DeleteActor();return;
        }
        if(!_ended)_runner.AdvanceFrame();
        if(_ended){if(_owner.TryStartController(_slot))_starting=true;return;}
        ((TokayCharacter)Npc).RunNativeUpdate(Context.Player);
    }
    public override bool DialogueOpen=>Context.DialogueOpen;
    public override bool ScriptExecutionBlocked=>Context.ScriptDialogueOpen||_secretPending||Context.Player.IsDying;
    public override bool HasActorBinding(CutsceneActorId actor)=>actor.Value=="Tokay";
    public override bool IsLinkedGame=>Context.Rooms.SaveData.IsLinkedGame;
    public override int FrameCounter=>Context.Entities.FrameCounter;
    public override void InitializeActorCollisionRadii(string actor)=>Npc.InitializeCollisionRadii();
    public override void SetActorButtonSensitive(string actor)=>Npc.SetScriptButtonSensitive(true);
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {if(!_button)return false;_button=false;Context.Player.ApplyInteractionInvincibility(0x80);return true;}
    public override void SetActorTextId(string actor,int id,string message)=>Npc.SetDialogue(id,message,false);
    public override void ShowLoadedText()=>ShowText(Npc.TextId,Npc.Message);
    public override void ShowText(int id,string message)=>ShowText(id,message,null);
    public override void ShowText(int id,string message,int? position)
    {
        message=message.Replace("\\secret1",_secret,StringComparison.Ordinal);
        if(id is 0x0a11 or 0x0a13 or 0x0a14 or 0x0a19 or 0x0a26 or 0x0a45 or 0x0a47 or 0x0a4a or 0x0a4b or 0x0a4d or 0x0a51)
            Context.ShowChoiceDialogue(message,textboxPosition:position,sourceTextId:id);
        else Context.ShowDialogue(message,position,sourceTextId:id);
    }
    public override bool TextOptionEquals(int value)=>Context.TryTakeDialogueChoice(out int choice)?choice==value:throw UnsupportedCommand("read missing Wild Tokay choice");
    public override bool RoomFlagSet(int flag)=>Context.Rooms.SaveData.HasRoomFlag(Npc.Record.Group,Npc.Record.Room,(byte)flag);
    public override int ReadMemory(string binding)=>binding switch
    {
        "Bracelet"=>Context.Inventory.HasTreasure(TreasureId.Bracelet)?1:0,
        "Level"=>Context.Rooms.SaveData.ReadWramByte(_owner.Database.WildLevelAddress),
        "Result"=>Context.Entities.RuntimeState.ReadWramByte(0xcfde),
        "CannotAfford"=>_cannotAfford,"SecretResult"=>_secretResult,
        _ when binding.StartsWith("Global:",StringComparison.Ordinal)=>Context.Rooms.SaveData.HasGlobalFlag(int.Parse(binding.AsSpan(7)))?1:0,
        _=>throw UnsupportedCommand(binding)
    };
    public override bool MemoryEquals(string binding,int value)=>ReadMemory(binding)==value;
    public override void SetGlobalFlag(int flag)=>Context.Rooms.SaveData.SetGlobalFlag(flag);
    public override void SetInputEnabled(bool enabled)
    {NativeScriptInputControl.SetEnabled(Context.Entities.RuntimeState,Context.Player,enabled);_ownsInput=!enabled;}
    public override void SetActorAnimation(string actor,int animation,string encoded)=>Npc.SetScriptAnimation(encoded);
    public override void WriteObjectByte(string actor,int address,int value)
    {
        if(address==0x60){Npc.SetAnimationCounter(value);return;}
        if(address==0x7b&&value==1){LoweredPrize=true;if(_prize is not null)_prize.Retired=true;return;}
        throw UnsupportedCommand($"Wild Tokay byte${address:x2}=${value:x2}");
    }
    public override void ScriptEnded()=>_ended=true;
    public override void RunNativeHandler(string handler)
    {
        switch(handler)
        {
            case "PreparePrize":
                int level=ReadMemory("Level");if(level==4&&(Context.Entities.NextRandomValue()&7)==0)level=5;
                Context.Entities.RuntimeState.SetWramByte(0xcfdd,(byte)level);CheckRupees();return;
            case "CheckRupees":CheckRupees();return;
            case "TakeRupees":Context.Inventory.AddRupees(-10);return;
            case "RaisePrize":RaisePrize();return;
            case "PrizeSound":Context.Sound.PlaySound(_owner.Interactions.SoundGetSeed);return;
            case "ResetRoomFlag40":Context.Rooms.SaveData.SetRoomFlag(Npc.Record.Group,Npc.Record.Room,0x40,false);return;
            case "GivePrize":GivePrize();return;
            case "BombCapacity":Context.Inventory.ApplyTokayBombCapacityUpgrade();return;
            case "GenerateSecret":_secret=new LinkedGameNpcDatabase().GenerateSecret(0x15,Context.Rooms.SaveData);return;
            case "AskSecret":
                _secretPending=true;int generation=_generation;
                if(_owner.OpenSecretMenu is null||!_owner.OpenSecretMenu(5,valid=>{if(generation!=_generation)return;_secretResult=valid?0:1;_secretPending=false;}))throw UnsupportedCommand("acquire MENU_SECRET TOKAY_SECRET $05");return;
            default:
                if(handler.StartsWith("LoadText:",StringComparison.Ordinal)){int id=Convert.ToInt32(handler.AsSpan(9).ToString(),16);Npc.SetDialogue(id,_owner.Interactions.Text(id),false);return;}
                throw UnsupportedCommand(handler);
        }
    }
    private void CheckRupees()=>_cannotAfford=Context.Inventory.Rupees>=10?0:1;
    private void RaisePrize()
    {
        Npc.SetScriptAnimation(_owner.Interactions.Animation(6));LoweredPrize=false;
        if(!Context.Entities.InteractionSlotAvailable)return;
        var visual=_owner.Database.Prize(Context.Entities.RuntimeState.ReadWramByte(0xcfdd));
        var record=new NpcRecord(Npc.Record.Group,Npc.Record.Room,0x63,visual.AccessorySubId,0,0,0,0,visual.Sprite,visual.TileBase,visual.Palette,0,false,visual.Animation,visual.Animation,visual.Animation,visual.Animation,"",NpcImplementationClassification.EventOwned);
        _prize=new(Npc,record,new(0,-12)){Visible=false};Context.Entities.AddEntity(_prize);
    }
    private void GivePrize()
    {
        int level=ReadMemory("Level");
        if(Context.Entities.RuntimeState.ReadWramByte(0xcfdd)==5)
        {
            int ring=new GashaSpotDatabase().SelectRing(2,(byte)Context.Entities.NextRandomValue());
            // giveRingToLink performs its checked allocation after selecting
            // the ring, so the RNG read survives a full interaction pool.
            if(!Context.Entities.InteractionSlotAvailable)return;
            Context.Entities.SpawnGroundTreasure(new GroundTreasureGrantRequest(Npc.Record.Group,Npc.Record.Room,0,(int)Context.Player.Position.Y,(int)Context.Player.Position.X,"TREASURE_OBJECT_RING_00","scriptHelper.s:tokayGame_givePrizeToLink")
            {SpawnMode=TreasureSpawnMode.Instant,GrabMode=TreasureGrabMode.OneHand,InventoryWrite=GroundTreasureInventoryWrite.UnappraisedRing,InventoryParameter=ring,RoomFlagTiming=GroundTreasureRoomFlagTiming.OnActivation}).BeginScriptGrant();
        }
        else
        {
            if(!Context.Entities.InteractionSlotAvailable)return;
            string name=level switch{0=>"TREASURE_OBJECT_SCENT_SEEDLING_00",1=>"TREASURE_OBJECT_RUPEES_0e",2=>"TREASURE_OBJECT_RUPEES_0f",3=>"TREASURE_OBJECT_GASHA_SEED_00",4=>"TREASURE_OBJECT_RUPEES_10",_=>throw UnsupportedCommand($"level${level:x2}")};
            Grant(name);_runner.WaitForNativeCounter(3);
        }
        if(level<4)Context.Rooms.SaveData.WriteWramByte(_owner.Database.WildLevelAddress,(byte)(level+1));
    }
    private void Grant(string name)
    {
        var treasure=Context.Treasures.GetObject(name);int flags=treasure.NativeFlags;
        Context.Entities.SpawnGroundTreasure(new GroundTreasureGrantRequest(Npc.Record.Group,Npc.Record.Room,0,(int)Context.Player.Position.Y,(int)Context.Player.Position.X,name,"scriptHelper.s:tokayGame_givePrizeToLink")
        {SpawnMode=(flags>>4)&7,GrabMode=flags&7,RoomFlagTiming=(flags&8)!=0?GroundTreasureRoomFlagTiming.OnActivation:GroundTreasureRoomFlagTiming.Never}).BeginScriptGrant();
    }
    public override void GiveItem(int id,int parameter)
    {
        if(id!=0x61||parameter!=0)throw UnsupportedCommand($"giveitem${id:x2}:${parameter:x2}");
        if(!Context.Entities.InteractionSlotAvailable)return;Grant("TREASURE_OBJECT_BOMB_UPGRADE_00");_runner.WaitForNativeCounter(3);
    }
    internal void Cancel()
    {
        _generation++;_secretPending=false;_runner.Clear();
        if(_ownsInput)SetInputEnabled(true);
        if(_prize is not null)_prize.Retired=true;
    }
}
