"""Bağlı emülatörde kurulum, gerçek dokunmalar ve sonuç/kayıt akışı kontrolü."""
import argparse
import json
import re
import subprocess
import time
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "output" / "game-validation"
OUT.mkdir(parents=True, exist_ok=True)
ADB = r"C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe"
DEVICE = "emulator-5554"
PACKAGE = "com.companyname.neuralquest"


def adb(*args):
    run = subprocess.run([ADB, "-s", DEVICE, *map(str, args)],
                         capture_output=True, timeout=90)
    if run.returncode:
        raise RuntimeError(run.stderr.decode("utf-8", errors="replace") +
                           run.stdout.decode("utf-8", errors="replace"))
    return run.stdout.decode("utf-8", errors="replace")


def dump():
    adb("shell", "uiautomator", "dump", "/sdcard/NeuralQuestQA.xml")
    xml = adb("shell", "cat", "/sdcard/NeuralQuestQA.xml")
    (OUT / "latest.xml").write_text(xml, encoding="utf-8")
    return ET.fromstring(xml)


def nodes(tree):
    return list(tree.iter("node"))


def label_nodes(tree, value, description=False):
    key = "content-desc" if description else "text"
    return [n for n in nodes(tree) if n.get(key) == value and n.get("enabled") == "true"]


def center(node):
    values = list(map(int, re.findall(r"\d+", node.get("bounds", ""))))
    assert len(values) == 4 and values[2] > values[0] and values[3] > values[1], node.attrib
    return ((values[0] + values[2]) // 2, (values[1] + values[3]) // 2)


def tap_node(node):
    adb("shell", "input", "tap", *center(node))


def tap(value, description=False, tree=None):
    tree = tree if tree is not None else dump()
    found = label_nodes(tree, value, description)
    assert found, f"Düğme bulunamadı: {value}"
    tap_node(found[-1])


def wait(predicate, message, timeout=25):
    end = time.monotonic() + timeout
    while time.monotonic() < end:
        tree = dump()
        if predicate(tree):
            return tree
        time.sleep(0.2)
    raise AssertionError(message)


def texts(tree):
    return [n.get("text") for n in nodes(tree) if n.get("text")]


def counter(tree, title):
    values = texts(tree)
    return values[values.index(title) + 1]


def cards(tree):
    result = {}
    for n in nodes(tree):
        match = re.fullmatch(r"Kapalı kart (\d+)", n.get("content-desc", ""))
        if match and n.get("enabled") == "true":
            result[int(match[1])] = n
    return result


def screenshot(name):
    adb("shell", "screencap", "-p", "/sdcard/NeuralQuestQA.png")
    adb("pull", "/sdcard/NeuralQuestQA.png", OUT / name)


def launch():
    adb("shell", "monkey", "-p", PACKAGE, "-c", "android.intent.category.LAUNCHER", "1")
    time.sleep(1)


def stats():
    tree = wait(lambda t: bool(label_nodes(t, "İstatistikler", True)), "Ana menü bulunamadı.")
    tap("İstatistikler", True, tree)
    tree = wait(lambda t: "İSTATİSTİKLER" in texts(t), "İstatistikler açılmadı.")
    summary = f"Tamamlanan oyun: {counter(tree, 'TOPLAM OYUN')}\nToplam skor: {counter(tree, 'TOPLAM SKOR')}"
    tap("KAPAT", tree=tree)
    return summary


def cluster_count(values):
    centers = []
    for value in sorted(values):
        if not centers or value - centers[-1] > 4:
            centers.append(value)
    return len(centers)


def wait_cards(count):
    return wait(lambda t: len(cards(t)) == count, f"{count} kapalı kart bulunamadı.")


def complete(count, level):
    tree = wait_cards(count)
    positions = {key: center(value) for key, value in cards(tree).items()}
    expected_columns = 3 if count == 12 else 4
    expected_rows = 5 if count == 20 else 4
    assert cluster_count([x for x, y in positions.values()]) == expected_columns
    assert cluster_count([y for x, y in positions.values()]) == expected_rows
    screenshot(f"{level}-tahta.png")
    for first in range(1, count + 1, 2):
        adb("shell", "input", "tap", *positions[first])
        adb("shell", "input", "tap", *positions[first + 1])
        # Emülatör yükünde bir animasyon sabit beklemeden uzun sürebilir.
        # Sonraki çifti, kalan kapalı kartlar gerçekten yeniden etkinleşince gönder.
        remaining = count - first - 1
        if remaining:
            wait_cards(remaining)
    tree = wait(lambda t: any("BAŞARIYLA ONARILDI" in s for s in texts(t)), f"{level} sonucu açılmadı.")
    summary = next(s for s in texts(tree) if s.startswith("Protokol:"))
    assert f"Hamle: {count // 2}" in summary, summary
    assert f"Kurtarılan modül: {count // 2}" in summary, summary
    screenshot(f"{level}-sonuc.png")
    print(f"BAŞARILI: {level}, {count} kart tamamlandı; sonuç ve %100 onarım.", flush=True)
    return tree


def bootstrap(apk):
    assert DEVICE.startswith("emulator-"), "Bu betik yalnızca emülatörü hedefler."
    print(adb("install", "-r", apk).strip(), flush=True)
    adb("shell", "am", "force-stop", PACKAGE)
    launch()
    tree = wait(lambda t: bool(label_nodes(t, "Sistem onarımını başlat", True)), "Başlat düğmesi bulunamadı.")
    screenshot("ana-menu.png")
    print("BAŞARILI: APK kuruldu ve ana menü açıldı.", flush=True)
    tap("Sistem onarımını başlat", True, tree)
    tree = wait_cards(12)
    screenshot("ilk-oyun.png")
    (OUT / "ilk-oyun.xml").write_text(ET.tostring(tree, encoding="unicode"), encoding="utf-8")
    print("BAŞARILI: Başlat, 12 kartlık gerçek oyunu açtı.", flush=True)


def full_test():
    # Kurulumdan sonra açık kalan oyun, normal geri dönüş yoluyla ana menüye alınır.
    tree = dump()
    if cards(tree):
        adb("shell", "input", "keyevent", "4")
        tree = wait(lambda t: bool(label_nodes(t, "ANA MENÜ")), "Geri dönüş onayı gelmedi.")
        tap("ANA MENÜ", tree=tree)
    baseline = stats()
    before = int(re.search(r"Tamamlanan oyun: (\d+)", baseline).group(1))
    tap("Sistem onarımını başlat", True)
    tree = wait_cards(12)
    positions = {key: center(n) for key, n in cards(tree).items()}

    # Yanlış çift açıkken üçüncü dokunma kuyruğa sızmamalıdır.
    for number in [1, 3, 5]:
        adb("shell", "input", "tap", *positions[number])
    time.sleep(1.1)
    tree = wait_cards(12)
    assert counter(tree, "HAMLE") == "1", texts(tree)
    print("BAŞARILI: Yanlış eşleşme ve hızlı üçüncü dokunma.", flush=True)

    tap("DURAKLAT", tree=tree)
    tree = wait(lambda t: "SİSTEM DURAKLATILDI" in texts(t), "Duraklatma paneli açılmadı.")
    frozen = counter(tree, "SÜRE")
    time.sleep(1.4)
    tree = dump()
    assert counter(tree, "SÜRE") == frozen
    tap("DEVAM ET", tree=tree)
    tree = wait_cards(12)
    print("BAŞARILI: Duraklatmada gerçek sayaç dondu, devam çalıştı.", flush=True)

    # Arka plana geçiş oyunu kendiliğinden duraklatmalıdır.
    adb("shell", "input", "keyevent", "3")
    time.sleep(1)
    launch()
    tree = wait(lambda t: "SİSTEM DURAKLATILDI" in texts(t), "Arka planda oyun duraklamadı.")
    tap("DEVAM ET", tree=tree)
    tree = wait_cards(12)

    tap_node(cards(tree)[1])
    tap("SİSTEM TARAMASI")
    time.sleep(1.2)
    tree = wait_cards(12)
    hint = [n for n in nodes(tree) if n.get("text") == "TARAMA KULLANILDI"]
    assert hint and hint[-1].get("enabled") == "false"
    assert counter(tree, "HAMLE") == "1"
    print("BAŞARILI: Bir kerelik tarama, yarım seçim ve değişmeyen hamle.", flush=True)

    tap("YENİDEN BAŞLAT", tree=tree)
    tree = wait(lambda t: "PROTOKOL YENİDEN BAŞLATILSIN MI?" in texts(t), "Yeniden başlatma onayı yok.")
    tap("YENİDEN BAŞLAT", tree=tree)
    tree = wait_cards(12)
    assert counter(tree, "HAMLE") == "0"
    assert label_nodes(tree, "SİSTEM TARAMASI")
    print("BAŞARILI: Yeniden başlatma sayaçları ve ipucunu sıfırladı.", flush=True)

    tree = complete(12, "Kolay")
    tap("SONRAKİ SEVİYE", tree=tree)
    tree = complete(16, "Orta")
    tap("SONRAKİ SEVİYE", tree=tree)
    tree = complete(20, "Zor")
    assert not label_nodes(tree, "SONRAKİ SEVİYE")
    tap("ANA MENÜ", tree=tree)
    after = stats()
    assert int(re.search(r"Tamamlanan oyun: (\d+)", after).group(1)) == before + 3, after
    adb("shell", "am", "force-stop", PACKAGE)
    launch()
    reread = stats()
    assert after == reread, "Uygulama yeniden açılınca istatistikler değişti."
    screenshot("istatistik-sonrasi-menu.png")
    result = {"durum": "başarılı", "cihaz": DEVICE, "tamamlanan_test_oyunu": 3,
              "istatistikler": after, "kontroller": [
                  "Kolay/Orta/Zor gerçek tamamlama", "tek dokunma kilidi", "duraklatma",
                  "arka plan", "sistem taraması", "yeniden başlatma", "sonraki seviye",
                  "Android geri tuşu", "uygulama yeniden açılışında yerel kayıt"]}
    (OUT / "sonuc.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print("BAŞARILI: Üç seviye ve uygulama yeniden açılışında gerçek Preferences kaydı doğrulandı.", flush=True)


def responsive_test():
    tree = wait(lambda t: bool(label_nodes(t, "Sistem onarımını başlat", True)), "Ana menü bulunamadı.")
    tap("Zor, 20 kart, 10 eşleşme", True, tree)
    tap("Sistem onarımını başlat", True)
    wait_cards(20)
    previous_size = re.search(r"Override size: (\d+x\d+)", adb("shell", "wm", "size"))
    previous_density = re.search(r"Override density: (\d+)", adb("shell", "wm", "density"))
    results = []
    try:
        for size, density, name in [("960x1920", "480", "320x640"), ("1200x1920", "320", "600x960")]:
            adb("shell", "wm", "size", size)
            adb("shell", "wm", "density", density)
            time.sleep(1)
            tree = dump()
            if label_nodes(tree, "DEVAM ET"):
                tap("DEVAM ET", tree=tree)
            tree = wait_cards(20)
            for node in cards(tree).values():
                x1, y1, x2, y2 = map(int, re.findall(r"\d+", node.get("bounds")))
                assert 1.20 < (y2 - y1) / (x2 - x1) < 1.30, "Bir kartın hücresi kesildi."
            assert label_nodes(tree, "SİSTEM TARAMASI") and label_nodes(tree, "YENİDEN BAŞLAT")
            screenshot(f"Zor-{name}.png")
            results.append({"olcu": name, "gorunen_kart": 20, "duzen": "4x5", "kesilme": False})
            print(f"BAŞARILI: {name} MAUI biriminde 20 kart aynı ekranda ve kesilmeden görünüyor.", flush=True)
    finally:
        adb("shell", "wm", "size", previous_size.group(1) if previous_size else "reset")
        adb("shell", "wm", "density", previous_density.group(1) if previous_density else "reset")
    time.sleep(1)
    tree = dump()
    if label_nodes(tree, "DEVAM ET"):
        tap("DEVAM ET", tree=tree)
    tree = wait_cards(20)
    positions = {key: center(value) for key, value in cards(tree).items()}
    for number in [1, 3]:
        adb("shell", "input", "tap", *positions[number])
    adb("shell", "input", "keyevent", "4")
    tree = wait(lambda t: bool(label_nodes(t, "ANA MENÜ")), "Bekleyen işlem sırasında geri dönüş açılamadı.")
    tap("ANA MENÜ", tree=tree)
    wait(lambda t: bool(label_nodes(t, "Sistem onarımını başlat", True)), "Ana menüye dönülemedi.")
    (OUT / "responsive-sonuc.json").write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")
    print("BAŞARILI: Ekran ölçüsü geri yüklendi; bekleyen eşleşmeden güvenli çıkış yapıldı.", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--bootstrap", type=Path)
    parser.add_argument("--responsive", action="store_true")
    args = parser.parse_args()
    try:
        if args.bootstrap:
            bootstrap(args.bootstrap.resolve())
        elif args.responsive:
            responsive_test()
        else:
            full_test()
    except Exception:
        try:
            screenshot("hata.png")
            (OUT / "hata-logcat.txt").write_text(adb("logcat", "-d", "-t", "250"), encoding="utf-8")
        except Exception:
            pass
        raise

