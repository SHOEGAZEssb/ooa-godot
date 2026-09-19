using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateKingMoblinBombsAndRecentering()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var input=(ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler=(ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update=(Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        void Step(Vector2 movement=default)
        {
            _inventory.RefillHealth();
            input.CaptureForValidation([],[],movement);scheduler.Advance(1.0/60,update);
        }
        LoadValidationRoom(2,0xaf);_player.WarpTo(new Vector2(24,88));Step();
        var boss=_entities.Entities<KingMoblinBoss>().Single();
        for(int i=0;!_dialogue.IsOpen && i<150;i++)Step(boss.State==8?Vector2.Right:Vector2.Zero);
        FailIf(!_dialogue.IsOpen,"King Moblin regression could not approach the intro through room 2:af.");
        _dialogue.Close();Step();

        // Independent OAM from partOamData5334a/53353/53856/53877/53898.
        string spark="8,0,12,0;8,8,12,32";
        string[] oam=[spark,"8,0,12,7;8,8,12,39",spark,
            "2,250,12,0;2,2,12,32;2,6,12,0;2,14,12,32;10,250,12,0;10,2,12,32;10,6,12,0;10,14,12,32",
            "0,248,0,0;0,0,2,0;0,8,2,32;0,16,0,32;16,248,0,64;16,0,2,64;16,8,2,96;16,16,0,96",
            "0,248,14,0;0,0,14,32;0,8,14,0;0,16,14,32;16,248,14,0;16,0,14,32;16,8,14,0;16,16,14,32"];
        var source=OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/spr_common_sprites.png");
        ulong[] hashes=oam.Select(cells=>OracleGraphicsCache.PixelHash(
            NpcCharacter.BuildPositionedOamTexture(source,cells,0x0c,2,null,true,0).Texture.GetImage())).ToArray();
        var flightUpdates=new Dictionary<KingMoblinBomb,int>();
        int[] launches=new int[2];
        var blastFrames=new HashSet<int>();
        void CheckBombs()
        {
            foreach(var bomb in _entities.Entities<KingMoblinBomb>())
            {
                if(bomb.Minion is not null && bomb.State==2)
                {
                    int age=flightUpdates.GetValueOrDefault(bomb);flightUpdates[bomb]=age+1;
                    if(age==0)launches[bomb.Minion.SubId]++;
                    // Minion apex: its previous copied Z is -$9a0; the
                    // part then adds -$280, -$260 in its first two updates.
                    if(age<2)FailIf(bomb.ZFixed!=(age==0?-0xc20:-0xe80) || bomb.Held || bomb.Position.Y<=8,
                        $"PART $47 minion {bomb.Minion.SubId} throw update {age}: Z=${bomb.ZFixed:x}, position={bomb.Position}; reserved bracelet motion must not touch this part.");
                }
                if(bomb.State!=(bomb.Minion is null?5:3))continue;
                int frame=bomb.AnimationFrame;
                FailIf(frame>6 || OracleGraphicsCache.PixelHash(bomb.CurrentAnimationTexture.GetImage())!=hashes[Math.Min(frame,5)],
                    $"PART ${(bomb.Minion is null?0x3f:0x47):x2} explosion frame {frame} did not use source common-sprites OAM/palette.");
                FailIf(bomb.ZIndex!=(bomb.Minion is null?NpcCharacter.BehindLinkZIndex:NpcCharacter.FixedLowPriorityZIndex),
                    "PART $3f/$47 explosion lost objectSetVisible82/83 priority.");
                if(bomb.Minion is null)blastFrames.Add(frame);
            }
        }
        foreach(int startX in new[]{0x30,0x70})
        {
            // Arrange the legitimate off-centre positions reached while
            // chasing returned bombs; let the real bomb expire and dispatch F.
            boss.Position=new Vector2(startX,32);
            bool sawReturn=false,centred=false;float previousX=startX;
            for(int i=0;i<1000;i++)
            {
                Step();CheckBombs();
                if(boss.State==0x10)sawReturn=true;
                if(sawReturn)
                {
                    FailIf(boss.Position.X<0x30 || boss.Position.X>0x70 ||
                        startX<0x4e && boss.Position.X<previousX || startX>0x52 && boss.Position.X>previousX,
                        $"King Moblin must return toward centre from ${startX:x2}, got {previousX}->{boss.Position.X}.");
                    previousX=boss.Position.X;
                    if(boss.State==0x0b){centred=true;break;}
                }
            }
            FailIf(!centred || boss.Position.X<0x4e || boss.Position.X>=0x53 || boss.Counter!=30,
                $"King Moblin failed to recenter from ${startX:x2} and resume its 30-update attack wait.");
        }
        FailIf(launches.Any(count=>count<2),$"Both minions must repeatedly throw: {string.Join(',',launches)} launches.");
        FailIf(!Enumerable.Range(0,7).All(blastFrames.Contains),"The big-bomb regression did not observe every explosion frame through deletion.");
        GD.Print("Validated $3f/$47 explosion pixels and draw priority, repeated minion ballistic throws, and King Moblin recentering from both sides through the application loop.");
    }
}
