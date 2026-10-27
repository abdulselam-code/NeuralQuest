"""Bu çalışmanın başındaki dosya özetleriyle varlık ve Aşama 4 koruma kontrolü."""
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "output" / "neon-panel-validation"
baseline = json.loads((OUT / "baslangic-hash.json").read_text(encoding="utf-8-sig"))
count = 0
menu_preserved = False
for item in baseline:
    path = Path(item["Path"])
    data = path.read_bytes()
    if path.name == "MainPage.xaml":
        # Yalnızca eklenen üst katmanı ve onu bağlayan ad/namespace'i çıkar.
        source = data.decode("utf-8").replace("\r\n", "\n")
        source = source.replace('             xmlns:controls="clr-namespace:NeuralQuest.Controls"\n', "")
        source = source.replace('x:Name="MenuEtkilesimAlani" ', "")
        source = source.replace('        <controls:NeonBilgiPaneli x:Name="BilgiPaneli" IsVisible="False" ZIndex="20" Kapandi="BilgiPaneli_Kapandi" />\n', "")
        variants = [source.encode("utf-8"), source.replace("\n", "\r\n").encode("utf-8")]
        menu_preserved = any(hashlib.sha256(v).hexdigest().upper() == item["Hash"] for v in variants)
        assert menu_preserved, "Ana menünün özgün yerleşiminde beklenmeyen değişiklik."
    else:
        assert hashlib.sha256(data).hexdigest().upper() == item["Hash"], f"Korunacak dosya değişti: {path}"
        count += 1

alerts = []
for path in (ROOT / "NeuralQuest").rglob("*.cs"):
    if "obj" in path.parts or "bin" in path.parts:
        continue
    if re.search(r"\b(DisplayAlert(?:Async)?|DisplayActionSheet(?:Async)?|AlertDialog)\b", path.read_text(encoding="utf-8-sig")):
        alerts.append(str(path.relative_to(ROOT)))
assert not alerts, alerts
result = {"durum": "başarılı", "ozeti_degismeyen_png_ve_asama4_dosyasi": count,
          "ana_menu_ozgun_yerlesimi_korundu": menu_preserved, "standart_uyari_cagrisi": alerts}
(OUT / "koruma.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(result, ensure_ascii=False))
