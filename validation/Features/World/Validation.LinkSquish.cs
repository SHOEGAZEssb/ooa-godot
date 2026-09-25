using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateLinkSquish()
    {
        var data = LinkSquishDatabase.Shared;
        var source = OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/spr_link.png");
        foreach (bool vertical in new[] { false,true })
        {
            var frames = data.Frames(vertical);
            FailIf(frames.Count != 3 || frames[0].Duration != 45 || frames[1].Duration != 4 || frames[2].Duration != 4 ||
                frames[0].Graphic != (vertical ? 0x33 : 0x32) || frames[1].Graphic != 2 || frames[2].Graphic != frames[0].Graphic ||
                frames[0].Parameter != 0 || frames[1].Parameter != 0xff || frames[2].Parameter != 0 ||
                frames[0].Next != 1 || frames[1].Next != 2 || frames[2].Next != 1 || data.FlickerCount != 20,
                "Link squish lost source animation $2d/$04/$04, terminal parameter or loop target.");
            // Independent specialObject00GfxPointers/OAM transcription.
            var shape = NpcCharacter.BuildOamTexture(source,vertical ? "8,0,0,0;8,8,0,32" : "0,4,0,0;16,4,2,0",
                vertical ? 0x3c : 0xce,0);
            var standing = NpcCharacter.BuildOamTexture(source,"8,0,0,0;8,8,2,0",0x20,0);
            FailIf(!frames[0].Texture.GetImage().GetData().SequenceEqual(shape.GetImage().GetData()) ||
                !frames[1].Texture.GetImage().GetData().SequenceEqual(standing.GetImage().GetData()),
                "Squish graphics must retain source OAM and the alternating graphic $02.");
            foreach (int parity in new[] { 0,1 })
            {
                var animation = new LinkSquishAnimation(vertical);
                int terminal = parity == 0 ? 83 : 84;
                for (int tick = 1; tick <= terminal; tick++)
                {
                    animation.Advance(tick-1+parity);
                    if (tick == 1) FailIf(animation.AnimationCounter != 44,"Squish initialization must fall through to its first animation decrement.");
                    if (tick == 44) FailIf(animation.State != 1 || animation.AnimationCounter != 1,"Squish initial frame must last through update44.");
                    if (tick == 45) FailIf(animation.State != 2 || animation.Frame != 1 || animation.AnimationCounter != 3 ||
                        animation.FlickerCounter != (parity == 0 ? 19 : 20),
                        "Terminal squish update must animate twice and decrement flicker only if visible.");
                    if (tick == 48) FailIf(animation.Frame != 2 || animation.AnimationCounter != 4,"Squish loop must switch back to its shape after the terminal frame.");
                    if (tick == 52) FailIf(animation.Frame != 1 || animation.AnimationCounter != 4,"Squish animation must loop to graphic $02, not its initial 45-update frame.");
                    FailIf(animation.Finished != (tick == terminal),"Squish must end exactly on its twentieth visible update.");
                }
            }
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batch in new[] { false,true })
        foreach (bool vertical in new[] { false,true })
        foreach (bool sideView in new[] { false,true })
        {
            void Step(int count = 1) =>
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
            LoadValidationRoom(sideView ? 6 : 4,sideView ? 0x95 : 0x9b);
            Vector2 origin = sideView ? new(40,40) : new(136,136);
            Vector2 destination = sideView ? new(56,40) : new(136,104);
            Vector2 fraction = vertical ? new(255/256.0f,1/256.0f) : new(1/256.0f,255/256.0f);
            _player.WarpTo(origin+fraction);
            _player.SetLocalRespawnPosition(destination+new Vector2(0.5f,0.5f),Vector2I.Left);
            FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.IsSolid(_player.LocalRespawnPosition),
                "Squish fixture positions must be outside solid room geometry.");
            _sound.ClearPlayRequestAudit();
            var cane = _entities.Somaria!;
            cane.Begin(_player,underwater:false);
            _player.ForceSideScrollSquish(vertical);
            Step();
            FailIf(!cane.Active || cane.Weapon is null,
                "Consuming a squish request must not clear item parents before state$11 initialization.");
            FailIf(_player.SquishAnimation is not { State:0 } || _sound.PlayRequestsFor(OracleSoundEngine.SndDamageEnemy) != 0,
                "Forced-state consumption must return before squish initialization and sound.");
            Step();
            FailIf(cane.Active || cane.Weapon is not null,
                "Squish initialization must clear the Cane parent; its ITEM$04 retires in the post pass.");
            var live = _player.SquishAnimation!;
            FailIf(live.State != 1 || live.AnimationCounter != 44 || _player.PatchCollisionsEnabled ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndDamageEnemy) != 1,
                "Squish initialization must disable collisions, sound once and animate immediately.");
            Step(43);
            FailIf(live.AnimationCounter != 1 || live.State != 1,"Live squish lost its initial-frame boundary.");
            Step();
            FailIf(live.State != 2 || live.Frame != 1 || live.AnimationCounter != 3,
                "Live terminal update must enter the imported flicker loop.");
            for (int i = 0; !live.Finished && i < 40; i++) Step();
            FailIf(!live.Finished || _player.Position != origin,
                "Finishing squish must select respawning without executing its coordinate copy yet.");
            // Seed the fields immediately before the source coordinate-copy
            // dispatch. This checks its writes without constructing an enemy
            // encounter or requiring dungeon progression.
            typeof(Player).GetField("_topDownAirZFixed",flags)!.SetValue(_player,-0x180);
            typeof(Player).GetField("_enemyKnockbackFrames",flags)!.SetValue(_player,7.0f);
            Step();
            FailIf(_player.SquishAnimation is not null || _player.Position != destination || _player.Visible,
                "The following Link dispatch must perform instant-respawn initialization.");
            FailIf(_player.PrecisePosition != destination+fraction || _player.FacingVector != Vector2I.Left ||
                _player.SwitchHookZFixed != 0 ||
                (float)typeof(Player).GetField("_enemyKnockbackFrames",flags)!.GetValue(_player)! != 0.0f,
                "State$02 respawn must copy only yh/xh, restore local direction, and clear both Z bytes and knockback.");
            LoadValidationRoom(6,0x95);
            _player.WarpTo(new(40,40));
            FailIf(!_player.PatchCollisionsEnabled,
                "Room load must release the collision lock inherited by an interrupted squish respawn.");
            _player.ForceSideScrollSquish(vertical);
            Step(2);
            FailIf(_player.SquishAnimation is not { State:1 },"Cancellation fixture must reach active squish initialization.");
            LoadValidationRoom(0,0x60);
            FailIf(_player.SideScrollSquished,"Room load must clear pending and active squish state.");
        }
    }
}
