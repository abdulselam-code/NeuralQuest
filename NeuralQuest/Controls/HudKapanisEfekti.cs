using Microsoft.Maui.Graphics;

namespace NeuralQuest.Controls;

// Kapanmış modalın yalnızca ışıklı kasasını söndürür. Native içerik hemen kalkar;
// bu dekoratif GraphicsView hiçbir dokunma veya erişilebilirlik hedefi oluşturmaz.
internal sealed class HudKapanisEfekti : IDrawable
{
    private const string Animasyon = "HudKapanis";
    private GraphicsView? _yuzey;
    private HudCerceve? _panel;
    private RectF _cerceve;
    private float _olcek = 1;

    public void Baslat(HudCerceve panel, View modal)
    {
        Durdur();
        if (modal.Parent is not Grid alan || panel.Width <= 0 || panel.Height <= 0) return;
        if (_yuzey is null)
        {
            _yuzey = new GraphicsView { Drawable = this, IsVisible = false, InputTransparent = true,
                BackgroundColor = Colors.Transparent, ZIndex = 100,
                HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
            AutomationProperties.SetIsInAccessibleTree(_yuzey, false);
            alan.Children.Add(_yuzey);
        }
        _cerceve = Konum(panel, alan);
        // Tam ekran bir etkileşim yüzeyi bırakma; yalnız eski kasanın alanını kullan.
        _yuzey.Margin = new Thickness(_cerceve.X, _cerceve.Y, 0, 0);
        _yuzey.WidthRequest = _cerceve.Width;
        _yuzey.HeightRequest = _cerceve.Height;
        _panel = panel;
        _olcek = 1;
        _yuzey.Opacity = 1;
        _yuzey.IsVisible = true;
        HudGecisi.OneAl(_yuzey);
        _yuzey.Invalidate();
        new Animation(t =>
        {
            _yuzey.Opacity = 1 - t;
            _olcek = 1 - (float)t * 0.012f;
            _yuzey.Invalidate();
        }).Commit(_yuzey, Animasyon, rate: 16, length: 180, easing: Easing.CubicIn,
            finished: (_, iptal) => { if (!iptal) Gizle(); });
    }

    public void Durdur()
    {
        _yuzey?.AbortAnimation(Animasyon);
        Gizle();
    }

    private void Gizle()
    {
        if (_yuzey is not null) _yuzey.IsVisible = false;
        _panel = null;
    }

    private static RectF Konum(View panel, Grid alan)
    {
#if ANDROID
        var ph = panel.Handler;
        var ah = alan.Handler;
        if ((ph?.ContainerView ?? ph?.PlatformView) is Android.Views.View p &&
            (ah?.ContainerView ?? ah?.PlatformView) is Android.Views.View a && a.Width > 0)
        {
            int[] pp = new int[2], ap = new int[2];
            p.GetLocationOnScreen(pp); a.GetLocationOnScreen(ap);
            float oran = (float)alan.Width / a.Width;
            return new RectF((pp[0] - ap[0]) * oran, (pp[1] - ap[1]) * oran,
                p.Width * oran, p.Height * oran);
        }
#endif
        double x = 0, y = 0;
        for (Element? e = panel; e is VisualElement v && e != alan; e = e.Parent)
        { x += v.X; y += v.Y; }
        return new RectF((float)x, (float)y, (float)panel.Width, (float)panel.Height);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (_panel is null) return;
        canvas.SaveState();
        try
        {
            canvas.Translate(dirtyRect.Width * (1 - _olcek) / 2,
                dirtyRect.Height * (1 - _olcek) / 2);
            canvas.Scale(_olcek, _olcek);
            _panel.Draw(canvas, new RectF(0, 0, dirtyRect.Width, dirtyRect.Height));
        }
        finally { canvas.RestoreState(); }
    }
}
