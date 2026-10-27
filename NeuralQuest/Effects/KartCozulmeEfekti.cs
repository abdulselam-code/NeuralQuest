using Microsoft.Maui.Graphics;

namespace NeuralQuest.Effects;

// İki kart tek bir MAUI animasyonuyla çizilir; ayrı zamanlayıcı veya parçacık görünümleri oluşturulmaz.
internal sealed class KartCozulmeEfekti : IDrawable
{
    private const string AnimasyonAdi = "KartCozulmesi";
    private const uint Sure = 680;
    private const float KartKaybolmaAni = 420;
    private static readonly Color[] Renkler =
        [Color.FromArgb("#39EFFF"), Color.FromArgb("#468CFF"), Color.FromArgb("#CB7AFF")];
    private readonly GraphicsView _katman;
    private readonly Grid _tahta;
    private readonly Image[] _kartlar;
    private readonly Parcacik[][] _parcaciklar;
    private readonly TaskCompletionSource<bool> _tamamlanma = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private float _zaman;
    private bool _bitti;

    private readonly record struct Parcacik(float YonX, float YonY, float Uzaklik, float Boyut,
        float Gecikme, float Omur, int Renk);

    public KartCozulmeEfekti(GraphicsView katman, Grid tahta, Image[] kartlar)
    {
        _katman = katman;
        _tahta = tahta;
        _kartlar = kartlar;
        _parcaciklar = new Parcacik[kartlar.Length][];
        for (int kart = 0; kart < kartlar.Length; kart++)
        {
            var rastgele = new Random(173 + kart * 97);
            _parcaciklar[kart] = new Parcacik[32];
            for (int i = 0; i < _parcaciklar[kart].Length; i++)
            {
                double aci = (i + rastgele.NextDouble() * 0.7) * Math.Tau / _parcaciklar[kart].Length;
                _parcaciklar[kart][i] = new Parcacik((float)Math.Cos(aci), (float)Math.Sin(aci),
                    0.5f + (float)rastgele.NextDouble() * 0.45f,
                    0.85f + (float)rastgele.NextDouble() * 0.5f,
                    95 + (float)rastgele.NextDouble() * 50,
                    420 + (float)rastgele.NextDouble() * 115, i % Renkler.Length);
            }
        }
    }

    public Task OynatAsync()
    {
        _katman.Drawable = this;
        _katman.IsVisible = true;
        KareyiGuncelle(0);
        new Animation(KareyiGuncelle).Commit(_katman, AnimasyonAdi, rate: 16, length: Sure,
            finished: (_, iptal) => Bitir(iptal));
        return _tamamlanma.Task;
    }

    public void Durdur()
    {
        if (_bitti)
            return;
        _katman.AbortAnimation(AnimasyonAdi);
        Bitir(true);
    }

    private void Bitir(bool iptal)
    {
        if (_bitti)
            return;
        _bitti = true;
        _katman.IsVisible = false;
        _katman.Drawable = null;
        foreach (var kart in _kartlar)
        {
            kart.Scale = 1;
            kart.Opacity = 0;
        }
        // İptal edilen bekleyiş de tamamlanır; eski oturum yeni oyuna geri dönemez.
        if (iptal)
            _tamamlanma.TrySetCanceled();
        else
            _tamamlanma.TrySetResult(true);
    }

    private void KareyiGuncelle(double ilerleme)
    {
        if (_bitti)
            return;
        _zaman = (float)ilerleme * Sure;
        float buyume = Aralik(_zaman, 0, 100);
        float cozulme = Aralik(_zaman, 110, KartKaybolmaAni);
        foreach (var kart in _kartlar)
        {
            kart.Scale = _zaman < 100
                ? 1 + 0.055 * Easing.CubicOut.Ease(buyume)
                : 1.055 - 0.505 * Easing.CubicIn.Ease(cozulme);
            kart.Opacity = 1 - Easing.CubicIn.Ease(cozulme);
        }
        _katman.Invalidate();
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (_bitti)
            return;
        canvas.SaveState();
        try
        {
            for (int i = 0; i < _kartlar.Length; i++)
            {
                var kart = _kartlar[i];
                if (kart.Parent is not Grid hucre || hucre.Width <= 0 || hucre.Height <= 0)
                    continue;
                // Ölçüler her karede yerleşimden alınır; ekran boyutuna veya çözünürlüğüne bağlı değildir.
                float genislik = (float)Math.Min(hucre.Width, hucre.Height * 1122.0 / 1402);
                float yukseklik = genislik * 1402 / 1122;
                float merkezX = (float)(_tahta.X + hucre.X + hucre.Width / 2);
                float merkezY = (float)(_tahta.Y + hucre.Y + hucre.Height / 2);
                CerceveyiCiz(canvas, merkezX, merkezY, genislik, yukseklik, (float)kart.Scale);
                EnerjiHalkasiniCiz(canvas, merkezX, merkezY, genislik);
                ParcaciklariCiz(canvas, _parcaciklar[i], merkezX, merkezY, genislik, yukseklik);
                IsikIziniCiz(canvas, merkezX, merkezY, genislik);
            }
        }
        finally { canvas.RestoreState(); }
    }

    private void CerceveyiCiz(ICanvas canvas, float x, float y, float genislik, float yukseklik, float olcek)
    {
        float parlama = (1 - Aralik(_zaman, 140, 350)) * (0.65f + 0.35f * Aralik(_zaman, 0, 60));
        if (parlama <= 0)
            return;
        float w = genislik * 0.88f * olcek, h = yukseklik * 0.91f * olcek;
        float sol = x - w / 2, ust = y - h / 2, kose = w * 0.09f;
        using var yol = new PathF();
        yol.MoveTo(sol + kose, ust);
        yol.LineTo(sol + w - kose, ust);
        yol.LineTo(sol + w, ust + kose);
        yol.LineTo(sol + w, ust + h - kose);
        yol.LineTo(sol + w - kose, ust + h);
        yol.LineTo(sol + kose, ust + h);
        yol.LineTo(sol, ust + h - kose);
        yol.LineTo(sol, ust + kose);
        yol.Close();
        canvas.FillColor = Renkler[0];
        canvas.Alpha = parlama * 0.24f;
        canvas.FillPath(yol);
        canvas.StrokeColor = Renkler[0];
        canvas.StrokeSize = 8;
        canvas.Alpha = parlama * 0.32f;
        canvas.DrawPath(yol);
        canvas.StrokeSize = 3;
        canvas.Alpha = parlama * 0.9f;
        canvas.DrawPath(yol);
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 1;
        canvas.Alpha = parlama;
        canvas.DrawPath(yol);
    }

    private void EnerjiHalkasiniCiz(ICanvas canvas, float x, float y, float w)
    {
        float t = Aralik(_zaman, 80, 450);
        if (t <= 0 || t >= 1)
            return;
        float r = w * (0.12f + 0.57f * t);
        canvas.StrokeColor = Renkler[1];
        canvas.StrokeSize = 7;
        canvas.Alpha = (1 - t) * 0.3f;
        canvas.DrawEllipse(x - r, y - r, r * 2, r * 2);
        canvas.StrokeColor = Renkler[0];
        canvas.StrokeSize = 2;
        canvas.Alpha = (1 - t) * 0.9f;
        canvas.DrawEllipse(x - r, y - r, r * 2, r * 2);
    }

    private void ParcaciklariCiz(ICanvas canvas, Parcacik[] parcaciklar, float x, float y, float w, float h)
    {
        foreach (var parcacik in parcaciklar)
        {
            float t = (_zaman - parcacik.Gecikme) / parcacik.Omur;
            if (t <= 0 || t >= 1)
                continue;
            float yol = 1 - (1 - t) * (1 - t);
            // Yayılım kartın çevresinde kalır; küçük kartlarda da en az 38 dp hareket eder.
            float yayilim = Math.Clamp(w, 76, 120);
            float dx = parcacik.YonX * yayilim * parcacik.Uzaklik;
            float dy = parcacik.YonY * yayilim * parcacik.Uzaklik;
            float px = x + dx * yol, py = y + dy * yol;
            float boyut = Math.Clamp(w * 0.065f, 4.5f, 7.5f) * parcacik.Boyut * (1 - t * 0.8f);
            float gorunurluk = Math.Min(1, t * 15) * (1 - t * t);
            var renk = Renkler[parcacik.Renk];

            // Hareket yönünün gerisindeki üç kısa bölüm neon ışık izini oluşturur.
            canvas.StrokeColor = renk;
            canvas.StrokeSize = Math.Max(1, boyut * 0.5f);
            for (int iz = 3; iz >= 1; iz--)
            {
                float bas = Math.Max(0, yol - iz * 0.045f);
                float son = Math.Max(0, yol - (iz - 1) * 0.045f);
                canvas.Alpha = gorunurluk * (4 - iz) * 0.16f;
                canvas.DrawLine(x + dx * bas, y + dy * bas, x + dx * son, y + dy * son);
            }
            canvas.FillColor = renk;
            canvas.Alpha = gorunurluk * 0.16f;
            canvas.FillRectangle(px - boyut * 1.6f, py - boyut * 1.6f, boyut * 3.2f, boyut * 3.2f);
            canvas.Alpha = gorunurluk;
            canvas.FillRectangle(px - boyut / 2, py - boyut / 2, boyut, boyut);
            canvas.FillColor = Colors.White;
            canvas.Alpha = gorunurluk * 0.8f;
            canvas.FillRectangle(px - boyut / 5, py - boyut / 5, boyut * 0.4f, boyut * 0.4f);
        }
    }

    private void IsikIziniCiz(ICanvas canvas, float x, float y, float w)
    {
        if (_zaman < KartKaybolmaAni)
            return;
        // Kart kaybolduktan sonra tam 260 ms hafif neon iz kalır.
        float t = Aralik(_zaman, KartKaybolmaAni, Sure);
        float parlama = (1 - t) * (1 - t);
        float yariCap = w * (0.12f + 0.26f * t);
        canvas.StrokeColor = Renkler[2];
        canvas.StrokeSize = 5;
        canvas.Alpha = parlama * 0.18f;
        canvas.DrawEllipse(x - yariCap, y - yariCap * 0.35f, yariCap * 2, yariCap * 0.7f);
        canvas.StrokeColor = Renkler[0];
        canvas.StrokeSize = 1.3f;
        canvas.Alpha = parlama * 0.8f;
        canvas.DrawEllipse(x - yariCap, y - yariCap * 0.35f, yariCap * 2, yariCap * 0.7f);
        canvas.StrokeColor = Colors.White;
        canvas.Alpha = parlama;
        canvas.DrawLine(x - w * 0.28f, y, x + w * 0.28f, y);
    }

    private static float Aralik(float deger, float baslangic, float bitis) =>
        Math.Clamp((deger - baslangic) / (bitis - baslangic), 0, 1);
}
