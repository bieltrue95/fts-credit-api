using FtsCredit.Api.Domain.Entities;
using FtsCredit.Api.Features.CreditRequest.GetStatus;
using Mapster;

namespace FtsCredit.Api.Common.Mapster;

public static class MappingConfig
{
    public static void Configure()
    {
        TypeAdapterConfig<Domain.Entities.CreditRequest, GetCreditStatusResponse>
            .NewConfig()
            .Map(dest => dest.CustomerName, src => src.Customer.Name)
            .Map(dest => dest.Document, src => src.Customer.Document);

        TypeAdapterConfig<RiskAnalysis, RiskAnalysisResponse>
            .NewConfig();
    }
}
