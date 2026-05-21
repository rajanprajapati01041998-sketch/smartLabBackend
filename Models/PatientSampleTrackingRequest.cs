public class PatientSampleTrackingRequest
{
    public int FieldBoyId { get; set; }

    public string? LoginBranchIdList { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public string? UHID { get; set; }

    public string? PatientName { get; set; }
}