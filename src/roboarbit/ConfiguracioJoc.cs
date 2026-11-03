using Microsoft.Data.SqlClient;

namespace roboarbit;

internal sealed record ConfiguracioJoc(int Amplada, int Alcada, int DuradaTornMs, int MaxTorns,
    int CaducitatReforcada, int CaducitatNormal, int MaxMovimentsTorn, int PuntsNormal, int PuntsReforcada)
{
    public static ConfiguracioJoc Llegir(SqlConnection connexio, SqlTransaction? transaccio = null)
    {
        using var ordre = new SqlCommand("SELECT clau, valor FROM dbo.configuracio;", connexio, transaccio);
        using SqlDataReader lector = ordre.ExecuteReader();
        var valors = new Dictionary<string, int>(StringComparer.Ordinal);
        while (lector.Read())
            valors.Add(lector.GetString(0), lector.GetInt32(1));

        int Valor(string clau)
        {
            if (!valors.TryGetValue(clau, out int valor) || valor <= 0)
                throw new InvalidOperationException($"La configuració {clau} ha d'existir i tenir un valor positiu.");
            return valor;
        }

        return new(Valor("amplada_tauler"), Valor("alcada_tauler"), Valor("durada_torn_ms"),
            Valor("max_torns"), Valor("caducitat_reforcada"), Valor("caducitat_normal"),
            Valor("max_moviments_torn"), Valor("punts_normal"), Valor("punts_reforcada"));
    }
}
