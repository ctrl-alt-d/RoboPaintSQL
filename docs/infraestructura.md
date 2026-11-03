# Infraestructura

[Torna al README](../README.md)

## Programes i connexió

El sistema utilitza una base de dades compartida de **Microsoft SQL Server**. El llenguatge SQL dels exemples i del DDL és **T-SQL**.

- El programa C# de cada alumne consulta el joc, el representa i envia ordres. Es connecta amb ADO.NET i el proveïdor `Microsoft.Data.SqlClient`.
- Un únic programa C# d'arbitratge valida totes les regles del joc, obre els torns, resol els moviments i actualitza el tauler.
- SQL Server conserva les dades, les claus i els permisos, i fixa la identitat i l'hora dels moviments.

L'àrbitre de `src/roboarbit/` implementa la inicialització i el bucle de torns amb `Microsoft.Data.SqlClient`, amb menú de consola i ordres directes. Espera Retorn per resoldre cada torn i mostra el tauler amb colors amb Spectre.Console, coordenades als quatre marges i les posicions dels robots després de confirmar-lo. El client de `src/robot/` és una [plantilla funcional](../src/robot/README.md) amb moviments aleatoris i estat en arrays perquè els alumnes en millorin l'estratègia. Els exemples de la [guia d'ADO.NET](consultes-i-actualitzacions-csharp.md) expliquen les operacions bàsiques.

El professor proporcionarà el servidor, la base de dades i les credencials. Hi ha 20 comptes, amb les lletres majúscules ASCII de la `A` a la `T`, sense `Ç`, `Ñ` ni altres caràcters. L'inici de sessió i l'usuari de base de dades tenen la mateixa lletra, que també correspon a `jugadors.id_jugador`. La identificació del joc es fa amb `USER_NAME()`, que retorna l'usuari de la base de dades.

## Instal·lació

El fitxer [sql/ddl-tsql.sql](../sql/ddl-tsql.sql) s'executa una sola vegada sobre una base de dades buida, amb un compte administrador i una eina que interpreti els separadors `GO`, com SQL Server Management Studio. No crea el servidor, la base de dades, els inicis de sessió ni les contrasenyes.

El DDL crea cinc taules dins de l'esquema `dbo`, les claus primàries i foranes, els valors inicials de configuració, dos rols i un disparador d'identificació. Encara no crea jugadors, caselles ni torns: els prepararà l'àrbitre. No conté restriccions `CHECK` ni índexs addicionals als de les claus primàries. La configuració, els estats, les coordenades, la pintura i la resta de regles els valida el programa C# de l'àrbitre.

Després s'executa `dotnet run --project src/roboarbit -- inicialitza-joc` des de l'arrel del repositori, amb l'SDK .NET 10 i `ROBOPAINTSQL_CONNEXIO` definida amb una connexió d'administrador a la mateixa base. L'àrbitre crea els inicis de sessió `A`, `B`, …, `T`, els usuaris corresponents i els afegeix a `rol_alumnes`. El rang inclou només aquestes 20 lletres ASCII. També crea els 20 jugadors actius i totes les caselles, segons les dimensions llegides de `configuracio`.

Les credencials es llegeixen de `src/roboarbit/.credencials-alumnes.csv`, exclòs de Git. Aquesta ruta es localitza a partir de l'executable i no depèn de la carpeta de treball; executar el menú des de `src/roboarbit` utilitza el mateix fitxer. En una aplicació publicada sense el projecte, el CSV es busca al costat de l'executable. Si no hi ha credencial per a un compte nou, C# genera una contrasenya aleatòria individual i la desa al CSV abans de crear-lo. Els comptes existents es reutilitzen sense canviar-ne les contrasenyes; si falta alguna de les seves credencials, l'operació s'atura i indica la ruta completa perquè el professor recuperi el fitxer original. No es mostren contrasenyes a la consola. Es pot indicar una altra ruta com a segon argument de l'operació.

La inicialització substitueix les dades de joc existents per una partida nova i confirma comptes, jugadors i caselles en una sola transacció. Conserva la configuració i reutilitza els comptes i les credencials. Si hi ha un error, restaura la partida anterior. No obre el primer torn i no es pot executar mentre el bucle de l'àrbitre estigui en marxa. El compte per arbitrar els torns pertany a `rol_arbitre` i es prepara per separat; aquest rol no permet crear comptes SQL. Per exemple, si ja existeix l'inici de sessió corresponent:

```sql
CREATE USER [arbitre] FOR LOGIN [compte_arbitre];
ALTER ROLE rol_arbitre ADD MEMBER [arbitre];
```

Els comptes d'alumnes només reben el rol indicat, sense permisos d'administració, modificació d'esquema o suplantació d'altres usuaris. L'àrbitre utilitza un compte diferent i no pertany a `rol_alumnes`.

## Bases existents

Si la base es va crear amb posicions obligatòries i `participants_torn`, **`inicialitza-joc` adapta l'esquema automàticament** abans de preparar la partida nova: permet coordenades `NULL` i elimina la taula antiga. L'adaptació i la reinicialització comparteixen transacció i bloqueig; si hi ha un error, es restaura també l'esquema anterior. Cal utilitzar una connexió d'administrador i tenir l'àrbitre aturat. Aquesta operació substitueix la partida, però conserva la configuració, els comptes i les credencials.

L'error SQL 515 sobre `jugadors.posicio_x` o `posicio_y` indica que la base conserva les columnes obligatòries de la versió antiga. Amb el codi actualitzat, torna a executar l'opció d'inicialitzar; no cal esborrar manualment les taules ni executar el DDL de creació sobre la base existent.

Si vols adaptar només l'esquema i conservar la partida, pots executar [sql/adaptar-entrada-torn-1.sql](../sql/adaptar-entrada-torn-1.sql) com a administrador amb l'àrbitre aturat. Conserva comptes, credencials, configuració i dades de la partida, excepte l'antiga llista de participants. És transaccional i es pot repetir. En reprendre, l'àrbitre retira les posicions antigues dels usuaris sense cap petició, però conserva les dels que ja han jugat. No recalcula els torns tancats.

## Taules

Els noms de taules, columnes, estats i paràmetres són en català. S'escriuen sense accents als identificadors per facilitar-ne l'ús al codi. Les coordenades s'emmagatzemen com a enters; l'àrbitre comprova que siguin dins del tauler. Les dates són `datetime2(3)` en UTC; en SQL Server, `timestamp` no és un tipus de data.

### `jugadors`

| Columna | Tipus | Significat |
|---|---|---|
| `id_jugador` | `char(1)`, clau primària | Lletra majúscula ASCII entre `A` i `T`; nom de l'usuari de base de dades. |
| `posicio_x` | `int`, admet `NULL` | Columna actual; `NULL` abans d'entrar al tauler. |
| `posicio_y` | `int`, admet `NULL` | Fila actual; `NULL` abans d'entrar al tauler. |
| `actiu` | `bit` | `1` si pot intervenir quan es resol el torn; `0` si està inactiu. |

Les dues coordenades comencen a `NULL`. L'àrbitre les fixa amb el primer `INSERT` vàlid del torn 1, sense pintar. Qui no entra al torn 1 continua fora tota la partida. L'activitat es consulta en resoldre; es recomana canviar-la entre torns. Desactivar un jugador no esborra la seva posició, pintura ni punts, però l'exclou dels moviments i les col·lisions.

### `torns`

| Columna | Tipus | Significat |
|---|---|---|
| `id_torn` | `int`, clau primària | Número correlatiu des d'1. |
| `estat` | `varchar(20)` | `OBERT`, `EN_ARBITRATGE` o `TANCAT`. |
| `data_inici` | `datetime2(3)` | Obertura segons el rellotge del servidor. |
| `data_limit` | `datetime2(3)` | Data prevista informativa; no limita les ordres amb el control manual. |
| `data_fi` | `datetime2(3)`, admet `NULL` | Final de la resolució; només té valor en estat `TANCAT`. |

L'àrbitre garanteix que només hi hagi un torn obert i que no se n'obri cap mentre l'anterior s'està resolent. També valida els estats i la coherència de les dates.

### `caselles`

| Columna | Tipus | Significat |
|---|---|---|
| `posicio_x` | `int`, clau primària composta | Columna. |
| `posicio_y` | `int`, clau primària composta | Fila. |
| `id_propietari` | `char(1)`, clau forana, admet `NULL` | Jugador propietari. |
| `nivell` | `tinyint` | `0`: lliure, `1`: normal, `2`: reforçada. |
| `torn_actualitzacio` | `int`, clau forana, admet `NULL` | Torn en què comença el període de caducitat del nivell actual. |

La clau primària és `(posicio_x, posicio_y)`. Una casella lliure té propietari i torn d'actualització nuls. Una casella pintada té tots dos valors informats. La degradació de reforçada a normal també actualitza `torn_actualitzacio`.

### `moviments`

| Columna | Tipus | Significat |
|---|---|---|
| `id_moviment` | `bigint IDENTITY`, clau primària | Número automàtic de l'ordre. |
| `id_torn` | `int`, clau forana | Torn indicat a l'ordre. |
| `id_jugador` | `char(1)`, clau forana | Usuari identificat pel servidor. |
| `nova_x` | `int` | Columna sol·licitada. |
| `nova_y` | `int` | Fila sol·licitada. |
| `data_moviment` | `datetime2(3)` | Hora UTC de recepció al servidor. |

Les claus foranes referencien `torns` i `jugadors` per separat. La participació en el torn la comprova l'àrbitre, no una clau forana composta.

El disparador `identificar_moviment` obté `USER_NAME()` i l'hora UTC, ignorant qualsevol usuari o data que intenti proporcionar el client. Comprova que l'usuari tingui un únic caràcter amb codi ASCII del 65 al 84: de la `A` a la `T`, exactament, sense truncaments ni variants accentuades. No valida cap regla del joc i permet inserir diverses files. El client només necessita enviar el torn i la destinació.

Una fila desada és una petició pendent de validació. S'hi poden desar ordres fora del període de recepció, destinacions invàlides o més ordres que les permeses; l'àrbitre les ignorarà. Les claus foranes continuen exigint que el torn i el jugador existeixin.

### `configuracio`

| Columna | Tipus | Significat |
|---|---|---|
| `clau` | `varchar(50)`, clau primària | Nom del paràmetre. |
| `valor` | `int` | Valor configurat; l'àrbitre en comprova la validesa. |

| Clau | Valor inicial |
|---|---:|
| `amplada_tauler` | 30 |
| `alcada_tauler` | 20 |
| `durada_torn_ms` | 3000 |
| `max_torns` | 200 |
| `caducitat_reforcada` | 20 |
| `caducitat_normal` | 20 |
| `max_moviments_torn` | 20 |
| `punts_normal` | 1 |
| `punts_reforcada` | 2 |

`durada_torn_ms` es conserva per compatibilitat i per calcular `data_limit`, però ara no fa avançar els torns ni descarta ordres.

La configuració es prepara abans de començar i es manté fixa durant tota la partida. La base de dades representa una única partida; no s'hi barregen historials de partides diferents. Per repetir-la, el professor pot tornar a executar `inicialitza-joc` a la mateixa base: esborra l'historial anterior i conserva la configuració, els comptes i les credencials. També pot preparar una base de dades nova amb el mateix DDL.

## Permisos

| Taula | `rol_alumnes` | `rol_arbitre` |
|---|---|---|
| `jugadors` | `SELECT` | `SELECT`, `INSERT`, `UPDATE` |
| `caselles` | `SELECT` | `SELECT`, `INSERT`, `UPDATE` |
| `torns` | `SELECT` | `SELECT`, `INSERT`, `UPDATE` |
| `moviments` | `SELECT`, `INSERT` amb identificació automàtica | `SELECT` |
| `configuracio` | `SELECT` | `SELECT`, `UPDATE` |

Només es concedeixen els permisos de la taula anterior. Per tant, els alumnes no tenen permís per fer `UPDATE` ni `DELETE`, ni per inserir en altres taules. Tampoc poden crear ni alterar objectes. El disparador comparteix propietari amb les taules i s'executa en el context de qui envia l'ordre, sense suplantar l'administrador. Els comptes no han de pertànyer a altres rols que els donin més permisos.

## Verificació del DDL

Abans de la nova entrada lliure, el DDL simplificat havia superat 39 comprovacions al contenidor `azuresqledge`, amb Microsoft Azure SQL Edge, motor versió `15.0.2000.1574`. Les proves comproven la creació de les taules, les claus, els permisos i la identificació automàtica. També confirmen que SQL Server desa les peticions sense validar les regles del joc, incloses 25 insercions simultànies. S'han fet en una base temporal separada, eliminada en acabar, sense modificar `src/`.

L'antic script de creació dels comptes havia superat 46 comprovacions addicionals: pertinença al rol, execució repetida sense canviar contrasenyes, connexió real dels 20 alumnes, lectura autoritzada, actualitzacions no autoritzades i identitat dels moviments. S'havia comprovat també que el disparador rebutja `U`, `Z`, `Ç` i `Ñ`. Ara la creació dels comptes correspon a C#. Els comptes de la base local `RoboPaintSQL` es conserven; les dades de prova d'aquelles comprovacions es van crear en una altra base temporal i es van eliminar en acabar.

La validació de l'instant de tall manual, límits d'ordres, moviments, pintura, caducitat i col·lisions està implementada en C#. Les proves de regles i de torns són a la mateixa suite de l'àrbitre; no s'atribueixen al DDL.

La inicialització C# ha superat 62 comprovacions amb els comptes existents en bases temporals del contenidor local i 65 en un servidor temporal nou, eliminat en acabar. S'han comprovat la creació de jugadors i caselles, les dimensions configurables, les connexions dels 20 alumnes, els permisos, el rebuig de dades o configuracions invàlides i la desfeta de comptes i dades davant d'errors. Les proves són a [tests/roboarbit.proves](../tests/roboarbit.proves/README.md). La base local `RoboPaintSQL` no s'ha reinicialitzat.

Abans de la nova entrada lliure, amb la reinicialització i la correcció de la ruta del CSV implementades, la suite havia superat 103 comprovacions SQL amb comptes existents en bases temporals, a més de 44 de regles i 2 de representació. Inclou substituir una partida amb dades, conservar comptes, credencials i configuració, rebutjar la inicialització mentre s'executa l'àrbitre i restaurar la partida anterior davant d'un error d'escriptura. També s'ha provat l'opció 1 del menú des de l'arrel, `src/roboarbit` i la carpeta de l'executable amb una còpia temporal de l'aplicació i del CSV. La base local `RoboPaintSQL` i les contrasenyes locals s'han conservat.

La suite anterior amb el menú i els torns havia superat 44 comprovacions de regles i 2 de representació sense SQL Server, i 90 comprovacions SQL en bases temporals amb comptes existents. Comprova també la selecció d'ordres, control manual amb Retorn, jugadors actius, col·lisions, reforç, cancel·lació amb desfeta, represa, exclusió de dues instàncies, límit de torns i menú. Les bases temporals s'han eliminat en acabar.

Amb l'entrada lliure al torn 1 i l'eliminació de `participants_torn`, la suite ha superat **54 comprovacions de regles, 2 de representació i 109 SQL** amb els comptes existents al contenidor local. Verifica la primera entrada vàlida sense pintar, les coordenades nul·les, el rebuig d'entrades tardanes, els moviments habituals des del torn 2 i l'activitat consultada en resoldre. També comprova l'adaptació d'una base amb l'esquema antic: eliminació de la taula, conservació de la partida i els permisos, repetició del script i rebuig mentre l'àrbitre té el bloqueig. Les proves s'han fet en bases temporals eliminades en acabar, sense adaptar ni reinicialitzar `RoboPaintSQL` ni canviar contrasenyes locals.

## Referències tècniques

La identificació del compte segueix el comportament de [USER_NAME](https://learn.microsoft.com/en-us/sql/t-sql/functions/user-name-transact-sql). L'assignació de l'emissor i de l'hora utilitza un [disparador INSTEAD OF](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-trigger-transact-sql).
