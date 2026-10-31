using Godot;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedWalkingAnimationRom()
    {
        var basic = new LinkWalkingAnimationInputs(false,0,false,0,0,false,0,false,false,true,false,1);
        LinkWalkingAnimationInputs[] profiles = [basic, basic with { WallPush=false },
            basic with { TurningDisabled=true }, basic with { ClimbingVine=true }, basic with { TextActive=true },
            basic with { ClimbingVine=true, WallPush=false },
            basic with { UsingShield=1 }, basic with { UsingShield=2 },
            basic with { ShieldEquipped=true }, basic with { ShieldEquipped=true, ShieldLevel=2 },
            basic with { ShieldEquipped=true, ShieldLevel=0, TurningDisabled=true },
            basic with { GrabState=0x83 }, basic with { GrabState=0x83, Minecart=true },
            basic with { GrabState=0x41, ClimbingVine=true }, basic with { GrabState=0xc2 },
            basic with { Minecart=true, UsingShield=2 },
            basic with { MagnetGloveState=1, UsingShield=2 },
            basic with { Underwater=true, TextActive=true, UsingShield=2, TurningDisabled=true, GrabState=0xc2 },
            basic with { Underwater=true, WallPush=false, ClimbingVine=true }];
        int count = 0;
        foreach (var game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            var native = new LinkCollisionRom(game:game);
            bool ages = game == OracleGame.Ages;
            foreach (var profile in profiles)
            for (int force=0; force<256; force++)
            for (int direction=0; direction<4; direction++)
            {
                var input = profile with { PushOverride=force, Underwater=profile.Underwater && ages };
                native[ages ? 0xcc34 : 0xcc50] = profile.Underwater ? (byte)0x40 : (byte)0;
                native[ages ? 0xcc5a : 0xcc75] = (byte)input.GrabState;
                native[ages ? 0xcc2c : 0xcc48] = input.Minecart || (direction & 1) != 0 ? (byte)0xd1 : (byte)0xd0;
                native[0xd100]=0;
                native[0xd101] = input.Minecart || (direction & 1) == 0 ? (byte)0x0a : (byte)0x0b;
                native[ages ? 0xcc5e : 0xcc79] = (byte)input.MagnetGloveState;
                native[ages ? 0xcc6f : 0xcc89] = (byte)input.UsingShield;
                native[ages ? 0xcc60 : 0xcc7b] = input.TurningDisabled ? (byte)1 : (byte)0;
                native[ages ? 0xcc66 : 0xcc81] = (byte)force;
                native[ages ? 0xcc68 : 0xcc83] = input.ClimbingVine ? (byte)1 : (byte)0;
                native[0xcba0] = input.TextActive ? (byte)1 : (byte)0;
                int mask=direction switch { 0=>0xc0, 1=>3, 2=>0x30, _=>0x0c };
                int key=direction switch { 0=>0x40, 1=>0x10, 2=>0x80, _=>0x20 };
                native[0xd033] = input.WallPush || (direction & 1) == 0 ? (byte)mask : (byte)0;
                native[ages ? 0xcc29 : 0xcc45] = input.WallPush || (direction & 1) != 0 ? (byte)key : (byte)0;
                native[ages ? 0xc689 : 0xc681] = input.ShieldEquipped && (direction & 1) == 0 ? (byte)1 : (byte)0;
                native[ages ? 0xc688 : 0xc680] = input.ShieldEquipped && (direction & 1) != 0 ? (byte)1 : (byte)0;
                native[ages ? 0xc6af : 0xc6a9] = (byte)input.ShieldLevel;
                native[0xd008]=(byte)direction; native[0xd034]=0x55;
                native[ages ? 0xcc65 : 0xcc80]=0xff;
                native.Call(ages ? 0x4589 : 0x457f,bank:6);
                var actual = LinkWalkingAnimation.Select(input);
                FailIf(native[0xc200] != actual.Offset || native[0xd034] != (actual.WritesVariant ? actual.Offset : 0x55) ||
                    native[ages ? 0xcc65 : 0xcc80] != (actual.PublishesPush ? direction : 0xff) ||
                    native[0xd008] != (actual.FacesUp ? 0 : direction),
                    $"{game} native WALK force${force:x2}, dir{direction}, profile={profile}: offset${native[0xc200]:x2}/{actual.Offset:x2}, var34${native[0xd034]:x2}, push${native[ages ? 0xcc65 : 0xcc80]:x2}, facing${native[0xd008]:x2}.");
                count++;
            }
            FailIf(native.RandomUpdates!=0,"Native walking style selection must not consume game RNG.");
        }
        GD.Print($"Validated {count} both-ROM WALK selections: all override bytes, four directions, source priority, raw grab RET, live key/probe checks, underwater and vine side effects.");
    }
}
