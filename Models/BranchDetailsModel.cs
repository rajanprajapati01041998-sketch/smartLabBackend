using System;
public class BranchDetailsModel
{
    public int BranchId { get; set; }
    public string BranchName { get; set; }
    public string BranchCode { get; set; }
    public int BranchTypeId { get; set; }
    public string Email { get; set; }
    public string ContactNo1 { get; set; }
    public string ContactNo2 { get; set; }
    public string Address { get; set; }
    public bool IsActive { get; set; }
    public int FYStartMonth { get; set; }
    public bool FYInBillReceipt { get; set; }

    public int DefaultCountryId { get; set; }
    public int DefaultStateId { get; set; }
    public int DefaultDistrictId { get; set; }
    public int DefaultCityId { get; set; }

    public int DefaultInsuranceCompanyId { get; set; }
    public int DefaultCorporateId { get; set; }

    public int CategoryTypeId { get; set; }
    public int ParentCenterId { get; set; }
    public int InvoiceRateId { get; set; }
    public int ProcessingLabId { get; set; }

    public string CenterPincode { get; set; }
    public int PaymentModeId { get; set; }
    public int ClientMRPId { get; set; }

    public int ProId { get; set; }
    public string Pro { get; set; }
    public string ReportEmail { get; set; }

    public decimal CreditLimit { get; set; }
    public decimal SecurityAmount { get; set; }
    public decimal IntimationLimit { get; set; }

    public string SettlementType { get; set; }
    public string UploadDealDocPath { get; set; }

    public string OwnerName { get; set; }
    public string PAN { get; set; }

    public int SaleExecutivesId { get; set; }
    public string SaleExecutives { get; set; }

    public bool isPrePrintedBarcode { get; set; }
    public bool isDueReport { get; set; }
    public bool isReportLock { get; set; }
    public bool isBookingLock { get; set; }
    public bool isSMSAllow { get; set; }
    public bool isEmailAllow { get; set; }

    public decimal TotalBalance { get; set; }
}