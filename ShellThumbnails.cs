using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace FlyVideos;

internal static class ShellThumbnails
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        void GetImage([In] SIZE size, [In] int flags, [Out] out IntPtr phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        [In] string pszPath,
        [In] IntPtr pbc,
        [In] ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)][Out] out IShellItemImageFactory factory);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    private const int SIIGBF_BIGGERSIZEOK = 0x01;
    private const int SIIGBF_THUMBNAILONLY = 0x08;

    public static BitmapSource? TryGet(string path, int width, int height)
    {
        CoInitializeEx(IntPtr.Zero, 0);
        IntPtr hbm = IntPtr.Zero;
        IShellItemImageFactory? factory = null;
        try
        {
            var iid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory);
            factory.GetImage(new SIZE { cx = width, cy = height }, SIIGBF_BIGGERSIZEOK | SIIGBF_THUMBNAILONLY, out hbm);
            if (hbm == IntPtr.Zero) return null;
            var source = Imaging.CreateBitmapSourceFromHBitmap(hbm, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hbm != IntPtr.Zero) DeleteObject(hbm);
            if (factory != null) Marshal.ReleaseComObject(factory);
        }
    }
}
