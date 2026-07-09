namespace remename.ViewModels;

public static class PlatformHelper
{
    // 使用预编译指令来区分平台 - 这是最可靠的方式
    public static bool IsDesktop => !IsMobile;

#if ANDROID || IOS
    public static bool IsMobile => true;
#else
    public static bool IsMobile => false;
#endif

#if ANDROID
    public static bool IsAndroid => true;
#else
    public static bool IsAndroid => false;
#endif

#if IOS
    public static bool IsIOS => true;
#else
    public static bool IsIOS => false;
#endif

#if WINDOWS
    public static bool IsWindows => true;
#else
    // 后备方案：运行时检测
    public static bool IsWindows => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
#endif

#if MACOS
    public static bool IsMacOS => true;
#else
    public static bool IsMacOS => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
#endif

#if LINUX
    public static bool IsLinux => true;
#else
    public static bool IsLinux => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
#endif

    // 推荐的触控元素最小尺寸
    public static double MinTouchTargetSize => IsMobile ? 44.0 : 32.0;

    // 获取推荐的窗口尺寸
    public static (double Width, double Height) GetRecommendedWindowSize()
    {
        if (IsMobile)
        {
            return (double.NaN, double.NaN); // 移动端使用全屏
        }

        return (950, 700); // PC端推荐尺寸
    }

    // 获取推荐的列表项高度
    public static double GetListItemHeight()
    {
        return IsMobile ? 48.0 : 32.0;
    }

    // 获取推荐的间距
    public static double GetSpacing()
    {
        return IsMobile ? 16.0 : 12.0;
    }

    // 获取推荐的内边距
    public static double GetPadding()
    {
        return IsMobile ? 20.0 : 24.0;
    }
}