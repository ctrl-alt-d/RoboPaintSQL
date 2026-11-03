-- RoboPaintSQL: estructura inicial per a Microsoft SQL Server.
-- Executar una sola vegada en una base de dades buida, com a administrador.
-- L'eina ha d'interpretar GO. No crea la base de dades ni els comptes.
-- Les regles del joc es validen en C#, al programa de l'àrbitre.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE dbo.configuracio
(
    clau varchar(50) NOT NULL PRIMARY KEY,
    valor int NOT NULL
);

CREATE TABLE dbo.jugadors
(
    id_jugador char(1) NOT NULL PRIMARY KEY,
    posicio_x int NULL,
    posicio_y int NULL,
    actiu bit NOT NULL DEFAULT (1)
);

CREATE TABLE dbo.torns
(
    id_torn int NOT NULL PRIMARY KEY,
    estat varchar(20) NOT NULL,
    data_inici datetime2(3) NOT NULL,
    data_limit datetime2(3) NOT NULL,
    data_fi datetime2(3) NULL
);

CREATE TABLE dbo.caselles
(
    posicio_x int NOT NULL,
    posicio_y int NOT NULL,
    id_propietari char(1) NULL REFERENCES dbo.jugadors (id_jugador),
    nivell tinyint NOT NULL DEFAULT (0),
    torn_actualitzacio int NULL REFERENCES dbo.torns (id_torn),
    PRIMARY KEY (posicio_x, posicio_y)
);

CREATE TABLE dbo.moviments
(
    id_moviment bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    id_torn int NOT NULL REFERENCES dbo.torns (id_torn),
    id_jugador char(1) NOT NULL REFERENCES dbo.jugadors (id_jugador),
    nova_x int NOT NULL,
    nova_y int NOT NULL,
    data_moviment datetime2(3) NOT NULL
);

INSERT INTO dbo.configuracio (clau, valor)
VALUES
    ('amplada_tauler', 30),
    ('alcada_tauler', 20),
    ('durada_torn_ms', 3000),
    ('max_torns', 200),
    ('caducitat_reforcada', 20),
    ('caducitat_normal', 20),
    ('max_moviments_torn', 20),
    ('punts_normal', 1),
    ('punts_reforcada', 2);
GO

-- El client envia el torn i la destinació. El servidor fixa l'emissor
-- i l'hora UTC, encara que el client intenti indicar-ne uns altres.
CREATE TRIGGER dbo.identificar_moviment
ON dbo.moviments
INSTEAD OF INSERT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @usuari nvarchar(128) = USER_NAME();
    IF DATALENGTH(@usuari) <> 2 OR UNICODE(@usuari) NOT BETWEEN 65 AND 84
        THROW 50001, N'L''usuari de base de dades ha de ser una lletra ASCII de la A a la T.', 1;

    INSERT INTO dbo.moviments (id_torn, id_jugador, nova_x, nova_y, data_moviment)
    SELECT id_torn, @usuari, nova_x, nova_y, SYSUTCDATETIME()
    FROM inserted;
END;
GO

CREATE ROLE rol_alumnes AUTHORIZATION dbo;
CREATE ROLE rol_arbitre AUTHORIZATION dbo;
GO

-- Els comptes només reben aquests permisos, sense altres rols ni privilegis.
GRANT SELECT ON OBJECT::dbo.configuracio TO rol_alumnes;
GRANT SELECT ON OBJECT::dbo.jugadors TO rol_alumnes;
GRANT SELECT ON OBJECT::dbo.torns TO rol_alumnes;
GRANT SELECT ON OBJECT::dbo.caselles TO rol_alumnes;
GRANT SELECT, INSERT ON OBJECT::dbo.moviments TO rol_alumnes;

GRANT SELECT, UPDATE ON OBJECT::dbo.configuracio TO rol_arbitre;
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.jugadors TO rol_arbitre;
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.torns TO rol_arbitre;
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.caselles TO rol_arbitre;
GRANT SELECT ON OBJECT::dbo.moviments TO rol_arbitre;
GO
