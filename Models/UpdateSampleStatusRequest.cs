public class UpdateSampleStatusRequest
{
    public int Id { get; set; }

    public bool? SamplePickup { get; set; }

    public bool? SampleDelivered { get; set; }

    public DateTime? SamplePickupDateTime { get; set; }

    public DateTime? SampleDeliveredDateTime { get; set; }
}