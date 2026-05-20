/*
  Stores FieldBoy (Phlebo) live tracking points (lat/long).

  Join key:
    dbo.FieldBoyMaster.FieldBoyId = dbo.FieldBoyLocationHistory.FieldBoyId
*/

IF OBJECT_ID('dbo.FieldBoyLocationHistory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FieldBoyLocationHistory
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FieldBoyLocationHistory PRIMARY KEY,
        FieldBoyId INT NOT NULL,
        Latitude DECIMAL(18,10) NOT NULL,
        Longitude DECIMAL(18,10) NOT NULL,
        AccuracyMeters DECIMAL(18,2) NULL,
        CapturedAtUtc DATETIME2(3) NOT NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_FieldBoyLocationHistory_CreatedAtUtc DEFAULT (SYSUTCDATETIME())
    );

    -- Fast “latest location” lookup per FieldBoyId
    CREATE INDEX IX_FieldBoyLocationHistory_FieldBoyId_CapturedAtUtc
        ON dbo.FieldBoyLocationHistory (FieldBoyId, CapturedAtUtc DESC, Id DESC);
END

