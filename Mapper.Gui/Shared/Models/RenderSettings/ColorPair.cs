using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Media;

namespace Mapper.Gui.Model
{
    public struct ColorPair
    {
        public Color Even { get; set; }
        public Color Odd { get; set; }

        public static ColorPair Overworld => new(Color.FromRgb(18, 18, 18), Color.FromRgb(22, 22, 22));
        public static ColorPair Nether => new(Color.FromRgb(22, 2, 3), Color.FromRgb(29, 3, 5));
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
