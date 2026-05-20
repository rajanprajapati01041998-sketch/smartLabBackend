/*
  Tracking locations storage for live tracking.

  Creates:
    - dbo.TrackingLocations table
    - index for fast latest-location reads
*/

IF OBJECT_ID('dbo.TrackingLocations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TrackingLocations
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TrackingLocations PRIMARY KEY,
        UserId INT NOT NULL,
        Latitude DECIMAL(18,10) NOT NULL,
        Longitude DECIMAL(18,10) NOT NULL,
        AccuracyMeters DECIMAL(18,2) NULL,
        CapturedAtUtc DATETIME2(3) NOT NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_TrackingLocations_CreatedAtUtc DEFAULT (SYSUTCDATETIME())
    );

    CREATE INDEX IX_TrackingLocations_UserId_CapturedAtUtc
        ON dbo.TrackingLocations (UserId, CapturedAtUtc DESC, Id DESC);
END

