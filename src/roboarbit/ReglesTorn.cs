namespace roboarbit;

internal readonly record struct Posicio(int X, int Y);
internal sealed record TornObert(int Id, DateTime Inici, DateTime Limit);
internal sealed record PeticioMoviment(long Id, string Jugador, Posicio Destinacio, DateTime Data);
internal sealed record Pintura(string? Propietari, byte Nivell, int? TornActualitzacio)
{
    public static Pintura Lliure { get; } = new(null, 0, null);
}
internal sealed record ResultatTorn(Dictionary<string, Posicio> Posicions, Dictionary<Posicio, Pintura> Caselles);

internal static class ReglesTorn
{
    public static Dictionary<string, Posicio> SeleccionarMoviments(ConfiguracioJoc configuracio, TornObert torn,
        DateTime tall, IReadOnlyDictionary<string, Posicio?> jugadors, IEnumerable<PeticioMoviment> peticions)
    {
        // Amb el control manual, data_limit és informativa; només s'aplica el tall.
        var seleccionats = new Dictionary<string, Posicio>();
        var recomptes = new Dictionary<string, int>();
        foreach (PeticioMoviment peticio in peticions.OrderBy(p => p.Id))
        {
            if (!jugadors.TryGetValue(peticio.Jugador, out Posicio? origen)
                || peticio.Data < torn.Inici || peticio.Data >= tall)
                continue;

            Posicio desti = peticio.Destinacio;
            if (!DinsTauler(configuracio, desti))
                continue;

            // El primer INSERT vàlid del torn 1 fixa l'entrada; la resta s'ignora.
            if (torn.Id == 1)
            {
                seleccionats.TryAdd(peticio.Jugador, desti);
                continue;
            }
            if (origen is not Posicio posicio
                || Math.Abs((long)desti.X - posicio.X) + Math.Abs((long)desti.Y - posicio.Y) != 1)
                continue;

            int recompte = recomptes.GetValueOrDefault(peticio.Jugador);
            if (recompte >= configuracio.MaxMovimentsTorn)
                continue;
            recomptes[peticio.Jugador] = recompte + 1;
            seleccionats[peticio.Jugador] = desti;
        }
        return seleccionats;
    }

    public static bool DinsTauler(ConfiguracioJoc configuracio, Posicio posicio) =>
        posicio.X >= 0 && posicio.X < configuracio.Amplada && posicio.Y >= 0 && posicio.Y < configuracio.Alcada;

    public static ResultatTorn Resoldre(ConfiguracioJoc configuracio, int idTorn,
        IReadOnlyDictionary<string, Posicio> participants, IReadOnlyDictionary<string, Posicio> moviments,
        IReadOnlyDictionary<Posicio, Pintura> caselles)
    {
        var resultat = new Dictionary<Posicio, Pintura>(caselles);
        foreach ((Posicio posicio, Pintura pintura) in caselles)
        {
            long edat = (long)idTorn - (pintura.TornActualitzacio ?? idTorn);
            if (pintura.Nivell == 2 && edat >= configuracio.CaducitatReforcada)
                resultat[posicio] = pintura with { Nivell = 1, TornActualitzacio = idTorn };
            else if (pintura.Nivell == 1 && edat >= configuracio.CaducitatNormal)
                resultat[posicio] = Pintura.Lliure;
        }

        var destinacions = idTorn == 1
            ? new Dictionary<string, Posicio>(moviments)
            : participants.ToDictionary(p => p.Key, p => moviments.GetValueOrDefault(p.Key, p.Value));
        foreach (var grup in destinacions.GroupBy(p => p.Value))
        {
            Posicio desti = grup.Key;
            if (grup.Count() > 1)
            {
                resultat[desti] = Pintura.Lliure;
                continue;
            }

            string jugador = grup.Single().Key;
            if (idTorn == 1 || desti == participants[jugador])
                continue;

            Pintura pintura = resultat[desti];
            if (pintura.Propietari == jugador)
                resultat[desti] = new(jugador, 2, idTorn);
            else if (pintura.Nivell != 2)
                resultat[desti] = new(jugador, 1, idTorn);
        }
        return new(destinacions, resultat);
    }
}
