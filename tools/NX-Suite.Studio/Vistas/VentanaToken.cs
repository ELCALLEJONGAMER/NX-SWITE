using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Controls;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>Diálogo para guardar y comprobar el token personal de GitHub.</summary>
    internal sealed class VentanaToken : Window
    {
        private readonly PasswordBox _token = new() { Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(6) };
        private readonly TextBlock _resultado = new() { Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap };

        public VentanaToken()
        {
            Title = "Token de GitHub";
            Width = 560; SizeToContent = SizeToContent.Height;
            Background = Formularios.Pincel("#121217");
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = Application.Current.MainWindow;

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(Formularios.Titulo("Token de GitHub"));
            panel.Children.Add(Formularios.Ayuda(
                "Sin token, GitHub solo permite 60 consultas por hora. Con un token personal sube a 5000.\n\n" +
                "Cómo crearlo: GitHub → Settings → Developer settings → Personal access tokens → " +
                "Fine-grained tokens → Generate new token. Acceso «Public repositories (read-only)» y sin permisos extra.\n\n" +
                "Se guarda solo en este PC (%AppData%\\NX-Studio), nunca en el repositorio."));

            var crear = new Button { Content = "🌐 Abrir página de tokens", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0) };
            crear.Click += (_, _) => Process.Start(new ProcessStartInfo(
                "https://github.com/settings/personal-access-tokens/new") { UseShellExecute = true });
            panel.Children.Add(crear);

            panel.Children.Add(new TextBlock { Text = "Token:", Margin = new Thickness(0, 14, 0, 0), Foreground = Formularios.Pincel("#9A9AA8") });
            _token.Password = ConfiguracionStudio.TokenGitHub ?? "";
            panel.Children.Add(_token);
            panel.Children.Add(_resultado);

            var botones = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var probar = new Button { Content = "Comprobar" };
            probar.Click += async (_, _) => await ComprobarAsync();
            var borrar = new Button { Content = "Quitar token", Margin = new Thickness(6, 0, 0, 0) };
            borrar.Click += (_, _) => { ConfiguracionStudio.TokenGitHub = null; DialogResult = true; };
            var guardar = new Button { Content = "Guardar", Margin = new Thickness(6, 0, 0, 0), IsDefault = true };
            guardar.Click += (_, _) => { ConfiguracionStudio.TokenGitHub = _token.Password; DialogResult = true; };
            botones.Children.Add(probar);
            botones.Children.Add(borrar);
            botones.Children.Add(guardar);
            panel.Children.Add(botones);
            Content = panel;

            Loaded += async (_, _) => await ComprobarAsync();
        }

        private async Task ComprobarAsync()
        {
            _resultado.Text = "Comprobando…";
            _resultado.Foreground = Formularios.Pincel("#9A9AA8");
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("NX-Suite.Studio");
                string t = _token.Password.Trim();
                if (t.Length > 0) http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t);
                using var r = await http.GetAsync("https://api.github.com/rate_limit");
                if (r.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _resultado.Text = "✗ Token no válido o caducado.";
                    _resultado.Foreground = Formularios.Pincel("#FF6B6B");
                    return;
                }
                var core = System.Text.Json.Nodes.JsonNode.Parse(await r.Content.ReadAsStringAsync())?["resources"]?["core"];
                int limite = core?["limit"]?.GetValue<int>() ?? 0, quedan = core?["remaining"]?.GetValue<int>() ?? 0;
                var reinicio = DateTimeOffset.FromUnixTimeSeconds(core?["reset"]?.GetValue<long>() ?? 0).LocalDateTime;
                _resultado.Text = $"{(t.Length > 0 ? "✓ Token válido" : "Sin token")} · quedan {quedan} de {limite} consultas (se reinicia a las {reinicio:HH:mm}).";
                _resultado.Foreground = Formularios.Pincel(quedan > 0 ? "#3DDC84" : "#FFA640");
            }
            catch (Exception ex)
            {
                _resultado.Text = "No se pudo comprobar: " + ex.Message;
                _resultado.Foreground = Formularios.Pincel("#FF6B6B");
            }
        }
    }
}
