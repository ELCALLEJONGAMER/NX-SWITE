"""Envía a Discord el resultado de un informe del catálogo.

Uso: python3 scripts/anunciar-discord.py <informe.md> <actualizaciones|estado>
  actualizaciones: publica los módulos de la sección «## Actualizado» (si hay).
  estado:          publica un resumen (todo al día / módulos que requieren atención).
Webhook: variable de entorno WEBHOOK. Si no está definida no hace nada.
"""
import json, os, re, sys, time, urllib.request

ruta, modo = sys.argv[1], sys.argv[2]
webhook = os.environ.get("WEBHOOK", "")
if not webhook:
    print("Webhook no definido"); sys.exit(0)

lineas, manual, seccion, sin_cambios = [], [], "", 0
for l in open(ruta, encoding="utf-8"):
    if l.startswith("## "):
        seccion = l[3:].split(" ")[0]
    elif l.startswith(("✅", "⚠️")):
        m = re.search(r"(\d+) (?:sin cambios|módulos revisados)", l)
        if m: sin_cambios = int(m.group(1))
    elif l.startswith("- **"):
        m = re.match(r"- \*\*(.+?)\*\*: (.+?)(?: \(.*)?$", l.strip())
        if not m: continue
        if seccion == "Actualizado": lineas.append(f"• **{m.group(1)}**: {m.group(2)}")
        elif seccion == "Manual": manual.append(f"• **{m.group(1)}**: {m.group(2)}")

if modo == "actualizaciones":
    if not lineas:
        print("Sin módulos actualizados"); sys.exit(0)
    texto = "📦 **Nuevas actualizaciones disponibles en NX-SWITE**\n" + "\n".join(lineas)
else:
    texto = "🩺 **Estado del catálogo NX-SWITE**\n"
    if lineas:
        texto += "📦 Publicados automáticamente:\n" + "\n".join(lineas) + "\n"
    if manual:
        texto += "⚠️ Requieren revisión:\n" + "\n".join(manual) + "\n"
    if not lineas and not manual:
        texto += f"✅ Todo al día ({sin_cambios} módulos revisados)."

req = urllib.request.Request(webhook, data=json.dumps({"content": texto[:2000]}).encode(),
                             headers={"Content-Type": "application/json", "User-Agent": "NX-SWITE"})
for intento in range(4):
    try:
        urllib.request.urlopen(req, timeout=30); break
    except Exception as e:
        print(f"Intento {intento + 1} fallido: {e}")
        if intento == 3: raise
        time.sleep(10 * (intento + 1))
print(texto)
