using NeuralQuest.Models;

namespace NeuralQuest.Services;

// Ayrı anahtar: serbest oyun rekorları ve başarıları değişmez.
public sealed class HikayeIlerlemeServisi(Func<string, string> oku, Action<string, string> yaz)
{
    private const string Anahtar = "neuralquest.hikaye.v1";
    public const int SeviyeSayisi = 9;
    public static int KartSayisi(int seviye) => seviye is >= 1 and <= SeviyeSayisi
        ? (seviye + 1) * 2 : throw new ArgumentOutOfRangeException(nameof(seviye));

    public (int Seviye, bool Tamamlandi) Oku()
    {
        var parcalar = oku(Anahtar).Split('|');
        return parcalar.Length == 2 && int.TryParse(parcalar[0], out int seviye) &&
            seviye is >= 1 and <= 10 && parcalar[1] is "0" or "1" && (seviye != 10 || parcalar[1] == "1")
            ? (seviye, parcalar[1] == "1") : (1, false);
    }

    public int Baslat()
    {
        var kayit = Oku();
        if (kayit.Seviye <= SeviyeSayisi) return kayit.Seviye;
        yaz(Anahtar, "1|1");
        return 1;
    }

    public void Kaydet(OyunOturumu oyun)
    {
        if (!oyun.Tamamlandi || oyun.HikayeSeviyesi is not int seviye) return;
        var kayit = Oku();
        // Tekrar gösterilen sonuç, yarım oyun ve eski oturum seviye açamaz.
        if (kayit.Seviye != seviye) return;
        yaz(Anahtar, $"{seviye + 1}|{(kayit.Tamamlandi || seviye == SeviyeSayisi ? 1 : 0)}");
    }
}
