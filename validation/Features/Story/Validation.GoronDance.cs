using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGoronDance()
    {
        RunGoronDance(0xed,false);
        ReinitializeGameplayForValidation();
        _saveData.SetLinkedGame(true);
        RunGoronDance(0xef,true);
    }
    private void RunGoronDance(int room,bool batched)
    {
        LoadValidationRoom(2,room);
        StepGameplayUpdates(4,Vector2.Zero);
        var cave=_roomEvents.Get<GoronCaveEvent>();
        var host=cave.Actors.Single(a=>a.Actor.Record.SubId==0);
        FailIf(cave.Actors.Count!=8||host.Dance is null,"Graceful Goron did not create seven ordered support dancers.");
        FailIf(cave.Actors.Where(a=>a.Actor.Record.SubId==1).Any(a=>a.Actor.Record.Id!=(batched?InteractionId.Subrosian:InteractionId.Goron)),
            $"Linked past dance hall did not replace its seven supporting Gorons with Subrosians: room {room:x2}, flags {_rooms.CurrentRoom.TilesetFlags:x2}, linked {_saveData.IsLinkedGame}.");
        foreach(var dancer in cave.Actors.Where(a=>a.Actor.Record.SubId==1).ToArray()) TalkGoronFromFloor(dancer);
        _inventory.AddRupees(100);
        int before=_inventory.Rupees;
        ApproachGoronFromFloor(host);
        StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        for(int i=0;i<800&&host.Dance!.State==1;i++)
        {
            if(_dialogue.IsOpen)
            { if(_dialogue.ChoiceActive) _dialogue.SubmitChoiceForValidation(0); else _dialogue.Close(); }
            StepGameplayUpdates(1,Vector2.Zero);
        }
        bool past=(_rooms.CurrentRoom.TilesetFlags&0x80)!=0;
        FailIf(host.Dance!.State!=2||host.Dance.Counter!=29||_inventory.Rupees!=before-(past?20:10),
            "Goron dance fee, script handoff, or initial 30-update counter diverged.");
        var wram=_entities.RuntimeState;
        int[] patterns=cave.Database.Bytes(past?"dance-silver":"dance-bronze");
        int[] firstSourcePattern=past?[2,2,2,0,2,2,1,255]:[2,2,1,255];
        FailIf(patterns.Length!=160||!patterns.Take(firstSourcePattern.Length).SequenceEqual(firstSourcePattern),
            "Dance source patterns lost their 10 x 16 shape or source-derived first pattern.");
        int guard=0, poseUpdates=0, playerPoseUpdates=0;
        var linkPoseTexture=typeof(Player).GetMethod("ScriptedLinkAnimationTexture",
            System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        while(!_inventory.HasTreasure(TreasureId.BrotherEmblem)&&guard++<6000)
        {
            if(_dialogue.IsOpen)
            { if(_dialogue.ChoiceActive) _dialogue.SubmitChoiceForValidation(0); else _dialogue.Close(); }
            string[] buttons=[];
            if(host.Dance.State==3&&host.Dance.Substate==1)
            {
                int beat=wram.ReadWramByte(0xcfdc),time=wram.ReadWramByte(0xcfd5)|(wram.ReadWramByte(0xcfd6)<<8);
                int move=patterns[wram.ReadWramByte(0xcfdf)*16+beat];
                if(wram.ReadWramByte(0xcfd4)==0||time+1==beat*20)
                    buttons=move==1?["attack"]:move==2?["item"]:[];
            }
            int updates=batched&&host.Dance.State==2&&host.Dance.Counter>2?2:1;
            StepGameplayUpdates(updates,Vector2.Zero,buttons,buttons,batched:batched);
            if(host.Dance.State==3&&wram.ReadWramByte(0xcfd4)!=0&&wram.ReadWramByte(0xcfd2)==6)
            {
                playerPoseUpdates++;
                foreach(var dancer in cave.Actors.Where(a=>a.Actor.Record.SubId==1))
                    // Goron tiles $20/$22 have 191 nonzero source pixels;
                    // Subrosian tiles $1c/$1e have 224.
                    FailIf(!dancer.Actor.Visible||dancer.Actor.CurrentAnimationOpaquePixels!=(batched?224:191),
                        $"Supporting dancer ${dancer.Actor.Record.Id:x2}:$01 lost its A-pose pixels in room $02:{room:x2}: {dancer.Actor.CurrentAnimationOpaquePixels}.");
                FailIf(_player.ScriptedLinkAnimationMode!=0x08||!_player.Visible,
                    "Goron dance A input did not select Link's visible DANCELEFT pose $08.");
                using Image linkPose=((Texture2D)linkPoseTexture.Invoke(_player,[0x08,false])!).GetImage();
                int opaque=0;
                for(int y=0;y<linkPose.GetHeight();y++)
                for(int x=0;x<linkPose.GetWidth();x++)
                    if(linkPose.GetPixel(x,y).A>0.1f) opaque++;
                // specialObjectAnimationData.s frame $1d: spr_link+$0d60,
                // OAM $00, two cells with 184 nonzero source pixels.
                FailIf(linkPose.GetSize()!=new Vector2I(16,16)||opaque!=184,
                    $"Link DANCELEFT $08 lost its source graphics: {opaque} opaque pixels.");
            }
            // interactionAnimation5a3e4 selects OAM tiles $20/$22. The first
            // two cells of source spr_gorondance_tingle_write contain 191
            // nonzero pixels, at (-8,-8), independent of the imported table.
            if(host.Actor.CurrentScriptAnimationSource=="127@8,0,32,0;8,8,34,0")
            {
                poseUpdates++;
                FailIf(!host.Actor.Visible||host.Actor.CurrentAnimationOpaquePixels!=191||
                    host.Actor.CurrentAnimationTextureSize!=new Vector2I(16,16)||
                    host.Actor.CurrentAnimationOffset!=new Vector2(-8,-8),
                    $"Graceful Goron $66:$00 A pose lost its $2b graphics in room $02:{room:x2}.");
            }
        }
        FailIf(poseUpdates==0,"Goron dance never exercised source A-pose animation $06.");
        FailIf(playerPoseUpdates==0,"Goron dance never exercised Link and supporting dancers' A poses.");
        FailIf(!_inventory.HasTreasure(TreasureId.BrotherEmblem)||wram.ReadWramByte(0xcfdb)!=0,
            $"Goron dance did not award Brother's Emblem after eight perfect rounds: state {host.Dance.State}/{host.Dance.Substate}, failures {wram.ReadWramByte(0xcfdb)}, beat {wram.ReadWramByte(0xcfdc)}.");
        for(int i=0;i<180;i++)
        { if(_dialogue.IsOpen) _dialogue.Close(); StepGameplayUpdates(1,Vector2.Zero); }
        FailIf(cave.BlocksGameplay||_player.CutsceneControlled||wram.ReadWramByte(0xcfda)!=0,
            "Goron dance completion did not clear native counters and release Link.");
        ApproachGoronFromFloor(host);
        StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        for(int i=0;i<10&&!_dialogue.IsOpen;i++) StepGameplayUpdates(1,Vector2.Zero);
        FailIf(!_dialogue.IsOpen,"Goron dance could not be repeated after completion.");
        for(int i=0;i<800&&host.Dance.State==1;i++) AdvanceGoronDialogue(1);
        for(int failure=0;failure<3;failure++)
        {
            for(int i=0;i<900&&!(host.Dance.State==3&&host.Dance.Substate==1);i++) AdvanceGoronDialogue(1);
            FailIf(host.Dance.State!=3||host.Dance.Substate!=1,"Repeat dance never reached its input window.");
            int[] currentPattern=cave.Database.Bytes("dance-"+new[]{"platinum","gold","silver","bronze"}[wram.ReadWramByte(0xcfdd)]);
            int move=currentPattern[wram.ReadWramByte(0xcfdf)*16];
            string[] wrong=move==1?["item"]:["attack"];
            StepGameplayUpdates(1,Vector2.Zero,wrong,wrong);
            FailIf(host.Dance.Substate!=4||host.Dance.Counter!=30||wram.ReadWramByte(0xcfd1)!=2,
                "Wrong dance move did not select failure $02 and the 30-update delay.");
            StepGameplayUpdates(29,Vector2.Zero,batched:batched);
            FailIf(wram.ReadWramByte(0xcfdb)!=failure,"Dance failure counted before update 30.");
            StepGameplayUpdates(1,Vector2.Zero);
            FailIf(wram.ReadWramByte(0xcfdb)!=failure+1,"Dance failure did not count on update 30.");
        }
        AdvanceGoronDialogue(220,1);
        FailIf(host.Dance.State!=1||_player.CutsceneControlled||wram.ReadWramByte(0xcfdb)!=0,
            "Third dance failure and declined retry did not clear the game and release Link.");
        _dialogue.Close(); cave.Cancel();
        FailIf(_player.CutsceneControlled,"Goron dance cancellation retained input ownership.");
    }
}
