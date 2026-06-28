Você é um desenvolvedor sênior .NET. Vamos construir uma API chamada fts-credit-api — um sistema de validação e originação de crédito bancário.

## Stack
- .NET 9 / ASP.NET Core / C#
- Clean Architecture com features organizadas por caso de uso
- PostgreSQL com EF Core 9 (Repository Pattern + Unit of Work)
- Redis para cache de score (StackExchange.Redis)
- RabbitMQ com Outbox Pattern
- JWT Bearer Authentication
- CORS
- FluentValidation
- Mapster
- Serilog com Correlation ID
- Microsoft.Extensions.Http.Resilience
- xUnit + Moq para testes
- Docker Compose (API + PostgreSQL + Redis + RabbitMQ)
- GitHub Actions CI básico

## Estrutura de pastas
src/FtsCredit.Api/
├── Features/
│   ├── CreditRequest/
│   │   ├── Create/  (Command · Handler · Validator · Dto · Controller)
│   │   └── GetStatus/  (Query · Handler · Dto)
│   ├── ScoreValidation/
│   │   └── ValidateScore/  (Command · Handler · Validator · Dto)
│   └── Receivables/
│       └── Anticipate/  (Command · Handler · Validator · Dto)
├── Domain/
│   ├── Entities/  (CreditRequest · Customer · RiskAnalysis · OutboxMessage)
│   ├── Enums/  (CreditStatus · RiskLevel · ProductType)
│   └── Interfaces/  (IRepository · IUnitOfWork · ICacheService · IEventPublisher)
├── Infrastructure/
│   ├── Persistence/  (AppDbContext · Migrations · CreditRequestRepository)
│   ├── Cache/  (RedisCacheService)
│   └── Messaging/  (OutboxPublisher · RabbitMqConsumer)
├── Common/
│   ├── Middleware/  (ErrorHandlingMiddleware · JwtMiddleware · CorrelationIdMiddleware)
│   ├── Mapster/  (MappingConfig)
│   └── Extensions/  (ServiceCollectionExtensions)
├── Program.cs
└── appsettings.json

tests/FtsCredit.Tests/
└── Features/
    ├── CreditRequest/  (CreateCreditRequestHandlerTests)
    └── ScoreValidation/  (ValidateScoreHandlerTests)

## Modelo de banco (PostgreSQL)
- customers: id, document, name, monthly_income, risk_level, created_at, updated_at
- credit_requests: id, customer_id (FK), amount, installments, status, product_type, approved_limit, rejection_reason, created_at
- risk_analyses: id, request_id (FK), score, approved_limit, risk_level, analysed_at, engine_version
- outbox_messages: id, aggregate_id (FK), event_type, payload (jsonb), status (PENDING/SENT/FAILED), created_at

## Redis
- key: score:{customer_id} | tipo: Hash | TTL: 300s
- campos: score, risk_level, approved_limit

## Endpoints
- POST /api/credit/request
- GET  /api/credit/{id}/status
- POST /api/credit/validate-score
- POST /api/credit/receivables

## Regras de negócio da engine de score
- Score mínimo para aprovação: 500
- Limite = monthly_income × fator_de_risco
- LOW  (score 750+):    fator 0.8
- MEDIUM (500–749):     fator 0.5
- HIGH (abaixo de 500): reprovado automaticamente
- Ao aprovar ou rejeitar: salva evento em outbox_messages na mesma transação

## Outbox Pattern
- Background service lê outbox_messages com status PENDING a cada 5 segundos
- Publica no RabbitMQ (exchange: credit, routing keys: credit.approved / credit.rejected)
- Atualiza status para SENT ou FAILED
- Retry via Microsoft.Extensions.Http.Resilience

## Regras gerais
- Todos os logs via Serilog com Correlation ID injetado por middleware
- CORS configurado para aceitar localhost:3000
- Swagger habilitado
- Migrations via EF Core
- Nenhum commit deve ter Co-authored-by

## Ordem de implementação
1. Estrutura de pastas e .csproj com todos os pacotes
2. Domain: entidades, enums e interfaces
3. Infrastructure: AppDbContext, migrations, repositories, Redis, RabbitMQ, OutboxPublisher
4. Features: handlers, validators, DTOs e controllers
5. Common: middlewares, Mapster, extensions
6. Program.cs completo
7. docker-compose.yml e Dockerfile
8. .github/workflows/ci.yml
9. Testes unitários dos handlers

Gere os arquivos completos, sem resumir código. Um arquivo por vez, na ordem acima.



numca faça commit com Co-authored-by.