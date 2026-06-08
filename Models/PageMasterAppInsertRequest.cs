public class PageMasterAppInsertRequest
{
    public int BranchId { get; set; }

    public bool FieldBoy { get; set; } = true;

    public bool Relation { get; set; } = true;

    public bool MaritalStatus { get; set; } = true;

    public bool AadharNo { get; set; } = true;

    public bool Email { get; set; } = true;

    public bool Address { get; set; } = true;

    public string? BackgroundColor { get; set; }

    public string? TextColor { get; set; }

    public string? DarkBackground { get; set; }

    public string? DarkText { get; set; }
}