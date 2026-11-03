using roboarbit;

internal static class ProvesTauler
{
    public static void Executar()
    {
        var configuracio = new ConfiguracioJoc(3, 2, 3000, 200, 20, 20, 2, 1, 2);
        var caselles = new Dictionary<Posicio, Pintura>
        {
            [new(0, 0)] = Pintura.Lliure, [new(1, 0)] = new("A", 1, 1), [new(2, 0)] = new("T", 2, 1),
            [new(0, 1)] = new("B", 2, 1), [new(1, 1)] = Pintura.Lliure, [new(2, 1)] = new("T", 1, 1)
        };
        var robots = new Dictionary<string, Posicio>
        {
            ["T"] = new(2, 0), ["B"] = new(1, 1), ["A"] = new(1, 1)
        };
        using var sortida = new StringWriter();
        RepresentacioTauler.Mostrar(sortida, configuracio, caselles, robots);
        string[] linies = sortida.ToString().Split(Environment.NewLine);
        if (!linies[3].Contains("0 . a T 0") || !linies[4].Contains("1 B . t 1") || sortida.ToString().Contains('\u001b'))
            throw new InvalidOperationException("Tauler: dimensions, orientació o nivells de pintura incorrectes.");
        if (!sortida.ToString().Contains("Robots (x, y): A=(1, 1); B=(1, 1); T=(2, 0)"))
            throw new InvalidOperationException("Tauler: cal mostrar tots els robots sense tapar la pintura.");
        if (!linies[2].Contains("0 1 2") || linies[2] != linies[5])
            throw new InvalidOperationException("Tauler: cal mostrar les coordenades x a dalt i a baix.");

        var gran = new ConfiguracioJoc(12, 11, 3000, 200, 20, 20, 2, 1, 2);
        var lliures = new Dictionary<Posicio, Pintura>();
        for (int y = 0; y < gran.Alcada; y++)
            for (int x = 0; x < gran.Amplada; x++)
                lliures.Add(new(x, y), Pintura.Lliure);
        using var sortidaGran = new StringWriter();
        RepresentacioTauler.Mostrar(sortidaGran, gran, lliures, new Dictionary<string, Posicio>());
        string[] liniesGrans = sortidaGran.ToString().Split(Environment.NewLine);
        string eix = liniesGrans[2];
        string filaDeu = liniesGrans[13];
        if (!filaDeu.Contains("10 ") || !filaDeu.Contains(" 10")
            || eix != liniesGrans[14]
            || filaDeu[eix.IndexOf("10", StringComparison.Ordinal) + 1] != '.'
            || filaDeu[eix.IndexOf("11", StringComparison.Ordinal) + 1] != '.')
            throw new InvalidOperationException("Tauler: coordenades de dues xifres desalineades.");
        Console.WriteLine("Superades 4 comprovacions de representació del tauler sense SQL Server.");
    }
}
