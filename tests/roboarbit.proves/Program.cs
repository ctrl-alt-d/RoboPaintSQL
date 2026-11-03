using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

ProvesRegles.Executar();
ProvesTauler.Executar();
if (args.SequenceEqual(new[] { "--regles" }))
    return;

// Executar des de l'arrel. Només crea i elimina bases temporals amb un nom propi.
string cadenaProves = Environment.GetEnvironmentVariable("ROBOPAINTSQL_CONNEXIO_PROVES")
    ?? throw new InvalidOperationException("Cal definir ROBOPAINTSQL_CONNEXIO_PROVES amb una connexió d'administrador.");
string? credencialsOriginals = Environment.GetEnvironmentVariable("ROBOPAINTSQL_CREDENCIALS_PROVES");
string carpetaTemporal = Directory.CreateTempSubdirectory("robopaintsql-proves-").FullName;
string rutaCredencials = Path.Combine(carpetaTemporal, ".credencials-alumnes.csv");
if (credencialsOriginals is not null)
    File.Copy(credencialsOriginals, rutaCredencials);

// Les proves passen la connexió a un procés fill després d'obrir-la; cal conservar-ne la clau en memòria.
var dadesConnexio = new SqlConnectionStringBuilder(cadenaProves)
    { InitialCatalog = "master", Pooling = false, PersistSecurityInfo = true };
using var administrador = new SqlConnection(dadesConnexio.ConnectionString);
administrador.Open();
string ddl = File.ReadAllText("sql/ddl-tsql.sql");
string programa = Path.GetFullPath("src/roboarbit/bin/Debug/net10.0/roboarbit.dll");
int comprovacions = 0;
string ultimError = "";

try
{
    if (Enter(administrador, "SELECT COUNT(*) FROM sys.sql_logins WHERE name IN ('A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T');") == 0)
    {
        AmbBase(connexio =>
        {
            ExecutarSql(connexio, "CREATE USER [T] WITHOUT LOGIN;");
            Comprovar(ExecutarArbitre(connexio) == 1 && SenseDades(connexio), "error durant la creació inicial de comptes");
            Comprovar(Enter(administrador, "SELECT COUNT(*) FROM sys.sql_logins WHERE name IN ('A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T');") == 0, "desfeta dels inicis de sessió nous");
            Comprovar(File.Exists(rutaCredencials) && File.ReadAllLines(rutaCredencials).Length == 21, "credencials generades conservades per al reintent");
        });
    }

    AmbBase(connexio =>
    {
        Comprovar(ExecutarArbitre(connexio) == 0, "inicialització completa");
        Comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE actiu = 1;") == 20, "20 jugadors actius");
        Comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.caselles;") == 600, "600 caselles");
        Comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.caselles WHERE nivell <> 0 OR id_propietari IS NOT NULL OR torn_actualitzacio IS NOT NULL;") == 0, "tauler sense pintura");
        Comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.caselles WHERE posicio_x < 0 OR posicio_x >= 30 OR posicio_y < 0 OR posicio_y >= 20;") == 0, "límits del tauler");
        Comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE posicio_x IS NULL AND posicio_y IS NULL;") == 20, "robots sense posició abans del primer INSERT");
        Comprovar(Enter(connexio, "SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo');") == 5, "cinc taules sense participants_torn");
        Comprovar(Enter(connexio, "SELECT (SELECT COUNT(*) FROM dbo.torns) + (SELECT COUNT(*) FROM dbo.moviments);") == 0, "sense torns ni peticions");

        byte[] csvAbans = File.ReadAllBytes(rutaCredencials);
        Comprovar(ExecutarArbitre(connexio) == 0, "reexecució amb usuaris i credencials existents");
        Comprovar(csvAbans.SequenceEqual(File.ReadAllBytes(rutaCredencials))
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.caselles;") == 600, "reexecució conserva el CSV i recrea el tauler");

        using (var altra = new SqlConnection(connexio.ConnectionString))
        {
            altra.Open();
            ExecutarSql(altra, "EXEC sys.sp_getapplock @Resource = N'RoboPaintSQL.arbitre', @LockMode = 'Exclusive', @LockOwner = 'Session';");
            Comprovar(ExecutarArbitre(connexio) == 1
                && Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors;") == 20,
                "reinicialització rebutjada mentre hi ha un àrbitre executant-se");
            ExecutarSql(altra, "EXEC sys.sp_releaseapplock @Resource = N'RoboPaintSQL.arbitre', @LockOwner = 'Session';");
        }

        var credencials = File.ReadAllLines(rutaCredencials).Skip(1)
            .Select(linia => linia.Split(';')).ToDictionary(camps => camps[0], camps => camps[1]);
        Comprovar(credencials.Count == 20, "CSV amb 20 credencials");
        for (char usuari = 'A'; usuari <= 'T'; usuari++)
        {
            var dadesAlumne = new SqlConnectionStringBuilder(connexio.ConnectionString)
            {
                UserID = usuari.ToString(), Password = credencials[usuari.ToString()], IntegratedSecurity = false
            };
            using var alumne = new SqlConnection(dadesAlumne.ConnectionString);
            alumne.Open();
            Comprovar(Enter(alumne, "SELECT IS_ROLEMEMBER('rol_alumnes');") == 1
                && Enter(alumne, "SELECT COUNT(*) FROM dbo.caselles;") == 600, $"connexió i lectura de {usuari}");
        }

        // SQL Server pot acceptar aquestes dades; només es comproven permisos, no regles del joc.
        ExecutarSql(connexio, "INSERT dbo.torns VALUES (1, 'OBERT', SYSUTCDATETIME(), DATEADD(second, 3, SYSUTCDATETIME()), NULL);");
        var dadesA = new SqlConnectionStringBuilder(connexio.ConnectionString) { UserID = "A", Password = credencials["A"] };
        using var alumneA = new SqlConnection(dadesA.ConnectionString);
        alumneA.Open();
        ExecutarSql(alumneA, "INSERT dbo.moviments (id_torn, nova_x, nova_y) VALUES (1, 1, 0);");
        Comprovar(Enter(alumneA, "SELECT COUNT(*) FROM dbo.moviments WHERE id_jugador = 'A';") == 1, "moviment identificat");
        bool actualitzacioRebutjada = false;
        try { ExecutarSql(alumneA, "UPDATE dbo.jugadors SET posicio_x = 2 WHERE id_jugador = 'A';"); }
        catch (SqlException error) when (error.Number == 229) { actualitzacioRebutjada = true; }
        Comprovar(actualitzacioRebutjada, "alumne sense permís d'actualització");

        ExecutarSql(connexio, """
            UPDATE dbo.caselles SET id_propietari = 'A', nivell = 2, torn_actualitzacio = 1
                WHERE posicio_x = 0 AND posicio_y = 0;
            UPDATE dbo.jugadors SET actiu = 0;
            UPDATE dbo.configuracio SET valor = 4 WHERE clau = 'amplada_tauler';
            UPDATE dbo.configuracio SET valor = 5 WHERE clau = 'alcada_tauler';
            """);
        string rutaAbsent = Path.Combine(carpetaTemporal, ".credencials-partida-absents.csv");
        Comprovar(ExecutarArbitre(connexio, rutaAbsent) == 1
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.caselles WHERE nivell = 2;") == 1
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.moviments;") == 1,
            "error de credencials conserva la partida anterior");

        // Simula un error d'escriptura després de buidar la partida, només en aquesta base temporal.
        ExecutarSql(connexio, """
            CREATE TRIGGER dbo.prova_error_inicialitzacio ON dbo.caselles AFTER INSERT AS
                THROW 50003, N'Error simulat de les proves.', 1;
            """);
        bool desfeta = false;
        try
        {
            try { roboarbit.InicialitzadorJoc.Inicialitzar(connexio, rutaCredencials); }
            catch (SqlException error) when (error.Number == 50003) { desfeta = true; }
        }
        finally { ExecutarSql(connexio, "DROP TRIGGER dbo.prova_error_inicialitzacio;"); }
        Comprovar(desfeta
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.caselles WHERE nivell = 2;") == 1
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.moviments;") == 1
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.torns;") == 1
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE actiu = 0;") == 20,
            "error durant la reinicialització desfà la neteja i restaura la partida anterior");

        Comprovar(ExecutarArbitre(connexio) == 0, "reinicialització d'una partida amb torns, moviments i pintura");
        Comprovar(Enter(connexio, "SELECT (SELECT COUNT(*) FROM dbo.torns) + (SELECT COUNT(*) FROM dbo.moviments);") == 0,
            "reinicialització elimina l'historial anterior");
        Comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.caselles WHERE posicio_x BETWEEN 0 AND 3 AND posicio_y BETWEEN 0 AND 4 AND nivell = 0 AND id_propietari IS NULL AND torn_actualitzacio IS NULL;") == 20
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.caselles;") == 20,
            "reinicialització recrea totes les caselles lliures amb les dimensions configurades");
        Comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE actiu = 1 AND posicio_x IS NULL AND posicio_y IS NULL;") == 20,
            "reinicialització reactiva els 20 robots sense posició");
        Comprovar(csvAbans.SequenceEqual(File.ReadAllBytes(rutaCredencials))
            && Enter(alumneA, "SELECT COUNT(*) FROM dbo.caselles;") == 20,
            "reinicialització conserva les credencials i l'accés dels usuaris existents");
        Comprovar(new roboarbit.ArbitreJoc(connexio).PrepararTorn(roboarbit.ConfiguracioJoc.Llegir(connexio))?.Id == 1,
            "la partida reinicialitzada torna a començar pel torn 1");
    });

    byte[] credencialsDesades = File.ReadAllBytes(rutaCredencials);
    AmbBase(connexio =>
    {
        // Copiem l'aplicació i el CSV per provar el menú sense tocar el fitxer local del professor.
        string arrelProva = Path.Combine(carpetaTemporal, "projecte-ruta");
        string carpetaProjecte = Path.Combine(arrelProva, "src", "roboarbit");
        string carpetaExecutable = Path.Combine(carpetaProjecte, "bin", "Debug", "net10.0");
        string carpetaOriginal = Path.GetDirectoryName(programa)!;
        foreach (string fitxer in Directory.EnumerateFiles(carpetaOriginal, "*", SearchOption.AllDirectories))
        {
            string desti = Path.Combine(carpetaExecutable, Path.GetRelativePath(carpetaOriginal, fitxer));
            Directory.CreateDirectory(Path.GetDirectoryName(desti)!);
            File.Copy(fitxer, desti);
        }
        File.WriteAllText(Path.Combine(carpetaProjecte, "roboarbit.csproj"), "<Project />");
        string csvProjecte = Path.Combine(carpetaProjecte, ".credencials-alumnes.csv");
        File.Copy(rutaCredencials, csvProjecte);
        string programaCopiat = Path.Combine(carpetaExecutable, "roboarbit.dll");

        foreach (var (carpetaTreball, descripcio) in new[]
        {
            (arrelProva, "l'arrel del repositori"),
            (carpetaProjecte, "src/roboarbit"),
            (carpetaExecutable, "la carpeta de l'executable")
        })
        {
            var inici = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = carpetaTreball,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            inici.ArgumentList.Add(programaCopiat);
            inici.Environment["ROBOPAINTSQL_CONNEXIO"] = connexio.ConnectionString;
            using Process proces = Process.Start(inici)!;
            Task<string> sortida = proces.StandardOutput.ReadToEndAsync();
            Task<string> errors = proces.StandardError.ReadToEndAsync();
            proces.StandardInput.WriteLine("1");
            proces.StandardInput.WriteLine("0");
            proces.StandardInput.Close();
            if (!proces.WaitForExit(30000))
            {
                proces.Kill(entireProcessTree: true);
                throw new InvalidOperationException("El menú no ha acabat la inicialització dins del temps de la prova.");
            }
            ultimError = errors.Result.Trim();
            foreach (string linia in File.ReadAllLines(csvProjecte).Skip(1))
                ultimError = ultimError.Replace(linia.Split(';')[1], "[credencial oculta]");
            Comprovar(proces.ExitCode == 0 && errors.Result == ""
                && sortida.Result.Contains("Joc inicialitzat:")
                && sortida.Result.Contains("Credencials locals:")
                && Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE actiu = 1;") == 20,
                $"menú inicialitza amb el CSV existent des de {descripcio}");
            foreach (string linia in File.ReadAllLines(csvProjecte).Skip(1))
                ComprovarSenseComptar(!sortida.Result.Contains(linia.Split(';')[1])
                    && !errors.Result.Contains(linia.Split(';')[1]), "menú sense contrasenyes a la consola");
        }
        Comprovar(credencialsDesades.SequenceEqual(File.ReadAllBytes(csvProjecte)),
            "el menú conserva les credencials amb qualsevol carpeta de treball");
    });

    AmbBase(connexio =>
    {
        ExecutarSql(connexio, "UPDATE dbo.configuracio SET valor = CASE clau WHEN 'amplada_tauler' THEN 4 ELSE 5 END WHERE clau IN ('amplada_tauler', 'alcada_tauler');");
        Comprovar(ExecutarArbitre(connexio) == 0, "tauler configurable de 4 per 5");
        Comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.caselles WHERE posicio_x BETWEEN 0 AND 3 AND posicio_y BETWEEN 0 AND 4;") == 20, "dimensions configurades respectades");
        Comprovar(credencialsDesades.SequenceEqual(File.ReadAllBytes(rutaCredencials)), "comptes reutilitzats sense modificar el CSV");
    });

    string[] claus = ["amplada_tauler", "alcada_tauler", "durada_torn_ms", "max_torns",
        "caducitat_reforcada", "caducitat_normal", "max_moviments_torn", "punts_normal", "punts_reforcada"];
    foreach (string clau in claus)
    {
        foreach (bool eliminar in new[] { false, true })
        {
            AmbBase(connexio =>
            {
                ExecutarSql(connexio, eliminar
                    ? $"DELETE dbo.configuracio WHERE clau = '{clau}';"
                    : $"UPDATE dbo.configuracio SET valor = 0 WHERE clau = '{clau}';");
                Comprovar(ExecutarArbitre(connexio) == 1 && SenseDades(connexio), $"configuració rebutjada: {clau}, absent={eliminar}");
            });
        }
    }

    AmbBase(connexio =>
    {
        ExecutarSql(connexio, "UPDATE dbo.configuracio SET valor = 1 WHERE clau = 'amplada_tauler'; UPDATE dbo.configuracio SET valor = 19 WHERE clau = 'alcada_tauler';");
        Comprovar(ExecutarArbitre(connexio) == 0 && Enter(connexio, "SELECT COUNT(*) FROM dbo.caselles;") == 19,
            "tauler petit permès perquè les entrades poden compartir casella");
    });

    AmbBase(connexio =>
    {
        ExecutarSql(connexio, "INSERT dbo.caselles (posicio_x, posicio_y) VALUES (0, 0);");
        Comprovar(ExecutarArbitre(connexio) == 0
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.caselles;") == 600
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE actiu = 1;") == 20,
            "inicialització substitueix les dades parcials existents");
    });

    AmbBase(connexio =>
    {
        string rutaAbsent = Path.Combine(carpetaTemporal, ".credencials-alumnes-absents.csv");
        Comprovar(ExecutarArbitre(connexio, rutaAbsent) == 1
            && !File.Exists(rutaAbsent) && SenseDades(connexio), "credencials absents de comptes existents rebutjades");
    });

    foreach (string csvInvalid in new[] { "capcalera incorrecta\n", "usuari;contrasenya\nU;exemple\n", "usuari;contrasenya\nA;exemple\nA;exemple\n" })
    {
        AmbBase(connexio =>
        {
            string rutaInvalida = Path.Combine(carpetaTemporal, ".credencials-alumnes-invalides.csv");
            File.WriteAllText(rutaInvalida, csvInvalid);
            Comprovar(ExecutarArbitre(connexio, rutaInvalida) == 1 && SenseDades(connexio), "CSV invàlid rebutjat");
        });
    }

    AmbBase(connexio =>
    {
        ExecutarSql(connexio, "CREATE USER [T] WITHOUT LOGIN;");
        Comprovar(ExecutarArbitre(connexio) == 1 && SenseDades(connexio), "usuari vinculat incorrectament rebutjat");
        Comprovar(Enter(connexio, "SELECT COUNT(*) FROM sys.database_principals WHERE name IN ('A', 'B', 'C', 'S');") == 0, "desfeta dels usuaris creats abans de l'error");
    });

    AmbBase(connexio =>
    {
        // Reconstruïm l'esquema anterior només en aquesta base temporal.
        ExecutarSql(connexio, """
            ALTER TABLE dbo.jugadors ALTER COLUMN posicio_x int NOT NULL;
            ALTER TABLE dbo.jugadors ALTER COLUMN posicio_y int NOT NULL;
            CREATE TABLE dbo.participants_torn (
                id_torn int NOT NULL REFERENCES dbo.torns(id_torn),
                id_jugador char(1) NOT NULL REFERENCES dbo.jugadors(id_jugador),
                PRIMARY KEY (id_torn, id_jugador));
            INSERT dbo.jugadors VALUES ('A', 2, 3, 1);
            INSERT dbo.torns VALUES (1, 'TANCAT', SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME());
            INSERT dbo.participants_torn VALUES (1, 'A');
            INSERT dbo.caselles VALUES (2, 3, 'A', 1, 1);
            CREATE USER [A] WITHOUT LOGIN;
            ALTER ROLE rol_alumnes ADD MEMBER [A];
            EXECUTE AS USER = 'A';
            INSERT dbo.moviments (id_torn, nova_x, nova_y) VALUES (1, 2, 3);
            REVERT;
            """);
        string adaptacio = File.ReadAllText("sql/adaptar-entrada-torn-1.sql");
        using (var altra = new SqlConnection(connexio.ConnectionString))
        {
            altra.Open();
            ExecutarSql(altra, "EXEC sys.sp_getapplock @Resource = N'RoboPaintSQL.arbitre', @LockMode = 'Exclusive', @LockOwner = 'Session';");
            bool rebutjada = false;
            try { ExecutarSql(connexio, adaptacio); }
            catch (SqlException error) when (error.Number == 50004) { rebutjada = true; }
            Comprovar(rebutjada && Enter(connexio, "SELECT COUNT(*) FROM dbo.participants_torn;") == 1
                && Enter(connexio, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.jugadors') AND name = 'posicio_x' AND is_nullable = 0;") == 1,
                "adaptació rebutjada sense canvis mentre l'àrbitre té el bloqueig");
            ExecutarSql(altra, "EXEC sys.sp_releaseapplock @Resource = N'RoboPaintSQL.arbitre', @LockOwner = 'Session';");
        }
        ExecutarSql(connexio, adaptacio);
        Comprovar(Enter(connexio, "SELECT COUNT(*) FROM sys.tables WHERE name = 'participants_torn';") == 0
            && Enter(connexio, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.jugadors') AND name IN ('posicio_x', 'posicio_y') AND is_nullable = 1;") == 2,
            "adaptació elimina participants_torn i permet coordenades NULL");
        Comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE posicio_x = 2 AND posicio_y = 3;") == 1
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.caselles WHERE nivell = 1;") == 1
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.moviments;") == 1
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.torns;") == 1
            && Enter(connexio, "SELECT COUNT(*) FROM dbo.configuracio;") == 9
            && Enter(connexio, "SELECT IS_ROLEMEMBER('rol_alumnes', 'A');") == 1,
            "adaptació conserva la partida, la configuració i els permisos existents");
        ExecutarSql(connexio, adaptacio);
        ExecutarSql(connexio, "UPDATE dbo.jugadors SET posicio_x = NULL, posicio_y = NULL;");
        Comprovar(Enter(connexio, "SELECT COUNT(*) FROM dbo.jugadors WHERE posicio_x IS NULL AND posicio_y IS NULL;") == 1,
            "adaptació repetible i coordenades NULL acceptades pel motor");
    });

    AmbBase(connexio => ProvesTorns.Executar(connexio, Comprovar));
    AmbBase(connexio => ProvesTorns.ProvarBucle(connexio, Comprovar));

    Console.WriteLine($"Superades {comprovacions} comprovacions SQL de la inicialització i els torns C#.");
}
finally
{
    Directory.Delete(carpetaTemporal, recursive: true);
}

void AmbBase(Action<SqlConnection> prova)
{
    string nomBase = "RoboPaintSQL_proves_" + Guid.NewGuid().ToString("N");
    ExecutarSql(administrador, $"CREATE DATABASE [{nomBase}];");
    try
    {
        var dadesBase = new SqlConnectionStringBuilder(dadesConnexio.ConnectionString) { InitialCatalog = nomBase };
        using var connexio = new SqlConnection(dadesBase.ConnectionString);
        connexio.Open();
        foreach (string lot in Regex.Split(ddl, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            if (!string.IsNullOrWhiteSpace(lot)) ExecutarSql(connexio, lot);
        prova(connexio);
    }
    finally
    {
        ExecutarSql(administrador, $"ALTER DATABASE [{nomBase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{nomBase}];");
    }
}

int ExecutarArbitre(SqlConnection connexio, string? ruta = null)
{
    var inici = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
    inici.ArgumentList.Add(programa);
    inici.ArgumentList.Add("inicialitza-joc");
    inici.ArgumentList.Add(ruta ?? rutaCredencials);
    inici.Environment["ROBOPAINTSQL_CONNEXIO"] = connexio.ConnectionString;
    using Process proces = Process.Start(inici)!;
    string sortida = proces.StandardOutput.ReadToEnd();
    string errors = proces.StandardError.ReadToEnd();
    proces.WaitForExit();
    if (File.Exists(rutaCredencials))
        foreach (string linia in File.ReadAllLines(rutaCredencials).Skip(1))
            ComprovarSenseComptar(!sortida.Contains(linia.Split(';')[1]) && !errors.Contains(linia.Split(';')[1]), "cap contrasenya a la consola");
    ultimError = errors.Trim();
    return proces.ExitCode;
}

bool SenseDades(SqlConnection connexio) => Enter(connexio,
    "SELECT (SELECT COUNT(*) FROM dbo.jugadors) + (SELECT COUNT(*) FROM dbo.caselles) + (SELECT COUNT(*) FROM dbo.torns);") == 0;

int Enter(SqlConnection connexio, string sql)
{
    using var ordre = new SqlCommand(sql, connexio);
    return Convert.ToInt32(ordre.ExecuteScalar());
}

void ExecutarSql(SqlConnection connexio, string sql)
{
    using var ordre = new SqlCommand(sql, connexio);
    ordre.ExecuteNonQuery();
}

void Comprovar(bool condicio, string descripcio)
{
    ComprovarSenseComptar(condicio, descripcio);
    comprovacions++;
    Console.WriteLine($"Correcte: {descripcio}");
}

void ComprovarSenseComptar(bool condicio, string descripcio)
{
    if (!condicio) throw new InvalidOperationException($"Ha fallat la comprovació: {descripcio}. {ultimError}");
}
