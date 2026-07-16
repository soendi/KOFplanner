using System;
using System.Drawing;

namespace KOFplanner.Forms;

// Loads the shared application icon (orange "KP" circle) and applies it to any Form.
public static class IconHelper
{
    private static Icon? _shared;

    public static Icon AppIcon
    {
        get
        {
            if (_shared == null)
            {
                // Prefer the compiled-in assembly icon so it works even if the file is missing.
                try { _shared = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location); }
                catch { _shared = null; }
            }
            return _shared!;
        }
    }

    public static void Apply(Form form)
    {
        var icon = AppIcon;
        if (icon != null) form.Icon = icon;
    }
}
