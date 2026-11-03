-- Adaptació d'una base existent a l'entrada lliure del torn 1.
-- Executar com a administrador amb l'àrbitre aturat. Es pot repetir.
-- Conserva les dades de la partida, excepte l'antiga llista de participants.
-- Per començar amb robots sense posició, executar després inicialitza-joc.

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @resultat int;
    EXEC @resultat = sys.sp_getapplock @Resource = N'RoboPaintSQL.arbitre',
        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0;
    IF @resultat < 0
        THROW 50004, N'Atura l''àrbitre abans d''adaptar l''esquema.', 1;

    ALTER TABLE dbo.jugadors ALTER COLUMN posicio_x int NULL;
    ALTER TABLE dbo.jugadors ALTER COLUMN posicio_y int NULL;

    IF OBJECT_ID(N'dbo.participants_torn', N'U') IS NOT NULL
        DROP TABLE dbo.participants_torn;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
