using System.Data;
using Microsoft.Data.SqlClient;

namespace robot;

internal static class Program
{
    private static async Task<int> Main()
    {
        string? cadenaConnexio = Environment.GetEnvironmentVariable("ROBOPAINTSQL_CONNEXIO");
        if (string.IsNullOrWhiteSpace(cadenaConnexio))
        {
            Console.Error.WriteLine("Defineix ROBOPAINTSQL_CONNEXIO amb la connexió del teu compte d'alumne (A–T).");
            return 1;
        }

        using var cancelacio = new CancellationTokenSource();
        Console.CancelKeyPress += (_, esdeveniment) =>
        {
            esdeveniment.Cancel = true;
            cancelacio.Cancel();
        };

        try
        {
            using var connexio = new SqlConnection(cadenaConnexio);
            await connexio.OpenAsync(cancelacio.Token);
            int jugador = IdentificarJugador(connexio);
            ConfiguracioRobot configuracio = LlegirConfiguracio(connexio);
            var aleatori = new Random();
            int ultimTornTractat = 0;
            Console.WriteLine($"Robot {(char)('A' + jugador)}. Ctrl+C per sortir. Esperant torns de l'àrbitre...");

            while (true)
            {
                cancelacio.Token.ThrowIfCancellationRequested();
                var torn = LlegirTorn(connexio);
                bool partidaAcabada = torn.Estat == "TANCAT" && torn.Id >= configuracio.MaxTorns;
                if (partidaAcabada || (torn.Estat == "OBERT" && torn.Id != ultimTornTractat))
                {
                    EstatJoc estat = LlegirEstat(connexio, configuracio);
                    MostrarEstat(estat, jugador);
                    if (estat.EstatTorn == "TANCAT" && estat.IdTorn >= configuracio.MaxTorns)
                    {
                        Console.WriteLine("Partida acabada.");
                        return 0;
                    }

                    if (estat.EstatTorn == "OBERT" && estat.IdTorn != ultimTornTractat)
                    {
                        if (estat.TePeticioPropia)
                            Console.WriteLine("Ja hi ha una petició pròpia en aquest torn; esperem el següent.");
                        else
                        {
                            // EXERCICI: modifica TriarDestinacio per millorar l'estratègia.
                            var destinacio = Estrategia.TriarDestinacio(estat, jugador, aleatori);
                            if (destinacio is { } posicio)
                            {
                                cancelacio.Token.ThrowIfCancellationRequested();
                                EnviarMoviment(connexio, estat.IdTorn, posicio.X, posicio.Y);
                                Console.WriteLine($"Petició desada: ({posicio.X}, {posicio.Y}). Cal esperar la resolució de l'àrbitre.");
                            }
                            else
                                Console.WriteLine("Sense moviment: robot inactiu, fora de la partida o sense cap veí dins del tauler.");
                        }
                        ultimTornTractat = estat.IdTorn;
                    }
                }

                // L'interval de consulta no és la durada del torn: només Retorn a l'àrbitre el tanca.
                await Task.Delay(200, cancelacio.Token);
            }
        }
        catch (OperationCanceledException) when (cancelacio.IsCancellationRequested)
        {
            Console.WriteLine("Robot aturat. Les peticions desades es conserven.");
            return 0;
        }
        catch (SqlException error)
        {
            // No reintentem un INSERT automàticament: podria haver quedat desat.
            Console.Error.WriteLine($"Error SQL {error.Number}. Revisa la connexió i els permisos. Pots tornar a executar el robot; consultarà les peticions existents.");
            return 1;
        }
        catch (InvalidOperationException error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
        catch (ArgumentException)
        {
            Console.Error.WriteLine("Revisa el format de ROBOPAINTSQL_CONNEXIO.");
            return 1;
        }
    }

    private static int IdentificarJugador(SqlConnection connexio)
    {
        using var ordre = new SqlCommand("SELECT USER_NAME(), IS_ROLEMEMBER('rol_alumnes');", connexio);
        using SqlDataReader lector = ordre.ExecuteReader();
        lector.Read();
        string usuari = lector.GetString(0);
        if (usuari.Length != 1 || usuari[0] < 'A' || usuari[0] > 'T'
            || lector.IsDBNull(1) || lector.GetInt32(1) != 1)
            throw new InvalidOperationException("Utilitza el compte d'alumne A–T assignat pel professor.");
        return usuari[0] - 'A';
    }

    private static ConfiguracioRobot LlegirConfiguracio(SqlConnection connexio)
    {
        int amplada = 0, alcada = 0, maxTorns = 0;
        using var ordre = new SqlCommand("SELECT clau, valor FROM dbo.configuracio;", connexio);
        using SqlDataReader lector = ordre.ExecuteReader();
        while (lector.Read())
        {
            switch (lector.GetString(0))
            {
                case "amplada_tauler": amplada = lector.GetInt32(1); break;
                case "alcada_tauler": alcada = lector.GetInt32(1); break;
                case "max_torns": maxTorns = lector.GetInt32(1); break;
            }
        }
        if (amplada <= 0 || alcada <= 0 || maxTorns <= 0)
            throw new InvalidOperationException("Falten dimensions o max_torns positius a configuracio.");
        return new(amplada, alcada, maxTorns);
    }

    private static (int Id, string Estat) LlegirTorn(SqlConnection connexio, SqlTransaction? transaccio = null)
    {
        using var ordre = new SqlCommand("""
            SELECT TOP (1) id_torn, estat FROM dbo.torns ORDER BY id_torn DESC;
            """, connexio, transaccio);
        using SqlDataReader lector = ordre.ExecuteReader();
        return lector.Read() ? (lector.GetInt32(0), lector.GetString(1)) : (0, "");
    }

    private static EstatJoc LlegirEstat(SqlConnection connexio, ConfiguracioRobot configuracio)
    {
        // Lectura breu i coherent. Tanquem la transacció abans de decidir, inserir o esperar.
        using SqlTransaction transaccio = connexio.BeginTransaction(IsolationLevel.RepeatableRead);
        var torn = LlegirTorn(connexio, transaccio);
        var estat = new EstatJoc(configuracio.Amplada, configuracio.Alcada)
        {
            IdTorn = torn.Id,
            EstatTorn = torn.Estat
        };

        using (var ordre = new SqlCommand("""
            SELECT id_jugador, posicio_x, posicio_y, actiu FROM dbo.jugadors;
            """, connexio, transaccio))
        using (SqlDataReader lector = ordre.ExecuteReader())
        {
            while (lector.Read())
            {
                int jugador = lector.GetString(0)[0] - 'A';
                estat.PosicionsX[jugador] = lector.IsDBNull(1) ? null : lector.GetInt32(1);
                estat.PosicionsY[jugador] = lector.IsDBNull(2) ? null : lector.GetInt32(2);
                estat.Actius[jugador] = lector.GetBoolean(3);
            }
        }

        using (var ordre = new SqlCommand("""
            SELECT posicio_x, posicio_y, id_propietari, nivell, torn_actualitzacio FROM dbo.caselles;
            """, connexio, transaccio))
        using (SqlDataReader lector = ordre.ExecuteReader())
        {
            while (lector.Read())
            {
                int x = lector.GetInt32(0), y = lector.GetInt32(1);
                estat.Propietaris[x, y] = lector.IsDBNull(2) ? '\0' : lector.GetString(2)[0];
                estat.Nivells[x, y] = lector.GetByte(3);
                estat.TornsPintura[x, y] = lector.IsDBNull(4) ? null : lector.GetInt32(4);
            }
        }

        // Evita repetir l'ordre si es reinicia el programa durant el mateix torn.
        using (var ordre = new SqlCommand("""
            SELECT COUNT(*) FROM dbo.moviments WHERE id_torn = @torn AND id_jugador = USER_NAME();
            """, connexio, transaccio))
        {
            ordre.Parameters.Add("@torn", SqlDbType.Int).Value = torn.Id;
            estat.TePeticioPropia = (int)ordre.ExecuteScalar()! > 0;
        }
        transaccio.Commit();
        return estat;
    }

    private static void EnviarMoviment(SqlConnection connexio, int idTorn, int novaX, int novaY)
    {
        using var ordre = new SqlCommand("""
            INSERT INTO dbo.moviments (id_torn, nova_x, nova_y)
            VALUES (@torn, @x, @y);
            """, connexio);
        ordre.Parameters.Add("@torn", SqlDbType.Int).Value = idTorn;
        ordre.Parameters.Add("@x", SqlDbType.Int).Value = novaX;
        ordre.Parameters.Add("@y", SqlDbType.Int).Value = novaY;
        // SQL Server atribueix l'emissor i l'hora; desar la petició no confirma el moviment.
        ordre.ExecuteNonQuery();
    }

    private static void MostrarEstat(EstatJoc estat, int jugador)
    {
        Console.WriteLine($"\nTorn {estat.IdTorn}: {estat.EstatTorn}. Pintura: . lliure, minúscula normal, majúscula reforçada.");
        int ampleCasella = (estat.Amplada - 1).ToString().Length + 1;
        int ampleFila = (estat.Alcada - 1).ToString().Length;
        string coordenades = new string(' ', ampleFila + 1);
        for (int x = 0; x < estat.Amplada; x++)
            coordenades += x.ToString().PadLeft(ampleCasella);
        Console.WriteLine(coordenades);
        for (int y = 0; y < estat.Alcada; y++)
        {
            Console.Write(y.ToString().PadLeft(ampleFila) + " ");
            for (int x = 0; x < estat.Amplada; x++)
            {
                char simbol = estat.Nivells[x, y] switch
                {
                    1 => char.ToLowerInvariant(estat.Propietaris[x, y]),
                    2 => estat.Propietaris[x, y],
                    _ => '.'
                };
                Console.Write(simbol.ToString().PadLeft(ampleCasella));
            }
            Console.WriteLine(" " + y.ToString().PadLeft(ampleFila));
        }
        Console.WriteLine(coordenades);
        for (int i = 0; i < estat.Actius.Length; i++)
            if (estat.PosicionsX[i] is int x && estat.PosicionsY[i] is int y)
                Console.WriteLine($"{(char)('A' + i)}: ({x}, {y}){(i == jugador ? " (tu)" : "")}{(estat.Actius[i] ? "" : " (inactiu)")}");
    }
}
