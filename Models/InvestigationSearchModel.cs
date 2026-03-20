namespace App.Models
{
    public class InvestigationSearchModel
    {
        public int ItemId { get; set; }
        public string Name { get; set; }
        public bool IsExternal { get; set; }
        public int? CategoryId { get; set; }
        public int? SubCategoryId { get; set; }
        public int? SubSubCategoryId { get; set; }
    }
}