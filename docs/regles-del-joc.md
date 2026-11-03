# Regles del joc

[Torna al README](../README.md)

## Objectiu

Cada jugador controla un robot que pinta les caselles per on passa. Guanya qui té més punts en acabar la partida. Els punts corresponen al territori que es conserva al final, no a la suma de punts obtinguts durant tots els torns. Si diversos jugadors acaben amb la mateixa puntuació màxima, comparteixen la victòria.

## Tauler i robots

El tauler inicial té 30 columnes i 20 files. Les dimensions són configurables. Hi ha 20 comptes disponibles, identificats amb les lletres majúscules ASCII de la `A` a la `T`. No s'utilitzen `Ç`, `Ñ` ni altres caràcters. En cada partida poden participar tots els jugadors o només els que s'hagin activat.

La coordenada `(0, 0)` és la cantonada superior esquerra. `x` augmenta cap a la dreta i `y` cap avall. En el tauler inicial, `x` va de 0 a 29 i `y` de 0 a 19. El tauler té límits: sortir per una vora no porta a l'altra.

Cada jugador té un robot. La seva posició és independent del propietari de la pintura que hi ha a sota. Al començament, totes les caselles són lliures i els robots encara no són al tauler: tenen les dues coordenades a `NULL`. Al torn 1, el primer `INSERT` vàlid de cada jugador a `moviments` tria qualsevol casella dins del tauler. L'àrbitre hi situa el robot en resoldre el torn, sense pintar. Les peticions següents d'aquell jugador al torn 1 s'ignoren; les invàlides no fixen l'entrada. Diversos robots poden escollir la mateixa casella i s'hi aplica la col·lisió habitual. Qui no entra durant el torn 1 queda fora del tauler tota la partida. Tenir un compte o estar marcat com a actiu no situa el robot al tauler. Els usuaris que no han enviat una entrada vàlida no es mostren com a robots ni intervenen en les col·lisions.

## Moviment

A partir del torn 2, el robot es pot desplaçar una casella en una de les quatre direccions:

| Direcció | Canvi de coordenades |
|---|---|
| Nord (`N`) | `(x, y - 1)` |
| Sud (`S`) | `(x, y + 1)` |
| Est (`E`) | `(x + 1, y)` |
| Oest (`O`) | `(x - 1, y)` |

No es permeten diagonals, salts ni destinacions fora del tauler. Totes les ordres d'un mateix torn es calculen respecte de la posició inicial d'aquell torn: enviar diverses ordres no permet encadenar passos.

Els moviments es resolen simultàniament. Si no s'accepta cap ordre del jugador, el robot es queda quiet. Quedar-se quiet no pinta, no reforça i no renova la pintura. Per esperar, no cal enviar cap ordre.

## Pintura

Quan un robot es desplaça efectivament a una casella sense col·lisió, s'apliquen aquestes regles:

| Estat anterior | Resultat |
|---|---|
| Lliure | Passa a ser pintura normal del robot que hi entra. |
| Normal del mateix jugador | Passa a ser reforçada. |
| Reforçada del mateix jugador | Manté el reforç i en renova la durada. |
| Normal d'un adversari | Passa a ser pintura normal del nou jugador. |
| Reforçada d'un adversari | No canvia. El robot pot travessar-la. |

L'àrbitre mostra el tauler amb Spectre.Console, amb un marc i colors per propietari. El tauler en mode text representa les caselles lliures amb `.`, la pintura normal amb la lletra minúscula del propietari i la reforçada amb la majúscula. Per exemple, `a` i `A` són territoris del jugador `A`. La visualització ha de permetre distingir també la posició dels robots, amb una capa separada o una llista de coordenades.

## Caducitat

La pintura reforçada es degrada a normal després de 20 torns sense renovar-se. Des d'aquesta degradació, la pintura normal dura 20 torns més. Una casella pintada directament com a normal també dura 20 torns si ningú hi torna a passar. Les dues durades són configurables.

La caducitat es comprova abans d'aplicar els moviments de cada torn. Si una casella es reforça en el torn 5, es degrada abans dels moviments del torn 25 i queda lliure abans dels moviments del torn 45, sempre que no s'hagi renovat o conquerit. La degradació a normal comença un nou període de caducitat normal.

## Col·lisions

Si dos o més robots participants acaben el torn a la mateixa casella, aquesta queda lliure, encara que estigués reforçada. Cap robot implicat no hi pinta en aquell torn.

També hi ha col·lisió si un robot entra a la casella d'un robot que s'ha quedat quiet. Els robots implicats es queden a la destinació compartida i es poden moure normalment en el torn següent. Si continuen compartint casella, es torna a netejar.

Dos robots que intercanvien posicions no col·lideixen, perquè les destinacions són diferents. No es comproven xocs a mig camí. Els robots inactius no intervenen en moviments ni col·lisions.

## Torns i ordres

La partida té inicialment 200 torns. La durada prevista és de 3 segons, informativa amb el control manual. L'àrbitre té en compte com a màxim les primeres 20 ordres vàlides de cada jugador i torn. Aquests valors són configurables.

Un torn passa pels estats `OBERT`, `EN_ARBITRATGE` i `TANCAT`. Només s'han d'enviar ordres mentre és obert. SQL Server desa les peticions; l'àrbitre en valida les regles i ignora les que arribin abans de l'obertura o a partir del tall de recepció.

L'àrbitre consulta `jugadors.actiu` en resoldre. Al torn 1 poden entrar els jugadors actius; des del torn 2 només intervenen els actius que ja tenen posició. No hi ha una taula de participants per torn. Per ara, el torn es tanca quan la persona que executa l'àrbitre prem Retorn. No es tanca automàticament per temps ni perquè tothom hagi enviat una ordre. `data_limit` és informativa i no descarta peticions; s'accepten les rebudes des de l'obertura i abans del tall manual. Els torns s'obren encara que no hi hagi robots al tauler, perquè es puguin enviar les entrades del torn 1.

Des del torn 2 es fa servir l'última de les ordres vàlides tingudes en compte de cada jugador, identificada pel número de moviment més alt. Les ordres invàlides no substitueixen una d'anterior ni consumeixen el límit. Si s'envien més de 20 ordres vàlides, les següents s'ignoren encara que s'hagin desat a la base de dades. El torn 1 és l'excepció: només es conserva la primera petició vàlida per jugador.

## Puntuació i participació

Inicialment, cada casella normal val 1 punt i cada casella reforçada, 2. Les lliures valen 0. Els valors de les caselles pintades són configurables. La classificació definitiva es calcula després de resoldre l'últim torn, inclosa la seva caducitat.

Cada alumne utilitza exclusivament les seves credencials. Es poden consultar les posicions i els territoris dels adversaris, jugar manualment o programar una estratègia autònoma. Les posicions, la pintura i els torns només els modifica l'àrbitre.
