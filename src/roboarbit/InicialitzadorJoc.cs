using System.Data;
using Microsoft.Data.SqlClient;

namespace roboarbit;

internal static class InicialitzadorJoc
{
    public static long Inicialitzar(SqlConnection connexio, string fitxerCredencials)
    {
        using SqlTransaction transaccio = connexio.BeginTransaction(IsolationLevel.Serializable);
        // Comparteix el bloqueig amb el bucle: no es pot reinicialitzar mentre s'està jugant.
        using (var bloqueig = new SqlCommand("""
            DECLARE @resultat int;
            EXEC @resultat = sys.sp_getapplock @Resource = N'RoboPaintSQL.arbitre',
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0;
            SELECT @resultat;
            """, connexio, transaccio))
        {
            if (Convert.ToInt32(bloqueig.ExecuteScalar()) < 0)
                throw new InvalidOperationException("Ja hi ha un àrbitre executant-se en aquesta base. Atura'l abans d'inicialitzar el joc.");
        }
        ConfiguracioJoc configuracio = ConfiguracioJoc.Llegir(connexio, transaccio);

        int amplada = configuracio.Amplada;
        int alcada = configuracio.Alcada;
        long nombreCaselles = (long)amplada * alcada;

        Dictionary<char, string> credencials = CredencialsAlumnes.Llegir(fitxerCredencials);
        HashSet<char> comptesExistents = LlegirComptesExistents(connexio, transaccio);
        foreach (char usuari in comptesExistents)
        {
            if (!credencials.ContainsKey(usuari))
                throw new InvalidOperationException($"Falta la credencial de {usuari} al fitxer '{Path.GetFullPath(fitxerCredencials)}', però el compte SQL ja existeix. Recupera el CSV original; no se'n canviarà la contrasenya.");
        }

        bool calDesar = false;
        for (char usuari = 'A'; usuari <= 'T'; usuari++)
        {
            if (!credencials.ContainsKey(usuari))
            {
                credencials.Add(usuari, CredencialsAlumnes.GenerarContrasenya());
                calDesar = true;
            }
        }

        if (calDesar)
            CredencialsAlumnes.Desar(fitxerCredencials, credencials);

        for (char usuari = 'A'; usuari <= 'T'; usuari++)
            PrepararCompte(connexio, transaccio, usuari, credencials[usuari]);

        AdaptarEsquema(connexio, transaccio);
        BuidarPartida(connexio, transaccio);
        CrearJugadors(connexio, transaccio);
        CrearCaselles(connexio, transaccio, amplada, alcada);
        transaccio.Commit();
        return nombreCaselles;
    }

    private static void AdaptarEsquema(SqlConnection connexio, SqlTransaction transaccio)
    {
        // Les bases antigues exigien una posició inicial i conservaven participants per torn.
        // L'adaptació es desfà també si després falla la creació de la partida nova.
        const string sql = """
            IF EXISTS (SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(N'dbo.jugadors') AND name = N'posicio_x' AND is_nullable = 0)
                ALTER TABLE dbo.jugadors ALTER COLUMN posicio_x int NULL;

            IF EXISTS (SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(N'dbo.jugadors') AND name = N'posicio_y' AND is_nullable = 0)
                ALTER TABLE dbo.jugadors ALTER COLUMN posicio_y int NULL;

            IF OBJECT_ID(N'dbo.participants_torn', N'U') IS NOT NULL
                DROP TABLE dbo.participants_torn;
            """;
        using var ordre = new SqlCommand(sql, connexio, transaccio);
        ordre.ExecuteNonQuery();
    }

    private static void BuidarPartida(SqlConnection connexio, SqlTransaction transaccio)
    {
        // Primer les taules dependents, per respectar les claus foranes.
        const string sql = """
            DELETE FROM dbo.moviments;
            DELETE FROM dbo.caselles;
            DELETE FROM dbo.torns;
            DELETE FROM dbo.jugadors;
            """;
        using var ordre = new SqlCommand(sql, connexio, transaccio);
        ordre.ExecuteNonQuery();
    }

    private static HashSet<char> LlegirComptesExistents(SqlConnection connexio, SqlTransaction transaccio)
    {
        var comptes = new HashSet<char>();
        using var ordre = new SqlCommand("SELECT name FROM sys.server_principals WHERE principal_id = SUSER_ID(@usuari);", connexio, transaccio);
        ordre.Parameters.Add("@usuari", SqlDbType.NVarChar, 1);
        for (char usuari = 'A'; usuari <= 'T'; usuari++)
        {
            ordre.Parameters["@usuari"].Value = usuari.ToString();
            if (ordre.ExecuteScalar() is string nom)
            {
                if (nom != usuari.ToString())
                    throw new InvalidOperationException($"El compte SQL de {usuari} ha de tenir exactament aquesta lletra majúscula.");
                comptes.Add(usuari);
            }
        }

        return comptes;
    }

    private static void PrepararCompte(SqlConnection connexio, SqlTransaction transaccio, char usuari, string contrasenya)
    {
        // CREATE LOGIN no admet un paràmetre directament a PASSWORD: cal SQL dinàmic amb QUOTENAME.
        const string sql = """
            DECLARE @sql nvarchar(max);
            IF SUSER_ID(@usuari) IS NULL
            BEGIN
                SET @sql = N'CREATE LOGIN ' + QUOTENAME(@usuari)
                    + N' WITH PASSWORD = ' + QUOTENAME(@contrasenya, '''')
                    + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
                EXEC sys.sp_executesql @sql;
            END;

            IF USER_ID(@usuari) IS NULL
            BEGIN
                SET @sql = N'CREATE USER ' + QUOTENAME(@usuari)
                    + N' FOR LOGIN ' + QUOTENAME(@usuari) + N';';
                EXEC sys.sp_executesql @sql;
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.database_principals
                WHERE name COLLATE Latin1_General_100_BIN2 = @usuari
                    AND sid = SUSER_SID(@usuari) AND type = 'S')
                THROW 50002, N'L''usuari existent no correspon al compte SQL esperat.', 1;

            IF IS_ROLEMEMBER('rol_alumnes', @usuari) <> 1
            BEGIN
                SET @sql = N'ALTER ROLE rol_alumnes ADD MEMBER ' + QUOTENAME(@usuari) + N';';
                EXEC sys.sp_executesql @sql;
            END;
            """;
        using var ordre = new SqlCommand(sql, connexio, transaccio);
        ordre.Parameters.Add("@usuari", SqlDbType.NVarChar, 1).Value = usuari.ToString();
        ordre.Parameters.Add("@contrasenya", SqlDbType.NVarChar, 128).Value = contrasenya;
        ordre.ExecuteNonQuery();
    }

    private static void CrearJugadors(SqlConnection connexio, SqlTransaction transaccio)
    {
        const string sql = """
            INSERT INTO dbo.jugadors (id_jugador, posicio_x, posicio_y, actiu)
            VALUES (@jugador, NULL, NULL, 1);
            """;
        using var ordre = new SqlCommand(sql, connexio, transaccio);
        ordre.Parameters.Add("@jugador", SqlDbType.Char, 1);
        for (char jugador = 'A'; jugador <= 'T'; jugador++)
        {
            ordre.Parameters["@jugador"].Value = jugador.ToString();
            ordre.ExecuteNonQuery();
        }
    }

    private static void CrearCaselles(SqlConnection connexio, SqlTransaction transaccio, int amplada, int alcada)
    {
        const string sql = """
            INSERT INTO dbo.caselles (posicio_x, posicio_y, id_propietari, nivell, torn_actualitzacio)
            VALUES (@x, @y, NULL, 0, NULL);
            """;
        using var ordre = new SqlCommand(sql, connexio, transaccio);
        ordre.Parameters.Add("@x", SqlDbType.Int);
        ordre.Parameters.Add("@y", SqlDbType.Int);
        for (int y = 0; y < alcada; y++)
        {
            for (int x = 0; x < amplada; x++)
            {
                ordre.Parameters["@x"].Value = x;
                ordre.Parameters["@y"].Value = y;
                ordre.ExecuteNonQuery();
            }
        }
    }
}
