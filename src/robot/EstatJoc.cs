namespace robot;

internal sealed record ConfiguracioRobot(int Amplada, int Alcada, int MaxTorns);

// Una còpia en memòria de les dades necessàries per decidir el moviment.
internal sealed class EstatJoc(int amplada, int alcada)
{
    public int Amplada { get; } = amplada;
    public int Alcada { get; } = alcada;
    public int IdTorn { get; set; }
    public string EstatTorn { get; set; } = "";
    public bool TePeticioPropia { get; set; }

    // Caselles: sempre [x, y]. '\0' indica que no hi ha propietari.
    // Nivells: 0 lliure, 1 normal, 2 reforçada. El torn és null si no hi ha pintura.
    public char[,] Propietaris { get; } = new char[amplada, alcada];
    public byte[,] Nivells { get; } = new byte[amplada, alcada];
    public int?[,] TornsPintura { get; } = new int?[amplada, alcada];

    // Jugadors: índex 0 = A, 1 = B, ..., 19 = T. null indica que és fora del tauler.
    public int?[] PosicionsX { get; } = new int?[20];
    public int?[] PosicionsY { get; } = new int?[20];
    public bool[] Actius { get; } = new bool[20];
}
