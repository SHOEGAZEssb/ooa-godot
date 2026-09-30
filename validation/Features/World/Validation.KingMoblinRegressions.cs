using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateKingMoblinDefeatTiming()
    {
        foreach(bool batch in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            void Step(int count=1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch);
            LoadValidationRoom(2,0xaf); _player.WarpTo(new Vector2(24,88)); Step();
            var boss=_entities.Entities<KingMoblinBoss>().Single();
            // Arrange zero health with a closed source collision gate. The
            // @dead return must prevent even state8's animation/intro dispatch.
            _player.SetBraceletLiftCollisionsDisabled(true);
            boss.Health=0; boss.InvincibilityCounter=3;
            Vector2 start=boss.Position; int animationFrame=boss.AnimationFrame;
            Step(4);
            FailIf(boss.State!=8 || boss.Health!=0 || boss.Position!=start ||
                boss.InvincibilityCounter!=0 || boss.AnimationFrame!=animationFrame,
                "King Moblin @dead must wait for Link collisions without dispatching its attack state.");
            _player.SetBraceletLiftCollisionsDisabled(false);
            Step(6);
            FailIf(boss.State!=0x12 || boss.Position.Y!=14 || !_entities.PlayerMovementDisabled,
                "King Moblin death must move upward six SPEED_300 updates to Y=$0e.");
            Step();
            FailIf(boss.State!=0x13 || boss.Position.Y!=11 || _entities.ScreenShakeCounter!=59,
                "King Moblin death must enter bounce/shake on update seven at Y=$0b.");
            // Let actual minion attack states observe the shared shake, then
            // inspect their escape hop's source fallthrough, marks and signal.
            // This fixture starts before the fight: initialize each through
            // the same state2 entry written by kingMoblin_stateA.
            foreach(var minion in boss.Minions) minion!.StartFight();
            var hopped=new HashSet<int>();
            for(int i=0;i<650 && boss.State!=0x15;i++)
            {
                int[] states=boss.Minions.Select(m=>m!.State).ToArray();
                Step();
                for(int index=0;index<2;index++)
                {
                    var minion=boss.Minions[index]!;
                    if(states[index]==7 && minion.State==8)
                        FailIf(((ITerrainShadowSource)minion.Bomb!).TerrainShadowZHigh is not null,
                            "Minion $56 state7 writes bomb visible=$81, disabling its terrain shadow for the escape throw.");
                    if(states[index]==8 && minion.State==9)
                    {
                        hopped.Add(index);
                        FailIf(minion.ZFixed!=-0x140,
                            $"Minion $56:{index:x2} state8 must fall through and integrate its hop on the exclamation update.");
                        FailIf(!_entities.Entities<NpcCharacter>().Any(n=>n.Record.Id==0x9f && n.ZIndex==12),
                            "Minion escape exclamation $9f must use visible80 above the actors.");
                    }
                }
            }
            FailIf(boss.State!=0x15 || boss.Counter!=98 || hopped.Count!=2,
                "King Moblin defeat must receive minion escape and begin the 98-update bomb chain.");
            FailIf(!boss.Data.Bytes("defeat-warp").SequenceEqual(new byte[]{0x80,0x09,0x00,0x45,0x03}),
                "kingMoblin_state15@warpDest must preserve all five source warp bytes.");
            int[] x=[0x48,0x58,0x38,0x68];
            for(int explosion=0;explosion<4;explosion++)
            {
                Step(explosion==0?1:32);
                FailIf(boss.Counter!=97-explosion*32 ||
                    !_entities.Entities<InteractionExplosionEffect>().Any(e=>e.Position==new Vector2(x[explosion],8)) ||
                    _currentRoom.GetMetatile(new Vector2(x[explosion],8))!=0xa1 ||
                    _saveData.HasGlobalFlag(GlobalFlag.MoblinsKeepDestroyed),
                    $"King Moblin chain explosion {explosion} must spawn at X=${x[explosion]:x2}, clear its tile, and defer completion.");
            }
            Step();
            FailIf(!IsTransitioning || _currentRoom.Group!=2 || boss.IsDead ||
                !_saveData.HasGlobalFlag(GlobalFlag.MoblinsKeepDestroyed),
                "King Moblin final counter update must request a fade while retaining its outgoing sprite.");

            // Exercise state14's source fallback with no minion escape signal.
            // Clearing the collision bit and writing wDisabledObjects happen
            // at @dead, not on every subsequent state dispatch.
            ReinitializeGameplayForValidation();
            LoadValidationRoom(2,0xaf); _player.WarpTo(new Vector2(24,88)); Step();
            boss=_entities.Entities<KingMoblinBoss>().Single(); boss.Health=0;
            for(int i=0;i<120 && boss.State!=0x14;i++) Step();
            FailIf(boss.State!=0x14 || boss.Counter!=150,
                "King Moblin bounce must enter state14 with 150 half-rate updates.");
            Step(298);
            FailIf(boss.State!=0x14 || boss.Counter!=1,
                "King Moblin state14 decremented its 150-update counter outside even frames.");
            Step(2);
            FailIf(boss.State!=0x0b || !boss.ControlsDisabled || boss.CollisionEnabled ||
                !_entities.PlayerMovementDisabled,
                "King Moblin state14 timeout must retain the @dead collision clear and input/menu masks.");
            LoadValidationRoom(2,0xae); Step();
            FailIf(_entities.PlayerMovementDisabled,"Cancelling King Moblin's death fallback retained the room's input mask.");
        }
    }

    private void ValidateKingMoblinBombsAndRecentering()
    {
        void Step(Vector2 movement=default)
        {
            _inventory.RefillHealth();
            Application.Capture([],[],movement);Application.Advance(1.0/60);
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
        bool bigShadow=false,smallShadow=false,minionShadow=false;
        void CheckBombs()
        {
            minionShadow|=boss.Minions.Any(minion=>minion!.TerrainShadowDrawn);
            foreach(var bomb in _entities.Entities<KingMoblinBomb>())
            {
                if(bomb.TerrainShadowDrawn)
                {
                    if(bomb.Minion is null) bigShadow=true; else smallShadow=true;
                }
                // parts/kingMoblinBomb.s state0 uses visiblec2; parts/bomb.s
                // state0 uses visiblec1. Neither ballistic state changes it.
                if(bomb.State is 1 or 3 or 4 && bomb.Minion is null ||
                    bomb.Minion is not null && bomb.State is 1 or 2)
                    FailIf(bomb.ZIndex!=(bomb.Minion is null?9:11),
                        $"PART ${(bomb.Minion is null?0x3f:0x47):x2} state ${bomb.State:x2} lost its native spawn/flight priority: {bomb.ZIndex}.");
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
                FailIf(((ITerrainShadowSource)bomb).TerrainShadowZHigh is not null || bomb.TerrainShadowDrawn,
                    "PART $3f/$47 explosions must clear visible bit6 and suppress their airborne terrain shadow.");
                int frame=bomb.AnimationFrame;
                FailIf(frame>6 || OracleGraphicsCache.PixelHash(bomb.CurrentAnimationTexture.GetImage())!=hashes[Math.Min(frame,5)],
                    $"PART ${(bomb.Minion is null?0x3f:0x47):x2} explosion frame {frame} did not use source common-sprites OAM/palette.");
                FailIf(bomb.ZIndex!=(bomb.Minion is null?ObjectDrawPriority.BehindLinkZIndex:ObjectDrawPriority.FixedLowPriorityZIndex),
                    "PART $3f/$47 explosion lost objectSetVisible82/83 priority.");
                if(bomb.Minion is null)blastFrames.Add(frame);
            }
        }
        FailIf(boss.ZIndex!=8 || boss.Minions.Any(minion=>minion!.ZIndex!=9),
            "King Moblin $7f/$56 must use visible83/c2 so PART $3f/$47 paints above its thrower.");
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
        FailIf(!bigShadow || !smallShadow || !minionShadow,
            "PART $3f/$47 and jumping ENEMY $56 must draw native terrain shadows during the fight.");
        GD.Print("Validated $3f/$47 explosion pixels and draw priority, repeated minion ballistic throws, and King Moblin recentering from both sides through the application loop.");
    }
}
