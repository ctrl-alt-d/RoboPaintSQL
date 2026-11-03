using System.Text;
using Spectre.Console;

namespace roboarbit;

internal static class RepresentacioTauler
{
    // Un color per jugador, en l'ordre A–T; la lletra també identifica el propietari.
    private static readonly string[] Colors =
    [
        "#ff5555", "#55cc55", "#ffff55", "#5599ff", "#ff55ff",
        "#55ffff", "#ff9933", "#aa88ff", "#ff88aa", "#aadd55",
        "#55aaff", "#ddaa77", "#cc77cc", "#77ddbb", "#dddddd",
        "#ffbb55", "#99aaff", "#ee7799", "#88ccaa", "#bbbb66"
    ];

    public static void Mostrar(TextWriter sortida, ConfiguracioJoc configuracio,
        IReadOnlyDictionary<Posicio, Pintura> caselles, IReadOnlyDictionary<string, Posicio> robots)
    {
        IAnsiConsole terminal = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(sortida),
            Ansi = ReferenceEquals(sortida, Console.Out) && !Console.IsOutputRedirected
                ? AnsiSupport.Detect : AnsiSupport.No
        });
        terminal.WriteLine("Pintura: . lliure, minúscula normal, majúscula reforçada.");
        var tauler = new Table().Border(TableBorder.Rounded).HideHeaders();
        tauler.AddColumn(new TableColumn("").Padding(0, 0));
        int ampleCasella = (configuracio.Amplada - 1).ToString().Length;
        int ampleFila = (configuracio.Alcada - 1).ToString().Length;
        var coordenades = new StringBuilder(new string(' ', ampleFila + 1));
        for (int x = 0; x < configuracio.Amplada; x++)
            coordenades.Append(x.ToString().PadLeft(ampleCasella)).Append(' ');
        tauler.AddRow(new Text(coordenades.ToString()));

        // (0, 0) és a dalt a l'esquerra. Reservem el mateix espai per número i casella.
        for (int y = 0; y < configuracio.Alcada; y++)
        {
            var fila = new StringBuilder(y.ToString().PadLeft(ampleFila) + " ");
            for (int x = 0; x < configuracio.Amplada; x++)
            {
                fila.Append(' ', ampleCasella - 1);
                Pintura pintura = caselles[new(x, y)];
                if (pintura.Nivell == 0)
                {
                    fila.Append(". ");
                    continue;
                }
                char propietari = pintura.Propietari![0];
                char lletra = pintura.Nivell == 1 ? char.ToLowerInvariant(propietari) : propietari;
                string estil = pintura.Nivell == 2 ? "bold " : "";
                fila.Append($"[{estil}{Colors[propietari - 'A']}]{lletra}[/] ");
            }
            fila.Append(y.ToString().PadLeft(ampleFila));
            tauler.AddRow(new Markup(fila.ToString()));
        }
        tauler.AddRow(new Text(coordenades.ToString()));
        terminal.Write(tauler);

        // La llista conserva visibles els robots que comparteixen casella i la pintura de sota.
        terminal.WriteLine("Robots (x, y): " + string.Join("; ", robots.OrderBy(r => r.Key, StringComparer.Ordinal)
            .Select(r => $"{r.Key}=({r.Value.X}, {r.Value.Y})")));
        terminal.WriteLine();
    }
}
