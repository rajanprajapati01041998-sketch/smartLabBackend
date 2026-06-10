public class BillReceiptReprintRequest
{
    public string? BranchIdList { get; set; }
    public string? UHID { get; set; }
    public string? Name { get; set; }
    public int Type { get; set; }
    public string? BillNo { get; set; }
    public string? ReceiptNo { get; set; }
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
}