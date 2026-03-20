namespace App.Models
{
    public class ReferDoctorModel
    {
        public int ReferDoctorId { get; set; }
        public string Title { get; set; }
        public string Name { get; set; }
        public string DoctorName { get; set; }
        public string ContactNo { get; set; }
        public string ClinicName { get; set; }
        public string Address { get; set; }
        public int ProId { get; set; }
        public bool IsActive { get; set; }
    }
}