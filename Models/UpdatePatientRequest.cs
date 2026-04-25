using App.Models;

public class UpdatePatientRequest
{
    public PatientMasterModel Patient { get; set; }
    public string? PatientImagePath { get; set; }
    public List<PatientDocumentMapping>? DocumentList { get; set; }
}

