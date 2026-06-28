using FtsCredit.Api.Domain.Interfaces;

namespace FtsCredit.Api.Features.CreditRequest.GetStatus;

public class GetCreditStatusHandler
{
    private readonly ICreditRequestRepository _repository;

    public GetCreditStatusHandler(ICreditRequestRepository repository) => _repository = repository;

    public async Task<GetCreditStatusResponse?> HandleAsync(GetCreditStatusQuery query, CancellationToken ct = default)
    {
        var request = await _repository.GetWithDetailsAsync(query.RequestId, ct);
        if (request is null) return null;

        RiskAnalysisResponse? riskAnalysis = null;
        if (request.RiskAnalysis is not null)
        {
            riskAnalysis = new RiskAnalysisResponse(
                request.RiskAnalysis.Score,
                request.RiskAnalysis.RiskLevel,
                request.RiskAnalysis.ApprovedLimit,
                request.RiskAnalysis.AnalysedAt,
                request.RiskAnalysis.EngineVersion);
        }

        return new GetCreditStatusResponse(
            request.Id,
            request.Customer.Name,
            request.Customer.Document,
            request.Amount,
            request.Installments,
            request.Status,
            request.ProductType,
            request.ApprovedLimit,
            request.RejectionReason,
            riskAnalysis,
            request.CreatedAt);
    }
}
