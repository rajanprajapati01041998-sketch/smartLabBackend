namespace App.Models
{
    public class ServiceAllDetailsForOPDBillingModel
    {
        public decimal MRP { get; set; }
        public decimal Rate { get; set; }
        public int RateListId { get; set; }
        public bool IsRateEditable { get; set; }
        public string? SampleVolume { get; set; }
        public string? ContainerColor { get; set; }

        public string ServiceName { get; set; }
        public string Code { get; set; }
        public string CorporateAlias { get; set; }
        public string CorporateCode { get; set; }

        public int ValidityDays { get; set; }

        public decimal DiscountPer { get; set; }
        public string DiscountReason { get; set; }

        public int IsNonPayable { get; set; }

        public int ServiceItemId { get; set; }
        public int CategoryId { get; set; }
        public int SubCategoryId { get; set; }
        public int SubSubCategoryId { get; set; }

        public int IsCorporateDiscount { get; set; }
        public int IsPrivilegedCardDiscount { get; set; }

        public int DefaultSampleTypeId { get; set; }
        public string? SampleType { get; set; }   // 👈 ADD THIS

        public string SampleTypeIdList { get; set; }
        public string SampleTypeList { get; set; }
        public List<SampleTypeModel> SampleTypes { get; set; } = new();
    }
}