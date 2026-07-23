using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;
public readonly record struct IntroSpriteFrame(int Duration, int SourceOffset, int BasePalette, IntroOamPart[] Parts);
