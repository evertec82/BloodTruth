using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TruthXR.Launcher {

// The few Windows functions the launcher calls directly.
static class NativeMethods {
    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr window, string app_name, string id_list);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    // Dark scroll bars for a text box (Windows 10 1809 and later; older versions keep the light
    // ones).
    public static void UseDarkScrollBars(Control control) {
        try {
            SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
        } catch (Exception) {
        }
    }

    // Gives a window the dark title bar of Windows 10 (2004 and later) and Windows 11. Older
    // versions ignore it and keep the normal title bar.
    public static void UseDarkTitleBar(Form form) {
        try {
            int enabled = 1;
            // 20 is DWMWA_USE_IMMERSIVE_DARK_MODE; Windows 10 builds before 2004 used 19.
            if (DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int)) != 0) {
                DwmSetWindowAttribute(form.Handle, 19, ref enabled, sizeof(int));
            }
        } catch (Exception) {
        }
    }
}

} // namespace TruthXR.Launcher
