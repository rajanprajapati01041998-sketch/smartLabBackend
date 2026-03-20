using System;
using System.Collections.Generic;
using LISD.Models;

namespace App.Models
{
    public class PatientMasterModel
    {
        // 🔹 Basic Info
        public int? HospId { get; set; }
        public int? BranchId { get; set; }
        public int? LoginBranchId { get; set; }
        public int? PatientId { get; set; }
        public string? UHID { get; set; }

        // 🔹 Personal Info
        public string? Title { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }

        // 🔹 Age Info
        public int? AgeYears { get; set; }
        public int? AgeMonths { get; set; }
        public int? AgeDays { get; set; }
        public DateTime? DOB { get; set; }

        // 🔹 Demographics
        public string? Gender { get; set; }
        public string? MaritalStatus { get; set; }
        public string? Relation { get; set; }
        public string? RelativeName { get; set; }

        // 🔹 Identity
        public string? AadharNumber { get; set; }
        public string? IdProofName { get; set; }
        public string? IdProofNumber { get; set; }

        // 🔹 Contact
        public string? ContactNumber { get; set; }
        public string? EmergencyContactNumber { get; set; }
        public string? Email { get; set; }

        // 🔹 Address
        public string? Address { get; set; }
        public int? CountryId { get; set; }
        public string? Country { get; set; }
        public int? StateId { get; set; }
        public string? State { get; set; }
        public int? DistrictId { get; set; }
        public string? District { get; set; }
        public int? CityId { get; set; }
        public string? City { get; set; }

        // 🔹 Insurance / Corporate
        public int? InsuranceCompanyId { get; set; }
        public int? CorporateId { get; set; }
        public string? CardNo { get; set; }
        public string? PrivilegedCardNumber { get; set; }

        // 🔹 Policy Details
        public string? PolicyNo { get; set; }
        public string? PolicyCardNo { get; set; }
        public string? ExpiryDate { get; set; }
        public string? CardHolder { get; set; }

        // 🔹 System Info
        public int? UserId { get; set; }
        public string? IpAddress { get; set; }
        public string? UniqueId { get; set; }

        // 🔹 Flags
        public int? IsVaccination { get; set; }
        public int? VIPPatient { get; set; }

        // 🔹 Visit Related
        public string? Type { get; set; } = "OPD";
        public int? TypeId { get; set; } = 1;
        public int? DoctorId { get; set; }
        public int? ReferDoctorId { get; set; }   // DB use
        public string? ReferalDoctor { get; set; } // UI use
        public int? ReferLabId { get; set; }
        public int? VisitTypeId { get; set; }
        public int? FieldBoyId { get; set; }
        public DateTime? CollectionDateTime { get; set; }

        // 🔹 Referral Info
        public string? ReferalNo { get; set; }
        public string? ReferalDate { get; set; }

        // 🔹 Medical Info
        public string? MedicalHistory { get; set; }

        // 🔹 Documents
        public string? PatientImagePath { get; set; }

        // 🔹 Services
        public List<ServiceItemModel>? Services { get; set; }

        // 🔹 Payments
        public List<PaymentModeMaster>? Payments { get; set; }

        // 🔹 Investigations (✅ ADDED)
        public List<InvestigationModel>? Investigations { get; set; }

        // 🔹 Financial Fields
        public decimal? GrossAmount { get; set; }
        public decimal? DiscountPercentage { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal? TotalTaxAmount { get; set; }
        public decimal? RoundOff { get; set; }
        public decimal? NetAmount { get; set; }
    }

    // ✅ Investigation Model
    public partial class InvestigationModel
    {
        public int? FTDID { get; set; }
        public int? InvestigationId { get; set; }
        public int? LabNo { get; set; }
        public int? TokenNo { get; set; }
        public int? IsUrgent { get; set; }
        public int? ReportingBranchId { get; set; }
        public string? Barcode { get; set; }
        public string? TestRemark { get; set; }
        public int? SampleTypeId { get; set; }
        public string? LabComment { get; set; }
    }
}
