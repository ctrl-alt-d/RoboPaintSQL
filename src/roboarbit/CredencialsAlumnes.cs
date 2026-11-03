using System.Security.Cryptography;
using System.Text;

namespace roboarbit;

internal static class CredencialsAlumnes
{
    public static string RutaPerDefecte()
    {
        // dotnet run pot tenir qualsevol carpeta de treball; partim de l'executable.
        for (DirectoryInfo? carpeta = new(AppContext.BaseDirectory); carpeta is not null; carpeta = carpeta.Parent)
        {
            if (File.Exists(Path.Combine(carpeta.FullName, "roboarbit.csproj")))
                return Path.Combine(carpeta.FullName, ".credencials-alumnes.csv");
        }

        // En una aplicació publicada sense el projecte, el CSV es desa al costat de l'executable.
        return Path.Combine(AppContext.BaseDirectory, ".credencials-alumnes.csv");
    }

    public static Dictionary<char, string> Llegir(string ruta)
    {
        var credencials = new Dictionary<char, string>();
        if (!File.Exists(ruta))
            return credencials;

        using var lector = new StreamReader(ruta, Encoding.UTF8);
        if (lector.ReadLine() != "usuari;contrasenya")
            throw new InvalidOperationException("El CSV ha de començar amb usuari;contrasenya.");

        while (lector.ReadLine() is string linia)
        {
            string[] camps = linia.Split(';');
            if (camps.Length != 2 || camps[0].Length != 1
                || camps[0][0] is < 'A' or > 'T'
                || string.IsNullOrWhiteSpace(camps[1]) || camps[1].Length > 128
                || !credencials.TryAdd(camps[0][0], camps[1]))
            {
                throw new InvalidOperationException("El CSV conté una fila invàlida o un usuari repetit. Calen lletres A–T i contrasenyes de fins a 128 caràcters, sense punt i coma.");
            }
        }

        return credencials;
    }

    public static string GenerarContrasenya()
    {
        return "Rp1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    }

    public static void Desar(string ruta, Dictionary<char, string> credencials)
    {
        string rutaCompleta = Path.GetFullPath(ruta);
        string rutaTemporal = rutaCompleta + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var opcions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
            opcions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        try
        {
            using (var escriptor = new StreamWriter(new FileStream(rutaTemporal, opcions), new UTF8Encoding(false)))
            {
                escriptor.WriteLine("usuari;contrasenya");
                foreach (var (usuari, contrasenya) in credencials.OrderBy(parella => parella.Key))
                    escriptor.WriteLine($"{usuari};{contrasenya}");
            }

            // Es desa abans de crear comptes: si falla SQL, es pot reintentar amb les mateixes claus.
            File.Move(rutaTemporal, rutaCompleta, overwrite: true);
        }
        finally
        {
            if (File.Exists(rutaTemporal))
                File.Delete(rutaTemporal);
        }
    }
}
