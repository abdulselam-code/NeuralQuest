using Microsoft.Maui.Graphics;

namespace NeuralQuest.Controls;

// Statik, boyuta uyarlanan HUD yüzeyi. Dekorasyon dokunma veya erişilebilirlik hedefi değildir.
public sealed class HudCerceve : ContentView, IDrawable
{
    private readonly GraphicsView _zemin;
    private readonly ContentView _govde;

    public static readonly BindableProperty GovdeProperty = BindableProperty.Create(nameof(Govde), typeof(View),
        typeof(HudCerceve), null, propertyChanged: (b, _, v) => ((HudCerceve)b)._govde.Content = (View?)v);
    public static readonly BindableProperty VurguProperty = BindableProperty.Create(nameof(Vurgu), typeof(Color),
        typeof(HudCerceve), Color.FromArgb("#4FE4FF"), propertyChanged: YenidenCiz);
    public static readonly BindableProperty IkincilVurguProperty = BindableProperty.Create(nameof(IkincilVurgu), typeof(Color),
        typeof(HudCerceve), Color.FromArgb("#9E6AFF"), propertyChanged: YenidenCiz);
    public static readonly BindableProperty IcerikPayiProperty = BindableProperty.Create(nameof(IcerikPayi), typeof(Thickness),
        typeof(HudCerceve), new Thickness(20), propertyChanged: (b, _, v) => ((HudCerceve)b)._govde.Padding = (Thickness)v);
    public static readonly BindableProperty EnerjiProperty = BindableProperty.Create(nameof(Enerji), typeof(double),
        typeof(HudCerceve), 1d, propertyChanged: YenidenCiz);

    public View? Govde { get => (View?)GetValue(GovdeProperty); set => SetValue(GovdeProperty, value); }
    public Color Vurgu { get => (Color)GetValue(VurguProperty); set => SetValue(VurguProperty, value); }
    public Color IkincilVurgu { get => (Color)GetValue(IkincilVurguProperty); set => SetValue(IkincilVurguProperty, value); }
    public Thickness IcerikPayi { get => (Thickness)GetValue(IcerikPayiProperty); set => SetValue(IcerikPayiProperty, value); }
    public double Enerji { get => (double)GetValue(EnerjiProperty); set => SetValue(EnerjiProperty, value); }
    public double CizgiVurgusu { get; set; }

    public HudCerceve()
    {
        _zemin = new GraphicsView { Drawable = this, InputTransparent = true, BackgroundColor = Colors.Transparent };
        AutomationProperties.SetIsInAccessibleTree(_zemin, false);
        _govde = new ContentView { Padding = IcerikPayi };
        var katman = new Grid { SafeAreaEdges = SafeAreaEdges.None };
        katman.Children.Add(_zemin);
        katman.Children.Add(_govde);
        Content = katman;
        SizeChanged += (_, _) => _zemin.Invalidate();
    }

    private static void YenidenCiz(BindableObject bindable, object oldValue, object newValue) =>
        ((HudCerceve)bindable)._zemin?.Invalidate();

    public void CizgiyiGuncelle(double deger)
    {
        CizgiVurgusu = deger;
        _zemin.Invalidate();
    }

    private static PathF KesikDikdortgen(float x, float y, float w, float h, float k)
    {
        var p = new PathF();
        p.MoveTo(x + k, y); p.LineTo(x + w - k, y); p.LineTo(x + w, y + k);
        p.LineTo(x + w, y + h - k); p.LineTo(x + w - k, y + h);
        p.LineTo(x + k, y + h); p.LineTo(x, y + h - k); p.LineTo(x, y + k); p.Close();
        return p;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // ContentView ile iç çizim yüzeyinin ölçüleri dar ekranlarda aynı olmayabilir.
        float w = dirtyRect.Width, h = dirtyRect.Height;
        if (w < 20 || h < 20) return;
        float k = Math.Clamp(Math.Min(w, h) * 0.09f, 7, 19);
        float enerji = (float)Math.Clamp(Enerji, 0.2, 1);
        canvas.SaveState();
        try
        {
            using var dis = KesikDikdortgen(2, 2, w - 4, h - 4, k);
            using var ic = KesikDikdortgen(7, 7, w - 14, h - 14, Math.Max(4, k - 3));
            using var yuzey = KesikDikdortgen(10, 10, w - 20, h - 20, Math.Max(3, k - 5));
            canvas.FillColor = Color.FromArgb("#060E20"); canvas.FillPath(dis);
            canvas.StrokeColor = Vurgu; canvas.StrokeSize = 5; canvas.Alpha = 0.1f * enerji; canvas.DrawPath(dis);
            canvas.StrokeSize = 1.2f; canvas.Alpha = 0.8f * enerji; canvas.DrawPath(dis);
            canvas.FillColor = Color.FromArgb("#101E34"); canvas.Alpha = 1; canvas.FillPath(ic);
            canvas.StrokeColor = Color.FromArgb("#34516A"); canvas.StrokeSize = 1; canvas.DrawPath(ic);
            canvas.FillColor = Color.FromArgb("#0B1529"); canvas.FillPath(yuzey);

            // Düşük kontrastlı yüzey çizgileri okunabilir içeriğin arkasında kalır.
            canvas.ClipPath(yuzey);
            canvas.StrokeColor = Vurgu; canvas.StrokeSize = 0.6f; canvas.Alpha = 0.025f;
            for (float y = 26; y < h - 14; y += 28) canvas.DrawLine(12, y, w - 12, y);
            canvas.Alpha = 0.075f;
            canvas.DrawLine(12, h * 0.33f, 24, h * 0.33f + 12);
            canvas.DrawLine(24, h * 0.33f + 12, 24, Math.Min(h - 20, h * 0.33f + 40));
            canvas.DrawLine(w - 12, h * 0.65f, w - 24, h * 0.65f - 12);
        }
        finally { canvas.RestoreState(); }

        canvas.SaveState();
        try
        {
            // Ayrık enerji rayları ve bağlantı uçları çerçeveye derinlik verir.
            canvas.StrokeColor = Vurgu; canvas.StrokeSize = 2; canvas.Alpha = 0.9f * enerji;
            canvas.DrawLine(k + 6, 4, w * 0.27f, 4);
            canvas.DrawLine(4, k + 7, 4, Math.Min(h - k - 7, h * 0.27f));
            canvas.DrawLine(w - 4, h * 0.73f, w - 4, h - k - 7);
            canvas.DrawLine(w * 0.73f, h - 4, w - k - 6, h - 4);
            canvas.StrokeColor = IkincilVurgu; canvas.Alpha = 0.9f * enerji;
            canvas.DrawLine(w * 0.65f, 4, w - k - 7, 4);
            canvas.DrawLine(k + 7, h - 4, w * 0.35f, h - 4);
            canvas.FillColor = Vurgu;
            for (int i = 0; i < 3; i++)
            {
                canvas.Alpha = (0.35f + i * 0.2f) * enerji;
                canvas.FillRectangle(w * 0.44f + i * 7, 3, 4, 2);
            }
            canvas.StrokeSize = 2; canvas.StrokeColor = Colors.White;
            canvas.Alpha = (float)Math.Sin(Math.Clamp(CizgiVurgusu, 0, 1) * Math.PI) * 0.7f;
            canvas.DrawLine(w * 0.35f, 4, w * (0.35f + 0.3f * (float)CizgiVurgusu), 4);
        }
        finally { canvas.RestoreState(); }
    }
}
