using roboarbit;

internal static class ProvesRegles
{
    public static void Executar()
    {
        int comprovacions = 0;
        void Comprovar(bool condicio, string cas)
        {
            if (!condicio) throw new InvalidOperationException($"Regles: {cas}");
            comprovacions++;
        }

        var configuracio = new ConfiguracioJoc(5, 5, 3000, 200, 20, 20, 2, 1, 2);
        DateTime inici = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var torn = new TornObert(25, inici, inici.AddSeconds(3));
        var participants = new Dictionary<string, Posicio> { ["A"] = new(1, 1) };
        Dictionary<string, Posicio> Seleccionar(params PeticioMoviment[] peticions) =>
            ReglesTorn.SeleccionarMoviments(configuracio, torn, inici.AddSeconds(2), participants.ToDictionary(p => p.Key, p => (Posicio?)p.Value), peticions);
        PeticioMoviment Peticio(long id, int x, int y) => new(id, "A", new(x, y), inici.AddSeconds(1));

        Posicio[] invalides = [new(-1, 1), new(5, 1), new(1, -1), new(1, 5), new(2, 2),
            new(3, 1), new(1, 1), new(int.MinValue, int.MaxValue)];
        foreach (Posicio desti in invalides)
            Comprovar(Seleccionar(Peticio(1, desti.X, desti.Y)).Count == 0, $"destinació invàlida {desti}");
        foreach (Posicio desti in new Posicio[] { new(1, 0), new(1, 2), new(0, 1), new(2, 1) })
            Comprovar(Seleccionar(Peticio(1, desti.X, desti.Y))["A"] == desti, $"pas cardinal {desti}");
        Comprovar(Seleccionar(Peticio(1, 2, 1) with { Jugador = "B" }).Count == 0, "no participant");
        foreach (DateTime data in new[] { inici.AddTicks(-1), inici.AddSeconds(2), inici.AddSeconds(3) })
            Comprovar(Seleccionar(Peticio(1, 2, 1) with { Data = data }).Count == 0, $"fora de recepció {data:O}");
        Comprovar(Seleccionar(Peticio(1, 2, 1) with { Data = inici }).Count == 1, "inici inclòs");
        Comprovar(ReglesTorn.SeleccionarMoviments(configuracio, torn, inici.AddSeconds(10), participants.ToDictionary(p => p.Key, p => (Posicio?)p.Value),
            [Peticio(1, 2, 1) with { Data = torn.Limit }, Peticio(2, 0, 1) with { Data = inici.AddSeconds(9) }])["A"]
            == new Posicio(0, 1), "control manual accepta ordres al límit antic i després fins al tall");
        Comprovar(Seleccionar(Peticio(1, 2, 1), Peticio(2, 2, 2))["A"] == new Posicio(2, 1), "ordres des de la posició inicial");
        Comprovar(Seleccionar(Peticio(5, 0, 1), Peticio(4, 1, 2), Peticio(3, 2, 1), Peticio(2, -1, 1))["A"]
            == new Posicio(1, 2), "primeres vàlides per id; invàlides no consumeixen el límit");
        Comprovar(Seleccionar(Peticio(1, 2, 1), Peticio(2, 1, 2), Peticio(3, 0, 1))["A"]
            == new Posicio(1, 2), "vàlides sobrants ignorades");

        Dictionary<Posicio, Pintura> Tauler()
        {
            var caselles = new Dictionary<Posicio, Pintura>();
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++) caselles.Add(new(x, y), Pintura.Lliure);
            return caselles;
        }

        var primerTorn = torn with { Id = 1 };
        var jugadors = new Dictionary<string, Posicio?> { ["A"] = null, ["B"] = null };
        Dictionary<string, Posicio> Entrades(params PeticioMoviment[] peticions) =>
            ReglesTorn.SeleccionarMoviments(configuracio, primerTorn, inici.AddSeconds(2), jugadors, peticions);
        Comprovar(Entrades(Peticio(1, 4, 4))["A"] == new Posicio(4, 4), "entrada lliure a la cantonada al torn 1");
        Comprovar(Entrades(Peticio(2, 0, 0), Peticio(1, 4, 4))["A"] == new Posicio(4, 4),
            "entrada escollida pel primer id, sense substituir-la ni encadenar passos");
        Comprovar(Entrades(Peticio(1, -1, 0), Peticio(2, 2, 3))["A"] == new Posicio(2, 3),
            "entrada fora del tauler ignorada abans de la primera vàlida");
        Comprovar(Entrades(Peticio(1, 0, 0) with { Data = inici.AddTicks(-1) },
            Peticio(2, 3, 3), Peticio(3, 4, 4) with { Data = inici.AddSeconds(2) })["A"] == new Posicio(3, 3),
            "entrada respecta l'obertura i el tall manual");
        Comprovar(Entrades(Peticio(1, 0, 0) with { Jugador = "C" }).Count == 0, "jugador inactiu no entra");
        Comprovar(Entrades().Count == 0, "sense INSERT no hi ha entrada");
        Comprovar(ReglesTorn.SeleccionarMoviments(configuracio, torn with { Id = 2 }, inici.AddSeconds(2),
            jugadors, [Peticio(1, 2, 3)]).Count == 0, "sense entrada al torn 1 no es pot entrar després");
        var senseRobots = new Dictionary<string, Posicio>();
        ResultatTorn entrada = ReglesTorn.Resoldre(configuracio, 1, senseRobots, Entrades(Peticio(1, 4, 4)), Tauler());
        Comprovar(entrada.Posicions.Count == 1 && entrada.Posicions["A"] == new Posicio(4, 4)
            && entrada.Caselles.Values.All(p => p == Pintura.Lliure), "aparició no pinta i no inventa robots");
        var entradesCompartides = Entrades(Peticio(1, 2, 2), Peticio(2, 2, 2) with { Jugador = "B" });
        entrada = ReglesTorn.Resoldre(configuracio, 1, senseRobots, entradesCompartides, Tauler());
        Comprovar(entrada.Posicions.Count == 2 && entrada.Posicions.Values.All(p => p == new Posicio(2, 2))
            && entrada.Caselles[new(2, 2)] == Pintura.Lliure, "entrada compartida amb col·lisió sense pintura");
        var posicionsEntrada = entrada.Posicions.ToDictionary(p => p.Key, p => (Posicio?)p.Value);
        var despresEntrada = ReglesTorn.SeleccionarMoviments(configuracio, torn with { Id = 2 }, inici.AddSeconds(2),
            posicionsEntrada, [Peticio(1, 4, 4), Peticio(2, 3, 2)]);
        Comprovar(despresEntrada["A"] == new Posicio(3, 2), "després d'entrar només es permet el pas cardinal");

        // Cas notificat: O i L tenien posicions antigues, però només T envia una entrada.
        var configuracioRecorregut = configuracio with { Alcada = 6 };
        var posicionsAntigues = new Dictionary<string, Posicio>
        {
            ["O"] = new(1, 2), ["L"] = new(1, 4), ["T"] = new(4, 4)
        };
        var jugadorsRecorregut = posicionsAntigues.ToDictionary(p => p.Key, p => (Posicio?)p.Value);
        var entradesRecorregut = ReglesTorn.SeleccionarMoviments(configuracioRecorregut, primerTorn,
            inici.AddSeconds(2), jugadorsRecorregut, [Peticio(1, 1, 2) with { Jugador = "T" }]);
        var casellesRecorregut = new Dictionary<Posicio, Pintura>();
        for (int y = 0; y < configuracioRecorregut.Alcada; y++)
            for (int x = 0; x < configuracioRecorregut.Amplada; x++)
                casellesRecorregut.Add(new(x, y), Pintura.Lliure);
        ResultatTorn recorregut = ReglesTorn.Resoldre(configuracioRecorregut, 1,
            posicionsAntigues, entradesRecorregut, casellesRecorregut);
        Comprovar(recorregut.Posicions.Count == 1 && recorregut.Posicions["T"] == new Posicio(1, 2),
            "només T entra; les posicions antigues d'O i L no els fan participants");
        for (int idTorn = 2; idTorn <= 4; idTorn++)
        {
            jugadorsRecorregut = recorregut.Posicions.ToDictionary(p => p.Key, p => (Posicio?)p.Value);
            var passos = ReglesTorn.SeleccionarMoviments(configuracioRecorregut, torn with { Id = idTorn },
                inici.AddSeconds(2), jugadorsRecorregut, [Peticio(idTorn, 1, idTorn + 1) with { Jugador = "T" }]);
            recorregut = ReglesTorn.Resoldre(configuracioRecorregut, idTorn,
                recorregut.Posicions, passos, recorregut.Caselles);
            Comprovar(recorregut.Posicions.Count == 1 && recorregut.Posicions["T"] == new Posicio(1, idTorn + 1)
                && Enumerable.Range(3, idTorn - 1).All(y => recorregut.Caselles[new(1, y)].Propietari == "T"),
                $"T pinta el recorregut al torn {idTorn}, sense col·lidir amb usuaris que no han entrat");
        }
        recorregut = ReglesTorn.Resoldre(configuracioRecorregut, 5, recorregut.Posicions,
            new Dictionary<string, Posicio>(), recorregut.Caselles);
        Comprovar(recorregut.Posicions.Count == 1 && recorregut.Posicions["T"] == new Posicio(1, 5),
            "un robot que ja ha entrat continua jugant encara que no enviï ordres en el torn següent");

        var destiPintura = new Posicio(2, 1);
        var moviment = new Dictionary<string, Posicio> { ["A"] = destiPintura };
        (Pintura Abans, Pintura Despres)[] casos = [
            (Pintura.Lliure, new("A", 1, 25)),
            (new("A", 1, 24), new("A", 2, 25)),
            (new("A", 2, 24), new("A", 2, 25)),
            (new("B", 1, 24), new("A", 1, 25)),
            (new("B", 2, 24), new("B", 2, 24))];
        foreach ((Pintura abans, Pintura despres) in casos)
        {
            var caselles = Tauler();
            caselles[destiPintura] = abans;
            ResultatTorn resultat = ReglesTorn.Resoldre(configuracio, 25, participants, moviment, caselles);
            Comprovar(resultat.Caselles[destiPintura] == despres, $"pintura {abans}");
            Comprovar(resultat.Posicions["A"] == destiPintura && caselles[destiPintura] == abans, "moviment i estat original preservat");
        }

        var tauler = Tauler();
        tauler[new(1, 1)] = new("A", 1, 24);
        var quiet = new Dictionary<string, Posicio>();
        Comprovar(ReglesTorn.Resoldre(configuracio, 25, participants, quiet, tauler).Caselles[new(1, 1)]
            == new Pintura("A", 1, 24), "quiet no pinta ni renova");
        tauler[destiPintura] = new("B", 2, 5);
        Comprovar(ReglesTorn.Resoldre(configuracio, 25, participants, moviment, tauler).Caselles[destiPintura]
            == new Pintura("A", 1, 25), "caducitat abans de conquerir reforçada enemiga");
        tauler[destiPintura] = new("B", 2, 5);
        var degradat = ReglesTorn.Resoldre(configuracio, 25, participants, quiet, tauler).Caselles;
        Comprovar(degradat[destiPintura] == new Pintura("B", 1, 25), "reforçada es degrada una sola vegada");
        Comprovar(ReglesTorn.Resoldre(configuracio, 44, participants, quiet, degradat).Caselles[destiPintura]
            == new Pintura("B", 1, 25), "nou període normal encara vigent");
        Comprovar(ReglesTorn.Resoldre(configuracio, 45, participants, quiet, degradat).Caselles[destiPintura]
            == Pintura.Lliure, "nou període normal caducat exactament al límit");
        tauler[destiPintura] = new("B", 1, 5);
        Comprovar(ReglesTorn.Resoldre(configuracio, 25, participants, quiet, tauler).Caselles[destiPintura]
            == Pintura.Lliure, "caducitat de jugador no participant");
        tauler[destiPintura] = new("B", 2, 6);
        Comprovar(ReglesTorn.Resoldre(configuracio, 25, participants, quiet, tauler).Caselles[destiPintura]
            == new Pintura("B", 2, 6), "reforç abans del límit");

        var dos = new Dictionary<string, Posicio> { ["A"] = new(1, 1), ["B"] = new(3, 1) };
        var xoc = new Dictionary<string, Posicio> { ["A"] = destiPintura, ["B"] = destiPintura };
        tauler[destiPintura] = new("B", 2, 24);
        var resultatXoc = ReglesTorn.Resoldre(configuracio, 25, dos, xoc, tauler);
        Comprovar(resultatXoc.Posicions.Values.All(p => p == destiPintura)
            && resultatXoc.Caselles[destiPintura] == Pintura.Lliure, "col·lisió neteja reforçada i comparteix destinació");
        var compartits = resultatXoc.Posicions;
        tauler[destiPintura] = new("A", 2, 24);
        Comprovar(ReglesTorn.Resoldre(configuracio, 26, compartits, quiet, tauler).Caselles[destiPintura]
            == Pintura.Lliure, "robots quiets a la mateixa casella tornen a netejar");
        dos["B"] = destiPintura;
        Comprovar(ReglesTorn.Resoldre(configuracio, 25, dos, moviment, tauler).Caselles[destiPintura]
            == Pintura.Lliure, "col·lisió amb robot quiet");
        var intercanvi = new Dictionary<string, Posicio> { ["A"] = destiPintura, ["B"] = new(1, 1) };
        tauler = Tauler();
        var resultatIntercanvi = ReglesTorn.Resoldre(configuracio, 25, dos, intercanvi, tauler);
        Comprovar(resultatIntercanvi.Caselles[destiPintura] == new Pintura("A", 1, 25)
            && resultatIntercanvi.Caselles[new(1, 1)] == new Pintura("B", 1, 25), "intercanvi sense col·lisió");

        Comprovar(ReglesTorn.Resoldre(configuracio, 25, participants, moviment, Tauler()).Caselles[destiPintura]
            == new Pintura("A", 1, 25), "només els participants intervenen en les col·lisions");
        var duradesCurtes = configuracio with { CaducitatNormal = 2, CaducitatReforcada = 3 };
        tauler[destiPintura] = new("B", 2, 22);
        Comprovar(ReglesTorn.Resoldre(duradesCurtes, 25, participants, quiet, tauler).Caselles[destiPintura]
            == new Pintura("B", 1, 25), "durades llegides de la configuració");
        Console.WriteLine($"Superades {comprovacions} comprovacions de regles C# sense SQL Server.");
    }
}
