using Microsoft.UI.Xaml;

// WinUI, WinUI proje yapısı ve proje şablonları hakkında
// daha fazla bilgi için: http://aka.ms/winui-project-info.

namespace NeuralQuest.WinUI
{
    /// <summary>
    /// Varsayılan Application sınıfını tamamlayan, uygulamaya özgü davranışları sağlar.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Tek örnekli uygulama nesnesini başlatır. Uygulama kodunda çalıştırılan ilk satırdır
        /// ve bu nedenle mantıksal olarak main() veya WinMain() ile eşdeğerdir.
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
