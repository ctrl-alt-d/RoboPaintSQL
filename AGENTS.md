# Context del projecte

RoboPaintSQL és una pràctica de bases de dades per a alumnes de cicles formatius de grau superior d'informàtica, amb una durada orientativa de 4 hores. Cada alumne controla un robot amb C# que competeix per pintar territori en un tauler compartit a SQL Server.

L'objectiu didàctic és integrar consultes i insercions SQL en C# amb ADO.NET. Els alumnes han de consultar el món, representar el tauler, enviar moviments i calcular la classificació. Més endavant poden automatitzar l'estratègia.

## Idioma i criteris de treball

- Escriu en català la documentació, els comentaris i els identificadors propis nous: taules, columnes, variables, etc. Als identificadors, utilitza noms sense accents.
- Prioritza solucions senzilles i fàcils d'explicar als alumnes.
- Davant d'una ambigüitat menor, tria l'opció més senzilla i documenta-la. Consulta l'usuari quan la decisió canviï substancialment la pràctica.
- Mantén coherents el DDL, els exemples SQL i els documents quan canviï una decisió.
- L'àrbitre C# implementa la inicialització i el bucle de torns. No ampliïs altres funcionalitats ni el client d'alumne com a part de canvis només documentals o SQL.

## On és cada cosa

| Ruta | Contingut |
|---|---|
| [README.md](README.md) | Entrada del projecte, objectius didàctics i enllaços als cinc documents. |
| [docs/regles-del-joc.md](docs/regles-del-joc.md) | Objectiu, tauler, moviments, pintura, caducitat, col·lisions, torns i puntuació. |
| [docs/infraestructura.md](docs/infraestructura.md) | Arquitectura, instal·lació, diccionari de taules, comptes, permisos i proves fetes. |
| [docs/regles-del-joc-i-infraestructura.md](docs/regles-del-joc-i-infraestructura.md) | Correspondència entre les regles i les dades; consistència i simultaneïtat. |
| [docs/arbitre.md](docs/arbitre.md) | Menú, inicialització, bucle de torns i regles implementades en C#. |
| [docs/consultes-i-actualitzacions-csharp.md](docs/consultes-i-actualitzacions-csharp.md) | Exemples d'ADO.NET amb `Microsoft.Data.SqlClient`, paràmetres, lectures, insercions i actualitzacions autoritzades. |
| [sql/ddl-tsql.sql](sql/ddl-tsql.sql) | Esquema T-SQL: cinc taules, claus, configuració inicial, rols, permisos i identificació dels moviments. |
| [sql/adaptar-entrada-torn-1.sql](sql/adaptar-entrada-torn-1.sql) | Adaptació de bases existents: coordenades nul·les i eliminació de `participants_torn`; conserva la resta de dades. |
| `src/robopaintsql.slnx` | Solució .NET; inclou `robot`, `roboarbit` i les proves de l'àrbitre. |
| `src/robot/` | Plantilla funcional de l'alumne: ADO.NET, estat en arrays i estratègia aleatòria substituïble. Instruccions a `src/robot/README.md`. |
| `src/roboarbit/` | Àrbitre C#: menú, `inicialitza-joc` i `comenca-joc` amb ADO.NET. |
| `src/roboshare/` | Esquelet de biblioteca per a codi compartit, referenciada per `robot` i `roboarbit`. |
| `tests/roboarbit.proves/` | Proves de regles en memòria i d'integració de la inicialització i els torns en bases temporals SQL Server. |
| `tests/robot.proves/` | Proves de l'estratègia aleatòria en memòria, sense SQL Server. |
| `draft_spec.md` | Esborrany inicial, amb noms i criteris antics. Conserva'l fins que l'usuari demani eliminar-lo. |
| `src/roboarbit/.credencials-alumnes.csv` | Credencials locals que llegeix o genera l'àrbitre, excloses de Git. No en copiïs el contingut a documentació, codi ni sortides de les eines. |

La documentació actual i el DDL defineixen els criteris adoptats; l'esborrany no és la font de referència. Els tres projectes C# utilitzen `net10.0`. L'àrbitre i el robot utilitzen `Microsoft.Data.SqlClient`; `roboshare` continua sent un esquelet. Després de confirmar cada torn, l'àrbitre mostra el tauler amb colors amb Spectre.Console, coordenades als quatre marges i les posicions de tots els robots. El robot mostra el tauler en text, carrega caselles en arrays `[x, y]` i jugadors en arrays amb índexs 0–19 (A–T), i envia una petició aleatòria per torn. `Estrategia.TriarDestinacio` és el punt d'extensió per als alumnes.

## Decisió principal: regles de negoci en C#

**Totes les regles del joc es validaran al programa C# de l'àrbitre. Mantén el DDL al mínim.**

SQL Server només conserva l'estructura, els tipus, la nul·labilitat, les claus primàries i foranes, els permisos i l'atribució fiable dels moviments. No hi afegeixis restriccions `CHECK`, índexs únics de negoci ni disparadors per validar les regles del joc.

El disparador `identificar_moviment` és una excepció d'infraestructura: obté `USER_NAME()`, exigeix una lletra ASCII majúscula de `A` a `T` i fixa l'emissor i l'hora UTC. Ignora qualsevol identitat o data enviada pel client. Permet inserir diverses files.

Un `INSERT` correcte només confirma que la petició s'ha desat. SQL pot desar coordenades invàlides, peticions d'un jugador no participant, peticions fora del període de recepció o més ordres de les permeses. L'àrbitre les llegirà i ignorarà les que no corresponguin. Les claus foranes exigeixen que existeixin el jugador i el torn, però no validen la participació.

## Model i permisos

Les cinc taules són `jugadors`, `caselles`, `torns`, `moviments` i `configuracio`, dins de l'esquema `dbo`.

- Els alumnes, amb `rol_alumnes`, poden consultar les cinc taules i inserir a `moviments`. No poden actualitzar ni esborrar dades, inserir a altres taules o alterar l'esquema.
- La inicialització de l'àrbitre utilitza una connexió d'administrador per crear comptes, jugadors i caselles. Durant els torns, amb `rol_arbitre`, prepararà torns i actualitzarà posicions i pintura. Consulta els moviments; no els modifica.
- La puntuació es calcula a partir de les caselles. No hi ha un comptador de punts desat a `jugadors`.
- Els camps `id_moviment`, `id_jugador` i `data_moviment` de les peticions els determina SQL Server. El client envia `id_torn`, `nova_x` i `nova_y`.
- Les dates utilitzen `datetime2(3)` en UTC. No utilitzis `timestamp` com a tipus de data de SQL Server.

## Decisions del joc que cal preservar

- Coordenades des de `(0, 0)`, amb `x` cap a la dreta i `y` cap avall. Tauler amb límits, sense connexió entre vores.
- Al torn 1, entrada amb la primera petició vàlida, sense pintar; des del torn 2, un pas cardinal per torn, calculat respecte de la posició inicial del torn. Quedar-se quiet no pinta ni renova pintura.
- Des del torn 2, l'àrbitre té en compte les primeres `max_moviments_torn` peticions vàlides de cada jugador i tria l'última d'aquestes per `id_moviment`. Les invàlides no consumeixen el límit; les vàlides sobrants s'ignoren.
- Per ara el torn només acaba quan la persona que executa l'àrbitre prem Retorn; ni el temps ni tenir totes les ordres el tanquen automàticament. Els jugadors actius es consulten en resoldre; no hi ha `participants_torn`.
- La caducitat s'aplica abans dels moviments. Quan una reforçada passa a normal, comença un nou període de caducitat normal.
- Les col·lisions es calculen per destinació, incloent-hi robots quiets, i netegen la casella. Intercanviar posicions no és col·lidir.
- El conjunt de peticions es captura en una transacció de resolució amb aïllament `SERIALIZABLE`. L'àrbitre aplica l'instant de tall manual en C#; `data_limit` és informativa i no descarta ordres; les peticions desades després no alteren un torn resolt.
- La configuració es manté fixa durant la partida. Una base de dades representa una sola partida.

Els detalls i els valors inicials són als documents i a `configuracio`; evita duplicar-los al codi sense llegir aquesta taula.

## Preparació SQL i credencials

1. Prepara una base de dades buida de SQL Server.
2. Executa `sql/ddl-tsql.sql` amb un administrador i una eina que interpreti `GO`. És una creació inicial, no una migració ni un script per repetir sobre taules existents.
3. Defineix `ROBOPAINTSQL_CONNEXIO` amb una connexió d'administrador a la mateixa base i executa, des de l'arrel, `dotnet run --project src/roboarbit -- inicialitza-joc`. Crea els inicis de sessió i usuaris exactament `A`–`T` que faltin, reutilitza els existents i prepara els 20 jugadors actius i totes les caselles lliures. No obre torns. Si ja hi ha dades de joc, les substitueix per una partida nova i conserva la configuració, els comptes i les credencials. La neteja i la creació comparteixen transacció; un error restaura la partida anterior. El mateix bloqueig que el bucle impedeix reinicialitzar mentre s'està jugant.
4. Conserva `src/roboarbit/.credencials-alumnes.csv` només en local i fora de Git. La ruta per defecte es localitza des de l'executable, trobant la carpeta de `roboarbit.csproj`, i no depèn de la carpeta de treball. Per tant, `dotnet run` des de `src/roboarbit` utilitza el mateix CSV. En una aplicació publicada sense el projecte, el CSV es busca al costat de l'executable. Es pot indicar una altra ruta com a segon argument; les rutes explícites relatives es resolen respecte de la carpeta de treball. Els comptes existents es reutilitzen sense canviar contrasenyes; si en falta la credencial, l'operació s'atura i indica la ruta completa consultada. Les credencials noves es desen abans de crear comptes, per permetre reintents sense perdre-les.
5. El compte per arbitrar torns es prepara per separat i s'afegeix a `rol_arbitre`; aquest rol no permet crear comptes SQL. Els inicis de sessió pertanyen al servidor: altres bases de partida al mateix servidor reutilitzen els comptes i el CSV.
6. Sense arguments, `dotnet run --project src/roboarbit` mostra el menú d'inicialitzar, començar o reprendre, i sortir. `comenca-joc` executa directament el bucle. Llegeix `durada_torn_ms` i la resta de configuració una vegada, reprèn el torn obert, espera Retorn per resoldre cada torn i s'atura a `max_torns`. `Ctrl+C` permet interrompre'l sense perdre els torns confirmats. Un bloqueig de sessió impedeix dues instàncies d'àrbitre a la mateixa base.

Els robots comencen amb les dues coordenades a `NULL`. El primer INSERT vàlid de cada jugador actiu al torn 1 tria qualsevol casella dins del tauler, sense pintar. Les peticions següents del torn 1 s'ignoren. Sense entrada al torn 1, el robot queda fora tota la partida. Des del torn 2 s'aplica el pas cardinal habitual. Les entrades poden compartir casella.

No desis contrasenyes al repositori, inclosa la de l'administrador. Els exemples C# utilitzen la variable d'entorn `ROBOPAINTSQL_CONNEXIO`.

## Entorn local i verificació

L'entorn utilitzat fins ara és el contenidor Docker `azuresqledge`, amb la imatge `mcr.microsoft.com/azure-sql-edge`, el port local `1433` i el motor `15.0.2000.1574`. La base local `RoboPaintSQL` s'ha preparat amb l'esquema anterior i els 20 comptes; no s'hi ha aplicat l'adaptació de l'entrada lliure. Comprova l'estat real del contenidor abans d'utilitzar-lo.

S'havien superat 39 comprovacions del DDL simplificat i 46 comprovacions de l'antic script de comptes, incloses connexions reals dels alumnes i intents de falsificar la identitat. Les dades de prova es van crear en bases temporals separades i aquestes es van eliminar. L'antic script de comptes s'ha substituït per la inicialització C#; aquelles comprovacions no acrediten la nova implementació.

La inicialització C# havia superat 62 comprovacions amb comptes existents en bases temporals del contenidor local i 65 amb comptes nous en un servidor temporal, eliminat en acabar. Amb el menú, el control manual amb Retorn i la representació del tauler amb Spectre.Console implementats, la suite ha superat 44 comprovacions de regles i 2 de representació sense SQL Server, i 90 comprovacions SQL d'inicialització i torns amb comptes existents. La suite està versionada a `tests/roboarbit.proves/`; el seu README explica les variables d'entorn de proves i l'opció `--regles`. No s'ha reinicialitzat `RoboPaintSQL` ni s'han canviat les contrasenyes locals.

Amb la reinicialització i la correcció de la ruta del CSV implementades, la suite ha superat 103 comprovacions SQL amb comptes existents, a més de les 44 de regles i les 2 de representació. Comprova també la substitució d'una partida amb dades, la conservació de comptes, credencials i configuració, el bloqueig mentre s'executa l'àrbitre i la restauració de la partida anterior davant d'un error d'escriptura. El menú s'ha provat des de l'arrel, `src/roboarbit` i la carpeta de l'executable amb una còpia temporal de l'aplicació i del CSV. Només s'han utilitzat bases temporals; `RoboPaintSQL` i les contrasenyes locals es conserven.

Quan canviïs SQL, prova'l en una base temporal sense esborrar ni reinicialitzar la base local de l'usuari. Quan només canviïs documentació, comprova els enllaços, els noms i la coherència amb els scripts. No atribueixis al DDL proves de regles que s'hauran d'implementar i verificar en C#.

Amb l'entrada lliure al torn 1 i l'eliminació de `participants_torn`, la suite ha superat 54 comprovacions de regles, 2 de representació i 109 SQL amb comptes existents. Inclou primera entrada vàlida sense pintar, robots sense posició, rebuig d'entrades tardanes, moviments des del torn 2 i jugadors actius en resoldre. També prova `sql/adaptar-entrada-torn-1.sql` sobre l'esquema antic en una base temporal: conservació de dades i permisos, coordenades nul·les, eliminació de la taula, repetició i bloqueig mentre s'arbitra. La base local i les contrasenyes s'han conservat. Per començar una partida nova en una base antiga, `inicialitza-joc` adapta l'esquema automàticament dins de la mateixa transacció: permet coordenades nul·les i elimina `participants_torn` si existeix. Cal aturar l'àrbitre i utilitzar una connexió d'administrador. El script separat permet adaptar només l'esquema i conservar la partida.

En reprendre una partida antiga, l'àrbitre retira les posicions dels usuaris sense cap petició a `moviments`. En resoldre el torn 1 descarta totes les posicions prèvies i només desa les entrades vàlides. Tenir compte o `actiu = 1` no implica ocupar una casella. Un cop s'ha entrat, no cal inserir a cada torn per continuar participant: sense ordre, el robot queda quiet. Per als canvis següents, no executis proves d'integració tret que l'usuari les torni a demanar; ha indicat que triguen massa.

Amb la retirada de robots sense ordres i l'adaptació automàtica de l'esquema antic a `inicialitza-joc`, la compilació ha acabat sense errors i han passat 59 comprovacions de regles i 2 de representació. No s'han executat proves d'integració per indicació de l'usuari; la correcció de l'error SQL 515 no s'ha verificat contra SQL Server en aquesta revisió.

Amb les coordenades als quatre marges i la plantilla del robot aleatori, la compilació ha acabat sense avisos ni errors i han passat 59 comprovacions de regles, 4 de representació i les proves en memòria de `tests/robot.proves/`. Aquestes últimes comproven entrades, passos cardinals, vores, taulers d'una sola fila o columna o casella, jugadors inactius i absència d'entrada tardana. No s'han executat proves d'integració ni s'ha utilitzat la base local. El client consulta les peticions pròpies en reprendre un torn per evitar repetir-ne una; cal executar una sola instància per compte.
