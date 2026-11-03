# Consultes i actualitzacions des de C#

[Torna al README](../README.md)

## ADO.NET i SQL Server

ADO.NET permet executar SQL des de C#. En aquesta pràctica farem servir el paquet NuGet `Microsoft.Data.SqlClient`, ja inclòs als projectes `robot` i `roboarbit`. En un projecte nou s'afegeix amb:

```sh
dotnet add package Microsoft.Data.SqlClient
```

Els exemples d'aquest document mostren les operacions bàsiques. La [plantilla del robot](../src/robot/README.md) les integra en un programa amb moviments aleatoris i estat en arrays. Els fragments següents utilitzen aquests espais de noms:

```csharp
using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
```

## Obrir una connexió

La cadena de connexió conté el servidor, la base de dades i les credencials que facilitarà el professor. Es pot proporcionar amb la variable d'entorn `ROBOPAINTSQL_CONNEXIO`, sense desar contrasenyes al repositori.

Exemple de format, amb valors que cal substituir:

```text
Server=servidor,1433;Database=RoboPaintSQL;User ID=A;Password=CONTRASENYA;Encrypt=True;TrustServerCertificate=False;
```

El professor assignarà un dels 20 comptes ASCII de la `A` a la `T`, amb la seva contrasenya individual. L'inici de sessió i l'usuari de base de dades tenen la mateixa lletra. El professor indicarà la configuració de certificats de l'entorn de classe. Les opcions de xifrat i validació es descriuen a la [documentació de connexions de Microsoft](https://learn.microsoft.com/en-us/sql/connect/ado-net/connection-string-syntax).

```csharp
string cadenaConnexio = Environment.GetEnvironmentVariable("ROBOPAINTSQL_CONNEXIO")
    ?? throw new InvalidOperationException("Falta ROBOPAINTSQL_CONNEXIO.");

using var connexio = new SqlConnection(cadenaConnexio);
await connexio.OpenAsync();
```

`using` allibera la connexió encara que es produeixi un error. Els exemples següents pressuposen que `connexio` està oberta.

## Llegir diverses files: `ExecuteReaderAsync`

Per consultar les posicions dels robots que ja han entrat al tauler (les coordenades `NULL` indiquen que encara són fora):

```csharp
using (var ordre = new SqlCommand("""
    SELECT id_jugador, posicio_x, posicio_y
    FROM dbo.jugadors
    WHERE posicio_x IS NOT NULL AND posicio_y IS NOT NULL
    ORDER BY id_jugador;
    """, connexio))
using (var lector = await ordre.ExecuteReaderAsync())
{
    while (await lector.ReadAsync())
    {
        string jugador = lector.GetString(0);
        int x = lector.GetInt32(1);
        int y = lector.GetInt32(2);
        Console.WriteLine($"{jugador}: ({x}, {y})");
    }
}
```

Les posicions de les columnes comencen a zero. El tipus de lectura ha de correspondre al tipus SQL: `int` amb `GetInt32`, `bigint` amb `GetInt64`, `tinyint` amb `GetByte` i `bit` amb `GetBoolean`. Si una columna admet `NULL`, primer cal comprovar `lector.IsDBNull(...)`. El lector s'ha de tancar abans d'executar una altra ordre sobre la mateixa connexió.

## Llegir un únic valor: `ExecuteScalarAsync`

Per obtenir el torn obert:

```csharp
int? tornObert;
using (var ordre = new SqlCommand("""
    SELECT id_torn
    FROM dbo.torns
    WHERE estat = 'OBERT';
    """, connexio))
{
    object? resultat = await ordre.ExecuteScalarAsync();
    tornObert = resultat is null or DBNull ? null : Convert.ToInt32(resultat);
}
```

Pot no haver-hi cap torn obert. El resultat també pot quedar antic immediatament després de la consulta: l'àrbitre comprovarà si la petició arriba a temps quan resolgui el torn. Per consultar la posició pròpia es pot fer servir `WHERE id_jugador = USER_NAME()`.

## Escriure amb paràmetres: `ExecuteNonQueryAsync`

El text SQL i els valors s'envien per separat. Els [paràmetres d'ADO.NET](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/configuring-parameters-and-parameter-data-types) tenen nom i tipus; no es construeix SQL concatenant entrades de l'usuari.

Aquest exemple és una funció que rep una destinació ja calculada:

```csharp
static async Task EnviarMovimentAsync(
    SqlConnection connexio, int idTorn, int novaX, int novaY)
{
    using var ordre = new SqlCommand("""
        INSERT INTO dbo.moviments (id_torn, nova_x, nova_y)
        VALUES (@id_torn, @nova_x, @nova_y);
        """, connexio);

    ordre.Parameters.Add("@id_torn", SqlDbType.Int).Value = idTorn;
    ordre.Parameters.Add("@nova_x", SqlDbType.Int).Value = novaX;
    ordre.Parameters.Add("@nova_y", SqlDbType.Int).Value = novaY;

    await ordre.ExecuteNonQueryAsync();
}
```

No s'envia `id_jugador`, `id_moviment` ni `data_moviment`: els determina el servidor. El disparador només identifica l'emissor i fixa l'hora; també permet insercions de diverses files, tot i que aquí enviem una petició per crida. Com que utilitza `SET NOCOUNT ON`, no s'ha d'exigir que el retorn d'`ExecuteNonQueryAsync` sigui 1.

Si no es produeix cap excepció, la petició s'ha desat. Això no garanteix que sigui vàlida: l'àrbitre comprovarà les regles del joc i ignorarà les peticions invàlides o rebudes a partir del tall manual. Cal consultar el torn tancat per veure el resultat del moviment.

## Fer un `UPDATE`: només amb el compte autoritzat

`ExecuteNonQueryAsync` també serveix per a un `UPDATE`. Per exemple, entre torns, el compte de l'àrbitre pot canviar l'activitat d'un jugador:

```csharp
using (var ordre = new SqlCommand("""
    UPDATE dbo.jugadors
    SET actiu = @actiu
    WHERE id_jugador = @id_jugador;
    """, connexio))
{
    ordre.Parameters.Add("@actiu", SqlDbType.Bit).Value = false;
    ordre.Parameters.Add("@id_jugador", SqlDbType.Char, 1).Value = "A";

    int filesAfectades = await ordre.ExecuteNonQueryAsync();
    Console.WriteLine($"Jugadors actualitzats: {filesAfectades}");
}
```

Aquest exemple requereix una connexió de l'àrbitre. Amb les credencials d'un alumne, SQL Server el rebutjarà. El robot d'un alumne es mou amb `INSERT INTO moviments`, mai amb `UPDATE jugadors`.

## Gestionar els errors

El torn es pot tancar entre la lectura i la inserció. Això és una situació prevista, no una raó per intentar modificar el tauler directament.

```csharp
try
{
    // idTorn, novaX i novaY provenen de la consulta i de la decisió del robot.
    await EnviarMovimentAsync(connexio, idTorn, novaX, novaY);
}
catch (SqlException error)
{
    Console.WriteLine($"Ordre no confirmada ({error.Number}): {error.Message}");
}
```

Aquest fragment s'integra en el programa amb les variables indicades. L'error 50001 del disparador indica que l'usuari de base de dades no té el format d'identificació del joc. També poden aparèixer errors de connexió, de permisos, de tipus de dades o de claus foranes, per exemple si el torn indicat no existeix.

Les peticions invàlides segons les regles del joc no provoquen una excepció SQL: l'àrbitre les ignora. Al torn 1, la primera petició vàlida tria qualsevol casella dins del tauler, sense pintar ni moure's més en aquell torn. Des del torn 2, el robot ha de tenir posició i calcular una destinació adjacent dins del tauler. Qui no entra al torn 1 no pot entrar més tard.

Si es perd la connexió després d'enviar una petició, no s'ha de suposar que ha fallat: pot haver quedat desada. Es poden consultar els moviments propis del torn abans de reintentar, per evitar duplicar peticions innecessàriament.

## Bucle de treball del robot

1. Consultar la configuració i identificar el jugador propi.
2. Detectar un torn nou i comprovar que el jugador és actiu a `jugadors`.
3. Al torn 1, triar una casella d'entrada dins del tauler. Des del torn 2, comprovar que es té posició, llegir el tauler i triar una destinació adjacent.
4. Inserir una ordre. Si no es vol moure, no enviar-ne cap.
5. Esperar que el torn es tanqui i refrescar el tauler i la classificació.

Per començar, es pot consultar l'estat cada 100–200 ms amb `await Task.Delay(...)` entre lectures. No s'ha de fer un bucle sense pausa ni mantenir una transacció oberta durant l'espera. Aquest interval és orientatiu i es pot ajustar amb el professor segons la càrrega del servidor.
