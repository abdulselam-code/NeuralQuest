"""Aşama 4: yalnız emulator-5554 üzerinde gerçek dokunmalar ve görsel kanıtlar.

Uygulama verilerini silmez. Tamamlanan deneme oyunları emülatör istatistiğine eklenir.
"""
import concurrent.futures
import json
import subprocess
import time

import android_smoke as qa

OUT = qa.ROOT / "output" / "asama4-validation"
OUT.mkdir(parents=True, exist_ok=True)
qa.OUT = OUT


def frame(name):
    data = subprocess.run([qa.ADB, "-s", qa.DEVICE, "exec-out", "screencap", "-p"],
                          capture_output=True, check=True, timeout=20).stdout
    (OUT / name).write_bytes(data)


def touch(point):
    qa.adb("shell", "input", "tap", *point)


def points(tree):
    return {key: qa.center(node) for key, node in qa.cards(tree).items()}


def pair(positions, first):
    touch(positions[first])
    touch(positions[first + 1])


def menu():
    return qa.wait(lambda t: bool(qa.label_nodes(t, "Sistem onarımını başlat", True)), "Ana menü yok.")


def start(count):
    tree = menu()
    title = {12: "Kolay, 12 kart, 6 eşleşme", 16: "Orta, 16 kart, 8 eşleşme", 20: "Zor, 20 kart, 10 eşleşme"}[count]
    selected = [n for n in qa.nodes(tree) if n.get("content-desc", "").startswith(title)]
    assert len(selected) == 1, title
    qa.tap_node(selected[0])
    qa.tap("Sistem onarımını başlat", True)
    time.sleep(1)
    frame(f"{count}-baslangic-bildirimi.png")
    return qa.wait_cards(count)


def leave():
    qa.adb("shell", "input", "keyevent", "4")
    tree = qa.wait(lambda t: bool(qa.label_nodes(t, "ANA MENÜ")), "Çıkış onayı yok.")
    qa.tap("ANA MENÜ", tree=tree)
    menu()


def levels():
    for count in [12, 16, 20]:
        tree = start(count)
        pos = points(tree)
        screenshot_counter = 0
        for first in range(1, count + 1, 2):
            pair(pos, first)
            done = (first + 1) // 2
            threshold = next((t for t in [25, 50, 75]
                              if (done - 1) * 200 < count * t <= done * 200), None)
            if threshold is not None:
                time.sleep(0.32)
                frame(f"{count}-esik-{threshold}.png")
                # uiautomator durağan ekran bekler; kısa mesaj döküm gelmeden sönebilir.
                # İçerik karelerde görsel olarak, eşikler mantık testlerinde doğrulanır.
                screenshot_counter += 1
                time.sleep(0.55)
                # Bir sonraki eşleşme mevcut bildirimin süresi içinde yapılır.
            else:
                time.sleep(0.85)
            # Ekran yakalama/yazılımsal GPU gecikmesi sabit uykudan uzun olabilir.
            # Sonraki çifti animasyon kilidi kalkmadan gönderip dokunma kaybetme.
            if done * 2 < count:
                qa.wait_cards(count - done * 2)
        tree = qa.wait(lambda t: "SİSTEM HAFIZASI BAŞARIYLA ONARILDI!" in qa.texts(t), "Sonuç yok.")
        assert screenshot_counter == 3
        assert not any(s in qa.texts(tree) for s in ["VERİLER KURTARILIYOR...",
            "SİNİR AĞLARI YENİDEN BAĞLANIYOR.", "SİSTEM KARARLILIĞI SAĞLANIYOR."])
        summary = next(s for s in qa.texts(tree) if s.startswith("Protokol:"))
        assert f"Hamle: {count // 2}" in summary
        frame(f"{count}-sonuc.png")
        qa.tap("ANA MENÜ", tree=tree)
        menu()
        print(f"BAŞARILI: {count} kart: eşik kareleri kaydedildi, etkileşim ve tek %100 sonuç başlığı doğrulandı.", flush=True)


def lifecycle_and_effect():
    tree = start(12)
    pos = points(tree)
    pause = qa.center(qa.label_nodes(tree, "DURAKLAT")[-1])
    restart = qa.center(qa.label_nodes(tree, "YENİDEN BAŞLAT")[-1])
    touch(pos[1])
    touch(pos[2])
    origin = time.monotonic()

    def capture(delay):
        time.sleep(max(0, origin + delay - time.monotonic()))
        frame(f"efekt-{int(delay * 1000):04d}ms.png")

    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        pending = [pool.submit(capture, d) for d in [0.06, 0.18, 0.30, 0.46, 0.62, 0.90]]
        touch(pos[3])
        for task in pending:
            task.result()
    tree = qa.wait_cards(10)
    assert qa.counter(tree, "HAMLE") == "1"
    assert points(tree) == {key: value for key, value in pos.items() if key > 2}
    pair(pos, 3)  # %33: ilk eşik
    time.sleep(0.6)
    touch(pause)
    tree = qa.wait(lambda t: "SİSTEM DURAKLATILDI" in qa.texts(t), "Duraklatma yok.")
    assert qa.counter(tree, "KURTARILAN MODÜL") == "2 / 6", qa.texts(tree)
    time.sleep(2.6)
    tree = qa.dump()
    qa.tap("DEVAM ET", tree=tree)
    time.sleep(0.18)
    frame("duraklatma-sonrasi-bildirim.png")
    tree = qa.dump()
    assert qa.counter(tree, "KURTARILAN MODÜL") == "2 / 6"
    pair(pos, 5)  # %50 bildirimi sürerken yeniden başlat
    time.sleep(0.25)
    touch(restart)
    tree = qa.wait(lambda t: "PROTOKOL YENİDEN BAŞLATILSIN MI?" in qa.texts(t), "Yeniden başlat onayı yok.")
    qa.tap("YENİDEN BAŞLAT", tree=tree)
    time.sleep(0.3)
    frame("yeniden-baslatma-kritik.png")
    tree = qa.wait_cards(12)
    assert qa.counter(tree, "HAMLE") == "0"
    time.sleep(2.5)
    tree = qa.dump()
    assert "SİNİR AĞLARI YENİDEN BAĞLANIYOR." not in qa.texts(tree)
    pos = points(tree)
    pair(pos, 1)
    time.sleep(0.9)
    pair(pos, 3)
    time.sleep(0.25)
    leave()
    tree = start(12)
    time.sleep(2.5)
    tree = qa.dump()
    assert len(qa.cards(tree)) == 12 and qa.counter(tree, "HAMLE") == "0"
    assert "VERİLER KURTARILIYOR..." not in qa.texts(tree)
    leave()
    print("BAŞARILI: Efekt kareleri, üçüncü dokunma, duraklatma, yeniden başlatma ve çıkış temizliği.", flush=True)


def responsive_effect():
    import re
    tree = start(20)
    old_size = re.search(r"Override size: (\d+x\d+)", qa.adb("shell", "wm", "size"))
    old_density = re.search(r"Override density: (\d+)", qa.adb("shell", "wm", "density"))
    try:
        for size, density, name, first, remaining in [
                ("960x1920", "480", "320x640", 1, 18),
                ("1200x1920", "320", "600x960", 3, 16)]:
            qa.adb("shell", "wm", "size", size)
            qa.adb("shell", "wm", "density", density)
            time.sleep(1)
            tree = qa.dump()
            if qa.label_nodes(tree, "DEVAM ET"):
                qa.tap("DEVAM ET", tree=tree)
            tree = qa.wait_cards(remaining + 2)
            for node in qa.cards(tree).values():
                x1, y1, x2, y2 = map(int, re.findall(r"\d+", node.get("bounds")))
                assert 1.20 < (y2 - y1) / (x2 - x1) < 1.30, "Kart hücresi kesildi."
            pair(points(tree), first)
            time.sleep(0.25)
            frame(f"efekt-{name}.png")
            time.sleep(0.18)
            frame(f"efekt-{name}-yayilim.png")
            time.sleep(0.18)
            frame(f"efekt-{name}-iz.png")
            qa.wait_cards(remaining)
            print(f"BAŞARILI: {name}, Zor seviyede 20 hücre ve görünür efekt karesi.", flush=True)
    finally:
        qa.adb("shell", "wm", "size", old_size.group(1) if old_size else "reset")
        qa.adb("shell", "wm", "density", old_density.group(1) if old_density else "reset")
    time.sleep(1)
    tree = qa.dump()
    if qa.label_nodes(tree, "DEVAM ET"):
        qa.tap("DEVAM ET", tree=tree)
    leave()


if __name__ == "__main__":
    import argparse
    from pathlib import Path
    parser = argparse.ArgumentParser()
    parser.add_argument("--part", choices=["all", "lifecycle", "responsive"], default="all")
    parser.add_argument("--output-dir", type=Path)
    args = parser.parse_args()
    if args.output_dir:
        OUT = args.output_dir.resolve()
        OUT.mkdir(parents=True, exist_ok=True)
        qa.OUT = OUT
    assert qa.DEVICE == "emulator-5554"
    qa.adb("shell", "am", "force-stop", qa.PACKAGE)
    qa.launch()
    if args.part == "all":
        levels()
    if args.part in ["all", "lifecycle"]:
        lifecycle_and_effect()
    if args.part in ["all", "responsive"]:
        responsive_effect()
    bolum_kontrolleri = {
        "all": ["eşik kareleri", "mesaj sırasında kart dokunması", "tek %100 sonuç başlığı",
                "efekt kareleri", "duraklatma", "yeniden başlatma", "çıkış temizliği",
                "320x640 ve 600x960 Zor seviye"],
        "lifecycle": ["efekt kareleri", "üçüncü dokunma", "duraklatma", "yeniden başlatma", "çıkış temizliği"],
        "responsive": ["320x640 ve 600x960 Zor seviye", "efekt kareleri", "ekran ölçüsünü geri yükleme"]
    }
    (OUT / f"sonuc-{args.part}.json").write_text(json.dumps({
        "cihaz": qa.DEVICE, "durum": "başarılı", "calistirilan_bolum": args.part,
        "seviyeler": [12, 16, 20] if args.part == "all" else [],
        "kontroller": bolum_kontrolleri[args.part],
        "not": "Mesaj içeriği ve efekt görünürlüğü kaydedilen karelerde görsel incelenir. Tek gösterim ve zamanlama mantık testlerinde sınanır."
    }, ensure_ascii=False, indent=2), encoding="utf-8")
