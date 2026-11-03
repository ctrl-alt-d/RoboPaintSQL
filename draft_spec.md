# PRÀCTICA: GUERRA DE TERRITORIS
## Batalla de robots amb C# i bases de dades

**Mòdul:** Bases de Dades  
**Durada:** 4 hores  
**Modalitat:** Individual, amb competició multijugador  
**Tecnologies:** C#, SQL i base de dades compartida

---

## 1. Introducció

Benvinguts a la Guerra de Territoris!

En aquesta pràctica participareu en una competició contra els vostres companys. Cadascú controlarà un robot que es desplaçarà per un tauler compartit, conquerint caselles i prenent territori als adversaris.

La particularitat és que **tots els robots viuen dins d'una mateixa base de dades**.

Cada alumne desenvoluparà un programa en C# capaç de consultar l'estat del tauler i enviar ordres de moviment.

Podeu controlar el vostre robot manualment o desenvolupar un algorisme que prengui les decisions automàticament.

**L'objectiu és aconseguir la màxima puntuació possible en finalitzar la partida.**

Però compte: els vostres territoris poden ser conquerits per altres jugadors i la pintura desapareix amb el temps.

Aconseguireu desenvolupar una estratègia guanyadora?

---

## 2. El tauler

El joc es desenvolupa inicialment sobre un tauler de **30 columnes i 20 files**, tot i que les seves dimensions poden canviar entre partides.

Hi participaran aproximadament 15 robots, identificats mitjançant lletres de la A a la O.

El tauler es representa mitjançant caràcters ASCII.

Cada casella pot trobar-se en un dels estats següents:

| Símbol | Significat |
|--------|------------|
| `.` | Casella lliure |
| `a` | Casella pintada pel jugador A |
| `A` | Casella reforçada pel jugador A |
| `b` | Casella pintada pel jugador B |
| `B` | Casella reforçada pel jugador B |

I així successivament per a tots els jugadors.

Els robots també tenen una posició dins del tauler, independentment de qui sigui el propietari de la casella que ocupen.

El programa inicial proporcionat permetrà visualitzar el tauler en mode text.

---

## 3. Regles del joc

### 3.1. Moviment

Cada jugador disposa d'un robot que es pot desplaçar una casella per torn en una de les direccions següents:

- `N`: nord.
- `S`: sud.
- `E`: est.
- `O`: oest.

Els moviments són simultanis: l'àrbitre recull les ordres i les executa en finalitzar cada torn.


**El tauler té limits.** Els robots no poden sortir del taulell.


### 3.2. Pintar territoris

Quan un robot entra en una casella, intenta pintar-la.

Hi ha tres estats possibles:

**Casella lliure**

No pertany a cap jugador. Quan un robot hi passa, queda pintada amb la seva lletra minúscula.

**Casella pintada**

Es representa mitjançant una lletra minúscula.

Pertany a un jugador, el jugador hi ha passat 1 cop per aquella casella.

**Casella reforçada**

Es representa mitjançant una lletra majúscula.

Pertany a un jugador, el jugador hi ha passat 2 cops per aquella casella.

**Regles**

Una casella reforçada no pot ser conquerida per un altre jugador mentre conservi la protecció.

Un robot pot travessar una cassella reforçada, però no modificar-la.

Si el propietari hi torna a passar, renova la protecció.

### 3.3. Caducitat de la pintura

La pintura no dura eternament.

- Una casella reforçada passa a ser normal després de 20 torns sense renovar-se. (parametritzat a la taula de configuració)
- Una casella normal desapareix després de 20 torns més sense renovar-se. (parametritzat a la taula de configuració)
- El propietari pot reforçar o renovar les seves caselles tornant-hi a passar.

Els temps de caducitat són configurables i poden canviar entre partides. (parametritzat a la taula de configuració)

### 3.4. Col·lisions

Si dos o més robots acaben un torn a la mateixa casella, es produeix una col·lisió.

**La casella queda completament neta**, independentment de qui en fos el propietari o del seu nivell de protecció.

Els robots no reben cap altra penalització i es podran moure normalment en el torn següent.

### 3.5. Puntuació

La puntuació es calcula segons els territoris que posseeix cada jugador.

| Tipus de casella | Punts |
|------------------|-------|
| Lliure | 0 |
| Pintada | 1 |
| Reforçada | 2 |

La classificació pot canviar constantment, ja que els robots conquereixen territoris i la pintura caduca.

**Guanya qui aconsegueixi més punts en acabar la partida.**

---

## 4. Funcionament dels torns

La partida consta inicialment de **200 torns**. (parametritzat a la taula de configuració)

Cada torn té una durada màxima de **3 segons**. (parametritzat a la taula de configuració)

L'àrbitre controla el desenvolupament de la partida i registra els torns a la base de dades.

Cada torn passa per tres estats:

| Estat | Descripció |
|-------|------------|
| OBERT | Els robots poden enviar ordres. |
| EN_ARBITRATJE | Es processen els moviments i s'actualitza el tauler. |
| TANCAT | El torn ha acabat i els resultats són definitius. |

Al començament de cada torn, l'àrbitre determina quins jugadors hi participen.

El torn acaba quan tots els participants han enviat almenys una ordre o quan transcorren els tres segons.

### Enviar moviments

Durant un torn obert, un jugador pot enviar diverses ordres de moviment.

Es permeten **fins a 20 ordres per jugador i torn**. (parametritzat a la taula de configuració)

L'àrbitre utilitzarà únicament l'última ordre vàlida rebuda abans de tancar el torn.

Per tant, podeu canviar d'opinió durant un torn, però el vostre robot es desplaçarà com a màxim una casella.

Si no envieu cap ordre, el vostre robot es quedarà quiet.

---

## 5. La base de dades compartida

Tots els alumnes es connectaran a una mateixa base de dades central.

Cadascú disposarà del seu propi usuari i contrasenya.

La base de dades conté la informació necessària per conèixer l'estat de la partida.

Entre d'altres, hi trobareu les taules següents:

| Taula | Contingut |
|-------|-----------|
| `jugadores` | Identificació, lletra i posició de cada robot. |
| `caselles` | Propietari i nivell de pintura de les caselles. |
| `turns` | Identificador, estat i temps de cada torn. |
| `participants_torn` | Jugadors autoritzats a participar en cada torn. |
| `movimients` | Ordres enviades pels jugadors. |
| `configuracio` | Dimensions, durada i regles configurables. |

### Permisos

Els alumnes podran consultar les taules necessàries mitjançant instruccions `SELECT`.

Tanmateix, **no podran modificar directament l'estat del tauler**.

Únicament podran inserir ordres a la taula `movimientos`.

L'àrbitre serà l'encarregat de validar les ordres i aplicar les regles.

Per exemple, per consultar el torn obert:

```sql
SELECT id_turno
FROM torns
WHERE estat = 'OBERT';
```

I per enviar un moviment:

```sql
INSERT INTO moviments (id_torn, nova_posicio_x, nova_posicio_y)
VALUES (57, 14, 20);
```

La base de dades identificarà automàticament l'usuari que envia l'ordre.

**No està permès modificar directament posicions, puntuacions ni territoris.**

---

## 6. Què heu de programar?

Rebreu un projecte inicial en C# que inclourà l'estructura bàsica de l'aplicació i el codi necessari per connectar-se a la base de dades.

La vostra tasca consistirà a completar les operacions SQL necessàries perquè el vostre robot pugui participar.

La pràctica es divideix en diverses missions.

### MISSIÓ 1. Conèixer el món

**Objectiu:** obtenir informació de la partida.

Implementeu les consultes necessàries per:

1. Obtenir la configuració de la partida.
2. Consultar el torn actual i el seu estat.
3. Obtenir la vostra posició.
4. Consultar la posició de tots els jugadors.
5. Obtenir la informació de les caselles.
6. Pintar taulell

Quan acabeu aquesta missió, el vostre programa haurà de ser capaç de mostrar el tauler actualitzat.

### MISSIÓ 2. Primer moviment!

**Objectiu:** aconseguir que el vostre robot es desplaci.

Implementeu la inserció d'ordres de moviment.

El programa haurà de:

1. Detectar quan comença un torn nou.
2. Triar una direcció.
3. Inserir l'ordre a la base de dades.
4. Esperar el resultat de l'arbitratge.
5. Mostrar la nova posició.

Comenceu amb moviments manuals utilitzant el teclat.

**Important:** el programa no ha d'intentar modificar directament les coordenades del robot. Ha de fer insert a la taula de moviments.

### MISSIÓ 3. Conquerir territoris

**Objectiu:** visualitzar i comprendre l'estat del tauler.

Implementeu les consultes necessàries per distingir entre caselles lliures, pintades i reforçades.

Mostreu correctament les lletres minúscules i majúscules.

Afegiu també una consulta que permeti conèixer quantes caselles normals i reforçades posseeix el vostre jugador.

### MISSIÓ 4. Classificació

**Objectiu:** calcular la puntuació dels jugadors.

Construïu una consulta SQL que mostri la classificació ordenada de major a menor puntuació.

Recordeu que les caselles normals valen un punt i les reforçades, dos.

Intenteu resoldre-ho utilitzant les instruccions SQL que coneixeu.

### MISSIÓ 5 (segona part de la pràctica). Robot autònom

**Objectiu:** desenvolupar un algorisme capaç de jugar sense intervenció humana.

En lloc de seleccionar manualment les direccions, el vostre programa haurà de decidir quin moviment ha de fer.

Podeu començar amb una estratègia senzilla:

**Nivell 1. Robot aleatori**

Selecciona una direcció aleatòria en cada torn.

**Nivell 2. Robot explorador**

Intenta desplaçar-se cap a caselles lliures per conquerir nous territoris.

**Nivell 3. Robot conqueridor**

Busca caselles enemigues vulnerables per prendre-les als adversaris.

**Nivell 4. Robot estrateg**

Combina exploració, conquesta i manteniment dels seus territoris. Pot tenir en compte les posicions enemigues i el temps que queda perquè caduqui la pintura.

No és obligatori implementar tots els nivells.

Cada alumne pot triar la seva pròpia estratègia.

---

## 7. Reptes addicionals

Si acabeu les missions principals, podeu intentar millorar el vostre robot.

Algunes idees:

**Evitar col·lisions.** Utilitzeu les posicions dels adversaris per intentar predir cap a on es mouran.

**Buscar territoris desprotegits.** Localitzeu caselles enemigues la protecció de les quals estigui a punt de caducar.

**Defensar territoris.** Dissenyeu rutes que permetin tornar periòdicament a les caselles pròpies.

**Analitzar rivals.** Observeu els moviments anteriors d'altres jugadors i intenteu detectar patrons.

**Canviar d'estratègia.** Dissenyeu un robot que jugui de manera diferent segons la classificació, la fase de la partida o el territori disponible.

Per exemple, pot ser interessant explorar durant els primers torns i concentrar-se a defensar territoris quan s'acosti el final.

---

## 8. Regles de participació

Per garantir que la competició sigui justa:

- Cada alumne utilitzarà exclusivament el seu usuari de base de dades.
- No es permet modificar les taules protegides (tampoc deixarà)
- No es permet superar el límit de 20 ordres per torn.
- Els programes han de respectar els estats dels torns.
- Es permet consultar les posicions i els territoris dels adversaris.
- Es permet utilitzar algorismes propis per decidir els moviments.
- També es permet controlar el robot manualment.

L'àrbitre serà qui determinarà el resultat definitiu de cada torn.

---

## 9. Objectiu final

En acabar la pràctica, el vostre programa haurà de ser capaç de connectar-se a la base de dades, consultar l'estat de la partida, representar el tauler, enviar moviments vàlids i mostrar la classificació.

Els alumnes que completin les funcionalitats bàsiques podran competir utilitzant un robot autònom.

**El repte no consisteix només a programar el robot més ràpid, sinó a utilitzar la informació de la base de dades per prendre millors decisions.**

Que guanyi el millor estrateg!


----




# Estructura de la base de dades

base de dadades: SQLSERVER o POSTGRESQL

## 1. jugadores

Conté la informació dels robots que participen en la partida.

| Camp | Tipus | Descripció |
|---|---|---|
| id_jugador | char(1), PK | Identificador del jugador, és una lletra, es correspon amb l'usuari de la base de dades |
| posicion_x | INT | Columna actual del robot |
| posicion_y | INT | Fila actual del robot |
| actiu | BOOLEAN | Indica si participa en la partida |

Cada alumne té un únic robot.

Els alumnes poden consultar aquesta taula, però només l'àrbitre la pot modificar.

## 2. caselles

Conté l'estat de la pintura de cada casella del tauler.

| Camp | Tipus | Descripció |
|---|---|---|
| posicion_x | INT, PK composta | Columna |
| posicion_y | INT, PK composta | Fila |
| id_propietario | char(1), FK, NULL | Jugador propietari de la pintura |
| nivel | INT | 0: lliure, 1: pintada, 2: reforçada |
| turno_actualizacion | INT, FK, NULL | Torn en què es va actualitzar la pintura |

La clau primària està formada per `(posicion_x, posicion_y)`.

Una casella lliure té `id_propietario = NULL` i `nivel = 0`.

L'àrbitre actualitza aquesta taula després de processar els moviments i aplicar la caducitat.

## 3. torns

Conté els torns de la partida.

| Camp | Tipus | Descripció |
|---|---|---|
| id_turno | INT, PK | Identificador del torn |
| estado | VARCHAR(20) | ABIERTO, EN_ARBITRAJE o CERRADO |
| fecha_inicio | TIMESTAMP | Moment d'obertura del torn |
| fecha_fin | TIMESTAMP, NULL | Moment de tancament del torn |

Només l'àrbitre pot crear torns i modificar-ne l'estat.

## 4. participantes_turno

Conté la llista de jugadors que poden participar en cada torn.

| Camp | Tipus | Descripció |
|---|---|---|
| id_torn | INT, PK composta, FK | Torn |
| id_jugador | char(1), PK composta, FK | Jugador participant |

La clau primària està formada per `(id_torn, id_jugador)`.

Quan s'obre un torn, l'àrbitre hi insereix els jugadors actius.

Els jugadors que es connectin durant un torn hauran d'esperar fins al següent.

## 5. moviments

Conté les ordres de moviment enviades pels alumnes.

| Camp | Tipus | Descripció |
|---|---|---|
| id_movimiento | BIGINT, PK | Identificador autoincremental |
| id_turno | INT, FK | Torn al qual pertany el moviment |
| id_jugador | char(1) | Usuari que ha enviat l'ordre |
| nueva_x | INT | Nova columna sol·licitada |
| nueva_y | INT | Nova fila sol·licitada |
| fecha_movimiento | TIMESTAMP | Moment de recepció de l'ordre |

Els alumnes només poden fer `INSERT` en aquesta taula.

El camp `usuario` s'ha d'omplir automàticament a partir de l'usuari autenticat de la connexió.

Cada jugador pot enviar fins a 20 ordres per torn.

L'àrbitre utilitza l'ordre vàlida amb l'identificador més alt de cada jugador i torn.

Si el jugador no envia cap ordre, es queda a la mateixa posició.

L'àrbitre comprova que el moviment sigui d'una sola casella, tenint en compte els límits toroidals del tauler.

## 6. configuracio

Conté els paràmetres configurables del joc.

| Camp | Tipus | Descripció |
|---|---|---|
| clave | VARCHAR(50), PK | Nom del paràmetre |
| valor | INT | Valor del paràmetre |

Valors inicials:

| clave | valor |
|---|---|
| ancho_tablero | 30 |
| alto_tablero | 20 |
| duracion_turno_ms | 3000 |
| max_turnos | 200 |
| caducidad_reforzada | 20 |
| caducidad_normal | 20 |
| max_movimientos_turno | 20 |
| puntos_normal | 1 |
| puntos_reforzada | 2 |

Només l'àrbitre o el professor poden modificar la configuració.

---

# Permisos

| Taula | Alumnes | Àrbitre |
|---|---|---|
| jugadores | SELECT | SELECT, INSERT, UPDATE |
| casillas | SELECT | SELECT, INSERT, UPDATE |
| turnos | SELECT | SELECT, INSERT, UPDATE |
| participantes_turno | SELECT | SELECT, INSERT |
| movimientos | SELECT, INSERT | SELECT |
| configuracion | SELECT | SELECT, INSERT, UPDATE |

Els alumnes no poden fer UPDATE ni DELETE sobre cap taula.

L'usuari que envia un moviment s'identifica automàticament amb les credencials de la connexió.

La validació dels moviments i l'actualització del tauler són responsabilitat de l'àrbitre.
