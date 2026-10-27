"""Neon panel akışları: yalnız emulator-5554; uygulama kayıtlarını silmez."""
import argparse
import json
import re
import time

import android_smoke as qa

OUT = qa.ROOT / "output" / "neon-panel-validation"
OUT.mkdir(parents=True, exist_ok=True)
qa.OUT = OUT


def no_native(tree):
    assert not any(n.get("resource-id") in ["android:id/alertTitle", "android:id/button1",
        "android:id/parentPanel"] for n in qa.nodes(tree)), "Standart Android uyarısı bulundu."


def panel(title):
    tree = qa.wait(lambda t: title in qa.texts(t), f"Panel yok: {title}")
    no_native(tree)
    assert not qa.cards(tree), "Modal açıkken kart dokunmaları etkin."
    assert not qa.label_nodes(tree, "SİSTEM TARAMASI"), "Modal açıkken tarama etkin."
    titles = ["SİSTEM DURAKLATILDI", "PROTOKOL YENİDEN BAŞLATILSIN MI?", "SİSTEMDEN AYRIL?"]
    assert sum(s in qa.texts(tree) for s in titles) == 1, "Birden fazla modal başlık var."
    return tree


def back():
    qa.adb("shell", "input", "keyevent", "4")


def menu():
    return qa.wait(lambda t: bool(qa.label_nodes(t, "Sistem onarımını başlat", True)), "Ana menü yok.")


def start(count=12):
    tree = menu()
    prefix = {12: "Kolay,", 16: "Orta,", 20: "Zor,"}[count]
    selected = [n for n in qa.nodes(tree) if n.get("content-desc", "").startswith(prefix)]
    assert len(selected) == 1
    qa.tap_node(selected[0])
    qa.tap("Sistem onarımını başlat", True)
    return qa.wait_cards(count)


def pair(tree, first):
    positions = {k: qa.center(n) for k, n in qa.cards(tree).items()}
    for number in [first, first + 1]:
        qa.adb("shell", "input", "tap", *positions[number])


def stage1():
    tree = start()
    qa.tap("SİSTEM TARAMASI", tree=tree)
    tree = qa.wait_cards(12)
    pair(tree, 1)
    tree = qa.wait_cards(10)
    qa.tap("DURAKLAT", tree=tree)
    tree = panel("SİSTEM DURAKLATILDI")
    frozen = qa.counter(tree, "SÜRE")
    time.sleep(1.3)
    tree = qa.dump()
    assert qa.counter(tree, "SÜRE") == frozen
    qa.screenshot("asama1-duraklatma.png")
    back()
    tree = qa.wait_cards(10)
    assert qa.counter(tree, "HAMLE") == "1" and qa.counter(tree, "KURTARILAN MODÜL") == "1 / 6"

    qa.tap("YENİDEN BAŞLAT", tree=tree)
    tree = panel("PROTOKOL YENİDEN BAŞLATILSIN MI?")
    qa.screenshot("asama1-yeniden-baslat.png")
    qa.tap("VAZGEÇ", tree=tree)
    tree = panel("SİSTEM DURAKLATILDI")
    qa.tap("YENİDEN BAŞLAT", tree=tree)
    panel("PROTOKOL YENİDEN BAŞLATILSIN MI?")
    back()
    tree = panel("SİSTEM DURAKLATILDI")
    qa.tap("DEVAM ET", tree=tree)
    tree = qa.wait_cards(10)
    assert qa.counter(tree, "HAMLE") == "1" and qa.label_nodes(tree, "TARAMA KULLANILDI") == []

    # Çıkıştan geri: aktif oyun ve duraklatılmış oyun önceki durumlarına döner.
    back()
    tree = panel("SİSTEMDEN AYRIL?")
    qa.screenshot("asama1-cikis.png")
    back()
    tree = qa.wait_cards(10)
    qa.tap("DURAKLAT", tree=tree)
    tree = panel("SİSTEM DURAKLATILDI")
    qa.tap("ANA MENÜ", tree=tree)
    panel("SİSTEMDEN AYRIL?")
    back()
    tree = panel("SİSTEM DURAKLATILDI")
    qa.tap("ANA MENÜ", tree=tree)
    tree = panel("SİSTEMDEN AYRIL?")
    qa.tap("DEVAM ET", tree=tree)
    tree = qa.wait_cards(10)

    qa.tap("YENİDEN BAŞLAT", tree=tree)
    tree = panel("PROTOKOL YENİDEN BAŞLATILSIN MI?")
    qa.tap("YENİDEN BAŞLAT", tree=tree)
    tree = qa.wait_cards(12)
    assert qa.counter(tree, "HAMLE") == "0" and qa.counter(tree, "KURTARILAN MODÜL") == "0 / 6"
    assert qa.label_nodes(tree, "SİSTEM TARAMASI")

    # Çözülme devam ederken yeniden başlatma eski animasyonu yeni tahtaya taşımaz.
    restart = qa.center(qa.label_nodes(tree, "YENİDEN BAŞLAT")[-1])
    pair(tree, 1)
    time.sleep(0.45)
    qa.adb("shell", "input", "tap", *restart)
    tree = panel("PROTOKOL YENİDEN BAŞLATILSIN MI?")
    qa.tap("YENİDEN BAŞLAT", tree=tree)
    tree = qa.wait_cards(12)
    time.sleep(1.1)
    tree = qa.dump()
    assert len(qa.cards(tree)) == 12 and qa.counter(tree, "HAMLE") == "0"
    back()
    tree = panel("SİSTEMDEN AYRIL?")
    qa.tap("ANA MENÜ", tree=tree)
    menu()
    (OUT / "asama1.json").write_text(json.dumps({"durum": "başarılı", "cihaz": qa.DEVICE,
        "kontroller": ["duraklatma ve süre", "kart/tarama kilidi", "vazgeç", "yeniden başlatma",
                       "aktif/duraklatılmış oyunda geri", "çıkıştan devam", "efekt sırasında temiz yeniden başlatma",
                       "ana menüye çıkış", "standart Android uyarısı yok"]}, ensure_ascii=False, indent=2), encoding="utf-8")
    print("BAŞARILI: Aşama 1 neon oyun panelleri, geri tuşu ve oturum temizliği.", flush=True)


PANELS = [("Başarılar", "BAŞARILAR", "KAPAT"), ("İstatistikler", "İSTATİSTİKLER", "KAPAT"),
          ("Nasıl Oynanır", "NASIL OYNANIR?", "ANLADIM"), ("Hakkında", "NEURAL QUEST", "KAPAT"),
          ("Ayarlar", "AYARLAR", "KAPAT")]


def info_open(description, title, close):
    qa.tap(description, True, menu())
    tree = qa.wait(lambda t: title in qa.texts(t) and bool(qa.label_nodes(t, close)), f"Bilgi paneli yok: {title}")
    no_native(tree)
    assert not qa.label_nodes(tree, "Sistem onarımını başlat", True), "Arka menü etkin."
    assert len(qa.label_nodes(tree, close)) == 1, "İki kapatma düğmesi göründü."
    return tree


def scroll_body(tree):
    bodies = [n for n in qa.nodes(tree) if n.get("class") == "android.widget.ScrollView" and n.get("enabled") == "true"]
    # Shell'in dış ScrollView kapsayıcısı da enabled=true raporlanır.
    # Son ScrollView, XAML'de en üstteki bilgi katmanının gözlenen içerik alanıdır.
    assert bodies, "Kaydırılabilir panel yok."
    x1, y1, x2, y2 = map(int, re.findall(r"\d+", bodies[-1].get("bounds")))
    qa.adb("shell", "input", "swipe", (x1 + x2) // 2, int(y1 + (y2 - y1) * 0.82),
           (x1 + x2) // 2, int(y1 + (y2 - y1) * 0.2), "350")
    return qa.dump()


def stage2():
    before = qa.stats()
    for index, (description, title, close) in enumerate(PANELS):
        tree = info_open(description, title, close)
        if description == "İstatistikler":
            assert "TOPLAM OYUN" in qa.texts(tree) and "TOPLAM SKOR" in qa.texts(tree), "Yeni panel başa kaydırılmadı."
        qa.screenshot(f"asama2-{index}-ust.png")
        observed = set(qa.texts(tree))
        if description in ["Başarılar", "İstatistikler", "Nasıl Oynanır"]:
            for _ in range(4):
                tree = scroll_body(tree)
                observed.update(qa.texts(tree))
            qa.screenshot(f"asama2-{index}-alt.png")
        if description == "Başarılar":
            assert all(t in observed for t in ["İLK ADIM", "HIZLI ZİHİN", "USTA OYUNCU", "KOLEKSİYONCU", "MÜKEMMEL ONARIM"])
            assert observed & {"KAZANILDI", "KİLİTLİ"}
        if description == "İstatistikler":
            assert all(t in observed for t in ["TOPLAM OYUN", "TOPLAM SKOR", "KOLAY PROTOKOLÜ", "ORTA PROTOKOLÜ", "ZOR PROTOKOLÜ"])
        if description == "Nasıl Oynanır":
            assert any("Sistem Taraması ipucunu oyun başına" in t for t in observed)
            assert any("ZOR: 20 kart / 10 çift" in t for t in observed)
        if description == "Hakkında":
            assert "Sürüm: 1.0" in observed and "Oyun çevrimdışı çalışır." in observed
        if description == "Ayarlar":
            assert any("değiştirilebilir ses veya titreşim ayarı bulunmuyor" in t for t in observed)
        if index % 2:
            back()
        else:
            qa.tap(close, tree=tree)
        menu()
        print(f"BAŞARILI: {description} içeriği, kaydırma/kapatma ve arka menü kilidi.", flush=True)
    qa.adb("shell", "am", "force-stop", qa.PACKAGE)
    qa.launch()
    menu()
    time.sleep(0.8)  # Soğuk açılıştaki PNG yerleşimi tamamlanmadan konum önbelleğe alınmaz.
    assert before == qa.stats(), "Bilgi panelleri veya yeniden açılış kayıtları değiştirdi."
    (OUT / "asama2.json").write_text(json.dumps({"durum": "başarılı", "cihaz": qa.DEVICE,
        "kontroller": ["beş neon bilgi paneli", "kaydırılan içerikler", "tek modal", "arka menü kilidi",
                       "kapatma ve Android geri tuşu", "gerçek sürüm", "yeniden açılışta istatistikler korunur"],
        "istatistikler": before}, ensure_ascii=False, indent=2), encoding="utf-8")


def transitions():
    # Kapanan yardımın gecikmiş animasyonu yeni Ayarlar içeriğini silememeli.
    pid = qa.adb("shell", "pidof", qa.PACKAGE).strip()
    def close_generation():
        log = qa.adb("logcat", "-d", "--pid=" + pid, "-v", "raw", "-s", "NeuralQuestHud:D", "*:S")
        return max([int(g) for g in re.findall(r"KAPANIYOR\|(\d+)\|", log)], default=0)
    for cycle in range(3):
        tree = menu()
        settings = qa.center(qa.label_nodes(tree, "Ayarlar", True)[0])
        tree = info_open("Nasıl Oynanır", "NASIL OYNANIR?", "ANLADIM")
        close_point = qa.center(qa.label_nodes(tree, "ANLADIM")[0])
        before = close_generation()
        qa.adb("shell", "input", "tap", *close_point)
        deadline = time.monotonic() + 3
        while close_generation() <= before:
            assert time.monotonic() < deadline, "Kapatma olayı UI kuyruğunda işlenmedi."
            time.sleep(0.01)
        # Animasyon tamamlanmasını bekleme; işlenmiş kapatma olayından hemen sonra dokun.
        qa.adb("shell", "input", "tap", *settings)
        tree = qa.wait(lambda t: "AYARLAR" in qa.texts(t), "Ardışık kapat/aç sırasında yeni panel kayboldu.")
        assert "NASIL OYNANIR?" not in qa.texts(tree)
        assert len(qa.label_nodes(tree, "KAPAT")) == 1
        back()
        menu()
        print(f"BAŞARILI: Hızlı geçiş döngüsü {cycle + 1}/3.", flush=True)
    print("BAŞARILI: Üç ardışık kapat/aç döngüsü; eski geçiş yeni paneli temizlemedi.", flush=True)


def responsive():
    old_size = re.search(r"Override size: (\d+x\d+)", qa.adb("shell", "wm", "size"))
    old_density = re.search(r"Override density: (\d+)", qa.adb("shell", "wm", "density"))
    try:
        for size, density, name in [("960x1920", "480", "320x640"), ("1200x1920", "320", "600x960"),
                                    ("320x640", "160", "320x640-px"), ("600x960", "160", "600x960-px")]:
            qa.adb("shell", "wm", "size", size)
            qa.adb("shell", "wm", "density", density)
            time.sleep(1)
            # MAUI yerel yoğunluk ölçülerini Activity bağlamında tutar. Yeni ekranı
            # gerçek bir cihaz açılışı gibi yükle; çalışan Activity'nin eski DPI'sını taşıma.
            qa.adb("shell", "am", "force-stop", qa.PACKAGE)
            qa.launch()
            menu()
            time.sleep(0.8)
            for index, (description, title, close) in enumerate(PANELS):
                tree = info_open(description, title, close)
                qa.screenshot(f"responsive-{name}-{index}.png")
                node = qa.label_nodes(tree, close)[0]
                x1, y1, x2, y2 = map(int, re.findall(r"\d+", node.get("bounds")))
                width, height = map(int, size.split("x"))
                assert 0 <= x1 < x2 <= width and 0 <= y1 < y2 <= height, "Kapatma düğmesi kesildi."
                # Başlık ve sabit alt eylem sistem çubuklarının güvenli tarafında kalmalı.
                title_node = qa.label_nodes(tree, title)[0]
                _, title_top, _, _ = map(int, re.findall(r"\d+", title_node.get("bounds")))
                assert title_top > 20 * int(density) / 160
                assert y2 < height - 20 * int(density) / 160
                if description in ["Başarılar", "İstatistikler", "Nasıl Oynanır"]:
                    observed = set(qa.texts(tree))
                    for _ in range(6):
                        tree = scroll_body(tree)
                        observed.update(qa.texts(tree))
                    last = {"Başarılar": "MÜKEMMEL ONARIM", "İstatistikler": "ZOR PROTOKOLÜ",
                            "Nasıl Oynanır": "ZOR: 20 kart / 10 çift"}[description]
                    assert last in qa.texts(tree), f"Son modül erişilemiyor: {last}"
                    last_node = qa.label_nodes(tree, last)[0]
                    qa.center(last_node)  # Kırpılmış/sıfır yüksekliğe sahip erişilebilirlik düğümü yetmez.
                    _, last_top, _, last_bottom = map(int, re.findall(r"\d+", last_node.get("bounds")))
                    assert title_top < last_top < last_bottom <= y1, "Son modül sabit eylemin arkasında kaldı."
                    assert qa.label_nodes(tree, close)[0].get("bounds") == node.get("bounds"), "Sabit kapatma kaydı."
                    qa.screenshot(f"responsive-{name}-{index}-alt.png")
                if description in ["Hakkında", "Ayarlar"]:
                    for _ in range(2):
                        tree = scroll_body(tree)
                    last_text = "Sürüm: 1.0" if description == "Hakkında" else "Oyun çevrimdışı çalışır; Sistem Taraması her oyun için bir kez kullanılabilir."
                    last_node = qa.label_nodes(tree, last_text)[0]
                    last_bottom = list(map(int, re.findall(r"\d+", last_node.get("bounds"))))[3]
                    close_top = list(map(int, re.findall(r"\d+", qa.label_nodes(tree, close)[0].get("bounds"))))[1]
                    assert 0 <= close_top - last_bottom < 70 * int(density) / 160, "Kompakt panelde gereksiz boş alan kaldı."
                qa.tap(close, tree=tree)
                menu()
            tree = start(20)
            qa.tap("YENİDEN BAŞLAT", tree=tree)
            tree = panel("PROTOKOL YENİDEN BAŞLATILSIN MI?")
            qa.screenshot(f"responsive-{name}-oyun.png")
            back()
            tree = panel("SİSTEM DURAKLATILDI")
            qa.screenshot(f"responsive-{name}-duraklatma.png")
            qa.tap("ANA MENÜ", tree=tree)
            tree = panel("SİSTEMDEN AYRIL?")
            qa.screenshot(f"responsive-{name}-cikis.png")
            qa.tap("ANA MENÜ", tree=tree)
            menu()
            print(f"BAŞARILI: {name}: beş bilgi paneli ve oyun onayları.", flush=True)
    finally:
        qa.adb("shell", "wm", "size", old_size.group(1) if old_size else "reset")
        qa.adb("shell", "wm", "density", old_density.group(1) if old_density else "reset")
        qa.adb("shell", "am", "force-stop", qa.PACKAGE)
        qa.launch()
    (OUT / "responsive.json").write_text(json.dumps({"durum": "başarılı",
        "mantiksal_boyutlar_dp": ["320x640", "600x960"], "piksel_boyutlari_160dpi": ["320x640", "600x960"]}), encoding="utf-8")


if __name__ == "__main__":
    from pathlib import Path
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", choices=["1", "2", "responsive", "regression", "transitions"], default="1")
    parser.add_argument("--output-dir", type=Path)
    args = parser.parse_args()
    if args.output_dir:
        OUT = args.output_dir.resolve()
        OUT.mkdir(parents=True, exist_ok=True)
        qa.OUT = OUT
    assert qa.DEVICE == "emulator-5554"
    qa.adb("shell", "am", "force-stop", qa.PACKAGE)
    qa.launch()
    menu()
    time.sleep(0.8)
    if args.stage == "1":
        stage1()
    elif args.stage == "2":
        stage2()
    elif args.stage == "transitions":
        transitions()
    elif args.stage == "responsive":
        responsive()
    else:
        qa.full_test()
        tree = info_open("Başarılar", "BAŞARILAR", "KAPAT")
        for _ in range(4):
            tree = scroll_body(tree)
        # Yeni taramasız tamamlamanın başarısı gerçek cihaz kaydından açılmış olmalı.
        matching = []
        for node in qa.nodes(tree):
            values = set(qa.texts(node))
            if "MÜKEMMEL ONARIM" in values and "KAZANILDI" in values and "İLK ADIM" not in values:
                matching.append(node)
        assert matching, "Yeni taramasız tamamlamanın başarısı görünmedi."
        qa.screenshot("mukemmel-onarim-yeni-kayit.png")
        qa.tap("KAPAT", tree=tree)
        menu()
        print("BAŞARILI: Yeni taramasız oyun sonrası Mükemmel Onarım cihaz kaydından açıldı.", flush=True)
