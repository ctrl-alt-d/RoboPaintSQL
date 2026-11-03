# Proves de l'àrbitre

Les proves executen l'aplicació C# i consulten SQL Server per comprovar el resultat. Cal executar-les des de l'arrel del repositori, amb l'SDK .NET 10.

Les regles també es poden provar sense SQL Server ni variables de connexió:

```sh
dotnet run --project tests/roboarbit.proves -- --regles
```

Aquesta modalitat comprova l'entrada lliure al torn 1, la primera petició vàlida per identificador, la recepció, les entrades compartides, l'absència de pintura inicial i el rebuig d'entrades després del torn 1. També prova les quatre direccions, coordenades extremes, diagonals i salts, la participació, l'instant de tall manual i el límit d'ordres, els cinc casos de pintura, la caducitat en dos períodes, els robots quiets, les col·lisions i els intercanvis de posicions.

Inclou el recorregut notificat de T: només aquest jugador envia l'entrada, mentre O i L tenen posicions antigues. Comprova que només T s'incorpora al resultat, que pinta els passos següents sense col·lidir amb O o L i que continua participant quan no envia cap ordre en un torn posterior.

També comprova la representació d'un tauler rectangular, els tres nivells de pintura i les coordenades de robots que comparteixen casella, sense ocultar la pintura. Verifica coordenades als quatre marges i l'alineació dels nombres de dues xifres. Actualment passen 59 comprovacions de regles i 4 de representació sense SQL Server.

Les proves de l'estratègia del client es poden executar per separat, també sense SQL Server:

```sh
dotnet run --project tests/robot.proves
```

Defineix `ROBOPAINTSQL_CONNEXIO_PROVES` amb una connexió d'administrador a un servidor SQL Server de proves. La suite crea bases amb el prefix `RoboPaintSQL_proves_` i les elimina en acabar cada cas, fins i tot si falla. No utilitza la base indicada a la connexió com a partida.

Si el servidor ja té comptes `A`–`T`, defineix també `ROBOPAINTSQL_CREDENCIALS_PROVES` amb la ruta del CSV que conté les seves credencials. La suite en fa una còpia temporal, reutilitza els comptes i no els canvia les contrasenyes. Si encara no hi ha comptes, utilitza un servidor temporal descartable: les proves creen els inicis de sessió, que són globals al servidor, i el servidor s'ha d'eliminar en acabar.

```sh
dotnet run --project tests/roboarbit.proves
```

Es comproven els 20 jugadors actius sense posició, les cinc taules i totes les caselles lliures, la lectura de les dimensions configurades, les connexions reals dels 20 alumnes, el rol, la inserció identificada i el rebuig d'actualitzacions. També es proven la reinicialització amb comptes i credencials existents, la neteja de torns, moviments i pintura, la conservació del CSV i de la configuració, la represa des del torn 1 i el rebuig d'inicialitzar mentre s'executa l'àrbitre. Es comprova que els errors de credencials i els errors d'escriptura durant la reinicialització conservin la partida anterior. A més, es proven paràmetres absents o no positius, un tauler petit que permet compartir entrades, la substitució de dades parcials existents, CSV invàlids, credencials absents i la desfeta de la transacció davant d'un usuari vinculat incorrectament. En un servidor nou també es comprova la desfeta dels inicis de sessió i la conservació del CSV generat per reintentar. La suite comprova que la consola no contingui cap de les contrasenyes del CSV.

La suite completa executa primer les regles i la representació, i després la inicialització i els torns contra SQL Server. Les proves de torns utilitzen usuaris temporals sense inici de sessió i comproven les operacions amb els permisos de `rol_arbitre` i les insercions amb `rol_alumnes`. Proven el tancament manual, l'acceptació després de la data prevista, l'espera amb totes les ordres rebudes i l'aturada per final d'entrada, la selecció d'ordres, l'entrada lliure amb el primer INSERT vàlid del torn 1 sense pintar, la prohibició d'entrada tardana, els jugadors actius en resoldre, les col·lisions, el reforç, la desfeta d'una resolució cancel·lada, la represa i el rebuig de dues instàncies. També executen el bucle fins al màxim configurat i el menú amb entrada simulada, incloses la classificació amb jugadors sense punts i la representació del tauler després de tancar un torn amb l'opció 2. La partida local no s'utilitza.

Per comprovar la ruta per defecte del CSV, es copien l'aplicació i les credencials en una carpeta temporal amb l'estructura del projecte. S'executa l'opció 1 del menú des de l'arrel, des de `src/roboarbit` i des de la carpeta de l'executable, sense passar cap ruta de credencials. En tots tres casos ha d'inicialitzar amb el mateix CSV i conservar-ne el contingut. El fitxer local del professor no es modifica.

També es reconstrueix l'esquema antic en una base temporal per provar [l'adaptació de l'entrada lliure](../../sql/adaptar-entrada-torn-1.sql): permetre les coordenades nul·les, eliminar `participants_torn`, conservar la resta de dades i els permisos, poder repetir el script i impedir-lo mentre l'àrbitre manté el bloqueig.

Amb comptes existents, la versió anterior a la retirada de robots sense ordres havia superat 54 comprovacions de regles, 2 de representació i 109 SQL en bases temporals del contenidor local.

Amb la retirada de robots sense ordres i l'adaptació automàtica de l'esquema antic a `inicialitza-joc`, la compilació ha acabat sense errors i han passat 59 comprovacions de regles i 2 de representació. No s'han executat proves d'integració per indicació de l'usuari; la correcció de l'error SQL 515 no s'ha verificat contra SQL Server en aquesta revisió.
