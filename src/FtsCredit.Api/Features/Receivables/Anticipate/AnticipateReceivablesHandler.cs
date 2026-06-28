namespace FtsCredit.Api.Features.Receivables.Anticipate;

public class AnticipateReceivablesHandler
{
    private const decimal DailyFeeRate = 0.0003m; // 0.03% ao dia

    public Task<AnticipateReceivablesResponse> HandleAsync(AnticipateReceivablesCommand cmd, CancellationToken ct = default)
    {
        var fee = cmd.TotalReceivables * DailyFeeRate * cmd.AnticipationDays;
        var netAmount = cmd.TotalReceivables - fee;

        return Task.FromResult(new AnticipateReceivablesResponse(
            cmd.Document,
            cmd.TotalReceivables,
            Math.Round(fee, 2),
            Math.Round(netAmount, 2),
            cmd.AnticipationDays));
    }
}
