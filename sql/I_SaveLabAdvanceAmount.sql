CREATE OR ALTER PROCEDURE [dbo].[I_SaveLabAdvanceAmount]
(
    @hospId int,
    @branchId int,
    @userId int,
    @ClientID int,
    @DepositDate date,
    @PaymentMode  nvarchar(50)=null,
    @PaymentModeId int,
    @PaidAmount decimal(16,6)=0.00,
    @ChequeCardNo nvarchar(50)=null,
    @ChequeCardDate date,
    @PaymentBankId int,
    @PayMode nvarchar(20)=null,
    @TransactionId nvarchar(50)=null,
    @remarks nvarchar(512)=null,
    @IpAddress nvarchar(20)
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @LabReceiptNo NVARCHAR(100);
    EXEC getBranchSequenceNumber 1, 29, @LabReceiptNo OUTPUT;

    INSERT INTO LabAdvanceAmount(
          HospId
        , BranchId
        , CreatedBy
        , LabReceiptNo
        , ClientID
        , DepositDate
        , PaymentMode
        , PaymentModeId
        , Amount
        , ChequeCardNo
        , ChequeCardDate
        , PaymentBankId
        , PayMode
        , TransactionId
        , IpAddress
        , remarks
        , status
        , statusId
    )
    VALUES(
          @hospId
        , @branchId
        , @userId
        , @LabReceiptNo
        , @ClientID
        , @DepositDate
        , @PaymentMode
        , @PaymentModeId
        , @PaidAmount
        , @ChequeCardNo
        , @ChequeCardDate
        , @PaymentBankId
        , @PayMode
        , @TransactionId
        , @IpAddress
        , @remarks
        , 'Pending'
        , 0
    );
END

