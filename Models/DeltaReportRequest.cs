namespace LISD.Models
{
    public class DeltaReportRequest
    {
        public string PatientInvestigationIdList { get; set; } = "";
        public int isHeaderPNG { get; set; }
        public int PrintBy { get; set; }
        public int branchId { get; set; }
    }
}