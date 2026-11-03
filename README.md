# RoboPaintSQL: guerra de territoris

Pràctica de bases de dades per a alumnes de cicles formatius de grau superior d'informàtica. Cada alumne controla un robot amb C# i competeix per pintar un tauler compartit a SQL Server.

**Durada orientativa:** 4 hores. **Modalitat:** individual, amb competició multijugador. **Tecnologies:** C#, ADO.NET i T-SQL.

## Documentació

Llegiu els documents en aquest ordre:

1. [Regles del joc](docs/regles-del-joc.md): objectiu, moviments, pintura, col·lisions i puntuació.
2. [Infraestructura](docs/infraestructura.md): programes, connexions, taules i permisos.
3. [Regles del joc i infraestructura](docs/regles-del-joc-i-infraestructura.md): representació de les regles a la base de dades.
4. [L'àrbitre](docs/arbitre.md): preparació de la partida i resolució de cada torn.
5. [Consultes i actualitzacions des de C#](docs/consultes-i-actualitzacions-csharp.md): connexió amb ADO.NET, paràmetres, lectura i escriptura.

## Fitxers del projecte

- [sql/ddl-tsql.sql](sql/ddl-tsql.sql): taules, claus, configuració inicial, permisos i identificació de qui envia els moviments. Les regles del joc es validen a l'àrbitre en C#.
- [sql/adaptar-entrada-torn-1.sql](sql/adaptar-entrada-torn-1.sql): adaptació de les bases existents a les posicions inicialment nul·les i eliminació de `participants_torn`.
- [src/roboarbit/](src/roboarbit/): àrbitre C# amb menú per inicialitzar la partida i començar o reprendre els torns.
- [src/robot/](src/robot/README.md): plantilla del client amb moviments aleatoris i estat del joc en arrays.
- `src/roboshare/`: esquelet de la biblioteca compartida.

El professor executa el DDL en una base de dades buida de SQL Server i després inicialitza el joc des de C#. Amb l'SDK .NET 10 i `ROBOPAINTSQL_CONNEXIO` definida amb una connexió d'administrador a aquesta base, executa des de l'arrel del repositori:

```sh
dotnet run --project src/roboarbit -- inicialitza-joc
```

L'àrbitre llegeix o genera les credencials a `src/roboarbit/.credencials-alumnes.csv`, exclòs de Git. Localitza aquest fitxer independentment de la carpeta des d'on s'executa; també funciona amb `dotnet run` des de `src/roboarbit`. Es pot indicar una altra ruta com a segon argument. Reutilitza els comptes existents sense canviar-ne les contrasenyes. Si la base ja conté dades de joc, adapta automàticament l'esquema antic, esborra les dades i prepara una partida nova, conservant la configuració. L'adaptació permet les coordenades `NULL` i evita l'error SQL 515. Cal aturar el bucle de l'àrbitre abans de reinicialitzar. Vegeu els detalls a [l'àrbitre](docs/arbitre.md).

Sense arguments, `dotnet run --project src/roboarbit` mostra el menú: inicialitzar, començar o reprendre, i sortir. La inicialització deixa els robots sense posició. Només entren els usuaris que envien una petició vàlida a `moviments`; els altres no ocupen caselles ni intervenen en les col·lisions. El primer `INSERT` vàlid de cada jugador al torn 1 tria la seva casella d'entrada, sense pintar. Des del torn 2 només es permet un pas cardinal.

Per executar directament el bucle, amb `ROBOPAINTSQL_CONNEXIO` definida amb el compte de `rol_arbitre`:

```sh
dotnet run --project src/roboarbit -- comenca-joc
```

Cada torn espera que la persona que executa l'àrbitre premi **Retorn**. Per ara no hi ha avanç automàtic ni descart de peticions per `configuracio.durada_torn_ms`. L'àrbitre aplica caducitat, moviments, col·lisions i pintura, mostra el tauler amb colors amb Spectre.Console, les coordenades als quatre marges i les posicions dels robots després de confirmar el torn, i continua fins a `max_torns`. `Ctrl+C` permet aturar i reprendre.

En un altre terminal, cada alumne defineix `ROBOPAINTSQL_CONNEXIO` amb el seu compte A–T i executa:

```sh
dotnet run --project src/robot
```

El robot entra en una casella aleatòria al torn 1 i envia un pas cardinal aleatori per torn a partir del torn 2. Mostra el tauler i carrega la pintura i els jugadors en arrays. Els alumnes poden modificar [Estrategia.TriarDestinacio](src/robot/Estrategia.cs) per millorar les decisions sense canviar les consultes SQL. Vegeu [les instruccions de la plantilla](src/robot/README.md).

## Treball de l'alumne

L'objectiu és aprendre a obtenir informació amb SQL i utilitzar-la des de C# per prendre decisions:

1. Entendre les consultes de configuració, torns, jugadors i caselles i la representació del tauler que ofereix la plantilla.
2. Canviar l'estratègia aleatòria o afegir moviments manuals i observar-ne el resultat després de l'arbitratge.
3. Distingir pintura normal i reforçada i consultar el territori propi.
4. Calcular i mostrar la classificació amb SQL, incloent-hi els jugadors sense punts.
5. Millorar les decisions automàtiques: explorar, conquerir o defensar territoris.

El document `draft_spec.md` es conserva com a esborrany històric fins que s'elimini; els documents enllaçats aquí defineixen els criteris adoptats.
