using System;
public class LoginRequest
{
    public string UserName { get; set; }
    public string UserPassword { get; set; }
    public int BranchId { get; set; }   // ✅ FIXED

    public string Browser { get; set; }
    public string Device { get; set; }
    public string Os { get; set; }
}