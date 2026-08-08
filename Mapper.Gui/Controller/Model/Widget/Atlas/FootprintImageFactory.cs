using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WorldEditor;

namespace Mapper.Gui.Controller
{
    /// <summary>
    /// Turns a world's stored region coordinates into a plate - the shape of where somebody
    /// actually went, which is the one portrait of a world that cannot collide with another's.
    ///
    /// It rasterises rather than building a DrawingImage, and that is the whole design. A vector
    /// plate has to be scaled to fit the art box, so a one-region gridline lands on a fraction of
    /// a device pixel: anti-aliased it greys into a smudge, and aliased - which rounds coverage to
    /// nothing or all - it disappears entirely wherever it falls badly. Neither is fixable by
    /// choosing a better line weight, because the scale factor is different for every world.
    ///
    /// Writing pixels removes the question. A region is a whole number of pixels, every boundary
    /// is exactly one pixel, and the image is displayed at its own size, so what is computed here
    /// is what appears.
    ///
    /// Safe off the UI thread: BitmapSource.Create takes a byte array and the result is frozen, so
    /// no DispatcherObject is ever touched.
    /// </summary>
    public static class FootprintImageFactory
    {
        /// <summary>
        /// The art box a plate gives this, less its margin. The bitmap is built to fit inside it
        /// so it can be shown at 1:1 - scaling it afterwards, in either direction, would undo the
        /// exactness that is the point of rasterising.
        /// </summary>
        private const int PLATE_PIXELS = 130;

        /// <summary>
        /// The coverage grid never exceeds this on its long side, so a very large world
        /// downsamples by a whole number of regions per cell rather than producing a bitmap with
        /// nothing legible in it.
        /// </summary>
        private const int MAX_CELLS = 50;

        /// <summary>
        /// The grid is squared up to at least this. Without a floor, a one-region world would be
        /// blown up to fill the plate and every small world would arrive the same size as every
        /// large one - at which point the plate has stopped saying anything.
        /// </summary>
        private const int MIN_EXTENT = 10;

        private const int REGION_IN_BLOCKS = 512;
        private const int MARKER_PIXELS = 15;

        /// <summary>
        /// One band per dimension, each taken from the material that dimension is mostly made of -
        /// grass, netherrack, end stone. The Overworld's are the dimension icon's own greens, so
        /// the picker and the dimension button agree without either being tuned to the other.
        ///
        /// Ordered dark to light within a band, and that ordering is load-bearing: the tone is
        /// chosen by a noise field, so neighbouring values have to land on neighbouring colours or
        /// the patches come out as confetti rather than as terrain.
        /// </summary>
        private static readonly uint[] OVERWORLD_TONES =
        {
            Rgb(46, 160, 42),
            Rgb(66, 184, 54),
            Rgb(84, 200, 62),
            Rgb(102, 216, 64),
            Rgb(124, 234, 80)
        };

        private static readonly uint[] NETHER_TONES =
        {
            Rgb(118, 34, 26),
            Rgb(148, 52, 30),
            Rgb(176, 72, 36),
            Rgb(202, 98, 42),
            Rgb(226, 130, 54)
        };

        private static readonly uint[] END_TONES =
        {
            Rgb(148, 146, 102),
            Rgb(176, 174, 124),
            Rgb(202, 200, 146),
            Rgb(222, 220, 166),
            Rgb(240, 238, 188)
        };

        // Well down from the land rather than a shade off it: the line is one pixel wide, and one
        // pixel only reads if it is a long way from what surrounds it. Still of the same hue,
        // though - a neutral dark would have read as a seam between tiles rather than as ground.
        private static readonly uint OVERWORLD_GRID = Rgb(24, 58, 20);
        private static readonly uint NETHER_GRID = Rgb(48, 12, 8);
        private static readonly uint END_GRID = Rgb(62, 60, 40);

        // Lifted from AxisTool, so the axes a plate draws are the same two colours the cardinal
        // axis tool paints onto the map itself - X_AXIS_COLOR and Z_AXIS_COLOR there. If those
        // move, these move. Two pixels wide, because one pixel of either was lost against a field
        // of green this saturated.
        private static readonly uint AXIS_X = Rgb(255, 48, 24);
        private static readonly uint AXIS_Z = Rgb(64, 226, 255);
        private const int AXIS_PIXELS = 2;

        private static readonly uint SKIN = Rgb(198, 152, 110);
        private static readonly uint HAIR = Rgb(48, 30, 18);
        private static readonly uint EYE = Rgb(60, 90, 150);

        // A compass, which is the game's own way of pointing at a spawn. A bed is what
        // Player.Spawn literally is, but a bed is a rectangle with a lighter rectangle on it, and
        // at fifteen pixels beside a head that read as a body rather than as an object.
        private static readonly uint COMPASS_FACE = Rgb(226, 222, 214);
        private static readonly uint COMPASS_NEEDLE = Rgb(198, 58, 48);

        private static readonly uint OUTLINE = Rgb(8, 9, 10);

        /// <summary>
        /// Null when the world has nothing stored, which is the caller's signal to fall back to
        /// the world's own icon.
        /// </summary>
        public static ImageSource? Create(IReadOnlyCollection<Coords> footprint, Level level, Dimension dimension)
        {
            if (footprint.Count < 1) return null;

            uint[] tones = TonesFor(dimension);
            uint grid = GridFor(dimension);

            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
            foreach (Coords coords in footprint)
            {
                if (coords.X < minX) minX = coords.X;
                if (coords.X > maxX) maxX = coords.X;
                if (coords.Z < minZ) minZ = coords.Z;
                if (coords.Z > maxZ) maxZ = coords.Z;
            }

            int width = maxX - minX + 1, height = maxZ - minZ + 1;

            // Whole regions per cell, so a cell is either wholly inside the world or wholly
            // outside it and the footprint's edges stay straight.
            int scale = Math.Max(1, (Math.Max(width, height) + MAX_CELLS - 1) / MAX_CELLS);

            int gridWidth = (width + scale - 1) / scale;
            int gridHeight = (height + scale - 1) / scale;

            int side = Math.Max(MIN_EXTENT, Math.Max(gridWidth, gridHeight));
            int offsetX = (side - gridWidth) / 2, offsetZ = (side - gridHeight) / 2;

            // Zero is empty; anything else is a land tone, one past its index in LAND_TONES.
            byte[] cells = new byte[side * side];
            foreach (Coords coords in footprint)
            {
                int index = (offsetZ + (coords.Z - minZ) / scale) * side + offsetX + (coords.X - minX) / scale;
                cells[index] = (byte)(1 + Tone(coords.X, coords.Z, tones.Length));
            }

            // A whole number of pixels per region, and the +1 is the far boundary's own column and
            // row - without it the last gridline would have nowhere to go.
            int cell = Math.Max(1, (PLATE_PIXELS - 1) / side);
            int size = side * cell + 1;

            uint[] pixels = new uint[size * size];

            PaintLand(pixels, size, cells, side, cell, tones);
            PaintGrid(pixels, size, cells, side, cell, grid);
            PaintAxes(pixels, size, minX, minZ, scale, offsetX, offsetZ, cell);
            PaintMarkers(pixels, size, level, minX, minZ, scale, offsetX, offsetZ, cell);

            return CreateBitmap(pixels, size);
        }

        private static void PaintLand(uint[] pixels, int size, byte[] cells, int side, int cell, uint[] tones)
        {
            for (int z = 0; z < side; z++)
            {
                for (int x = 0; x < side; x++)
                {
                    byte tone = cells[z * side + x];
                    if (tone == 0) continue;

                    Fill(pixels, size, x * cell, z * cell, cell, cell, tones[tone - 1]);
                }
            }
        }

        /// <summary>
        /// Two octaves of value noise over the region grid. A plain hash per region was the first
        /// attempt and came out as static - every region independent of its neighbours - where
        /// what the plate wants is patches, the way terrain actually varies.
        ///
        /// The low frequency makes the patches, about five regions across; the high one breaks
        /// their edges so they do not read as circles. Both are derived from the region's own
        /// coordinates, so a world still draws identically every time.
        /// </summary>
        private static int Tone(int x, int z, int toneCount)
        {
            // The two dials worth knowing. The first frequency sets how wide a patch is - about
            // 1/0.2, so five regions - and the second octave's weight is how much per-region
            // jitter breaks its edges. Push that weight up and the field turns back into grain,
            // which is what a plain hash gave and what this replaced.
            double value = Noise(x * 0.2, z * 0.2) * 0.82
                         + Noise(x * 0.52, z * 0.52) * 0.18;

            // Interpolating four lattice points pulls the result hard toward the middle, so raw
            // value noise almost never reaches either end of its own range - the darkest and
            // lightest greens went unused and the field read as two tones with grain on top.
            // Stretching about the centre is what turns it back into bands.
            value = (value - 0.5) * 1.75 + 0.5;

            return Math.Clamp((int)(value * toneCount), 0, toneCount - 1);
        }

        /// <summary>
        /// Value noise rather than true Perlin: it interpolates hashed lattice points instead of
        /// gradients, which is a handful of operations and indistinguishable at five colours.
        /// </summary>
        private static double Noise(double x, double z)
        {
            int x0 = (int)Math.Floor(x), z0 = (int)Math.Floor(z);
            double fx = x - x0, fz = z - z0;

            // Smoothstep on the fractions. Interpolating them raw leaves the lattice visible as a
            // diamond grid, which is the one artefact that would look like a bug rather than terrain.
            fx = fx * fx * (3 - 2 * fx);
            fz = fz * fz * (3 - 2 * fz);

            double top = Lerp(Lattice(x0, z0), Lattice(x0 + 1, z0), fx);
            double bottom = Lerp(Lattice(x0, z0 + 1), Lattice(x0 + 1, z0 + 1), fx);

            return Lerp(top, bottom, fz);
        }

        private static double Lattice(int x, int z)
        {
            uint hash = (uint)(x * 73856093) ^ (uint)(z * 19349663);

            hash ^= hash >> 13;
            hash *= 0x85EBCA6B;
            hash ^= hash >> 16;

            return hash / (double)uint.MaxValue;
        }

        private static uint[] TonesFor(Dimension dimension)
        {
            if (dimension == Dimension.Nether) return NETHER_TONES;
            if (dimension == Dimension.TheEnd) return END_TONES;

            // Anything a datapack adds gets the Overworld's band. It is the only one of the three
            // that does not name a specific material, so it is the safe default for a dimension
            // this app knows nothing else about.
            return OVERWORLD_TONES;
        }

        private static uint GridFor(Dimension dimension)
        {
            if (dimension == Dimension.Nether) return NETHER_GRID;
            if (dimension == Dimension.TheEnd) return END_GRID;

            return OVERWORLD_GRID;
        }

        private static double Lerp(double from, double to, double amount)
        {
            return from + (to - from) * amount;
        }

        /// <summary>
        /// All four edges of every filled region, which is why no line can be missing: a boundary
        /// is drawn because a region is there, not because a ruled line happened to survive a clip
        /// and a rounding. Shared edges get painted twice in the same colour, which costs nothing.
        /// </summary>
        private static void PaintGrid(uint[] pixels, int size, byte[] cells, int side, int cell, uint grid)
        {
            for (int z = 0; z < side; z++)
            {
                for (int x = 0; x < side; x++)
                {
                    if (cells[z * side + x] == 0) continue;

                    int left = x * cell, top = z * cell;

                    Fill(pixels, size, left, top, cell + 1, 1, grid);
                    Fill(pixels, size, left, top + cell, cell + 1, 1, grid);
                    Fill(pixels, size, left, top, 1, cell + 1, grid);
                    Fill(pixels, size, left + cell, top, 1, cell + 1, grid);
                }
            }
        }

        /// <summary>
        /// X=0 and Z=0, across the whole bitmap rather than only over the land. Their job is to say
        /// where the world sits relative to the origin, and a world that grew off to one side says
        /// that by having its axes near an edge - which only reads if the lines cross the empty
        /// part of the plate too.
        /// </summary>
        private static void PaintAxes(uint[] pixels, int size, int minX, int minZ, int scale, int offsetX, int offsetZ, int cell)
        {
            // Only drawn when the origin actually falls inside the bitmap: a world a long way from
            // spawn has no axis to show, and clamping one to the edge would put a line where the
            // origin is not.
            int x = (offsetX + (0 - minX) / scale) * cell;
            int z = (offsetZ + (0 - minZ) / scale) * cell;

            // The vertical line stands at X=0, so the axis running along it is Z - blue. The
            // horizontal one stands at Z=0 and runs along X - red.
            if (x >= 0 && x < size) Fill(pixels, size, x, 0, AXIS_PIXELS, size, AXIS_Z);
            if (z >= 0 && z < size) Fill(pixels, size, 0, z, size, AXIS_PIXELS, AXIS_X);
        }

        private static void PaintMarkers(uint[] pixels, int size, Level level, int minX, int minZ, int scale, int offsetX, int offsetZ, int cell)
        {
            int playerX = ToPixel(level.Player.Position.X, minX, scale, offsetX, cell, size);
            int playerZ = ToPixel(level.Player.Position.Z, minZ, scale, offsetZ, cell, size);

            int spawnX = ToPixel(level.Player.Spawn.X, minX, scale, offsetX, cell, size);
            int spawnZ = ToPixel(level.Player.Spawn.Z, minZ, scale, offsetZ, cell, size);

            // Dropped when the two land on top of each other, which they do for anyone who logged
            // out where they sleep - a marker peeking out from behind the head is worse than no
            // marker, and a plate that appears to show one thing should not quietly show two.
            int gap = Math.Max(Math.Abs(spawnX - playerX), Math.Abs(spawnZ - playerZ));
            if (gap >= MARKER_PIXELS) PaintCompass(pixels, size, spawnX, spawnZ);

            PaintPlayerHead(pixels, size, playerX, playerZ);
        }

        /// <summary>
        /// Block coordinates to the bitmap, clamped so a marker cannot fall off the edge. A player
        /// standing outside every stored region reads as sitting on the edge they left from.
        /// </summary>
        private static int ToPixel(float block, int min, int scale, int offset, int cell, int size)
        {
            int region = MathUtilities.FindSectionY((int)block, REGION_IN_BLOCKS);
            int position = (int)((offset + (region - min) / (double)scale + 0.5) * cell);

            int half = MARKER_PIXELS / 2;
            return Math.Clamp(position, half, size - half - 1);
        }

        /// <summary>
        /// The front of a head on an 8x8 grid - the same square the game's own skin gives a face,
        /// so the proportions are not invented.
        /// </summary>
        private static void PaintPlayerHead(uint[] pixels, int size, int centreX, int centreZ)
        {
            // The marker's own extent, which is not the bitmap's - hence the two names.
            const int HEAD = MARKER_PIXELS;

            int left = centreX - HEAD / 2, top = centreZ - HEAD / 2;

            int Unit(double value) => (int)Math.Round(value * HEAD / 8.0);

            Fill(pixels, size, left - 1, top - 1, HEAD + 2, HEAD + 2, OUTLINE);
            Fill(pixels, size, left, top, HEAD, HEAD, SKIN);

            // Hair over the crown and down both temples, which is what separates a head from a
            // plain square at this size.
            Fill(pixels, size, left, top, HEAD, Unit(2), HAIR);
            Fill(pixels, size, left, top + Unit(2), Unit(1), Unit(4), HAIR);
            Fill(pixels, size, left + HEAD - Unit(1), top + Unit(2), Unit(1), Unit(4), HAIR);

            Fill(pixels, size, left + Unit(2), top + Unit(3), Unit(1.5), Unit(1.5), EYE);
            Fill(pixels, size, left + Unit(4.5), top + Unit(3), Unit(1.5), Unit(1.5), EYE);
        }

        /// <summary>
        /// A pale disc with a red needle at its centre. Round on purpose: the head beside it is
        /// square, and at this size the silhouette is the only thing telling the two apart before
        /// any detail inside them resolves.
        /// </summary>
        private static void PaintCompass(uint[] pixels, int size, int centreX, int centreZ)
        {
            double radius = MARKER_PIXELS / 2.0;

            PaintDisc(pixels, size, centreX, centreZ, radius + 1, OUTLINE);
            PaintDisc(pixels, size, centreX, centreZ, radius, COMPASS_FACE);
            PaintDisc(pixels, size, centreX, centreZ, radius * 0.38, COMPASS_NEEDLE);
        }

        private static void PaintDisc(uint[] pixels, int size, int centreX, int centreZ, double radius, uint colour)
        {
            int bound = (int)Math.Ceiling(radius);
            double squared = radius * radius;

            for (int z = -bound; z <= bound; z++)
            {
                for (int x = -bound; x <= bound; x++)
                {
                    if (x * x + z * z > squared) continue;
                    Fill(pixels, size, centreX + x, centreZ + z, 1, 1, colour);
                }
            }
        }

        /// <summary>
        /// Clipped rather than guarded by the caller, so every painter above can write in whatever
        /// coordinates the world gives it without checking the edges itself.
        /// </summary>
        private static void Fill(uint[] pixels, int size, int x, int z, int width, int height, uint colour)
        {
            int fromX = Math.Max(0, x), toX = Math.Min(size, x + width);
            int fromZ = Math.Max(0, z), toZ = Math.Min(size, z + height);

            for (int row = fromZ; row < toZ; row++)
            {
                int offset = row * size;
                for (int column = fromX; column < toX; column++) pixels[offset + column] = colour;
            }
        }

        private static ImageSource CreateBitmap(uint[] pixels, int size)
        {
            byte[] buffer = new byte[pixels.Length * 4];
            Buffer.BlockCopy(pixels, 0, buffer, 0, buffer.Length);

            // Pbgra32 because the transparent background is written as all-zero, which is the
            // premultiplied form of "nothing" - and every colour painted over it is fully opaque,
            // where premultiplied and straight agree.
            BitmapSource output = BitmapSource.Create(size, size, 96, 96,
                PixelFormats.Pbgra32, null, buffer, size * 4);

            output.Freeze();

            return output;
        }

        /// <summary>Packs to the 0xAARRGGBB an int array shares with Pbgra32's byte order.</summary>
        private static uint Rgb(byte red, byte green, byte blue)
        {
            return 0xFF000000u | (uint)(red << 16) | (uint)(green << 8) | blue;
        }
    }
}
