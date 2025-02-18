using System;

using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.OS;
using LibVLCSharp.Forms.Shared;
using Xamarin.Forms;
using Java.Lang;

namespace InteractivePlayer.Droid
{
    [Activity(Label = "InteractivePlayer", Icon = "@mipmap/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize )]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
           
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);
            LoadApplication(new App());
        }

        protected override void OnPause()
        {
            base.OnPause();
            MessagingCenter.Send("app", "OnPause");
        }

        protected override void OnResume()
        {
            base.OnResume();
            MessagingCenter.Send("app", "OnResume");
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}