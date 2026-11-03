namespace robot;

internal static class Estrategia
{
    // Retorna una destinació, o null per no enviar cap ordre.
    // Aquesta és la funció que els alumnes poden canviar: totes les dades són als arrays d'estat.
    public static (int X, int Y)? TriarDestinacio(EstatJoc estat, int jugador, Random aleatori)
    {
        if (!estat.Actius[jugador])
            return null;

        // Al torn 1 podem entrar a qualsevol casella; l'entrada encara no pinta.
        if (estat.IdTorn == 1)
            return (aleatori.Next(estat.Amplada), aleatori.Next(estat.Alcada));

        // Estem a mitja partida?
        if (estat.PosicionsX[jugador] is not int x || estat.PosicionsY[jugador] is not int y)
            return (aleatori.Next(estat.Amplada), aleatori.Next(estat.Alcada));

        // Preparem com a màxim quatre destinacions vàlides respecte de la posició inicial.
        int[] canvisX = [0, 1, 0, -1];
        int[] canvisY = [-1, 0, 1, 0];
        int[] candidatesX = new int[4];
        int[] candidatesY = new int[4];
        int quantitat = 0;
        for (int direccio = 0; direccio < 4; direccio++)
        {
            int novaX = x + canvisX[direccio];
            int novaY = y + canvisY[direccio];
            if (novaX >= 0 && novaX < estat.Amplada && novaY >= 0 && novaY < estat.Alcada)
            {
                candidatesX[quantitat] = novaX;
                candidatesY[quantitat] = novaY;
                quantitat++;
            }
        }

        if (quantitat == 0)
            return null; // Per exemple, un tauler d'una sola casella.

        // EXERCICI: puntua les candidates consultant Propietaris, Nivells,
        // TornsPintura i les posicions dels altres robots, i tria la millor.
        int triada = aleatori.Next(quantitat);
        return (candidatesX[triada], candidatesY[triada]);
    }
}
