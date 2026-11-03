using robot;

// Només memòria: no obre connexions ni insereix moviments.
var aleatori = new Random(1234);
int comprovacions = 0;
foreach (var (amplada, alcada) in new[] { (1, 1), (1, 5), (6, 1), (3, 7), (30, 20) })
{
    var estat = new EstatJoc(amplada, alcada) { IdTorn = 1 };
    estat.Actius[19] = true;
    for (int intent = 0; intent < 100; intent++)
    {
        var entrada = Estrategia.TriarDestinacio(estat, 19, aleatori);
        Comprovar(entrada is { } p && p.X >= 0 && p.X < amplada && p.Y >= 0 && p.Y < alcada,
            "L'entrada ha de ser dins del tauler.");
    }
    estat.IdTorn = 2;
    Comprovar(Estrategia.TriarDestinacio(estat, 19, aleatori) is null,
        "Un robot sense entrada no es pot incorporar tard.");

    for (int y = 0; y < alcada; y++)
        for (int x = 0; x < amplada; x++)
        {
            estat.PosicionsX[19] = x;
            estat.PosicionsY[19] = y;
            for (int intent = 0; intent < 20; intent++)
            {
                var moviment = Estrategia.TriarDestinacio(estat, 19, aleatori);
                Comprovar(amplada == 1 && alcada == 1 ? moviment is null
                    : moviment is { } p && p.X >= 0 && p.X < amplada && p.Y >= 0 && p.Y < alcada
                        && Math.Abs(p.X - x) + Math.Abs(p.Y - y) == 1,
                    "Cal fer un únic pas cardinal dins del tauler, o esperar si no hi ha veïns.");
                Comprovar(estat.PosicionsX[19] == x && estat.PosicionsY[19] == y,
                    "Decidir no ha de modificar la posició inicial del torn.");
            }
        }
    estat.Actius[19] = false;
    foreach (int torn in new[] { 1, 2 })
    {
        estat.IdTorn = torn;
        Comprovar(Estrategia.TriarDestinacio(estat, 19, aleatori) is null,
            "Un jugador inactiu no envia cap ordre.");
    }
}

// Des del centre han de poder sortir les quatre direccions, sense afavorir sempre la mateixa.
var centre = new EstatJoc(3, 3) { IdTorn = 2 };
centre.Actius[0] = true;
centre.PosicionsX[0] = centre.PosicionsY[0] = 1;
var destinacions = new HashSet<(int X, int Y)>();
for (int intent = 0; intent < 100; intent++)
    destinacions.Add(Estrategia.TriarDestinacio(centre, 0, aleatori)!.Value);
Comprovar(destinacions.SetEquals(new[] { (1, 0), (2, 1), (1, 2), (0, 1) }),
    "L'estratègia aleatòria ha de permetre les quatre direccions.");
Console.WriteLine($"Superades {comprovacions} comprovacions de l'estratègia del robot sense SQL Server.");

void Comprovar(bool condicio, string missatge)
{
    if (!condicio)
        throw new InvalidOperationException(missatge);
    comprovacions++;
}
