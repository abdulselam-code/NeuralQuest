namespace NeuralQuest.Services;

public static class CihazKayitlari
{
    public static HikayeIlerlemeServisi Hikaye { get; } = new(
        anahtar => Preferences.Default.Get(anahtar, string.Empty),
        (anahtar, deger) => Preferences.Default.Set(anahtar, deger));

    public static OyunIstatistikServisi Istatistikler { get; } = new(
        anahtar => Preferences.Default.Get(anahtar, string.Empty),
        (anahtar, deger) => Preferences.Default.Set(anahtar, deger));
}

