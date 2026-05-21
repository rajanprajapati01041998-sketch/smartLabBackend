public class UpdatePatientServiceItem
{
    public int ServiceItemId { get; set; }
    public int SubSubCategoryId { get; set; }
    public string? ServiceName { get; set; }
    public decimal Amount { get; set; }
    public int Qty { get; set; } = 1;
    public int IsUrgent { get; set; } = 0;
    public string? Barcode { get; set; }
    public string? TestRemark { get; set; }
    public int CorporateId { get; set; }   // ✅ add this

}