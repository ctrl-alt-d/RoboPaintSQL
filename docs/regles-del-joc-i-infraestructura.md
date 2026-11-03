# Regles del joc i infraestructura

[Torna al README](../README.md)

## De les regles a les dades

| Regla | Representació i responsable |
|---|---|
| Un robot per jugador | Una fila a `jugadors`, identificada per `id_jugador`. |
| Posició del robot | `jugadors.posicio_x` i `jugadors.posicio_y`, inicialment `NULL`; només les actualitza l'àrbitre. |
| Dimensions del tauler | `configuracio`: `amplada_tauler` i `alcada_tauler`. L'àrbitre crea una fila de `caselles` per coordenada. |
| Casella lliure | `nivell = 0`, `id_propietari IS NULL` i `torn_actualitzacio IS NULL`. |
| Pintura normal o reforçada | `nivell = 1` o `nivell = 2`, amb propietari i torn d'actualització. |
| Jugadors que intervenen | `jugadors.actiu = 1` en resoldre; des del torn 2 també han de tenir posició. |
| Entrada al tauler | Primera petició vàlida del torn 1: qualsevol casella dins del tauler, sense pintar. |
| Tancament manual | Retorn a la consola de l'àrbitre; `data_limit` i `durada_torn_ms` són informatius. |
| Ordre d'un jugador | `INSERT` a `moviments` amb `id_torn`, `nova_x` i `nova_y`. |
| Identitat de l'emissor | El disparador obté `USER_NAME()` i desa `id_jugador`, juntament amb l'hora UTC del servidor. |
| Validesa de les peticions | L'àrbitre en C# comprova la participació, l'instant de tall i el desplaçament. |
| Última ordre vàlida | Des del torn 2, l'àrbitre selecciona la de major `id_moviment` entre les primeres `max_moviments_torn` peticions vàlides del jugador. |
| Ordres limitades | L'àrbitre ignora les peticions vàlides que superin `max_moviments_torn`; SQL Server les pot desar igualment. |
| Caducitat | L'àrbitre compara `id_torn - torn_actualitzacio` amb la durada del nivell actual. |
| Col·lisió | L'àrbitre agrupa les destinacions i neteja les compartides. |
| Puntuació | Consulta sobre `caselles`, agrupada per propietari, amb els valors de `configuracio`. |
| Final de partida | L'àrbitre deixa d'obrir torns després de `max_torns`. |

## Exemple d'ordre

Per entrar a `(4, 7)`, el jugador envia la seva primera petició vàlida mentre el torn 1 és obert:

```sql
INSERT INTO dbo.moviments (id_torn, nova_x, nova_y)
VALUES (1, 4, 7);
```

La posició es confirma en resoldre el torn 1. No pinta ni permet més desplaçaments en aquell torn.

Si el jugador `A` és a `(4, 7)` en el torn 12, pot demanar anar a l'est:

```sql
INSERT INTO dbo.moviments (id_torn, nova_x, nova_y)
VALUES (12, 5, 7);
```

No s'envia la direcció textual, sinó la coordenada de destinació. Des del torn 2, l'àrbitre comprova en C# que es compleix `ABS(nova_x - posicio_x) + ABS(nova_y - posicio_y) = 1` i que la destinació és dins del tauler. No existeix cap excepció per connectar vores.

La inserció no mou immediatament el robot. Desa una petició que l'àrbitre aplicarà quan tanqui la recepció d'ordres. Per comprovar el resultat cal esperar que el torn sigui `TANCAT` i tornar a consultar la posició.

Un `INSERT` correcte només significa que la petició s'ha desat. SQL Server exigeix que el torn i el jugador existeixin, però les regles de participació, temps, límit d'ordres i coordenades les valida l'àrbitre. Una petició invàlida o posterior al tancament es pot desar i després s'ignora. El resultat de pintura també depèn de les col·lisions i de l'estat de la casella.

## Canvis sobre una casella

| Esdeveniment | Canvi a `caselles` |
|---|---|
| Entrada en una casella lliure o normal enemiga | Propietari nou, `nivell = 1`, `torn_actualitzacio = torn actual`. |
| Entrada en una casella pròpia normal | `nivell = 2`, `torn_actualitzacio = torn actual`. |
| Entrada en una casella pròpia reforçada | Es manté el nivell i es renova `torn_actualitzacio`. |
| Entrada en una casella enemiga reforçada | No es modifica la casella. |
| Caducitat d'una reforçada | `nivell = 1`, mateix propietari i `torn_actualitzacio = torn actual`. |
| Caducitat d'una normal o col·lisió | `nivell = 0`, propietari i torn d'actualització nuls. |
| Robot quiet sense col·lisió | No modifica la pintura. |

La caducitat s'aplica una sola vegada per casella a l'inici de la resolució, sobre el seu nivell anterior. Després es calculen les col·lisions i, finalment, es pinta a les destinacions sense col·lisió. Això permet conquerir una casella enemiga que acaba de perdre el reforç.

## Puntuació derivada

No hi ha cap columna de puntuació a `jugadors`. Els punts es calculen a partir del territori actual, per evitar mantenir dues versions de la mateixa informació. La consulta ha d'incloure també els jugadors sense territori, amb zero punts. Els empats es poden mostrar ordenats per lletra, però aquesta ordenació no desfà l'empat.

La lletra visible tampoc no es desa a `caselles`: es deriva del propietari i del nivell. Les posicions dels robots i la pintura es consulten per separat.

## Consistència i simultaneïtat

El disparador no bloqueja el torn ni aplica límits d'ordres. L'àrbitre captura les peticions en una transacció amb aïllament `SERIALIZABLE` i les valida en C# respecte de la participació, les posicions inicials i l'instant de tall manual. Només aplica les peticions d'aquest conjunt; les que es desin després no poden alterar un resultat ja resolt.

L'àrbitre resol el torn en una transacció: o bé es desen conjuntament posicions, pintura i tancament, o bé no es desa cap canvi parcial. Els clients no han de fer servir `NOLOCK` ni lectures de dades sense confirmar.

Per obtenir diverses taules corresponents a un mateix estat, el client pot fer una transacció curta de lectura amb aïllament `SERIALIZABLE`, consultar primer el torn i després la resta de dades, i acabar-la immediatament. No ha de mantenir-la oberta mentre espera el teclat o el torn següent. Les consultes independents poden correspondre a instants diferents si l'àrbitre resol un torn entre una consulta i la següent.
