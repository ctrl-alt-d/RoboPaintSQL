using System.Data;
using Microsoft.Data.SqlClient;

namespace roboarbit;

internal sealed class ArbitreJoc(SqlConnection connexio)
{
    public void Executar(CancellationToken cancelacio)
    {
        // Bloqueig de sessió dins d'aquesta base; també s'allibera si el procés mor.
        using var bloqueig = new SqlCommand("""
            DECLARE @resultat int;
            EXEC @resultat = sys.sp_getapplock @Resource = N'RoboPaintSQL.arbitre',
                @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0;
            SELECT @resultat;
            """, connexio);
        if (Convert.ToInt32(bloqueig.ExecuteScalar()) < 0)
            throw new InvalidOperationException("Ja hi ha un àrbitre executant-se en aquesta base.");

        try
        {
            ConfiguracioJoc configuracio = ConfiguracioJoc.Llegir(connexio);
            ComprovarPartida(configuracio);
            Console.WriteLine($"Partida manual: màxim {configuracio.MaxTorns} torns. Retorn per resoldre cada torn; Ctrl+C per aturar i poder reprendre.");
            while (true)
            {
                cancelacio.ThrowIfCancellationRequested();
                TornObert? torn = PrepararTorn(configuracio);
                if (torn is null)
                    break;
                Console.WriteLine($"Torn {torn.Id} obert. Prem Retorn per calcular-lo i passar al següent.");
                EsperarConfirmacio(cancelacio);
                ResoldreTorn(configuracio, torn, cancelacio);
                Console.WriteLine($"Torn {torn.Id} tancat.");
                MostrarTauler(configuracio, torn.Id);
            }
            MostrarClassificacio(configuracio);
        }
        finally
        {
            using var alliberar = new SqlCommand("""
                EXEC sys.sp_releaseapplock @Resource = N'RoboPaintSQL.arbitre', @LockOwner = 'Session';
                """, connexio);
            alliberar.ExecuteNonQuery();
        }
    }

    private static void Esperar(int milisegons, CancellationToken cancelacio)
    {
        cancelacio.WaitHandle.WaitOne(milisegons);
        cancelacio.ThrowIfCancellationRequested();
    }

    private static void EsperarConfirmacio(CancellationToken cancelacio)
    {
        if (Console.IsInputRedirected)
        {
            // Una entrada acabada atura el joc; no ha de confirmar torns automàticament.
            string? linia = Task.Run(Console.ReadLine, cancelacio).WaitAsync(cancelacio).GetAwaiter().GetResult();
            if (linia is null)
                throw new OperationCanceledException(cancelacio);
        }
        else
        {
            // No bloquegem ReadLine: Ctrl+C ha de poder alliberar el bloqueig de sessió.
            while (true)
            {
                Esperar(50, cancelacio);
                if (Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Enter)
                    break;
            }
        }
        cancelacio.ThrowIfCancellationRequested();
    }

    private void ComprovarPartida(ConfiguracioJoc configuracio)
    {
        using SqlTransaction transaccio = connexio.BeginTransaction(IsolationLevel.Serializable);
        // Les partides antigues poden conservar robots aleatoris que mai no han jugat.
        using (var ordre = new SqlCommand("""
            UPDATE j SET posicio_x = NULL, posicio_y = NULL
            FROM dbo.jugadors AS j
            WHERE (j.posicio_x IS NOT NULL OR j.posicio_y IS NOT NULL)
                AND NOT EXISTS (SELECT 1 FROM dbo.moviments AS m WHERE m.id_jugador = j.id_jugador);
            """, connexio, transaccio))
            ordre.ExecuteNonQuery();

        var jugadors = new HashSet<string>();
        using (var ordre = new SqlCommand("SELECT id_jugador, posicio_x, posicio_y FROM dbo.jugadors;", connexio, transaccio))
        using (SqlDataReader lector = ordre.ExecuteReader())
        {
            while (lector.Read())
            {
                string jugador = lector.GetString(0);
                if (jugador.Length != 1 || jugador[0] < 'A' || jugador[0] > 'T'
                    || lector.IsDBNull(1) != lector.IsDBNull(2)
                    || (!lector.IsDBNull(1)
                        && !ReglesTorn.DinsTauler(configuracio, new(lector.GetInt32(1), lector.GetInt32(2)))))
                    throw new InvalidOperationException("Hi ha jugadors amb identitat o posició invàlida.");
                jugadors.Add(jugador);
            }
        }
        if (jugadors.Count == 0)
            throw new InvalidOperationException("Cal inicialitzar el joc abans de començar.");

        Dictionary<Posicio, Pintura> caselles = LlegirCaselles(transaccio);
        if (caselles.Count != (long)configuracio.Amplada * configuracio.Alcada
            || caselles.Any(c => !ReglesTorn.DinsTauler(configuracio, c.Key)))
            throw new InvalidOperationException("El tauler no correspon a les dimensions configurades.");
        if (caselles.Values.Any(p => p.Nivell > 2
            || (p.Nivell == 0 ? p.Propietari is not null || p.TornActualitzacio is not null
                : p.Propietari is null || !jugadors.Contains(p.Propietari) || p.TornActualitzacio is null)))
            throw new InvalidOperationException("Hi ha caselles amb un estat de pintura invàlid.");
        transaccio.Commit();
    }

    // Reprèn el torn obert o publica el següent, també si encara no ha entrat cap robot.
    internal TornObert? PrepararTorn(ConfiguracioJoc configuracio)
    {
        using SqlTransaction transaccio = connexio.BeginTransaction(IsolationLevel.Serializable);
        int ultimId = 0;
        using (var ordre = new SqlCommand("""
            SELECT TOP (1) id_torn, estat, data_inici, data_limit
            FROM dbo.torns ORDER BY id_torn DESC;
            """, connexio, transaccio))
        using (SqlDataReader lector = ordre.ExecuteReader())
        {
            if (lector.Read())
            {
                ultimId = lector.GetInt32(0);
                string estat = lector.GetString(1);
                if (estat == "OBERT")
                    return new(ultimId, lector.GetDateTime(2), lector.GetDateTime(3));
                if (estat != "TANCAT")
                    throw new InvalidOperationException("L'últim torn té un estat inesperat. Cal revisar la partida.");
            }
        }
        if (ultimId >= configuracio.MaxTorns)
            return null;

        DateTime inici = HoraServidor(transaccio);
        // Es conserva data_limit per compatibilitat; ara no tanca ni limita el torn.
        var torn = new TornObert(ultimId + 1, inici, inici.AddMilliseconds(configuracio.DuradaTornMs));
        using (var ordre = new SqlCommand("""
            INSERT dbo.torns (id_torn, estat, data_inici, data_limit)
            VALUES (@torn, 'OBERT', @inici, @limit);
            """, connexio, transaccio))
        {
            ordre.Parameters.Add("@torn", SqlDbType.Int).Value = torn.Id;
            AfegirData(ordre, "@inici", torn.Inici);
            AfegirData(ordre, "@limit", torn.Limit);
            ordre.ExecuteNonQuery();
        }
        transaccio.Commit();
        return torn;
    }

    internal void ResoldreTorn(ConfiguracioJoc configuracio, TornObert torn, CancellationToken cancelacio = default)
    {
        using SqlTransaction transaccio = connexio.BeginTransaction(IsolationLevel.Serializable);
        using (var ordre = new SqlCommand("SELECT estat FROM dbo.torns WHERE id_torn = @torn;", connexio, transaccio))
        {
            ordre.Parameters.Add("@torn", SqlDbType.Int).Value = torn.Id;
            string? estat = ordre.ExecuteScalar() as string;
            if (estat == "TANCAT")
                return;
            if (estat != "OBERT")
                throw new InvalidOperationException("El torn que s'intenta resoldre no és obert.");
        }
        DateTime tall = HoraServidor(transaccio);
        if (torn.Id == 1)
        {
            // El primer torn només incorpora les entrades vàlides, encara que hi hagués posicions antigues.
            using var ordre = new SqlCommand("UPDATE dbo.jugadors SET posicio_x = NULL, posicio_y = NULL;", connexio, transaccio);
            ordre.ExecuteNonQuery();
        }
        Dictionary<string, Posicio?> jugadors = LlegirJugadorsActius(transaccio);
        var participants = jugadors.Where(j => j.Value.HasValue)
            .ToDictionary(j => j.Key, j => j.Value!.Value);
        List<PeticioMoviment> peticions = LlegirPeticions(torn.Id, transaccio);
        Dictionary<string, Posicio> moviments = ReglesTorn.SeleccionarMoviments(configuracio, torn, tall, jugadors, peticions);
        CanviarEstat(torn.Id, "EN_ARBITRATGE", transaccio);
        Dictionary<Posicio, Pintura> caselles = LlegirCaselles(transaccio);
        ResultatTorn resultat = ReglesTorn.Resoldre(configuracio, torn.Id, participants, moviments, caselles);
        DesarResultat(resultat, caselles, transaccio);
        CanviarEstat(torn.Id, "TANCAT", transaccio);
        cancelacio.ThrowIfCancellationRequested();
        transaccio.Commit();
    }

    private DateTime HoraServidor(SqlTransaction transaccio)
    {
        // Mateixa precisió que les dates del disparador; no s'utilitza el rellotge del client.
        using var ordre = new SqlCommand("SELECT CAST(SYSUTCDATETIME() AS datetime2(3));", connexio, transaccio);
        return (DateTime)ordre.ExecuteScalar()!;
    }

    private static void AfegirData(SqlCommand ordre, string nom, DateTime valor)
    {
        SqlParameter parametre = ordre.Parameters.Add(nom, SqlDbType.DateTime2);
        parametre.Scale = 3;
        parametre.Value = valor;
    }

    private Dictionary<string, Posicio?> LlegirJugadorsActius(SqlTransaction transaccio)
    {
        using var ordre = new SqlCommand("""
            SELECT id_jugador, posicio_x, posicio_y FROM dbo.jugadors WHERE actiu = 1;
            """, connexio, transaccio);
        using SqlDataReader lector = ordre.ExecuteReader();
        var jugadors = new Dictionary<string, Posicio?>();
        while (lector.Read())
            jugadors.Add(lector.GetString(0), lector.IsDBNull(1) ? null : new(lector.GetInt32(1), lector.GetInt32(2)));
        return jugadors;
    }

    private List<PeticioMoviment> LlegirPeticions(int idTorn, SqlTransaction transaccio)
    {
        using var ordre = new SqlCommand("""
            SELECT id_moviment, id_jugador, nova_x, nova_y, data_moviment
            FROM dbo.moviments WHERE id_torn = @torn ORDER BY id_moviment;
            """, connexio, transaccio);
        ordre.Parameters.Add("@torn", SqlDbType.Int).Value = idTorn;
        using SqlDataReader lector = ordre.ExecuteReader();
        var peticions = new List<PeticioMoviment>();
        while (lector.Read())
            peticions.Add(new(lector.GetInt64(0), lector.GetString(1),
                new(lector.GetInt32(2), lector.GetInt32(3)), lector.GetDateTime(4)));
        return peticions;
    }

    private Dictionary<Posicio, Pintura> LlegirCaselles(SqlTransaction? transaccio = null)
    {
        using var ordre = new SqlCommand("SELECT posicio_x, posicio_y, id_propietari, nivell, torn_actualitzacio FROM dbo.caselles;", connexio, transaccio);
        using SqlDataReader lector = ordre.ExecuteReader();
        var caselles = new Dictionary<Posicio, Pintura>();
        while (lector.Read())
            caselles.Add(new(lector.GetInt32(0), lector.GetInt32(1)), new(
                lector.IsDBNull(2) ? null : lector.GetString(2), lector.GetByte(3), lector.IsDBNull(4) ? null : lector.GetInt32(4)));
        return caselles;
    }

    private void CanviarEstat(int idTorn, string estat, SqlTransaction transaccio)
    {
        using var ordre = new SqlCommand("""
            UPDATE dbo.torns SET estat = @estat,
                data_fi = CASE WHEN @estat = 'TANCAT' THEN SYSUTCDATETIME() ELSE NULL END
            WHERE id_torn = @torn;
            """, connexio, transaccio);
        ordre.Parameters.Add("@torn", SqlDbType.Int).Value = idTorn;
        ordre.Parameters.Add("@estat", SqlDbType.VarChar, 20).Value = estat;
        ordre.ExecuteNonQuery();
    }

    private void DesarResultat(ResultatTorn resultat, Dictionary<Posicio, Pintura> anteriors, SqlTransaction transaccio)
    {
        using (var ordre = new SqlCommand("UPDATE dbo.jugadors SET posicio_x = @x, posicio_y = @y WHERE id_jugador = @jugador;", connexio, transaccio))
        {
            ordre.Parameters.Add("@jugador", SqlDbType.Char, 1);
            ordre.Parameters.Add("@x", SqlDbType.Int);
            ordre.Parameters.Add("@y", SqlDbType.Int);
            foreach ((string jugador, Posicio posicio) in resultat.Posicions)
            {
                ordre.Parameters["@jugador"].Value = jugador;
                ordre.Parameters["@x"].Value = posicio.X;
                ordre.Parameters["@y"].Value = posicio.Y;
                ordre.ExecuteNonQuery();
            }
        }
        using (var ordre = new SqlCommand("""
            UPDATE dbo.caselles SET id_propietari = @propietari, nivell = @nivell, torn_actualitzacio = @actualitzacio
            WHERE posicio_x = @x AND posicio_y = @y;
            """, connexio, transaccio))
        {
            ordre.Parameters.Add("@x", SqlDbType.Int);
            ordre.Parameters.Add("@y", SqlDbType.Int);
            ordre.Parameters.Add("@propietari", SqlDbType.Char, 1);
            ordre.Parameters.Add("@nivell", SqlDbType.TinyInt);
            ordre.Parameters.Add("@actualitzacio", SqlDbType.Int);
            foreach ((Posicio posicio, Pintura pintura) in resultat.Caselles)
            {
                if (pintura == anteriors[posicio])
                    continue;
                ordre.Parameters["@x"].Value = posicio.X;
                ordre.Parameters["@y"].Value = posicio.Y;
                ordre.Parameters["@propietari"].Value = (object?)pintura.Propietari ?? DBNull.Value;
                ordre.Parameters["@nivell"].Value = pintura.Nivell;
                ordre.Parameters["@actualitzacio"].Value = (object?)pintura.TornActualitzacio ?? DBNull.Value;
                ordre.ExecuteNonQuery();
            }
        }
    }

    private void MostrarTauler(ConfiguracioJoc configuracio, int idTorn)
    {
        // El torn ja està confirmat: mostrem la pintura i totes les posicions desades.
        Dictionary<Posicio, Pintura> caselles = LlegirCaselles();
        var robots = new Dictionary<string, Posicio>();
        using (var ordre = new SqlCommand("SELECT id_jugador, posicio_x, posicio_y FROM dbo.jugadors WHERE posicio_x IS NOT NULL AND posicio_y IS NOT NULL;", connexio))
        using (SqlDataReader lector = ordre.ExecuteReader())
            while (lector.Read())
                robots.Add(lector.GetString(0), new(lector.GetInt32(1), lector.GetInt32(2)));

        Console.WriteLine($"Tauler després del torn {idTorn}:");
        RepresentacioTauler.Mostrar(Console.Out, configuracio, caselles, robots);
    }

    private void MostrarClassificacio(ConfiguracioJoc configuracio)
    {
        using var ordre = new SqlCommand("""
            SELECT j.id_jugador, COALESCE(SUM(CASE c.nivell
                WHEN 1 THEN CAST(@normal AS bigint) WHEN 2 THEN CAST(@reforcada AS bigint) ELSE 0 END), 0) AS punts
            FROM dbo.jugadors AS j LEFT JOIN dbo.caselles AS c ON c.id_propietari = j.id_jugador
            GROUP BY j.id_jugador ORDER BY punts DESC, j.id_jugador;
            """, connexio);
        ordre.Parameters.Add("@normal", SqlDbType.Int).Value = configuracio.PuntsNormal;
        ordre.Parameters.Add("@reforcada", SqlDbType.Int).Value = configuracio.PuntsReforcada;
        using SqlDataReader lector = ordre.ExecuteReader();
        Console.WriteLine("Partida acabada. Classificació final:");
        while (lector.Read())
            Console.WriteLine($"{lector.GetString(0)}: {lector.GetInt64(1)} punts");
    }
}
