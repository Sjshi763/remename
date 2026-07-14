using System;

namespace remename.ViewModels;

public static class AppPath
{
    public static string GetApplicationDataDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "remename");
        }

        if (OperatingSystem.IsAndroid())
        {
            // On Android, LocalApplicationData resolves inside the app's private
            // /data/user/0/<package> area and does not require storage permission.
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "remename");
        }

        if (OperatingSystem.IsIOS() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "remename");
        }

        return System.IO.Path.Combine(AppContext.BaseDirectory, "remename-data");
    }
}
