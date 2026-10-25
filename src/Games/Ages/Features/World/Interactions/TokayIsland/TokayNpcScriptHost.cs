using Godot;
using System;
using System.Linq;

namespace Oracles;

// interactionRunScript is dispatched only by this Tokay's native object slot.
internal sealed class TokayNpcScriptHost : NpcInteractionCommandHost
{
    private readonly TokayNpcRoomEntity _slot;
    private readonly TokayInteractionDatabase _data;
    private readonly InventoryState _inventory;
    private readonly int _returnedItems;
    private bool _ownsInput;
    private GroundTreasurePickup? _reward;
    private int _shopItemMatch;
    private Vector2 _precisePosition;
    private int _plotSpeed;
    private int _vineZ;
    private int _vineSpeedZ;
    private int _escapeZ;
    private int _escapeSpeedZ;
    private int _plotCounter;
    private int _emberSnapshot;
    internal int NativePressedButton { get; private set; }
    internal int MovementCounter => ScriptActor.Record.SubId is 0x0b or 0x0f or 0x10 ? ActorMovementCounter("Tokay") : _plotCounter;
    internal Vector2 PrecisePosition => _precisePosition;

    internal static bool Matches(NpcCharacter npc) => npc.Active && npc is TokayCharacter &&
        npc.Record.SubId is >= 0x06 and <= 0x0b or >= 0x0e and <= 0x18 or >= 0x1d and <= 0x1f;

    internal TokayNpcScriptHost(TokayNpcRoomEntity slot, TokayInteractionDatabase data,
        RoomSession rooms, RoomEntityManager entities, DialogueBox dialogue, InventoryState inventory)
        : base("Tokay", rooms, entities, dialogue, CutsceneCommandCatalog.Load(
            slot.Npc.Record.SubId is >= 0x06 and <= 0x0a
                ? AgesGameContent.AssetRoot + "cutscenes/tokay_holder_commands.tsv"
                : slot.Npc.Record.SubId == 0x0e ? AgesGameContent.AssetRoot + "cutscenes/tokay_shopkeeper_commands.tsv"
                : slot.Npc.Record.SubId == 0x11 ? AgesGameContent.AssetRoot + "cutscenes/tokay_seedling_commands.tsv"
                : slot.Npc.Record.SubId == 0x1d ? AgesGameContent.AssetRoot + "cutscenes/tokay_shield_commands.tsv"
                : slot.Npc.Record.SubId == 0x1e ? AgesGameContent.AssetRoot + "cutscenes/tokay_vine_commands.tsv"
                : slot.Npc.Record.SubId == 0x0b ? AgesGameContent.AssetRoot + "cutscenes/tokay_escape_commands.tsv"
                : slot.Npc.Record.SubId == 0x0f ? AgesGameContent.AssetRoot + "cutscenes/tokay_dimitri_1_commands.tsv"
                : slot.Npc.Record.SubId == 0x10 ? AgesGameContent.AssetRoot + "cutscenes/tokay_dimitri_2_commands.tsv"
                : AgesGameContent.AssetRoot + "cutscenes/tokay_generic_commands.tsv"))
    {
        _slot=slot; _data=data; _inventory=inventory;
        _precisePosition=slot.Npc.Position;
        _returnedItems=slot.Npc is TokayHoldingItemCharacter { HeldItemVisible: true } ? 0 :
            ((TokayCharacter)slot.Npc).ReturnedItemDialogue == 0x0a0d ? 2 : 1;
        BindObject(slot.Npc);
        if(slot.Npc.Record.SubId is 0x0f or 0x10)SetNativeActorSpeed("Tokay",data.DimitriDepartureSpeed);
    }

    internal void Advance(Player player) => AdvanceObject(player);
    internal void AdvanceDimitri(Player player)
    {
        if(ScriptActor.Record.SubId==0x0f&&(Entities.RuntimeState.ReadWramByte(AgesWramAddress.wScrollMode)&0x0e)!=0)return;
        if((CompanionRuntimeState.ReadScriptSignals(Entities.RuntimeState)&4)==0)((TokayCharacter)ScriptActor).RunNativeUpdate(player);
        AdvanceObject(player);
        ScriptActor.SetScriptDrawOffset(_precisePosition.X>=0xf0 ? new Vector2(-256,0) : Vector2.Zero);
    }
    internal void AdvanceEscape(Player player)
    {
        AdvanceObject(player);
        if(!ScriptActor.Active)return;
        ScriptActor.AdvanceAnimationUpdates(MovementCounter!=0 ? 2 : 1);
        ScriptActor.SetScriptDrawOffset(new(_precisePosition.X>=0xf0 ? -256 : 0,_escapeZ>>8));
    }
    internal void AdvanceVine(Player player)
    {
        OracleObjectMath.UpdateSpeedZ(ref _vineZ,ref _vineSpeedZ,0x10);
        ScriptActor.SetScriptDrawOffset(new(0,_vineZ>>8));
        ((TokayCharacter)ScriptActor).RunNativeUpdate(player);
        if(!RoomFlagSet(0x40))
        {
            Vector2 delta=OracleObjectMath.ToPixelPosition(player.Position)-ScriptActor.Position;
            int x=(int)Mathf.Abs(delta.X),y=(int)Mathf.Abs(delta.Y);
            if(x+y>=0x18)return;
            NativePressedButton=x>=y ? delta.X<0 ? 6 : 2 : delta.Y<0 ? 0 : 4;
            SetNativeObjectButton(NativePressedButton!=0);
        }
        AdvanceObject(player);
    }
    protected override bool MatchesAndPrepare(NpcCharacter npc) => ReferenceEquals(npc, _slot.Npc);
    public override bool TryInteract(NpcInteractionTarget target, Player player)
    {
        if(!MatchesAndPrepare(target.Npc)||ObjectButtonPending||!QueueObjectInteraction(target,player))return false;
        NativePressedButton=1;return true;
    }
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        bool pressed=base.TryConsumeActorButton(actor);
        if(pressed)NativePressedButton=0;
        return pressed;
    }
    public override void ShowLoadedText() => ShowDialogue(ScriptActor.Message, false);
    public override void ShowText(int textId, string message) => ShowText(textId,message,null);
    public override void ShowText(int textId, string message, int? textboxPosition)
    {
        float y=Entities.WorldToScreen(ScriptPlayer.Position).Y;
        if(textId==0x0a20)Dialogue.ShowGameplayChoiceMessage(message,y,0,textboxPosition??ScriptActor.TextPosition);
        else Dialogue.ShowGameplayMessage(message,y,textboxPosition??ScriptActor.TextPosition);
    }
    public override int ReadMemory(string binding) => binding switch
    {
        _ when binding.StartsWith("Treasure:", StringComparison.Ordinal) =>
            _inventory.HasTreasure(Convert.ToInt32(binding.AsSpan(9).ToString(),16)) ? 1 : 0,
        "ReturnedItems"=>_returnedItems,
        "ShopItemMatch"=>_shopItemMatch,
        "LinkY"=>((int)Mathf.Floor(ScriptPlayer.Position.Y))&0xff,
        "EscapeZHigh"=>(_escapeZ>>8)&0xff,
        "EmberSnapshot"=>_emberSnapshot,
        "PressedA"=>NativePressedButton,
        "TextOption"=>TakeChoice(),
        _ when binding.StartsWith("DimitriMask:",StringComparison.Ordinal)=>
            (Rooms.SaveData.ReadWramByte(AgesWramAddress.wDimitriState)&Convert.ToInt32(binding.AsSpan(12).ToString(),16))!=0?1:0,
        _ when binding.StartsWith("CompanionMask:",StringComparison.Ordinal)=>
            (CompanionRuntimeState.ReadScriptSignals(Entities.RuntimeState)&Convert.ToInt32(binding.AsSpan(14).ToString(),16))!=0?1:0,
        _=>throw UnsupportedCommand($"read '{binding}'")
    };
    public override bool MemoryEquals(string binding,int value)=>ReadMemory(binding)==value;
    public override bool RoomFlagSet(int flag) =>
        Rooms.SaveData.HasRoomFlag(Rooms.ActiveGroup, Rooms.CurrentRoom.Id, checked((byte)flag));
    public override void OrRoomFlag(int flag) =>
        Rooms.SaveData.SetRoomFlag(Rooms.ActiveGroup, Rooms.CurrentRoom.Id, checked((byte)flag));
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) =>
        RequireActor(actor).SetScriptAnimation(encodedAnimation);
    public override void SetActorMovementAnimation(string actor,int angle,string encodedAnimation)
    {
        RequireActor(actor).SetNativeAngle(angle);
        ScriptActor.SetFacingDirection(new((int)OracleObjectMath.StrictCardinalVector(angle).X,(int)OracleObjectMath.StrictCardinalVector(angle).Y));
        ScriptActor.SetScriptAnimation(encodedAnimation);
    }
    public override void MoveActorAtSpeed(string actor,int speed,int angle)
    {
        RequireActor(actor).SetStatePosition(NativeObjectMovement.ApplySpeed(Entities.RuntimeState,ref _precisePosition,speed,angle));
    }
    public override void ScriptEnded()
    {
        if(ScriptActor.Record.SubId is not (0x0b or 0x0f or 0x10))throw UnsupportedCommand("end this Tokay script");
        _ownsInput=false;
        _slot.DeleteActor();
    }
    public override void WriteObjectByte(string actor, int address, int value)
    {
        _=RequireActor(actor);
        if(address==0x71&&value==0&&ScriptActor.Record.SubId is 0x10 or 0x1e)
        {NativePressedButton=0;SetNativeObjectButton(false);return;}
        if(ScriptActor.Record.SubId==0x1e)
        {
            if(address==0x54&&value is 0 or 0xff){_vineSpeedZ=(_vineSpeedZ&0xff00)|value;return;}
            throw UnsupportedCommand($"write vine Tokay.${address:x2}=${value:x2}");
        }
        if (address != 0x7b || value != 1) throw UnsupportedCommand($"write Tokay.${address:x2}=${value:x2}");
        ((TokayCharacter)ScriptActor).NativeAnimation=TokayAnimationMode.FaceLink;
        if (ScriptActor is TokayHoldingItemCharacter holder) holder.RemoveHeldItem();
        else if (((TokayCharacter)ScriptActor).Accessory is { } accessory) accessory.Retired=true;
    }
    public override void SetInputEnabled(bool enabled)
    {
        NativeScriptInputControl.SetEnabled(Entities.RuntimeState, ScriptPlayer, enabled);
        _ownsInput=!enabled;
        if (enabled) base.SetInputEnabled(true);
    }
    public override void SetDisabledObjects(int value)=>Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects,checked((byte)value));
    private int TakeChoice()=>Dialogue.TryTakeChoiceResult(out int choice)?choice:throw UnsupportedCommand("read a missing hungry Tokay text choice");
    public override void RunNativeHandler(string handler)
    {
        if(handler.StartsWith("OrDimitri:",StringComparison.Ordinal))
        {
            Rooms.SaveData.WriteWramByte(AgesWramAddress.wDimitriState,(byte)(Rooms.SaveData.ReadWramByte(AgesWramAddress.wDimitriState)|Convert.ToInt32(handler.AsSpan(10).ToString(),16)));return;
        }
        if(handler.StartsWith("OrCompanion:",StringComparison.Ordinal))
        {CompanionRuntimeState.OrScriptSignals(Entities.RuntimeState,Convert.ToInt32(handler.AsSpan(12).ToString(),16));return;}
        if(handler.StartsWith("DimitriAngle:",StringComparison.Ordinal))
        {SetPlotAngle(Convert.ToInt32(handler.AsSpan(13).ToString(),16));return;}
        if(handler.StartsWith("Ember:",StringComparison.Ordinal))
        {
            string[] parts=handler.Split(':');
            Entities.TryCreateInteraction(new TokayRescueEmberSpawn(new TokayRescueEmberDatabase().Record,
                new(Convert.ToInt32(parts[2],16),Convert.ToInt32(parts[1],16))));return;
        }
        switch(handler)
        {
            case "CheckEmberSeeds":_emberSnapshot=_inventory.HasTreasure(TreasureId.EmberSeeds)?_inventory.EmberSeeds:0;return;
            case "ConsumeEmberSeed":
                // tokayDecNumEmberSeeds requests HUD refresh before SUB/DAA.
                Entities.RuntimeState.SetWramByte(0xcbe9,0xff);
                if(!_inventory.TryConsumeSeedsFromScript(TreasureId.EmberSeeds,1))throw UnsupportedCommand("decrement Ember Seeds after their retained nonzero test");return;
            case "EnableMenu":Entities.RuntimeState.SetWramByte(AgesWramAddress.wMenuDisabled,0);return;
            case "PlotAngle:10": SetPlotAngle(0x10); return;
            case "PlotFaceLink":
                SetPlotAngle((OracleObjectMovement.Shared.RelativeAngle(ScriptActor.Position,ScriptPlayer.Position)+4)&0x18); return;
            case "PlotFlip": SetPlotAngle(ScriptActor.NativeAngle^0x10); return;
            case "PlotSpeed:28": _plotSpeed=0x28; return;
            case "PlantSeedling":
                Rooms.SaveData.SetRoomFlag(Rooms.ActiveGroup,Rooms.CurrentRoom.Id,0x80);
                Rooms.SaveData.SetRoomFlag(Rooms.ActiveGroup-1,Rooms.CurrentRoom.Id,0x80);
                _inventory.LoseTreasure(_data.TreasureScentSeedling); return;
            case "PlantDecoration":
                Entities.TryCreateInteraction(new TokaySeedlingDecorationSpawn(new TokaySeedlingPlotDatabase().Record)); return;
            case "PlantSound": Entities.RequestSound(_data.SoundGetSeed); return;
            case "EscapeJump": _escapeSpeedZ=-0x1c0;return;
            case "EscapeGravity": OracleObjectMath.UpdateSpeedZ(ref _escapeZ,ref _escapeSpeedZ,0x20);return;
            case "EscapeJumpSound": Entities.RequestSound(_data.SoundJump);return;
            case "VineFaceLink":
                int angle=(OracleObjectMovement.Shared.RelativeAngle(ScriptActor.Position,ScriptPlayer.Position)+4)&0x18;
                ScriptActor.SetScriptAnimation(_data.Animation(angle>>3));return;
            case "VineLinkLeft": ScriptPlayer.Face(Vector2I.Left);return;
            case "VineExclamation":
                Entities.TryCreateInteraction(new ExclamationMarkSpawn(ScriptActor.Position+new Vector2(-13,-13)));return;
            case "GiveShield":
                if (Entities.InteractionSlotAvailable)
                {
                    int subid=_inventory.ShieldLevel<2 ? 1 : 2;
                    GiveTreasureObject($"TREASURE_OBJECT_SHIELD_{subid:x2}",TreasureId.Shield,subid,subid+1);
                }
                return;
        }
        if (handler == "FindShopItem")
        {
            // objectFindSameTypeObjectWithID returns A=the matching ID,
            // not H. tokayFindShopItem stores that retained $81 in var3f.
            _shopItemMatch=Entities.Entities<TokayShopItem>().Any(item=>!item.Removed) ? 0x81 : 0;
            return;
        }
        if (handler.StartsWith("LoadText:", StringComparison.Ordinal))
        {
            int id=Convert.ToInt32(handler.AsSpan(9).ToString(),16);
            ScriptActor.SetDialogue(id,_data.Text(id),ScriptActor.Record.CanFace);
            return;
        }
        if (handler != "GiveHeldItem") throw UnsupportedCommand($"run '{handler}'");
        // scriptHelp.tokayGiveItemToLink: failed allocation returns before
        // touching seeds, level or inventory. The script still continues.
        if (!Entities.InteractionSlotAvailable) return;
        TokayHeldItemRecord item=_data.HeldItem(ScriptActor.Record.SubId);
        GiveTreasureObject(item.GrantObject,item.Treasure,item.GrantSubId,item.GrantParameter);
        if (item.Treasure == TreasureId.SeedSatchel) _inventory.PrepareReturnedTokaySeedSatchel();
    }
    private void GiveTreasureObject(string name,int treasureId,int subid,int parameter)
    {
        TreasureObjectRecord treasure=Entities.Treasures.GetObject(name);
        Vector2 position=ScriptPlayer.Position;
        _reward=Entities.SpawnGroundTreasure(new GroundTreasureGrantRequest(
            Rooms.ActiveGroup,Rooms.CurrentRoom.Id,0,(int)position.Y,(int)position.X,
            name,ScriptActor.Record.SubId==0x1d ? "scriptHelper.s:tokayGiveShieldUpgradeToLink" : "scriptHelper.s:tokayGiveItemToLink")
        {
            SpawnMode=(treasure.NativeFlags>>4)&7, GrabMode=treasure.NativeFlags&7,
            RoomFlagTiming=(treasure.NativeFlags&8)!=0 ? GroundTreasureRoomFlagTiming.OnActivation : GroundTreasureRoomFlagTiming.Never,
            ExpectedTreasureId=treasureId, ExpectedSubId=subid,
            ExpectedObjectParameter=parameter
        });
        _reward.BeginScriptGrant();
    }
    private void SetPlotAngle(int angle)
    {
        ScriptActor.SetNativeAngle(angle);
        ScriptActor.SetFacingDirection(new((int)OracleObjectMath.StrictCardinalVector(angle).X,(int)OracleObjectMath.StrictCardinalVector(angle).Y));
        ScriptActor.SetScriptAnimation(_data.Animation(angle>>3));
    }
    public override bool UpdateNativeHandler(string handler,CutsceneActorId? actor,int commandUpdate,int frames,string payload)
    {
        if(handler!="PlotMove") throw UnsupportedCommand($"update '{handler}'");
        if(commandUpdate==0) { _plotCounter=frames; return false; }
        if(--_plotCounter==0) return true;
        ScriptActor.SetStatePosition(NativeObjectMovement.ApplySpeed(Entities.RuntimeState,
            ref _precisePosition,_plotSpeed,ScriptActor.NativeAngle));
        return false;
    }
    protected override void ResetHostState()
    {
        if (_ownsInput) SetInputEnabled(true);
        _reward?.Finish(ScriptPlayer);
        _reward=null;
    }
}
