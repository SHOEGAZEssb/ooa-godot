using System;

namespace Oracles;

internal sealed partial class GanonCharacter
{
    private BossDeathExplosionEffect? _deathExplosion;
    private void BeginDeath(RoomEntityFrame frame)
    {
        // enemyCode04 waits for checkLinkVulnerable, including the shared
        // contact signal cleared after Link and any active recoil/invincibility.
        if(!frame.Player.NativePuzzleResetVulnerable||frame.Player.NativeContactSignal)return;
        Health=0x40;State=0x0e;Substate=0;Counter=120;
        World.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisableLinkCollisionsAndMenu,1);
        World.Entities.OnSoundRequested(SoundId.SndBossDead);RestartAnimation(14);
        World.Defeated();
        World.Entities.OnSoundRequested(SoundId.SndCtrlStopMusic);
        World.Entities.OnRoomEntityDialogueRequested(0x2f0e,World.Data.Text(0x2f0e),Position);
    }
    private void DeathFrame()
    {
        switch(Substate)
        {
            case 0:
                if(Dec()!=0){Visible=!Visible;return;}
                Counter=1;
                if(!World.Entities.PartSlotAvailable)return;
                var explosion=World.Entities.Spawn(new BossDeathExplosionSpawn(Position.Floor(),0,ZFixed>>8,NativeSubid:1));
                _deathExplosion=(BossDeathExplosionEffect)explosion;
                Substate=1;ZFixed&=255;Visible=false;
                World.Entities.OnSoundRequested(SoundId.SndBigExplosion2);return;
            case 1:
                if((_deathExplosion!.AnimationParameter&128)==0)return;
                Substate=2;Counter=8;World.FastWhiteFade();return;
            case 2:
                if(Dec()!=0)return;
                Substate=3;Counter=30;World.Entities.RuntimeState.SetWramByte(0xcbe3,0);
                World.ReloadArena(0);World.Fade(false,2,false);return;
            case 3:
                if(World.Entities.PaletteFadeActiveSource()||Dec()!=0)return;
                World.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisableLinkCollisionsAndMenu,0);
                Finish();return;
            default:throw new NotSupportedException($"ganon_stateE: substate${Substate:x2}.");
        }
    }
}
