using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using System;

namespace remename.Android;

[Activity(
    Label = "remename.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Keep password fields and other sensitive app content out of
        // screenshots, screen recordings, and the recent-apps preview.
        Window?.SetFlags(WindowManagerFlags.Secure, WindowManagerFlags.Secure);
    }
}

[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    public AndroidApp(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        App.SmbCredentialStore = new AndroidSmbCredentialStore(this);
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
