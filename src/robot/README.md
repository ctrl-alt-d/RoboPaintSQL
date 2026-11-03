# Plantilla del robot

[Torna al README](../../README.md)

Cal l'SDK .NET 10, una partida inicialitzada i el compte d'alumne A–T assignat pel professor. En el terminal de l'alumne, defineix `ROBOPAINTSQL_CONNEXIO` amb la cadena de connexió d'aquest compte, tal com explica la [guia d'ADO.NET](../../docs/consultes-i-actualitzacions-csharp.md). Utilitza un terminal diferent del de l'àrbitre, que té una connexió amb altres permisos. No desis contrasenyes al codi ni al repositori.

Des de l'arrel del repositori:

```sh
dotnet run --project src/robot
```

Executa el robot abans que el professor tanqui el torn 1. Si encara no hi ha torns, espera. La primera petició tria una casella aleatòria dins del tauler, sense pintar. A partir del torn 2, tria a l'atzar un dels veïns cardinals dins dels límits. Aquesta estratègia inicial no evita els altres robots ni té en compte la pintura: millorar-la és part de la pràctica.

Envia una petició per torn, encara que el límit configurat en permeti més, i consulta els torns cada 200 ms. Només el professor, prement Retorn a l'àrbitre, resol els moviments. `data_limit` no impedeix enviar la petició. Un `INSERT` correcte confirma que la petició s'ha desat; el resultat real es veu després de resoldre el torn. El programa mostra el tauler amb coordenades i les posicions quan detecta un torn obert nou, i també mostra l'estat final en acabar la partida.

`Ctrl+C` atura el robot. Si el torn continua obert, el pots tornar a executar: consulta si ja existeix alguna petició pròpia en aquell torn i, si és així, no n'envia cap altra. No executis dos robots alhora amb el mateix compte: la consulta prèvia no és un bloqueig entre instàncies. Si no has entrat al torn 1, no podràs incorporar-te més tard; caldrà esperar una partida nova. El robot s'atura quan es tanca el màxim de torns configurat; torna'l a executar per a una partida nova.

## On modificar l'estratègia

- [Program.cs](Program.cs): connexió ADO.NET, consultes, bucle, inserció amb paràmetres i representació. Llegeix `amplada_tauler`, `alcada_tauler` i `max_torns` de `configuracio` una vegada en començar.
- [EstatJoc.cs](EstatJoc.cs): dades llegides de SQL Server en arrays.
- [Estrategia.cs](Estrategia.cs): `TriarDestinacio(estat, jugador, aleatori)` retorna `(x, y)` o `null` per quedar-se quiet sense inserir cap petició. No executa SQL ni modifica el tauler.

Els arrays de caselles s'indexen amb **`[x, y]`**, amb `x` cap a la dreta i `y` cap avall:

| Array | Contingut |
|---|---|
| `Propietaris[x, y]` | Lletra A–T del propietari; `'\0'` si és lliure. |
| `Nivells[x, y]` | `0` lliure, `1` normal, `2` reforçada. |
| `TornsPintura[x, y]` | Torn de l'última actualització; `null` si és lliure. |
| `PosicionsX[i]`, `PosicionsY[i]` | Coordenades del jugador; `null` si és fora del tauler. |
| `Actius[i]` | Indica si el jugador és actiu. |

Als arrays de jugadors, `0` representa A, `1` representa B, fins a `19`, que representa T. El paràmetre `jugador` és l'índex propi, obtingut amb `USER_NAME()`; `(char)('A' + jugador)` dona la lletra. Un jugador amb posició continua ocupant casella només si és actiu en resoldre; la representació també mostra les posicions desades dels inactius.

Per exemple, dins de `TriarDestinacio`, `estat.Nivells[novaX, novaY] == 0` permet detectar una candidata lliure i `estat.Propietaris[novaX, novaY] == (char)('A' + jugador)` identifica pintura pròpia. Pots començar prioritzant caselles lliures, després pintura normal enemiga i evitant robots actius. Cal respectar els límits i el pas cardinal. Les posicions corresponen a l'estat confirmat: no permeten saber amb certesa què faran els altres robots en aquest torn.

La lectura del torn, dels jugadors, de les caselles i de les peticions pròpies utilitza una transacció breu amb `REPEATABLE READ`. Es tanca abans de decidir, inserir o esperar. Les dades són una còpia per calcular l'estratègia; les regles definitives les aplica l'àrbitre. Després de llegir-les el torn es pot tancar, i una petició tardana pot quedar desada però ser ignorada.

Com a exercicis addicionals, es pot calcular la classificació amb SQL, afegir control manual o llegir més paràmetres de configuració per tenir en compte la caducitat i els punts.

## Comprovar l'estratègia sense SQL Server

```sh
dotnet run --project tests/robot.proves
```

Comprova entrades dins del tauler, passos cardinals des de totes les caselles de diversos taulers rectangulars, vores, taulers d'una sola fila o columna, espera en un tauler d'una casella, jugadors inactius i absència d'entrada tardana. No obre cap connexió.
