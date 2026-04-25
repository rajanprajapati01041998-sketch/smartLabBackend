
using System;
public class PatientDocumentMapping
{
    public int documentId { get; set; }

    public string? documentName { get; set; }   // ✅ file name

    public string? documentPath { get; set; }   // ✅ saved path

    public string? documentBytes { get; set; }  // ✅ base64 from frontend

    public string? fileExtension { get; set; }  // optional (pdf, jpg)
}