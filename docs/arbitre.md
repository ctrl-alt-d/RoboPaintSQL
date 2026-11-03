# L'àrbitre

[Torna al README](../README.md)

L'àrbitre és un únic programa C# executat pel professor. Durant els torns utilitza `rol_arbitre`; la inicialització requereix una connexió d'administrador perquè també crea comptes SQL. És l'autoritat que modifica les posicions i la pintura. Els alumnes només proposen moviments.

La inicialització, l'obertura i la resolució dels torns estan implementades a `src/roboarbit/`. La consola informa dels torns, dibuixa el tauler després de confirmar cadascun i mostra la classificació final.

## Menú i execució

Des de l'arrel del repositori:

```sh
dotnet run --project src/roboarbit
```

El menú ofereix `1. Inicialitzar el joc (esborra la partida anterior)`, `2. Començar o reprendre el joc` i `0. Sortir`. Cada operació obre la connexió indicada a `ROBOPAINTSQL_CONNEXIO`. Es pot utilitzar una connexió d'administrador per preparar la partida i tornar a executar el programa amb la del compte de `rol_arbitre` per jugar.

També es conserven les ordres directes `inicialitza-joc [fitxer-credencials]` i `comenca-joc`. Aquesta última executa el mateix bucle que l'opció 2 del menú:

```sh
dotnet run --project src/roboarbit -- comenca-joc
```

`Ctrl+C` atura el bucle i retorna al menú, o acaba l'ordre directa. Es conserven els torns confirmats i el torn obert es pot reprendre. Un bloqueig de sessió de SQL Server (`sp_getapplock`) impedeix executar dues instàncies sobre la mateixa base.

Cada torn espera que la persona que executa l'àrbitre premi **Retorn**. Això tanca la recepció, calcula el torn, desa el resultat i dibuixa el tauler amb [Spectre.Console](https://spectreconsole.net/). No avança automàticament encara que tothom hagi enviat una ordre o hagi passat el temps configurat.

Per conservar la compatibilitat amb les bases existents, `durada_torn_ms` i `data_limit` continuen presents i es calcula la data prevista. Ara són informatius: no limiten l'espera ni l'acceptació de peticions. L'únic límit superior de recepció és l'instant de tall de la resolució manual.
La configuració es llegeix una vegada en començar o reprendre i s'ha de mantenir fixa durant tota la partida, també entre execucions.

## Preparació de la partida

Abans del primer torn, l'àrbitre:

1. Obté el mateix bloqueig exclusiu que el bucle de torns: si hi ha un àrbitre executant-se, rebutja la inicialització. Llegeix els nou paràmetres de `configuracio` i comprova que hi siguin tots i que tinguin valors positius.
2. Llegeix el CSV local de credencials i genera contrasenyes aleatòries individuals per als comptes que encara no existeixen i no tenen credencial. Desa les noves credencials abans de crear cap compte. Si un compte ja existeix però falta la seva credencial, s'atura sense canviar-li la contrasenya.
3. Crea els inicis de sessió i usuaris `A`–`T` que faltin i els afegeix a `rol_alumnes`. Reutilitza els existents i comprova que cada usuari estigui vinculat al compte correcte.
4. Adapta les bases antigues: permet `NULL` a `jugadors.posicio_x` i `posicio_y` i elimina `participants_torn` si encara existeix. Això evita l'error SQL 515 en crear robots sense posició. Esborra les dades anteriors de `moviments`, `caselles`, `torns` i `jugadors`, en aquest ordre per respectar les claus foranes. Conserva la configuració, els comptes i les credencials. Crea els 20 jugadors actius amb les dues coordenades a `NULL`, encara fora del tauler. El primer moviment vàlid del torn 1 triarà la casella d'entrada, sense pintar.
5. Crea totes les caselles amb `nivell = 0`, sense propietari ni torn d'actualització. No pinta les posicions inicials ni obre cap torn.

La preparació SQL, inclosa l'adaptació de l'esquema antic, es fa en una única transacció `SERIALIZABLE`. Un error abans de confirmar desfà els comptes i les dades creats durant l'operació i restaura les dades de la partida anterior. Si ja s'havien desat contrasenyes noves al CSV, es conserven per poder reintentar amb les mateixes credencials. El programa no mostra contrasenyes a la consola. Repetir la inicialització substitueix la partida existent per una de nova, sense torns ni pintura.

Des de l'arrel del repositori, amb `ROBOPAINTSQL_CONNEXIO` definida amb una connexió d'administrador a la base del joc:

```sh
dotnet run --project src/roboarbit -- inicialitza-joc
```

La ruta per defecte és `src/roboarbit/.credencials-alumnes.csv`, independentment de la carpeta de treball. El programa localitza la carpeta del projecte a partir de l'executable; es pot executar `dotnet run` directament des de `src/roboarbit` i el menú utilitza el mateix CSV. En una aplicació publicada sense el projecte, el fitxer es busca al costat de l'executable. Per utilitzar un altre fitxer, cal passar-ne la ruta com a segon argument després de `inicialitza-joc`; si és relativa, es resol respecte de la carpeta de treball. Si falta una credencial d'un compte existent, l'error indica la ruta completa del CSV consultat.

El format és `usuari;contrasenya`, amb una fila per lletra, sense duplicats ni punt i coma dins de la contrasenya. El fitxer es manté fora de Git i només s'ha de distribuir cada credencial al seu alumne. Els comptes són del servidor SQL: una altra base de partida al mateix servidor reutilitza els mateixos inicis de sessió i el mateix CSV.

La configuració es manté fixa fins al final. Es recomana fer les altes i baixes d'activitat entre torns; l'àrbitre consulta `actiu` en resoldre, sense fixar una llista en obrir. La connexió o desconnexió d'un client no canvia automàticament `actiu`. La coherència dels estats, les dates i els nivells de pintura també és responsabilitat d'aquest programa; el DDL no els valida.

## 1. Obrir el torn

En una transacció curta, l'àrbitre comprova que l'anterior és `TANCAT`, crea el torn amb el número següent. Desa `data_inici` amb `SYSUTCDATETIME()` i calcula `data_limit` sumant-hi `durada_torn_ms`. El nou estat és `OBERT`.

El primer torn és l'1 i s'obre encara que tots els robots tinguin posició `NULL` o no hi hagi jugadors actius. No hi ha cap taula `participants_torn`.

## 2. Rebre les ordres

Durant el torn obert, els clients consulten les dades i insereixen peticions a `moviments`. SQL Server només hi fixa l'emissor i l'hora. L'àrbitre llegeix les peticions i en comprova la participació, l'instant de recepció, les coordenades i el nombre d'ordres vàlides.

L'àrbitre espera Retorn sense mantenir cap transacció oberta. El torn continua obert encara que tots els participants tinguin ordres vàlides o s'hagi superat `data_limit`. Si es confirma el torn 1 sense una entrada vàlida d'un jugador, el seu robot queda fora tota la partida. Des del torn 2, els robots sense ordres vàlides queden quiets. Si s'acaba l'entrada redirigida, s'atura i conserva el torn obert.

## 3. Tancar la recepció

L'àrbitre comença una transacció curta de resolució amb aïllament `SERIALIZABLE`. Pren l'hora UTC del servidor com a instant de tall i llegeix les peticions del torn. La lectura manté estable aquest conjunt mentre es resol el torn; les insercions concurrents que hi interfereixin esperaran fins que acabi la transacció.

Amb el conjunt llegit, valida les peticions, passa a `EN_ARBITRATGE` i treballa només amb les peticions capturades des de l'obertura i abans de l'instant de tall. No aplica `data_limit` ni exigeix ordres de tots els participants.

Una inserció posterior pot quedar desada igualment, fins i tot amb el torn ja tancat. L'àrbitre la ignora i no torna a resoldre aquell torn. Desar una petició no garanteix que sigui vàlida ni que es tingui en compte.

L'estat `EN_ARBITRATGE` existeix dins d'aquesta transacció; altres connexions amb lectures normals poden quedar esperant i observar directament el canvi d'`OBERT` a `TANCAT` quan es confirmi. No cal que els clients arribin a veure l'estat intermedi.

## 4. Calcular les destinacions

L'àrbitre llegeix els jugadors actius de `jugadors` dins de la transacció de resolució. Al torn 1 conserva la primera petició vàlida de cada jugador per `id_moviment`: ha d'arribar dins del període de recepció i indicar qualsevol casella dins del tauler. Les següents s'ignoren, sense encadenar moviments. L'aparició no pinta. Sense petició vàlida, les coordenades continuen a `NULL`; no es permet entrar després del torn 1.

Des del torn 2, per a cada jugador actiu que ja té posició, conserva la posició inicial i processa les seves peticions per `id_moviment` ascendent:

1. Ignora les de jugadors inactius o sense posició.
2. Ignora les rebudes abans de `data_inici` o a partir de l'instant de tall.
3. Ignora les destinacions fora del tauler, les diagonals, els salts i els moviments sense desplaçament. La distància cardinal respecte de la posició inicial ha de ser exactament 1.
4. Conserva les primeres `max_moviments_torn` peticions vàlides del jugador i ignora les següents. Les peticions invàlides no consumeixen aquest límit.
5. Selecciona l'última de les conservades, amb l'identificador més alt. Si no n'hi ha, la destinació és la posició inicial i es marca que el robot no s'ha mogut.

Totes aquestes comprovacions es programen en C#. La presència d'una petició invàlida és una situació prevista: s'ignora i es continua, sense aturar la partida. Els càlculs de distàncies utilitzen `long` per evitar desbordaments amb coordenades invàlides extremes.

Les destinacions de tots els robots es calculen abans de modificar cap posició. Resoldre jugador per jugador fent servir posicions ja actualitzades trencaria la simultaneïtat.

## 5. Aplicar la caducitat

Sobre les caselles a l'inici de la resolució:

- Si el nivell era 2 i `id_torn - torn_actualitzacio >= caducitat_reforcada`, passa a nivell 1 i s'actualitza el torn d'actualització al torn actual.
- Si el nivell era 1 i `id_torn - torn_actualitzacio >= caducitat_normal`, queda lliure i els camps de propietari i torn d'actualització passen a `NULL`.

Cada casella només pot patir una degradació en aquest pas. Una reforçada que acaba de passar a normal no s'ha de tornar a avaluar com si ja hagués esgotat el període normal.

## 6. Aplicar col·lisions, posicions i pintura

L'àrbitre agrupa tots els participants per destinació, incloent-hi els que no es mouen. Al torn 1 només agrupa les entrades acceptades; els robots sense posició queden fora. Les destinacions amb dos o més robots queden lliures. Aquest resultat té prioritat sobre qualsevol intent de pintar.

Tot seguit actualitza les posicions de tots els participants. Des del torn 2, a les destinacions sense col·lisió aplica les regles de pintura únicament si el robot s'ha mogut: pinta les lliures, pren les normals enemigues, reforça o renova les pròpies i respecta les reforçades enemigues.

Els jugadors que no són participants no es mouen ni intervenen en les col·lisions. La seva pintura continua sotmesa a caducitat.

## 7. Confirmar i continuar

L'àrbitre canvia l'estat a `TANCAT`, omple `data_fi` amb l'hora UTC del servidor i confirma la transacció. Els nous resultats ja són consultables.

Tot seguit, tant a l'opció 2 del menú com a `comenca-joc`, llegeix les caselles i les posicions desades i mostra el tauler amb Spectre.Console, amb un marc i un color per propietari. La pintura reforçada també apareix en negreta quan el terminal ho admet. Les coordenades `x` apareixen a dalt i a baix i les `y` als dos costats. Cada fila correspon a una coordenada `y`, de dalt a baix, i cada columna de caselles a una coordenada `x`, d'esquerra a dreta, començant per `(0, 0)`. Les caselles reserven prou espai per alinear els nombres de diverses xifres. Les caselles lliures són `.`, la pintura normal és la lletra minúscula del propietari i la reforçada és la majúscula. A sota hi ha les coordenades dels robots que ja han entrat al tauler, inclosos els inactius i els que comparteixen casella, ordenats per identificador. Aquesta llista permet veure la pintura sense tapar-la amb els robots. Cada torn afegeix una representació nova a la consola, sense esborrar les anteriors.

Si s'arriba a `max_torns`, calcula la classificació final i no obre cap altre torn. Altrament, comença el següent. La puntuació és una consulta sobre la pintura resultant, no un comptador acumulat.

## Errors i reinici

Si falla la resolució abans de confirmar, es desfà tota la transacció: el torn continua `OBERT` i les peticions desades es conserven. En reintentar, l'àrbitre torna a capturar i validar les peticions. Torna a esperar Retorn abans de resoldre; `data_limit` no descarta peticions.

En reiniciar, l'àrbitre consulta l'estat persistent: espera Retorn per resoldre l'últim torn si encara és obert o continua després de l'últim tancat. No ha de tornar a aplicar un torn tancat ni tornar a inicialitzar el tauler. En començar o reprendre, retira les posicions heretades de versions antigues dels usuaris que no tenen cap `INSERT` a `moviments`: les dues coordenades passen a `NULL`. Si el torn 1 encara és obert, la seva resolució descarta totes les posicions prèvies i només desa les entrades vàlides. Un robot que ja ha entrat continua al tauler encara que no enviï ordres als torns següents. Només hi pot haver una instància de l'àrbitre executant-se.

## Codi i proves

`ConfiguracioJoc.cs` llegeix i valida els paràmetres; `ReglesTorn.cs` selecciona moviments i calcula el resultat en memòria; `ArbitreJoc.cs` coordina el bucle i les transaccions ADO.NET; `RepresentacioTauler.cs` escriu la pintura amb coordenades als quatre marges i les posicions dels robots. `Program.cs` presenta el menú i conserva les ordres directes. El client de l'alumne disposa d'una [plantilla amb estratègia aleatòria i estat en arrays](../src/robot/README.md).

Les [proves de l'àrbitre](../tests/roboarbit.proves/README.md) combinen comprovacions de regles sense servidor amb proves d'integració en bases temporals SQL Server. L'instant de tall es decideix en C# amb l'hora del servidor quan es confirma el torn manualment; el DDL continua limitat a estructura, permisos i identificació.
