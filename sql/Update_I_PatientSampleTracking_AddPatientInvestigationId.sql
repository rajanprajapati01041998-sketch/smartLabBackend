IF COL_LENGTH('dbo.PatientSampleTracking', 'PatientInvestigationId') IS NULL
BEGIN
    ALTER TABLE dbo.PatientSampleTracking
    ADD PatientInvestigationId NVARCHAR(1000) NULL;
END
ELSE
BEGIN
    ALTER TABLE dbo.PatientSampleTracking
    ALTER COLUMN PatientInvestigationId NVARCHAR(1000) NULL;
END
GO

ALTER PROCEDURE [dbo].[I_PatientSampleTracking]
(
    @PatientId INT,
    @UHID NVARCHAR(100),
    @VisitId INT,
    @FTID INT,
    @ReceiptId INT,
    @LabNo INT = NULL,
    @TotalPayment DECIMAL(18,2) = NULL,
    @PatientInvestigationIds NVARCHAR(1000) = NULL,
    @FieldBoyId INT,
    @LoginBranchId INT = 0,
    @CollectionDateTime DATETIME = NULL,
    @Result INT OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO PatientSampleTracking
    (
        PatientId,
        UHID,
        VisitId,
        FTID,
        ReceiptId,
        LabNo,
        TotalPayment,
        PatientInvestigationId,
        FieldBoyId,
        LoginBranchId,
        CollectionDateTime
    )
    VALUES
    (
        @PatientId,
        @UHID,
        @VisitId,
        @FTID,
        @ReceiptId,
        @LabNo,
        @TotalPayment,
        @PatientInvestigationIds,
        @FieldBoyId,
        @LoginBranchId,
        @CollectionDateTime
    );

    SET @Result = SCOPE_IDENTITY();
END
GO
