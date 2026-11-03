using Microsoft.Data.SqlClient;

namespace roboarbit;

internal static class Program
{
    private static int Main(string[] arguments)
    {
        if (arguments.Length == 0)
            return MostrarMenu();
        if (arguments.Length > 2
            || (arguments[0] != "inicialitza-joc" && arguments[0] != "comenca-joc")
            || (arguments[0] == "comenca-joc" && arguments.Length != 1))
        {
            Console.Error.WriteLine("Ús: roboarbit [inicialitza-joc [fitxer-credencials] | comenca-joc]");
            return 1;
        }
        return ExecutarOpcio(arguments[0], arguments.Length == 2 ? arguments[1] : null);
    }

    private static int MostrarMenu()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("RoboPaintSQL — Àrbitre");
            Console.WriteLine("1. Inicialitzar el joc (esborra la partida anterior)");
            Console.WriteLine("2. Començar o reprendre el joc");
            Console.WriteLine("0. Sortir");
            Console.Write("Opció: ");
            string? opcio = Console.ReadLine();
            if (opcio is null or "0")
                return 0;
            if (opcio is "1" or "2")
                ExecutarOpcio(opcio == "1" ? "inicialitza-joc" : "comenca-joc");
            else
                Console.WriteLine("Opció no vàlida.");
        }
    }

    private static int ExecutarOpcio(string opcio, string? rutaCredencials = null)
    {
        string? cadenaConnexio = Environment.GetEnvironmentVariable("ROBOPAINTSQL_CONNEXIO");
        if (string.IsNullOrWhiteSpace(cadenaConnexio))
        {
            Console.Error.WriteLine("Cal definir ROBOPAINTSQL_CONNEXIO amb una connexió a la base del joc. Inicialitzar requereix administrador; començar requereix rol_arbitre.");
            return 1;
        }

        try
        {
            using var connexio = new SqlConnection(cadenaConnexio);
            connexio.Open();
            if (opcio == "inicialitza-joc")
            {
                string fitxer = rutaCredencials ?? CredencialsAlumnes.RutaPerDefecte();
                long nombreCaselles = InicialitzadorJoc.Inicialitzar(connexio, fitxer);
                Console.WriteLine($"Joc inicialitzat: 20 jugadors actius sense posició i {nombreCaselles} caselles lliures. Entrada amb el primer moviment vàlid del torn 1. Encara no hi ha cap torn obert.");
                Console.WriteLine($"Credencials locals: {Path.GetFullPath(fitxer)}");
            }
            else
            {
                using var cancelacio = new CancellationTokenSource();
                ConsoleCancelEventHandler aturar = (_, esdeveniment) =>
                {
                    esdeveniment.Cancel = true;
                    cancelacio.Cancel();
                };
                Console.CancelKeyPress += aturar;
                try { new ArbitreJoc(connexio).Executar(cancelacio.Token); }
                finally { Console.CancelKeyPress -= aturar; }
            }
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Joc aturat. Es pot reprendre amb l'opció de començar el joc.");
            return 0;
        }
        catch (SqlException error)
        {
            // Els missatges del motor poden contenir dades de les ordres de creació de comptes.
            Console.Error.WriteLine($"Error SQL {error.Number} {error.Message}. Comprova la connexió, el DDL i els permisos. L'operació en curs no s'ha confirmat; els torns anteriors es conserven.");
        }
        catch (InvalidOperationException error)
        {
            Console.Error.WriteLine(error.Message);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("No s'ha pogut llegir o desar el fitxer local de credencials. Comprova la ruta i els permisos.");
        }
        catch (ArgumentException)
        {
            Console.Error.WriteLine("La cadena de connexió o la ruta de credencials no té un format vàlid.");
        }

        return 1;
    }
}
