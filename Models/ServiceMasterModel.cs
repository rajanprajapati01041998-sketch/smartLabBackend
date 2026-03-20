using System;

namespace App.Models
{
    public class ServiceMasterModel
    {
        public int? HospId { get; set; }
        public int? ServiceItemId { get; set; }

        public int? CategoryId { get; set; }
        public int? SubCategoryId { get; set; }
        public int? SubSubCategoryId { get; set; }

        public string Name { get; set; }
        public string Code { get; set; }

        public bool? IsActive { get; set; }

        public int? CreatedBy { get; set; }
        public DateTime? CreatedOn { get; set; }

        public int? LastModifiedBy { get; set; }
        public DateTime? LastModifiedOn { get; set; }

        public string IpAddress { get; set; }
        public string UniqueId { get; set; }

        public DateTime? ValidityStartsFrom { get; set; }
        public DateTime? ValidityEndsOn { get; set; }

        public int? CardValidityMonths { get; set; }
        public int? CardAllowedMembers { get; set; }

        public bool? IsOutSource { get; set; }

        public int? PrintSequence { get; set; }

        public int? ReportTypeId { get; set; }
        public string ReportType { get; set; }

        public bool? IsSampleRequired { get; set; }
        public int? SampleTypeId { get; set; }

        public int? LabMethodId { get; set; }

        public int? ForGenderId { get; set; }
        public string ForGender { get; set; }

        public bool? IsAllowedOnlineReport { get; set; }
        public bool? IsPrintAlone { get; set; }
        public bool? IsSampleSegregationRequired { get; set; }
        public bool? IsDepartmentReceivingRequired { get; set; }

        public string ItemDiscription { get; set; }
        public string HSNCode { get; set; }
        public string CommodityCode { get; set; }

        public int? ManufacturerId { get; set; }
        public int? DrugCategoryId { get; set; }

        public string VEDType { get; set; }

        public bool? Expirable { get; set; }

        public int? PurchaseUnitId { get; set; }
        public int? SaleUnitId { get; set; }

        public string Packing { get; set; }

        public string PurchaseUnitName { get; set; }
        public string SaleUnitName { get; set; }

        public decimal? GSTPer { get; set; }

        public int? DoctorId { get; set; }

        public decimal? MinLevel { get; set; }
        public decimal? MaxLevel { get; set; }
        public decimal? ReorderLevel { get; set; }

        public string WARRANTY { get; set; }
        public string AMC { get; set; }
        public string Insurance { get; set; }

        public string StockType { get; set; }

        public int? DepartmentId { get; set; }

        public decimal? IssueFactor { get; set; }

        public string CatalogCode { get; set; }

        public bool? IsAutoScheduled { get; set; }

        public string DisplayName { get; set; }
        public string ShortName { get; set; }

        public string SampleVolume { get; set; }
        public string ContainerColour { get; set; }
        public string SampleRemark { get; set; }

        public int? AgeGroupInDays { get; set; }

        public bool? IsAllergyTest { get; set; }

        public string InvestigationComment { get; set; }

        public string ItemTypeName { get; set; }
        public int? ItemTypeId { get; set; }

        public int? TatInHour { get; set; }

        public bool? IsDelete { get; set; }

        public string SampleTypeIdList { get; set; }
    }
}