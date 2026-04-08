using System;
public class WalletAmountResult
{
    public decimal TotalCredit { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal Balance { get; set; }
    public decimal BalanceMain { get; set; }
    public decimal BalanceMainDashboard { get; set; }
    public int IsDebitGreater { get; set; }
}