"""Kurulu kart setini altı ekran/seviye birleşiminde gerçek dokunmalarla doğrular.

Yalnız emulator-5554 hedeflenir. Uygulama verileri silinmez; ekran ayarları geri yüklenir.
"""
import concurrent.futures
import json
import re
import subprocess
import time

import android_smoke as qa

OUT = qa.ROOT / "output" / "card-redesign-2026-10-26" / "android"
OUT.mkdir(parents=True, exist_ok=True)
qa.OUT = OUT


def frame(name):
    run = subprocess.run([qa.ADB, "-s", qa.DEVICE, "exec-out", "screencap", "-p"],
                         capture_output=True, check=True, timeout=20)
    (OUT / name).write_bytes(run.stdout)


def capture_sequence(prefix, action, delays):
    action()
    origin = time.monotonic()

    def capture(delay):
        time.sleep(max(0, origin + delay - time.monotonic()))
        frame(f"{prefix}-{int(delay * 1000):04d}ms.png")

    with concurrent.futures.ThreadPoolExecutor(max_workers=len(delays)) as pool:
        jobs = [pool.submit(capture, delay) for delay in delays]
        for job in jobs:
            job.result()


def menu():
    return qa.wait(lambda t: bool(qa.label_nodes(t, "Sistem onarımını başlat", True)), "Ana menü yok.")


def start(count):
    tree = menu()
    prefix = {12: "Kolay,", 16: "Orta,", 20: "Zor,"}[count]
    found = [n for n in qa.nodes(tree) if n.get("content-desc", "").startswith(prefix)]
    assert len(found) == 1
    qa.tap_node(found[0])
    qa.tap("Sistem onarımını başlat", True)
    return qa.wait_cards(count)


def leave():
    qa.adb("shell", "input", "keyevent", "4")
    tree = qa.wait(lambda t: "SİSTEMDEN AYRIL?" in qa.texts(t), "Neon çıkış paneli yok.")
    assert not any(n.get("resource-id") == "android:id/alertTitle" for n in qa.nodes(tree))
    qa.tap("ANA MENÜ", tree=tree)
    menu()


def run():
    assert qa.DEVICE == "emulator-5554"
    old_size = re.search(r"Override size: (\d+x\d+)", qa.adb("shell", "wm", "size"))
    old_density = re.search(r"Override density: (\d+)", qa.adb("shell", "wm", "density"))
    results = []
    try:
        for size, density, name in [("960x1920", "480", "320x640"), ("1200x1920", "320", "600x960")]:
            qa.adb("shell", "wm", "size", size)
            qa.adb("shell", "wm", "density", density)
            # Yoğunluk değişikliğinin Android kaynaklarına uygulanması için uygulamayı yeniden aç.
            qa.adb("shell", "am", "force-stop", qa.PACKAGE)
            qa.launch()
            menu()
            time.sleep(0.8)
            width, height = map(int, size.split("x"))
            for count in [12, 16, 20]:
                tree = start(count)
                positions = {key: qa.center(n) for key, n in qa.cards(tree).items()}
                bounds = []
                for node in qa.cards(tree).values():
                    x1, y1, x2, y2 = map(int, re.findall(r"\d+", node.get("bounds")))
                    assert 0 <= x1 < x2 <= width and 0 <= y1 < y2 <= height
                    assert 1.20 < (y2 - y1) / (x2 - x1) < 1.30, "Kart hücresi kesildi veya oranı bozuldu."
                    bounds.append([x1, y1, x2, y2])
                prefix = f"{name}-{count}"
                frame(f"{prefix}-kapali.png")
                hint = qa.center(qa.label_nodes(tree, "SİSTEM TARAMASI")[-1])
                capture_sequence(f"{prefix}-acik", lambda: qa.adb("shell", "input", "tap", *hint), [0.1, 0.32, 0.55])
                tree = qa.wait_cards(count)
                assert qa.counter(tree, "HAMLE") == "0"
                hint_nodes = [n for n in qa.nodes(tree) if n.get("text") == "TARAMA KULLANILDI"]
                assert hint_nodes and hint_nodes[-1].get("enabled") == "false"
                qa.adb("shell", "input", "tap", *positions[1])
                capture_sequence(f"{prefix}-eslesme", lambda: qa.adb("shell", "input", "tap", *positions[2]),
                                 [0.08, 0.22, 0.4, 0.62, 0.9])
                tree = qa.wait_cards(count - 2)
                assert qa.counter(tree, "HAMLE") == "1"
                assert qa.counter(tree, "KURTARILAN MODÜL") == f"1 / {count // 2}"
                assert all(qa.center(n) == positions[key] for key, n in qa.cards(tree).items())
                frame(f"{prefix}-eslesme-sonrasi.png")
                leave()
                results.append({"ekran": name, "kart_sayisi": count, "hucre_sinirlari": bounds,
                                "kapali_acik_kareler": True, "eslesme_ve_hucre_konumu": "başarılı"})
                print(f"BAŞARILI: {name}, {count} kart; kapalı/açık görünüm, tarama, eşleşme ve sabit hücreler.", flush=True)
    finally:
        qa.adb("shell", "wm", "size", old_size.group(1) if old_size else "reset")
        qa.adb("shell", "wm", "density", old_density.group(1) if old_density else "reset")
    (OUT / "gorsel-test-sonuclari.json").write_text(json.dumps({"cihaz": qa.DEVICE, "durum": "başarılı", "senaryolar": results},
        ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    import argparse
    from pathlib import Path
    parser = argparse.ArgumentParser()
    parser.add_argument('--output-dir', type=Path)
    args = parser.parse_args()
    if args.output_dir:
        OUT = args.output_dir.resolve()
        OUT.mkdir(parents=True, exist_ok=True)
        qa.OUT = OUT
    run()
