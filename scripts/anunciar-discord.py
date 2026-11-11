"""Envía a Discord el resultado de un informe del catálogo.

Uso: python3 scripts/anunciar-discord.py <informe.md> <actualizaciones|estado>
  actualizaciones: publica los módulos de la sección «## Actualizado» (si hay).
  estado:          publica un resumen (todo al día / módulos que requieren atención).
Webhook: variable de entorno WEBHOOK. Si no está definida no hace nada.
"""
import json, os, re, sys, urllib.request

ruta, modo = sys.argv[1], sys.argv[2]
webhook = os.environ.get("WEBHOOK", "")
if not webhook:
    print("Webhook no definido"); sys.exit(0)

lineas, manual, seccion, resumen = [], [], "", ""
for l in open(ruta, encoding="utf-8"):
    if l.startswith("## "):
        seccion = l[3:].split(" ")[0]
    elif l.startswith(("✅", "⚠️")) and not resumen:
        resumen = l.strip()
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
    texto = "🩺 **Estado del catálogo NX-SWITE**\n" + (resumen or "Sin informe")
    if manual:
        texto += "\nRequieren revisión:\n" + "\n".join(manual)

req = urllib.request.Request(webhook, data=json.dumps({"content": texto[:2000]}).encode(),
                             headers={"Content-Type": "application/json", "User-Agent": "NX-SWITE"})
urllib.request.urlopen(req)
print(texto)
