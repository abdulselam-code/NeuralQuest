using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralQuest.Models;

namespace NeuralQuest.Services;

public sealed class SeviyeIstatistigi
{
    public int TamamlanmaSayisi { get; set; }
    public long? EnIyiSureTicks { get; set; }
    public int? EnAzHamle { get; set; }
    public int EnIyiSkor { get; set; }
}

public sealed class OyunIstatistikleri
{
    public int ToplamTamamlananOyun { get; set; }
    public long ToplamSkor { get; set; }
    public Guid SonOyunKimligi { get; set; }
    public Dictionary<string, SeviyeIstatistigi> Seviyeler { get; set; } = new();
    // Eski kayıtlarda tarama bilgisi yoktur; yokluk başarı kanıtı sayılmaz.
    public bool TaramasizOyunTamamlandi { get; set; }
}

public sealed record BasariDurumu(string Baslik, string Aciklama, bool Kazanildi);

public static class OyunBasarilari
{
    public static IReadOnlyList<BasariDurumu> Hesapla(OyunIstatistikleri kayit)
    {
        bool Tamamlandi(OyunSeviyesi seviye) =>
            kayit.Seviyeler.GetValueOrDefault(seviye.ToString())?.TamamlanmaSayisi > 0;
        bool zor = Tamamlandi(OyunSeviyesi.Zor);
        return
        [
            new("İLK ADIM", "İlk seviyeyi tamamla.", Tamamlandi(OyunSeviyesi.Kolay)),
            new("HIZLI ZİHİN", "Bir oyunu 1 dakikadan kısa sürede tamamla.",
                kayit.Seviyeler.Values.Any(s => s.TamamlanmaSayisi > 0 &&
                    s.EnIyiSureTicks is >= 0 and < TimeSpan.TicksPerMinute)),
            new("USTA OYUNCU", "Zor seviyeyi tamamla.", zor),
            // Tamamlanmış Zor protokolü, oyundaki on farklı modülün tamamının kanıtıdır.
            new("KOLEKSİYONCU", "On farklı güvenlik modülünü eşleştir.", zor),
            new("MÜKEMMEL ONARIM", "Bir oyunu Sistem Taraması kullanmadan tamamla.", kayit.TaramasizOyunTamamlandi)
        ];
    }
}

[JsonSerializable(typeof(OyunIstatistikleri))]
internal partial class IstatistikJsonBaglami : JsonSerializerContext { }

// Depolama işlemleri dışarıdan verilir; aynı kayıt biçimi cihazda ve mantık testlerinde kullanılır.
public sealed class OyunIstatistikServisi(Func<string, string> oku, Action<string, string> yaz)
{
    private const string Anahtar = "neuralquest.istatistik.v1";
    private static readonly object KayitKilidi = new();

    public OyunIstatistikleri Oku()
    {
        lock (KayitKilidi)
        {
            try
            {
                string json = oku(Anahtar);
                if (string.IsNullOrWhiteSpace(json))
                    return new();
                var kayit = JsonSerializer.Deserialize(json, IstatistikJsonBaglami.Default.OyunIstatistikleri) ?? new();
                kayit.Seviyeler ??= new();
                return kayit;
            }
            catch (JsonException)
            {
                // Bozulmuş bir kayıt oyunun açılmasını engellemez.
                return new();
            }
        }
    }

    public void Kaydet(OyunSonucu sonuc)
    {
        lock (KayitKilidi)
        {
            var kayit = Oku();
            // Sonuç panelinin tekrar açılması aynı oyunu ikinci kez saydırmaz.
            if (kayit.SonOyunKimligi == sonuc.OyunKimligi)
                return;
            string anahtar = sonuc.Seviye.ToString();
            if (!kayit.Seviyeler.TryGetValue(anahtar, out var seviye))
                kayit.Seviyeler[anahtar] = seviye = new();

            seviye.TamamlanmaSayisi++;
            seviye.EnIyiSureTicks = Math.Min(seviye.EnIyiSureTicks ?? long.MaxValue, sonuc.Sure.Ticks);
            seviye.EnAzHamle = Math.Min(seviye.EnAzHamle ?? int.MaxValue, sonuc.Hamle);
            seviye.EnIyiSkor = Math.Max(seviye.EnIyiSkor, sonuc.Skor);
            kayit.ToplamTamamlananOyun++;
            kayit.ToplamSkor += sonuc.Skor;
            kayit.SonOyunKimligi = sonuc.OyunKimligi;
            kayit.TaramasizOyunTamamlandi |= sonuc.TaramaKullanildi == false;
            // Tek JSON değeri, toplamlar ve seviye rekorlarının birlikte saklanmasını sağlar.
            yaz(Anahtar, JsonSerializer.Serialize(kayit, IstatistikJsonBaglami.Default.OyunIstatistikleri));
        }
    }
}

