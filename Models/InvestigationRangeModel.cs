using System;

public class InvestigationRangeModel
{
    public string ObservationName { get; set; }
    public int ObservationId { get; set; }
    public int InvastigationId { get; set; }
    public string MinValue { get; set; }
    public string MaxValue { get; set; }
    public string DisplayRange { get; set; }
    public string Unit { get; set; }
}