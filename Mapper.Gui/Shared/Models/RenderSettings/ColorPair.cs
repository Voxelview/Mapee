using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Media;

namespace Mapper.Gui.Model
{
    public struct ColorPair
    {
        public Color Even { get; set; }
        public Color Odd { get; set; }

        // Sits just above the panel colour, on the panel's own slate tilt, so the map's empty
        // ground reads as the same material as the chrome around it rather than the flat neutral
        // it used to be, which went cold against slate. The two moves this pair takes are
        // independent and want keeping that way: brightness is where the pair sits, contrast is
        // the 5-step gap between the cells. Shift both together to relight it, spread them to
        // sharpen it - a lift applied to one cell alone changes both at once.
        // Duplicated in every style's Settings/Render.json - this is only the fallback.
        public static ColorPair Overworld => new(Color.FromRgb(17, 18, 20), Color.FromRgb(22, 23, 25));
        // All three pairs are tuned to the same perceived step between their cells - about 5,
        // measuring the channel deltas under luma weights (green 0.72, red 0.21, blue 0.07)
        // rather than as raw numbers. That weighting is the whole point here: these two step
        // along a hue rather than up in lightness, so counting raw deltas badly misjudges them.
        // The Nether's step is almost entirely red, so its old +7 red / +1 green read at 3.4 -
        // two thirds of the Overworld's - and needed widening to +10 / +2 to draw level. The
        // End's +7 / +4 / +7 looks like the biggest step of the three and measures 5.0, level
        // with the Overworld already, so it is untouched. Widen along the existing direction on
        // any retune; a flat +5 on all three channels would wash the hue out of the light cell.
        public static ColorPair Nether => new(Color.FromRgb(22, 2, 3), Color.FromRgb(32, 4, 6));
        public static ColorPair TheEnd => new(Color.FromRgb(15, 11, 22), Color.FromRgb(22, 15, 29));

        public static bool operator ==(ColorPair left, ColorPair right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(ColorPair left, ColorPair right)
        {
            return !(left == right);
        }

        public ColorPair(Color even, Color odd) 
        {
            Even = even;
            Odd = odd;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if(obj is not ColorPair pair) return false;
            return Even == pair.Even && Odd == pair.Odd;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Even, Odd);
        }
    }
}
