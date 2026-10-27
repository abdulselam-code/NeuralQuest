using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace NeuralQuest
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SistemCubuklariniAyarla();
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);
            if (hasFocus) SistemCubuklariniAyarla();
        }

        private void SistemCubuklariniAyarla()
        {
            if (Window is not { } pencere) return;
            var lacivert = Android.Graphics.Color.ParseColor("#061326");
            pencere.SetBackgroundDrawable(new Android.Graphics.Drawables.ColorDrawable(lacivert));
            var denetleyici = WindowCompat.GetInsetsController(pencere, pencere.DecorView);
            if (denetleyici is not null)
            {
                denetleyici.AppearanceLightStatusBars = false;
                denetleyici.AppearanceLightNavigationBars = false;
            }

            // Android 15+ saydam sistem çubuklarını zorunlu kılar. Mevcut koyu
            // edge-to-edge arka planı ve Container güvenli alanları korunur.
            // Eski sürümlerde opak lacivert kullan; tam ekran/gizleme bayrağı ekleme.
            if (Build.VERSION.SdkInt < BuildVersionCodes.VanillaIceCream)
            {
#pragma warning disable CA1422, CS0618 // Yalnızca API < 35 için desteklenen renk ayarı.
                pencere.SetStatusBarColor(lacivert);
                pencere.SetNavigationBarColor(lacivert);
#pragma warning restore CA1422, CS0618
            }
        }
    }
}
