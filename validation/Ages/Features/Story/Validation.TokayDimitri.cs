using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private (TokayNpcRoomEntity First,TokayNpcRoomEntity Second) BeginTokayDimitriTrade(bool batched,bool stopAtSeeds=false)
    {
        var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
        save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
        InitializeTransientSession(save);_saveData.WriteWramByte(AgesWramAddress.wDimitriState,1);
        _inventory.GiveTreasure(TreasureId.Essence,2);_inventory.GiveTreasure(TreasureId.EmberSeeds,0x20);
        _inventory.SetScriptedEquippedItems(0,0);LoadValidationRoom(0,0xaa);
        var first=_entities.EntityAdapters<TokayNpcRoomEntity>().Single(s=>s.Npc.Record.SubId==0x0f);
        var second=_entities.EntityAdapters<TokayNpcRoomEntity>().Single(s=>s.Npc.Record.SubId==0x10);
        StepGameplayUpdates(4,Vector2.Zero,batched:batched);
        ApproachHungryTokay(first.Npc,batched);
        void Step(int n)=>StepGameplayUpdates(n,Vector2.Zero,batched:batched);
        void Text(int id)=>FailIf(!_dialogue.IsOpen||_dialogue.CurrentMessage!=DialogueBox.PlainText(new TokayInteractionDatabase().Text(id)),
            $"Hungry Tokay expected TX_${id:x4}: open={_dialogue.IsOpen}, message={_dialogue.CurrentMessage}, cmd={first.Script!.CurrentCommandIndex}, ember={first.Script.ReadMemory("EmberSnapshot"):x2}/{_inventory.EmberSeeds:x2}.");
        StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);Text(0x0a1f);
        _dialogue.Close();Step(1);Text(0x0a20);
        FailIf(!_dialogue.ChoiceActive||!_dialogue.CurrentMessage.Contains("Will you trade"),"TX_0a20 must retain its unterminated fallthrough into TX_0a21's trade prompt and choices.");
        _dialogue.SubmitChoiceForValidation(0);Step(2);Text(0x0a23);
        _dialogue.Close();Step(1);
        FailIf(_entities.Entities<TokayRescueEmberRoomEntity>().Count!=1,"The first spawninteraction must yield before creating the second $8f.");
        Step(1);
        FailIf(_entities.Entities<TokayRescueEmberRoomEntity>().Count!=2||_inventory.EmberSeeds!=0x19,
            "The second checked source spawn must occur on its own update after one BCD Ember Seed is consumed.");
        if(stopAtSeeds)return(first,second);
        Step(1);FailIf(first.Script!.NativeCounter1!=30,"Source wait30 must install after both spawn yields.");
        Step(29);FailIf(_dialogue.IsOpen||first.Script.NativeCounter1!=1,"Hungry Tokay opened TX_0a24 before wait30 expired.");
        Step(1);Text(0x0a24);_dialogue.Close();Step(1);
        FailIf(first.Script.NativeCounter1!=60,"Source wait60 must install after TX_0a24 closes.");
        Step(59);FailIf(_dialogue.IsOpen||first.Script.NativeCounter1!=1,"Hungry Tokay opened TX_0a25 before wait60 expired.");
        Step(1);Text(0x0a25);_dialogue.Close();Step(1);
        return(first,second);
    }

    private void ApproachHungryTokay(NpcCharacter actor,bool batched)
    {
        foreach(int distance in new[]{40,32,24,20})
        foreach(var (direction,action) in new[]{(Vector2.Up,"move_up"),(Vector2.Down,"move_down"),(Vector2.Left,"move_left"),(Vector2.Right,"move_right")})
        {
            Vector2 start=actor.Position-direction*distance;
            if(!Enumerable.Range(0,distance-12).All(i=>
            {
                Vector2 p=start+direction*i;
                return p.X>=6&&p.Y>=6&&p.X<_currentRoom.Width-6&&p.Y<_currentRoom.Height-6&&
                    !_collision.Collides(p)&&!_entities.BlocksLink(p)&&
                    new[]{new Vector2(-5,-2),new Vector2(5,-2),new Vector2(-5,5),new Vector2(5,5)}
                        .All(o=>_currentRoom.GetTerrainInfo(p+o).Hazard==HazardType.None);
            }))continue;
            _player.WarpTo(start);StepGameplayUpdates(distance,direction,[action],[action],batched);
            FailIf(_collision.Collides(_player.Position)||!actor.CanTalkTo(_player),"Hungry Tokay must be reachable through the room's actual collision.");
            return;
        }
        throw new InvalidOperationException($"Room0:$aa has no safe approach to Tokay${actor.Record.SubId:x2}.");
    }

    private void ValidateTokayDimitriDeparture()
    {
        foreach(bool batched in new[]{false,true})
        {
            var (firstSlot,secondSlot)=BeginTokayDimitriTrade(batched);
            var first=(TokayCharacter)firstSlot.Npc;var second=(TokayCharacter)secondSlot.Npc;
            void Step(int n)=>StepGameplayUpdates(n,Vector2.Zero,batched:batched);
            FailIf(firstSlot.Script!.MovementCounter!=16||secondSlot.Script!.MovementCounter!=32||
                first.Position!=new Vector2(24,72)||second.Position!=new Vector2(56,88),
                "Closing TX_0a25 must initialize independent source moveleft counters without applying movement.");
            Step(15);
            FailIf(first.Position.X!=0xfa||second.Position.X!=0x1a||first.ScriptDrawOffset.X!=-256||
                first.CurrentAnimationFrame!=0||second.CurrentAnimationFrame!=0,"Source15/31 moves must preserve unsigned coordinates and the retained left-edge pose.");
            Step(1);FailIf(!first.Active||firstSlot.Script.MovementCounter!=0,"First Tokay must yield on counter2 zero before deleting.");
            Step(1);FailIf(first.Active||!second.Active||_runtimeState.ReadWramByte(0xcc02)!=0||
                _runtimeState.ReadWramByte(0xcc8a)!=0x81||(_saveData.ReadWramByte(AgesWramAddress.wDimitriState)&2)!=0,
                "First Tokay must enable menus and delete while its peer still owns the incomplete departure.");
            Step(14);FailIf(second.Position.X!=0xfa||second.ScriptDrawOffset.X!=-256,"Second Tokay must retain its final source pose after31 moves.");
            Step(1);FailIf(!second.Active||(_saveData.ReadWramByte(AgesWramAddress.wDimitriState)&2)!=0,"Second Tokay must yield on counter2 zero.");
            Step(1);FailIf(!second.Active||_runtimeState.ReadWramByte(0xcc8a)!=0||(_saveData.ReadWramByte(AgesWramAddress.wDimitriState)&2)==0,
                "Second Tokay must publish bit1 and enable objects before the source scriptend update.");
            Step(1);FailIf(second.Active,"Second Tokay scriptend must release its physical slot.");
            LoadValidationRoom(0,0xaa);Step(3);
            FailIf(_entities.Entities<TokayCharacter>().Any(a=>a.Record.SubId is 0x0f or 0x10&&a.Active),"Completed Tokays must remain deleted on re-entry.");

            BeginTokayDimitriTrade(batched);Step(4);LoadValidationRoom(4,0xa1);
            FailIf(_runtimeState.ReadWramByte(0xcc8a)!=0||(_saveData.ReadWramByte(AgesWramAddress.wDimitriState)&2)!=0||
                _entities.Entities<TokayRescueEmberRoomEntity>().Any(),"Cancellation must release raw control without completing the departure.");
            LoadValidationRoom(0,0xaa);Step(3);
            FailIf(_entities.Entities<TokayCharacter>().Count(a=>a.Record.SubId is 0x0f or 0x10&&a.Active)!=2,"Cancelled departure must re-enter with both Tokays.");
        }
    }

    private void ValidateTokayDimitriScrollEntry()
    {
        _saveData.WriteWramByte(AgesWramAddress.wDimitriState,0);_inventory.GiveTreasure(TreasureId.Essence,2);
        _saveData.SetRoomFlag(0,0xba,0x40);LoadValidationRoom(0,0xba);
        _player.WarpTo(new(24,4));_player.UpdatePushingState(Vector2.Up);CheckRoomExit(_player);
        FailIf(!_transitions.ScrollActive||_rooms.CurrentRoom.Id!=0xaa,"Post-D3 room0:$ba must scroll north into0:$aa.");
        for(int i=0;i<90&&_transitions.ScrollActive;i++)
        {
            StepGameplayUpdates(1,Vector2.Zero);
            FailIf(_transitions.ScrollActive&&(_dialogue.IsOpen||_runtimeState.ReadWramByte(0xcc8a)!=0),
                "Tokay$0f must retain its handler-local scroll gate before disableinput/showtext.");
        }
        for(int i=0;i<4&&!_dialogue.IsOpen;i++)StepGameplayUpdates(1,Vector2.Zero);
        FailIf(!_dialogue.IsOpen||_runtimeState.ReadWramByte(0xcc8a)!=0x81||_dialogue.Position.Y!=24||
            !_dialogue.CurrentMessage.Contains("This huge fish"),"First eligible post-scroll script must show top TX_0a1d.");
        _dialogue.Close();StepGameplayUpdates(1,Vector2.Zero);
        var second=_entities.EntityAdapters<TokayNpcRoomEntity>().Single(s=>s.Npc.Record.SubId==0x10);
        FailIf(second.Script!.NativeCounter1!=30,"Second Tokay must observe its peer's source signal in its own slot and install wait30.");
        StepGameplayUpdates(29,Vector2.Zero);FailIf(_dialogue.IsOpen,"Second Tokay opened TX_0a1e early.");
        StepGameplayUpdates(1,Vector2.Zero);
        FailIf(!_dialogue.IsOpen||_dialogue.Position.Y!=24||!_dialogue.CurrentMessage.Contains("Red fish"),"Second Tokay must present top TX_0a1e on wait30 zero.");
        _dialogue.Close();
        for(int i=0;i<4&&!_dialogue.IsOpen;i++)StepGameplayUpdates(1,Vector2.Zero);
        var dimitri=_entities.Entities<DimitriCompanionRoomEntity>().Single();
        // text.yaml: TX_2100 begins with explicit \\pos(2), unlike the
        // preceding Tokay messages which choose their position from Link Y.
        FailIf(!_dialogue.IsOpen||_dialogue.Position.Y!=96||dimitri.Position!=new Vector2(48,72)||dimitri.AnimationIndex!=0x24||
            _dialogue.CurrentMessage!=DialogueBox.PlainText(new DimitriDatabase().Text(0x2100)),
            $"The separate $71:$07 slot must observe var3e bit1 and present Dimitri's TX_2100: open{_dialogue.IsOpen}, pos{_dialogue.Position}, fish{dimitri.Position}/anim{dimitri.AnimationIndex:x2}, signal{CompanionRuntimeState.ReadScriptSignals(_runtimeState):x2}, text={_dialogue.CurrentMessage}.");
        _dialogue.Close();StepGameplayUpdates(1,Vector2.Zero);
        FailIf((_saveData.ReadWramByte(AgesWramAddress.wDimitriState)&1)==0,"Companion script must remember its completed introduction.");
    }

    private void ValidateTokayRescueEmberEffects()
    {
        foreach(bool batched in new[]{false,true})
        {
            BeginTokayDimitriTrade(batched,stopAtSeeds:true);
            var effects=_entities.Entities<TokayRescueEmberRoomEntity>().ToArray();
            bool Opaque(Texture2D t)=>t.GetImage().GetData().Where((_,i)=>(i&3)==3).Any(a=>a!=0);
            FailIf(effects.Length!=2||effects[0].Position!=new Vector2(24,72)||effects[1].Position!=new Vector2(56,88)||
                effects[0].ZFixed!=-256||effects[1].ZFixed!=0||effects.Any(e=>e.Phase!=1||!e.Visible||!Opaque(e.CurrentTexture)),
                "The two $8f births must retain their source order, independent updates and one-update stagger.");
            void Step(int n)=>StepGameplayUpdates(n,Vector2.Zero,batched:batched);
            Step(16);FailIf(effects[0].ZFixed!=-2176||effects[1].ZFixed!=-2176,"Both source seed arcs must reach their discrete -$0880 apex.");
            for(int i=0;i<50&&!_dialogue.IsOpen;i++)Step(1);
            FailIf(!_dialogue.IsOpen,"Ember test must reach TX_0a24.");
            Step(4);FailIf(effects.Any(e=>e.Phase!=2||e.Visible||e.ZFixed!=0),"Always-update seeds must land and observe text while normal interactions freeze.");
            Step(10);FailIf(effects.Any(e=>e.Phase!=2),"Flames must wait for active text to close.");
            _dialogue.Close();Step(1);
            FailIf(effects.Any(e=>e.Phase!=3||e.Counter!=58||!e.Visible||!Opaque(e.CurrentTexture)),"Source flame setup must install counter58 without consuming its first decrement.");
            byte[] first=effects[0].CurrentTexture.GetImage().GetData();bool animated=false;
            _dialogue.ShowMessage("Ember always-update hold.",104);_runtimeState.SetWramByte(0xcc8a,0x83);
            for(int i=0;i<57;i++){Step(1);animated|=!first.SequenceEqual(effects[0].CurrentTexture.GetImage().GetData());}
            FailIf(!animated||effects.Any(e=>e.Counter!=1||e.Finished),"Flames must animate under text and DISABLE_INTERACTIONS through update57.");
            Step(1);FailIf(effects.Any(e=>!e.Finished)||_entities.Entities<TokayRescueEmberRoomEntity>().Any(),"The58th flame update must delete and release both source slots.");
            _dialogue.Close();LoadValidationRoom(4,0xa1);
        }
    }

    private void ValidateDimitriCompanion()
    {
        void StepInput(string action, Vector2 movement = default, int frames = 1)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                Input.BeginOriginalUpdate(new ApplicationInputSnapshot([action], frame == 0 ? [action] : [], movement));
                try { StepRoomEventFrames(1); }
                finally { Input.EndOriginalUpdate(); }
            }
        }
        CompanionRuntimeState.Clear(_runtimeState, CompanionRuntimeState.Read(_runtimeState).Id);
        CompanionRuntimeState.ForgetRemembered(_runtimeState);
        _inventory.GiveTreasure(TreasureId.Essence, 2);
        _saveData.WriteWramByte(AgesWramAddress.wDimitriState, 3);
        LoadValidationRoom(0, 0xaa);
        StepGameplayUpdates(4, Vector2.Zero);
        var dimitri = _entities.Entities<DimitriCompanionRoomEntity>().Single();
        var database = new DimitriDatabase();
        FailIf(database.Animations.Length != 40 || !database.CanSwallow(0x10) ||
            database.CanSwallow(0x13), "Dimitri source animations or mouth collision modes are incorrect.");
        _player.WarpTo(dimitri.Position + new Vector2(0, 16));
        _player.Face(Vector2I.Up);
        StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
        for (int frame = 0; frame < 4 && !_dialogue.IsOpen; frame++) StepGameplayUpdates(1, Vector2.Zero);
        FailIf(!_dialogue.IsOpen ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(database.Text(0x2101)),
            "Rescued Dimitri did not offer his first-meeting TX_2101 through $71:$07.");
        _dialogue.Close();
        for (int frame = 0; frame < 180 && !_dialogue.IsOpen; frame++)
        {
            _player._PhysicsProcess(1.0 / 60.0);
            StepRoomEventFrames(1);
        }
        FailIf(!_dialogue.IsOpen || !dimitri.LinkRiding || !_player.CompanionRideActive ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(database.Text(0x2106)) ||
            (_saveData.ReadWramByte(AgesWramAddress.wDimitriState) & 0x20) != 0,
            $"Dimitri rescue failed its mount/tutorial gate: {dimitri.Phase}, Link={_player.Position}.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf((_saveData.ReadWramByte(AgesWramAddress.wDimitriState) & 0x20) == 0 ||
            !CompanionRuntimeState.IsActive(_runtimeState, SpecialObjectId.Dimitri) ||
            _player.Position != dimitri.Position + new Vector2(0, -10),
            "Dimitri tutorial completion did not retain the companion slot and source Link offset.");

        Vector2 landPosition = dimitri.PrecisePosition;
        dimitri.SetScreenTransitionPosition(new Vector2(64, 16), Vector2.Zero, _player);
        StepRoomEventFrames(1);
        FailIf(!dimitri.InWater || dimitri.AnimationIndex != 7,
            "Dimitri did not select the retained-angle $ff left-facing swimming animation in room 0:aa's sea.");
        StepInput("item");
        FailIf(!dimitri.LinkRiding || dimitri.Phase != DimitriPhase.Riding,
            "Dimitri accepted a forbidden B dismount while swimming.");
        StepInput("move_right", Vector2.Right);
        FailIf(dimitri.PrecisePosition != new Vector2(64, 16),
            "Dimitri moved on the source angle-change update.");
        StepInput("move_right", Vector2.Right, 4);
        FailIf(dimitri.PrecisePosition != new Vector2(68, 16) ||
            _player.Position != dimitri.Position + new Vector2(-5, -10),
            $"Dimitri did not swim at SPEED_100 or use the right-facing Link offset: position={dimitri.PrecisePosition}, Link={_player.Position}, direction={dimitri.Direction}, phase={dimitri.Phase}, dialogue={_dialogue.IsOpen}, solid={_rooms.CurrentRoom.IsSolid(new Vector2(65, 24))}, tile={_rooms.CurrentRoom.GetMetatile(new Vector2(65, 24)):x2}.");
        dimitri.SetScreenTransitionPosition(landPosition, Vector2.Zero, _player);
        StepRoomEventFrames(1);
        FailIf(dimitri.InWater, "Dimitri did not return to his ground animation on leaving water.");

        StepInput("attack");
        FailIf(dimitri.Phase != DimitriPhase.Eating ||
            !_entities.Entities<DimitriMouthRoomEntity>().Any(),
            "Dimitri A did not create ITEM_DIMITRI_MOUTH $2b.");
        for (int frame = 0; frame < 80 && dimitri.Phase == DimitriPhase.Eating; frame++)
            StepRoomEventFrames(1);
        FailIf(dimitri.Phase != DimitriPhase.Riding || _entities.Entities<DimitriMouthRoomEntity>().Any(),
            "Dimitri bite did not retract and release its 12-update mouth item.");
        dimitri.SetScreenTransitionPosition(landPosition + new Vector2(-8, 0), Vector2.Zero, _player);
        StepRoomEventFrames(1);
        StepInput("item");
        for (int frame = 0; frame < 120 && dimitri.Phase == DimitriPhase.Dismounting; frame++)
        {
            _player._PhysicsProcess(1.0 / 60.0);
            StepRoomEventFrames(1);
        }
        FailIf(_player.CompanionRideActive || !CompanionRuntimeState.TryGetRemembered(
            _runtimeState, SpecialObjectId.Dimitri, 0, 0xaa, out _), "Dimitri land dismount lost the remembered companion.");
        Vector2 rememberedPosition = dimitri.Position;
        LoadValidationRoom(0, 0xaa);
        dimitri = _entities.Entities<DimitriCompanionRoomEntity>().Single();
        FailIf(dimitri.Phase != DimitriPhase.Waiting || dimitri.Position != rememberedPosition,
            "Dimitri preset did not preserve the remembered companion after wDimitriState bit $20.");
        _saveData.WriteWramByte(AgesWramAddress.wDimitriState, 0x63);
        CompanionRuntimeState.ForgetRemembered(_runtimeState);
        LoadValidationRoom(0, 0xaa);
        FailIf(_entities.Entities<DimitriCompanionRoomEntity>().Any(),
            "Dimitri preset ignored wDimitriState bit $40.");
        _saveData.WriteWramByte(AgesWramAddress.wDimitriState, 0x23);
        OracleRoomData mainland = _world.LoadRoom(0, 0x98);
        Vector2 landing = (from y in Enumerable.Range(1, 6)
            from x in Enumerable.Range(1, 8).Reverse()
            select new Vector2(x * 16, y * 16))
            .First(point => mainland.GetTerrainInfo(point + new Vector2(0, 5)).Hazard == HazardType.None &&
                Enumerable.Range((int)point.X - 5, mainland.Width - (int)point.X + 5).All(x =>
                    !mainland.IsSolid(new Vector2(x, point.Y - 5)) &&
                    !mainland.IsSolid(new Vector2(x, point.Y + 8))));
        CompanionRuntimeState.BeginAnimalRide(_runtimeState, _saveData, SpecialObjectId.Dimitri, 0x98, landing, ObjectDirection.Right);
        LoadValidationRoom(0, 0x98);
        dimitri = _entities.Entities<DimitriCompanionRoomEntity>().Single();
        for (int frame = 0; frame < 180 && !_dialogue.IsOpen; frame++)
        {
            _player._PhysicsProcess(1.0 / 60.0);
            StepRoomEventFrames(1);
        }
        FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != DialogueBox.PlainText(database.Text(0x2104)) ||
            _player.CompanionRideActive || CompanionRuntimeState.ReadRemembered(_runtimeState).Id != 0,
            $"Mainland $71:$06 failed to dismount/forget Dimitri before TX_2104: {dimitri.Phase}.");
        _dialogue.Close();
        for (int frame = 0; frame < 240 && _entities.Entities<DimitriCompanionRoomEntity>().Any(); frame++)
            StepRoomEventFrames(1);
        FailIf(_entities.Entities<DimitriCompanionRoomEntity>().Any(),
            $"Dimitri's mainland departure did not finish walking offscreen: {dimitri.Phase}, {dimitri.Position}, landing={landing}.");
        GD.Print("Validated Dimitri source visuals, rescue mount/tutorial, eating item, land dismount, and preset re-entry predicates.");
    }
}
