using System.Runtime.InteropServices;

namespace WorldEditor
{
    public sealed class LibDeflateDecompressorHandle : SafeHandle
    {
        public override bool IsInvalid => handle == IntPtr.Zero;

        public LibDeflateDecompressorHandle() : base(IntPtr.Zero, true)
        {
        }

        protected override bool ReleaseHandle()
        {
            LibDeflate.FreeDecompressor(handle);
            return true;
        }
    }
}
