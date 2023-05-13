using System;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Karan.Churi;
using Microsoft.Xna.Framework.Input;
using XenophyteAndroidWallet.Event;
using XenophyteAndroidWallet.User;
using ZXing.Mobile;
using static Android.Resource;

namespace XenophyteAndroidWallet
{
    [Activity(Label = "Xenophyte-Android-Wallet"
        , MainLauncher = true
        , Icon = "@drawable/xenophyte"
        , AlwaysRetainTaskState = true
        , LaunchMode = LaunchMode.SingleInstance
        , ScreenOrientation = ScreenOrientation.Portrait
        , ConfigurationChanges =
            ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize
        )]
    public class Startup : Microsoft.Xna.Framework.AndroidGameActivity
    {
        private PermissionManager _permission;

        protected override void OnCreate(Bundle bundle)
        {
            CultureInfo.DefaultThreadCurrentCulture = ClassUserSetting.GlobalCultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = ClassUserSetting.GlobalCultureInfo;
            base.OnCreate(bundle);

            ThreadPool.SetMinThreads(65535, 100);
            ThreadPool.SetMaxThreads(65535, 100);
            ServicePointManager.DefaultConnectionLimit = 65535;

            SetContentView(Layout.ListContent); // Not important, this is just initialized like that to provide a content view not NULL for ask permissions at runtime.
            _permission = new PermissionManager();
            if(_permission.CheckAndRequestPermissions(this))
            {
                MobileBarcodeScanner.Initialize(Application);
                var g = new Interface(this);
                SetContentView((View)g.Services.GetService(typeof(View)));
                g.Run();
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            AskPermission(requestCode, permissions, grantResults);
        }

        private async void AskPermission(int requestCode, string[] permissions, Permission[] grantResults)
        {
            _permission.CheckResult(requestCode, permissions, grantResults);

            var granted = _permission.Status[0].Granted;
            var denied = _permission.Status[0].Denied;
#if DEBUG
            System.Diagnostics.Debug.WriteLine("GRANTED: {" + string.Join(", ", granted.ToArray()) + "}");
            System.Diagnostics.Debug.WriteLine("DENIED: {" + string.Join(", ", denied.ToArray()) + "}");
#endif

            if (denied.Count == 0)
            {
                MobileBarcodeScanner.Initialize(Application);
                var g = new Interface(this);
                SetContentView((View)g.Services.GetService(typeof(View)));
                g.Run();
            }
            else
            {
                var result = await MessageBox.Show("Permission denied", "Permissions are denied, they are necessary for running the Android Wallet, do you want to try again?\nIf no the application will be closed.", new[] { "No", "Yes" });
                if (result == 0)
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                else
                    _permission.CheckAndRequestPermissions(this);

            }
        }

        /// <summary>
        /// Start QrCode Scanner.
        /// </summary>
        /// <returns></returns>
        public async Task<string> StartQrCodeScanner()
        {
            var scanner = new MobileBarcodeScanner
            {
                TopText = "Xenophyte Android Wallet",
            };

            var result = await scanner.Scan();


            return result?.Text != null ? result.Text : string.Empty;
        }
    }
}

