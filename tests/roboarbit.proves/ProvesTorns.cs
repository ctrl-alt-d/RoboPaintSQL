using System.Diagnostics;
using Microsoft.Data.SqlClient;
using roboarbit;

internal static class ProvesTorns
{
    public static void Executar(SqlConnection connexio, Action<bool, string> comprovar)
    {
        PrepararDades(connexio);
        ConfiguracioJoc configuracio = ConfiguracioJoc.Llegir(connexio);
        var arbitre = new ArbitreJoc(connexio);
        TornObert torn = AmbRol(connexio, () => arbitre.PrepararTorn(configuracio))!;
        comprovar(torn.Id == 1 && (torn.Limit - torn.Inici).TotalMilliseconds == configuracio.DuradaTornMs,
            "torn 1 amb durada configurada i dates del servidor");
        comprovar(Enter(connexio, "SELECT COUNT(*) FROM sys.tables WHERE name = 'participants_torn';") == 0,
            "l'àrbitre funciona sense participants_torn");
        comprovar(AmbRol(connexio, () => arbitre.PrepararTorn(configuracio)) == torn, "represa del mateix torn obert");

        Inserir(connexio, "A", 1, int.MinValue, int.MaxValue);
        Inserir(connexio, "A", 1, 0, 1);
        Inserir(connexio, "A", 1, 3, 2);
        Inserir(connexio, "A", 1, 0, 0);
        Inserir(connexio, "B", 1, 2, 1);
        Inserir(connexio, "C", 1, 3, 2);
        Sql(connexio, "UPDATE dbo.jugadors SET actiu = 1 WHERE id_jugador = 'C';");
        Inserir(connexio, "E", 1, 1, 1);
        Inserir(connexio, "F", 1, -1, 0);
        bool desfet = false;
        Thread.Sleep(5);
        try { AmbRol(connexio, () => { arbitre.ResoldreTorn(configuracio, torn, new CancellationToken(true)); return true; }); }
        catch (OperationCanceledException) { desfet = true; }
        comprovar(desfet && Enter(connexio, "SELECT COUNT(*) FROM dbo.torns WHERE estat = 'OBERT' AND data_fi IS NULL;") == 1
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE posicio_x IS NOT NULL OR posicio_y IS NOT NULL;") == 0,
            "cancel·lació desfà les entrades i conserva el torn obert");
        comprovar(Resoldre(connexio, arbitre, configuracio, torn), "resolució manual del torn d'entrada");
        comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE id_jugador = 'A' AND posicio_x = 0 AND posicio_y = 1;") == 1,
            "primera entrada vàlida; invàlides i peticions següents ignorades");
        comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE id_jugador = 'C' AND posicio_x = 3 AND posicio_y = 2;") == 1,
            "es consulten els jugadors actius en resoldre, sense una llista fixada en obrir");
        comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE id_jugador IN ('D', 'E', 'F') AND posicio_x IS NULL AND posicio_y IS NULL;") == 3,
            "sense petició, inactiu o amb entrada invàlida es queda fora del tauler");
        comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.caselles WHERE nivell <> 0;") == 0,
            "l'aparició no pinta");
        comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.torns WHERE estat = 'TANCAT' AND data_fi IS NOT NULL;") == 1,
            "tancament amb data final");
        int nombreMoviments = Enter(connexio, "SELECT COUNT(*) FROM dbo.moviments;");
        Inserir(connexio, "D", 1, 1, 1);
        comprovar(Resoldre(connexio, arbitre, configuracio, torn)
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE id_jugador = 'D' AND posicio_x IS NULL;") == 1
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.moviments;") == nombreMoviments + 1,
            "entrada posterior al tancament es desa però no s'aplica");

        torn = AmbRol(connexio, () => arbitre.PrepararTorn(configuracio))!;
        comprovar(torn.Id == 2, "següent torn després de les entrades");
        Inserir(connexio, "A", 2, 0, 1);
        Inserir(connexio, "A", 2, 3, 2);
        Inserir(connexio, "A", 2, 0, 0);
        Inserir(connexio, "A", 2, 1, 1);
        Inserir(connexio, "A", 2, 0, 2);
        Inserir(connexio, "B", 2, 1, 1);
        Inserir(connexio, "C", 2, 2, 2);
        Inserir(connexio, "D", 2, 1, 0);
        comprovar(Resoldre(connexio, arbitre, configuracio, torn)
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE posicio_x = 1 AND posicio_y = 1;") == 2
            && Enter(connexio, "SELECT nivell FROM dbo.caselles WHERE posicio_x = 1 AND posicio_y = 1;") == 0,
            "pas cardinal, límit d'ordres vàlides i col·lisió simultània des del torn 2");
        comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE id_jugador = 'D' AND posicio_x IS NULL;") == 1,
            "un jugador sense entrada al torn 1 no pot aparèixer al torn 2");
        comprovar(Enter(connexio, "SELECT nivell FROM dbo.caselles WHERE posicio_x = 2 AND posicio_y = 2;") == 1,
            "el desplaçament després de l'entrada pinta normalment");

        torn = AmbRol(connexio, () => arbitre.PrepararTorn(configuracio))!;
        Sql(connexio, "UPDATE dbo.jugadors SET actiu = 0 WHERE id_jugador = 'B';");
        Sql(connexio, "UPDATE dbo.caselles SET id_propietari = 'B', nivell = 2, torn_actualitzacio = 2 WHERE posicio_x = 1 AND posicio_y = 1;");
        Sql(connexio, "UPDATE dbo.torns SET data_limit = DATEADD(millisecond, -10, SYSUTCDATETIME()) WHERE id_torn = 3;");
        torn = AmbRol(connexio, () => arbitre.PrepararTorn(configuracio))!;
        Inserir(connexio, "A", 3, 1, 1);
        Inserir(connexio, "B", 3, 1, 2);
        comprovar(Resoldre(connexio, arbitre, configuracio, torn)
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE id_jugador = 'A' AND posicio_x = 1 AND posicio_y = 1;") == 1,
            "resolució manual sense ordres vàlides: els robots queden quiets");
        comprovar(Enter(connexio, "SELECT nivell FROM dbo.caselles WHERE posicio_x = 1 AND posicio_y = 1;") == 2
            && Enter(connexio, "SELECT posicio_y FROM dbo.jugadors WHERE id_jugador = 'B';") == 1,
            "inactiu en resoldre no es mou ni col·lideix, també si es desactiva durant el torn");

        torn = AmbRol(connexio, () => arbitre.PrepararTorn(configuracio))!;
        Inserir(connexio, "A", 4, 0, 1);
        Inserir(connexio, "C", 4, 3, 2);
        Resoldre(connexio, arbitre, configuracio, torn);
        torn = AmbRol(connexio, () => arbitre.PrepararTorn(configuracio))!;
        Inserir(connexio, "A", 5, 0, 0);
        Resoldre(connexio, arbitre, configuracio, torn);
        torn = AmbRol(connexio, () => arbitre.PrepararTorn(configuracio))!;
        Inserir(connexio, "A", 6, 0, 1);
        comprovar(Resoldre(connexio, arbitre, configuracio, torn)
            && Enter(connexio, "SELECT nivell FROM dbo.caselles WHERE posicio_x = 0 AND posicio_y = 1;") == 2,
            "retorn a territori propi el reforça en SQL");
        comprovar(AmbRol(connexio, () => arbitre.PrepararTorn(configuracio)) is null
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.torns;") == 6, "màxim de torns respectat");
    }

    public static void ProvarBucle(SqlConnection connexio, Action<bool, string> comprovar)
    {
        PrepararDades(connexio);
        Sql(connexio, "UPDATE dbo.configuracio SET valor = 40 WHERE clau = 'durada_torn_ms'; UPDATE dbo.configuracio SET valor = 2 WHERE clau = 'max_torns';");
        var arbitre = new ArbitreJoc(connexio);
        // Una altra connexió manté el bloqueig mentre es comprova el rebuig de dues instàncies.
        using (var altra = new SqlConnection(connexio.ConnectionString))
        {
            altra.Open();
            Sql(altra, "EXEC sys.sp_getapplock @Resource = N'RoboPaintSQL.arbitre', @LockMode = 'Exclusive', @LockOwner = 'Session';");
            bool rebutjat = false;
            try { AmbRol(connexio, () => { arbitre.Executar(default); return true; }); }
            catch (InvalidOperationException) { rebutjat = true; }
            comprovar(rebutjat, "dues instàncies d'àrbitre rebutjades");
            Sql(altra, "EXEC sys.sp_releaseapplock @Resource = N'RoboPaintSQL.arbitre', @LockOwner = 'Session';");
        }

        Sql(connexio, "UPDATE dbo.jugadors SET actiu = 0;");
        comprovar(AmbRol(connexio, () => arbitre.PrepararTorn(ConfiguracioJoc.Llegir(connexio)))?.Id == 1,
            "el torn 1 s'obre encara que no hi hagi robots al tauler ni jugadors actius");
        Sql(connexio, "UPDATE dbo.jugadors SET actiu = 1 WHERE id_jugador = 'A';");
        using (Process proces = ComencarProces(connexio, "comenca-joc"))
        {
            Task<string> sortida = proces.StandardOutput.ReadToEndAsync();
            Task<string> errors = proces.StandardError.ReadToEndAsync();
            EsperarCondicio(() => Enter(connexio, "SELECT COUNT(*) FROM dbo.torns WHERE id_torn = 1 AND estat = 'OBERT';") == 1);
            Thread.Sleep(150);
            comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.torns WHERE estat = 'TANCAT';") == 0,
                "el termini no fa avançar el torn sense Retorn");
            Inserir(connexio, "A", 1, 1, 0);
            Thread.Sleep(150);
            comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.torns WHERE estat = 'TANCAT';") == 0,
                "tenir ordres de tothom tampoc fa avançar el torn sense Retorn");
            proces.StandardInput.WriteLine();
            proces.StandardInput.Flush();
            EsperarCondicio(() => Enter(connexio, "SELECT COUNT(*) FROM dbo.torns WHERE id_torn = 2 AND estat = 'OBERT';") == 1);
            comprovar(Enter(connexio, "SELECT posicio_x FROM dbo.jugadors WHERE id_jugador = 'A';") == 1
                && Enter(connexio, "SELECT nivell FROM dbo.caselles WHERE posicio_x = 1 AND posicio_y = 0;") == 0,
                "Retorn confirma l'entrada sense pintar, inclosa una ordre posterior al termini antic");
            proces.StandardInput.Close();
            AcabarProces(proces);
            comprovar(proces.ExitCode == 0 && errors.Result == ""
                && Enter(connexio, "SELECT COUNT(*) FROM dbo.torns WHERE id_torn = 2 AND estat = 'OBERT';") == 1,
                "final d'entrada atura el joc i conserva el torn obert per reprendre");
            comprovar(sortida.Result.Contains("Tauler després del torn 1:"), "ordre directa mostra el tauler confirmat");
        }

        Inserir(connexio, "A", 2, 2, 0);
        Thread.Sleep(5);
        // L'opció 2 reprèn el torn obert i només el resol amb un nou Retorn.
        using (Process proces = ComencarProces(connexio))
        {
            Task<string> lecturaSortida = proces.StandardOutput.ReadToEndAsync();
            Task<string> errors = proces.StandardError.ReadToEndAsync();
            proces.StandardInput.WriteLine("incorrecta");
            proces.StandardInput.WriteLine("2");
            proces.StandardInput.WriteLine();
            proces.StandardInput.WriteLine("0");
            proces.StandardInput.Close();
            AcabarProces(proces);
            string sortida = lecturaSortida.Result;
            comprovar(proces.ExitCode == 0 && errors.Result == "" && sortida.Contains("Opció no vàlida.")
                && sortida.Contains("Classificació final:") && sortida.Contains("A: 1 punts")
                && sortida.Contains("C: 0 punts"), "menú: entrada invàlida, represa manual, classificació i sortir");
            comprovar(sortida.IndexOf("Tauler després del torn 2:", StringComparison.Ordinal)
                    > sortida.IndexOf("Torn 2 tancat.", StringComparison.Ordinal)
                && sortida.Contains("..a.")
                && sortida.Contains("Robots (x, y): A=(2, 0)") && !sortida.Contains("B=(") && !sortida.Contains("C=("),
                "opció 2 mostra el tauler i només els robots que han entrat després de confirmar el torn");
        }
        comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.torns WHERE estat = 'TANCAT';") == 2,
            "bucle manual s'atura al màxim configurat");
        AmbRol(connexio, () => { arbitre.Executar(default); return true; });
        comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.torns;") == 2, "reprendre partida acabada no crea torns");
    }

    private static Process ComencarProces(SqlConnection connexio, string? ordre = null)
    {
        var inici = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        inici.ArgumentList.Add(Path.GetFullPath("src/roboarbit/bin/Debug/net10.0/roboarbit.dll"));
        if (ordre is not null) inici.ArgumentList.Add(ordre);
        inici.Environment["ROBOPAINTSQL_CONNEXIO"] = connexio.ConnectionString;
        return Process.Start(inici)!;
    }

    private static void AcabarProces(Process proces)
    {
        if (proces.WaitForExit(5000)) return;
        proces.Kill(entireProcessTree: true);
        throw new InvalidOperationException("El procés de proves de l'àrbitre no s'ha aturat.");
    }

    private static void EsperarCondicio(Func<bool> condicio)
    {
        var rellotge = Stopwatch.StartNew();
        while (!condicio())
        {
            if (rellotge.ElapsedMilliseconds > 5000)
                throw new InvalidOperationException("No s'ha observat el torn esperat.");
            Thread.Sleep(10);
        }
    }

    private static void PrepararDades(SqlConnection connexio)
    {
        Sql(connexio, """
            UPDATE dbo.configuracio SET valor = 4 WHERE clau = 'amplada_tauler';
            UPDATE dbo.configuracio SET valor = 6 WHERE clau = 'max_torns';
            UPDATE dbo.configuracio SET valor = 3 WHERE clau = 'alcada_tauler';
            UPDATE dbo.configuracio SET valor = 60000 WHERE clau = 'durada_torn_ms';
            UPDATE dbo.configuracio SET valor = 2 WHERE clau = 'max_moviments_torn';
            INSERT dbo.jugadors VALUES ('A', NULL, NULL, 1), ('B', NULL, NULL, 1), ('C', NULL, NULL, 0),
                ('D', NULL, NULL, 1), ('E', NULL, NULL, 0), ('F', NULL, NULL, 1);
            CREATE USER [A] WITHOUT LOGIN;
            CREATE USER [B] WITHOUT LOGIN;
            CREATE USER [C] WITHOUT LOGIN;
            ALTER ROLE rol_alumnes ADD MEMBER [A];
            ALTER ROLE rol_alumnes ADD MEMBER [B];
            ALTER ROLE rol_alumnes ADD MEMBER [C];
            CREATE USER [D] WITHOUT LOGIN;
            CREATE USER [E] WITHOUT LOGIN;
            CREATE USER [F] WITHOUT LOGIN;
            ALTER ROLE rol_alumnes ADD MEMBER [D];
            ALTER ROLE rol_alumnes ADD MEMBER [E];
            ALTER ROLE rol_alumnes ADD MEMBER [F];
            CREATE USER [arbitre_proves] WITHOUT LOGIN;
            ALTER ROLE rol_arbitre ADD MEMBER [arbitre_proves];
            """);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 4; x++)
                Sql(connexio, $"INSERT dbo.caselles (posicio_x, posicio_y) VALUES ({x}, {y});");
    }

    private static T AmbRol<T>(SqlConnection connexio, Func<T> accio)
    {
        Sql(connexio, "EXECUTE AS USER = 'arbitre_proves';");
        try { return accio(); }
        finally { Sql(connexio, "REVERT;"); }
    }

    private static void Inserir(SqlConnection connexio, string jugador, int torn, int x, int y)
    {
        Sql(connexio, $"EXECUTE AS USER = '{jugador}';");
        try { Sql(connexio, $"INSERT dbo.moviments (id_torn, nova_x, nova_y) VALUES ({torn}, {x}, {y});"); }
        finally { Sql(connexio, "REVERT;"); }
    }

    private static bool Resoldre(SqlConnection connexio, ArbitreJoc arbitre, ConfiguracioJoc configuracio, TornObert torn)
    {
        Thread.Sleep(5); // El tall exclusiu té precisió de mil·lisegons.
        AmbRol(connexio, () => { arbitre.ResoldreTorn(configuracio, torn); return true; });
        return true;
    }

    private static int Enter(SqlConnection connexio, string sql)
    {
        using var ordre = new SqlCommand(sql, connexio);
        return Convert.ToInt32(ordre.ExecuteScalar());
    }

    private static void Sql(SqlConnection connexio, string sql)
    {
        using var ordre = new SqlCommand(sql, connexio);
        ordre.ExecuteNonQuery();
    }
}
