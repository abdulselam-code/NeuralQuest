"""Gerçek dokunmalarla parçacık karelerini ve animasyon sırasında güvenli iptali doğrular."""
import concurrent.futures
import json
import re
import subprocess
import time

import android_smoke as qa

OUT = qa.OUT / "eslesme-efekti"
OUT.mkdir(exist_ok=True)


def positions(tree):
    return {key: qa.center(node) for key, node in qa.cards(tree).items()}


def touch(point):
    qa.adb("shell", "input", "tap", *point)


def pair(points, first):
    touch(points[first])
    touch(points[first + 1])


def frame(name):
    run = subprocess.run([qa.ADB, "-s", qa.DEVICE, "exec-out", "screencap", "-p"],
                         capture_output=True, check=True, timeout=20)
    (OUT / name).write_bytes(run.stdout)


def start():
    qa.tap("Sistem onarımını başlat", True)
    return qa.wait_cards(12)


def run():
    assert qa.DEVICE.startswith("emulator-")
    tree = qa.wait_cards(12)
    points = positions(tree)
    pause = qa.center(qa.label_nodes(tree, "DURAKLAT")[-1])
    restart = qa.center(qa.label_nodes(tree, "YENİDEN BAŞLAT")[-1])

    touch(points[1])
    frame("00-on-yuz.png")
    touch(points[2])
    origin = time.monotonic()

    def capture(delay):
        time.sleep(max(0, origin + delay - time.monotonic()))
        frame(f"01-eslesme-{int(delay * 1000):04d}ms.png")

    with concurrent.futures.ThreadPoolExecutor(max_workers=7) as pool:
        pending = [pool.submit(capture, delay) for delay in [0.06, 0.17, 0.30, 0.46, 0.63, 0.85, 1.12]]
        touch(points[3])  # Eşleşme sürerken üçüncü kart açılamamalıdır.
        for task in pending:
            task.result()

    tree = qa.wait_cards(10)
    assert qa.counter(tree, "HAMLE") == "1"
    assert qa.counter(tree, "KURTARILAN MODÜL") == "1 / 6"
    assert all(qa.center(node) == points[key] for key, node in qa.cards(tree).items())
    recovered = [n for n in qa.nodes(tree) if n.get("content-desc", "").endswith(", kurtarıldı")]
    assert len(recovered) == 2 and all(n.get("enabled") == "false" for n in recovered)
    touch(points[1])
    assert qa.counter(qa.dump(), "HAMLE") == "1"
    print("BAŞARILI: İki kartın efekt kareleri kaydedildi; hücreler sabit, üçüncü dokunma ve boş hücreler kilitli.", flush=True)

    pair(points, 3)
    time.sleep(0.25)
    touch(pause)
    tree = qa.wait(lambda t: "SİSTEM DURAKLATILDI" in qa.texts(t), "Efekt sırasında duraklatılamadı.")
    qa.tap("DEVAM ET", tree=tree)
    tree = qa.wait_cards(8)
    time.sleep(1.1)
    assert qa.counter(qa.dump(), "KURTARILAN MODÜL") == "2 / 6"
    frame("02-duraklatma-sonrasi.png")

    pair(points, 5)
    time.sleep(0.25)
    touch(restart)
    tree = qa.wait(lambda t: "PROTOKOL YENİDEN BAŞLATILSIN MI?" in qa.texts(t), "Efekt sırasında yeniden başlatılamadı.")
    qa.tap("YENİDEN BAŞLAT", tree=tree)
    tree = qa.wait_cards(12)
    assert qa.counter(tree, "HAMLE") == "0"
    points = positions(tree)
    pair(points, 1)
    time.sleep(0.25)
    qa.adb("shell", "input", "keyevent", "3")
    qa.launch()
    tree = qa.wait(lambda t: "SİSTEM DURAKLATILDI" in qa.texts(t), "Arka plana geçiş efekti durdurmadı.")
    qa.tap("DEVAM ET", tree=tree)
    qa.wait_cards(10)
    pair(points, 3)
    time.sleep(0.25)
    qa.adb("shell", "input", "keyevent", "4")
    tree = qa.wait(lambda t: bool(qa.label_nodes(t, "ANA MENÜ")), "Efekt sırasında çıkış onayı yok.")
    qa.tap("ANA MENÜ", tree=tree)
    tree = start()
    time.sleep(1.1)
    assert len(qa.cards(qa.dump())) == 12
    print("BAŞARILI: Efekt sırasında duraklatma, yeniden başlatma, arka plan ve sayfadan çıkış temizlendi.", flush=True)

    # Aynı efekt küçük ve büyük ekranlarda da ölçülen kart merkezlerini izlemelidir.
    old_size = re.search(r"Override size: (\d+x\d+)", qa.adb("shell", "wm", "size"))
    old_density = re.search(r"Override density: (\d+)", qa.adb("shell", "wm", "density"))
    try:
        for size, density, name, first, left in [
                ("960x1920", "480", "320x640", 1, 10),
                ("1200x1920", "320", "600x960", 3, 8)]:
            qa.adb("shell", "wm", "size", size)
            qa.adb("shell", "wm", "density", density)
            time.sleep(1)
            tree = qa.dump()
            if qa.label_nodes(tree, "DEVAM ET"):
                qa.tap("DEVAM ET", tree=tree)
            tree = qa.wait_cards(left + 2)
            pair(positions(tree), first)
            time.sleep(0.3)
            frame(f"03-efekt-{name}.png")
            qa.wait_cards(left)
            print(f"BAŞARILI: {name} boyutunda efekt ve sabit hücreler.", flush=True)
    finally:
        qa.adb("shell", "wm", "size", old_size.group(1) if old_size else "reset")
        qa.adb("shell", "wm", "density", old_density.group(1) if old_density else "reset")
    time.sleep(1)
    tree = qa.dump()
    if qa.label_nodes(tree, "DEVAM ET"):
        qa.tap("DEVAM ET", tree=tree)
    qa.adb("shell", "input", "keyevent", "4")
    tree = qa.wait(lambda t: bool(qa.label_nodes(t, "ANA MENÜ")), "Ana menü onayı yok.")
    qa.tap("ANA MENÜ", tree=tree)
    result = {"durum": "başarılı", "cihaz": qa.DEVICE,
              "kontroller": ["iki kartın parçacık kareleri", "sabit ve dokunulamaz boş hücreler",
                             "hızlı üçüncü dokunma", "duraklatma", "yeniden başlatma",
                             "arka plan", "sayfadan çıkış", "320x640", "600x960"]}
    (OUT / "sonuc.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    run()
