using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using NX_Suite.Catalogo;

// Uso:
//   nx-catalogo [--entrada catalogo/catalogo.json] [--salida catalogo/catalogo.json]
//               [--informe catalogo/ultimo-informe.md]
//               [--solo id1,id2] [--simular] [--sin-validar] [--previsualizar]
//               [--aprobacion-manual id1,id2]
//
// --aprobacion-manual: módulos que nunca se actualizan solos (por defecto
// «firmware»); si hay versión nueva se informan como «Manual».
//
// --previsualizar: no actualiza nada; simula la versión publicada de cada
// módulo y escribe en el informe el árbol de archivos que quedaría en la SD.
//
// Por defecto actualiza catalogo/catalogo.json en sitio (es el catálogo que lee
// la app). Escribe además un informe Markdown con lo actualizado y lo que
// requiere revisión manual.

string entrada = Arg("--entrada") ?? Path.Combine("catalogo", "catalogo.json");
string salida  = Arg("--salida")  ?? entrada;
string rutaInforme = Arg("--informe") ?? Path.Combine("catalogo", "ultimo-informe.md");
bool simular   = args.Contains("--simular");
bool previsualizar = args.Contains("--previsualizar");
bool firmas    = !args.Contains("--sin-validar");
var solo = Arg("--solo")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                         .ToHashSet(StringComparer.OrdinalIgnoreCase);
var aprobacionManual = (Arg("--aprobacion-manual") ?? "firmware")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

if (!File.Exists(entrada))
{
    Console.Error.WriteLine($"No existe el catálogo de entrada: {entrada}");
    return 2;
}

var raiz = JsonNode.Parse(File.ReadAllText(entrada, Encoding.UTF8)) as JsonObject
           ?? throw new InvalidDataException("El catálogo no es un objeto JSON.");
var modulos = raiz["Modulos"] as JsonArray ?? new JsonArray();

using var github = new GitHubReleases();
var actualizador = new ActualizadorCatalogo(github, firmas);
var informes = new List<InformeModulo>();

foreach (var modulo in modulos.OfType<JsonObject>())
{
    string id = modulo["Id"]?.GetValue<string>() ?? "?";
    if (solo != null && !solo.Contains(id)) continue;

    InformeModulo inf;
    try
    {
        bool manual = aprobacionManual.Contains(id);
        inf = previsualizar
            ? await actualizador.PrevisualizarAsync(modulo)
            : await actualizador.ProcesarAsync(modulo, aplicar: !simular && !manual);
        if (manual && inf.Resultado == ResultadoModulo.Actualizado)
            inf = new InformeModulo(id, ResultadoModulo.Manual,
                inf.Detalle.Replace(" (simulación)", "") + " (requiere aprobación manual)");
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("GITHUB_TOKEN"))
    {
        Console.Error.WriteLine(ex.Message);
        return 3;
    }
    catch (Exception ex)
    {
        inf = new InformeModulo(id, ResultadoModulo.Manual, $"Error: {ex.Message}");
    }

    informes.Add(inf);
    Console.WriteLine($"[{inf.Resultado,-11}] {inf.Id,-24} {inf.Detalle}");
}

var informe = new StringBuilder();
informe.AppendLine($"# Informe de actualización del catálogo ({DateTime.Now:yyyy-MM-dd HH:mm})");
informe.AppendLine();
// Solo se detallan los módulos que requieren atención; el resto se resume en un contador.
var atencion = informes.Where(i => i.Resultado is ResultadoModulo.Actualizado or ResultadoModulo.Manual).ToList();
int sinCambios = informes.Count - atencion.Count;
informe.AppendLine(atencion.Count == 0
    ? $"✅ Todo al día ({sinCambios} módulos revisados)."
    : $"⚠️ {atencion.Count} módulo(s) requieren atención · {sinCambios} sin cambios.");
informe.AppendLine();
foreach (var grupo in atencion.GroupBy(i => i.Resultado).OrderBy(g => g.Key))
{
    informe.AppendLine($"## {grupo.Key} ({grupo.Count()})");
    informe.AppendLine();
    foreach (var i in grupo)
    {
        informe.AppendLine($"- **{i.Id}**: {i.Detalle}");
        if (i.Vista != null)
        {
            informe.AppendLine();
            informe.AppendLine("<details><summary>Estructura en la microSD</summary>");
            informe.AppendLine();
            informe.AppendLine(i.Vista);
            informe.AppendLine("</details>");
            informe.AppendLine();
        }
    }
    informe.AppendLine();
}

int actualizados = informes.Count(i => i.Resultado == ResultadoModulo.Actualizado);
if (previsualizar)
{
    string rutaVista = Arg("--informe") ?? Path.Combine("catalogo", "previsualizacion.md");
    File.WriteAllText(rutaVista, informe.ToString(), new UTF8Encoding(false));
    Console.WriteLine($"\nPrevisualización -> {rutaVista}");
}
else if (!simular)
{
    var utf8 = new UTF8Encoding(false);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(rutaInforme))!);
    File.WriteAllText(rutaInforme, informe.ToString(), utf8);

    // Solo se reescribe el catálogo si hay cambios: evita PRs vacíos por formato.
    if (actualizados > 0 || salida != entrada)
    {
        var opciones = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(salida))!);
        File.WriteAllText(salida, raiz.ToJsonString(opciones), utf8);
    }
    Console.WriteLine($"\n{actualizados} módulo(s) actualizados -> {salida}");
}
else
{
    Console.WriteLine($"\nSimulación: {actualizados} módulo(s) tienen actualización disponible.");
}

return 0;

string? Arg(string nombre)
{
    int i = Array.IndexOf(args, nombre);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
