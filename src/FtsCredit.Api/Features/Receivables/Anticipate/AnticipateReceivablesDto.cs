namespace FtsCredit.Api.Features.Receivables.Anticipate;

public record AnticipateReceivablesCommand(
    string Document,
    decimal TotalReceivables,
    int AnticipationDays
);

public record AnticipateReceivablesResponse(
    string Document,
    decimal TotalReceivables,
    decimal Fee,
    decimal NetAmount,
    int AnticipationDays
);
