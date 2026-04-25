public class UpdatePatientServicesRequest
{
    public int? PatientId { get; set; }
    public string? UHID { get; set; }
    public int BranchId { get; set; }
    public int LoginBranchId { get; set; }
    public int UserId { get; set; }
    public string? IpAddress { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public List<UpdatePatientServiceItem> Services { get; set; } = new();
}