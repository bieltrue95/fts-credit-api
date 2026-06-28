# fts-credit-api

API de validação e originação de crédito bancário construída com .NET 9, Clean Architecture e infraestrutura adequada para sistemas financeiros de produção.

---

## Sumário

- [Visão Geral](#visão-geral)
- [Arquitetura](#arquitetura)
- [Stack e Justificativas](#stack-e-justificativas)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Modelo de Dados](#modelo-de-dados)
- [Engine de Score](#engine-de-score)
- [Outbox Pattern](#outbox-pattern)
- [Endpoints](#endpoints)
- [Autenticação](#autenticação)
- [Executando com Docker](#executando-com-docker)
- [Executando Localmente](#executando-localmente)
- [Testes](#testes)
- [CI/CD](#cicd)
- [Variáveis de Ambiente](#variáveis-de-ambiente)

---

## Visão Geral

O `fts-credit-api` recebe pedidos de crédito, avalia risco via engine de score, aprova ou rejeita a solicitação e publica eventos de domínio de forma confiável em um broker de mensagens. A arquitetura garante consistência dos dados mesmo diante de falhas de rede ou do broker, com rastreabilidade total de cada requisição via Correlation ID.

```
Cliente HTTP
    │
    ▼
[CorrelationIdMiddleware] → [ErrorHandlingMiddleware]
    │
    ▼
[JWT Bearer Auth]
    │
    ▼
CreditController
    │
    ├─ POST /request ──────→ CreateCreditRequestHandler
    │                               │
    │                    ┌──────────┴──────────┐
    │                    │                     │
    │               IScoreEngine          IOutboxWriter
    │              (Domain Service)     (Domain Interface)
    │                    │                     │
    │              [Redis Cache]        OutboxService → PostgreSQL (mesma tx)
    │                    │                     │
    │               PostgreSQL        [OutboxPublisher - 5s]
    │                                          │
    │                                       RabbitMQ
    │
    ├─ GET /{id}/status ──→ GetCreditStatusHandler → PostgreSQL (read)
    ├─ POST /validate-score → ValidateScoreHandler → Redis / ScoreEngine
    └─ POST /receivables ──→ AnticipateReceivablesHandler
```

---

## Arquitetura

### Clean Architecture com Feature Folders (Vertical Slice)

O código é organizado por **caso de uso**, não por camada técnica. Cada feature (`Create/`, `GetStatus/`, `ValidateScore/`, `Anticipate/`) é autossuficiente: Command/Query, Handler, Validator e DTO vivem juntos.

**Por quê vertical slice?** Em arquiteturas horizontais (`Controllers/`, `Services/`, `Repositories/`), adicionar uma feature exige tocar em múltiplos diretórios. Com feature folders, o impacto de qualquer mudança é localizado. Remover uma feature é deletar uma pasta.

### Camadas e direção das dependências

```
Domain          ←── Features (Handlers)
   ↑                      ↑
Infrastructure ────────────┘
```

- **Domain**: entidades, enums, interfaces e serviços de domínio. Zero dependências externas — não conhece banco, broker ou HTTP.
- **Features**: casos de uso. Dependem apenas de interfaces do Domain.
- **Infrastructure**: implementações concretas (EF Core, Redis, RabbitMQ). Depende do Domain e é desconhecida pelas Features.

### Aggregate `CreditRequest`

`CreditRequest` é o aggregate root. `RiskAnalysis` é uma entidade filha dentro do aggregate — existe e é acessada apenas através de `CreditRequest`. Não possui repositório próprio e não navega de volta ao root (`RiskAnalysis` não tem propriedade `CreditRequest`, apenas o FK `RequestId`).

### Domain Service `IScoreEngine`

A lógica de score (cálculo, classificação de risco, elegibilidade) vive em `Domain/Services/ScoreEngine`, implementando `Domain/Interfaces/IScoreEngine`. Dois handlers diferentes (`CreateCreditRequestHandler` e `ValidateScoreHandler`) compartilham a mesma lógica sem duplicação. Registrado como Singleton pois é stateless.

### `OutboxMessage` pertence à Infrastructure

`OutboxMessage` é um artefato técnico de infraestrutura — o domínio não sabe e não deve saber que existe um broker ou uma tabela de outbox. A separação é:

| Camada | Responsabilidade |
|---|---|
| Domain | Define `IOutboxWriter` (interface para enfileirar eventos) |
| Infrastructure | `OutboxMessage`, `OutboxService` (implementa `IOutboxWriter`), `OutboxPublisher` |

Os handlers chamam `IOutboxWriter.EnqueueAsync(aggregateId, eventType, payload)` sem conhecer `OutboxMessage`.

### Repository Pattern + Unit of Work

Cada aggregate root tem seu repositório com interface em `Domain/Interfaces/`. O `IUnitOfWork` coordena o `CommitAsync()` numa única transação. Os handlers conhecem apenas interfaces — não sabem se estão falando com PostgreSQL ou um in-memory store de testes.

---

## Stack e Justificativas

| Tecnologia | Motivo da Escolha |
|---|---|
| **.NET 9 / ASP.NET Core** | LTS, performance líder em frameworks web, excelente suporte a workloads assíncronos |
| **PostgreSQL 16** | Open source, suporte nativo a `jsonb` (payload do Outbox), comportamento transacional previsível |
| **EF Core 9 + Npgsql** | Migrations versionadas no projeto, LINQ type-safe, provider nativo para features do Postgres |
| **Redis** | Cache distribuído com TTL nativo; evita recalcular o score do mesmo cliente em janelas de 5 minutos |
| **RabbitMQ** | Broker de mensagens maduro, Direct Exchange com routing keys por tipo de evento, Management UI para observabilidade |
| **JWT Bearer** | Autenticação stateless — sem sessão no servidor, escala horizontalmente sem sticky sessions |
| **FluentValidation** | Validação separada do handler: declarativa, testável isoladamente, mensagens de erro estruturadas |
| **Mapster** | Alternativa ao AutoMapper com performance superior (árvores de expressão compiladas), configuração centralizada |
| **Serilog** | Logging estruturado com sinks intercambiáveis (Console, File, Seq). Correlation ID propagado via `LogContext` |
| **Microsoft.Extensions.Http.Resilience** | Retry com backoff exponencial via Polly, integrado ao pipeline padrão do .NET |
| **xUnit + Moq** | xUnit é o padrão de fato no ecossistema .NET moderno; Moq permite mockar interfaces sem infraestrutura real |

---

## Estrutura do Projeto

```
fts-credit-api/
├── src/
│   └── FtsCredit.Api/
│       ├── Features/                         # Casos de uso (Vertical Slice)
│       │   ├── CreditRequest/
│       │   │   ├── Create/                   # Command · Handler · Validator · DTO
│       │   │   └── GetStatus/                # Query · Handler · DTO
│       │   ├── ScoreValidation/
│       │   │   └── ValidateScore/
│       │   └── Receivables/
│       │       └── Anticipate/
│       ├── Domain/                           # Núcleo — zero dependências externas
│       │   ├── Entities/                     # CreditRequest (aggregate root) · Customer · RiskAnalysis (filho do aggregate)
│       │   ├── Enums/                        # CreditStatus · RiskLevel · ProductType
│       │   ├── Interfaces/                   # IRepository · IUnitOfWork · ICacheService · IEventPublisher · IOutboxWriter · IScoreEngine
│       │   └── Services/                     # ScoreEngine (domain service — lógica pura de score)
│       ├── Infrastructure/                   # Implementações concretas das interfaces do Domain
│       │   ├── Persistence/                  # AppDbContext · Migrations · Repositories · OutboxMessage · OutboxService
│       │   ├── Cache/                        # RedisCacheService
│       │   └── Messaging/                    # RabbitMqPublisher · OutboxPublisher (BackgroundService)
│       ├── Common/
│       │   ├── Middleware/                   # CorrelationId · ErrorHandling
│       │   ├── Mapster/                      # MappingConfig centralizado
│       │   └── Extensions/                  # ServiceCollectionExtensions (registro de DI)
│       ├── Controllers/
│       │   └── CreditController.cs
│       └── Program.cs
├── tests/
│   └── FtsCredit.Tests/
│       └── Features/
│           ├── CreditRequest/                # CreateCreditRequestHandlerTests (6 cenários)
│           └── ScoreValidation/              # ValidateScoreHandlerTests (7 cenários)
├── docker-compose.yml
└── .github/workflows/ci.yml
```

---

## Modelo de Dados

### `customers`
| Coluna | Tipo | Descrição |
|---|---|---|
| `id` | `uuid` | PK |
| `document` | `varchar(14)` | CPF (11) ou CNPJ (14). Índice único |
| `name` | `varchar(200)` | |
| `monthly_income` | `numeric(18,2)` | Base para cálculo do limite de crédito |
| `risk_level` | `text` | `Low` / `Medium` / `High` — atualizado a cada análise |
| `created_at` | `timestamptz` | |
| `updated_at` | `timestamptz` | Atualizado a cada nova solicitação do mesmo cliente |

### `credit_requests`
| Coluna | Tipo | Descrição |
|---|---|---|
| `id` | `uuid` | PK |
| `customer_id` | `uuid` | FK → customers |
| `amount` | `numeric(18,2)` | Valor solicitado |
| `installments` | `int` | Parcelas (1–360) |
| `status` | `text` | `Pending` / `Approved` / `Rejected` |
| `product_type` | `text` | `PersonalLoan` / `ConsignedLoan` / `ReceivablesAnticipation` |
| `approved_limit` | `numeric(18,2)?` | Preenchido apenas se aprovado |
| `rejection_reason` | `text?` | Preenchido apenas se rejeitado |
| `created_at` | `timestamptz` | |

### `risk_analyses`
Entidade filha do aggregate `CreditRequest`. Não possui repositório próprio — só é acessada via `CreditRequest.RiskAnalysis`.

| Coluna | Tipo | Descrição |
|---|---|---|
| `id` | `uuid` | PK |
| `request_id` | `uuid` | FK → credit_requests (1:1, unique) |
| `score` | `int` | Score calculado (100–900) |
| `approved_limit` | `numeric(18,2)` | Limite calculado pela engine |
| `risk_level` | `text` | Classificação resultante |
| `analysed_at` | `timestamptz` | |
| `engine_version` | `varchar(20)` | Versão da engine no momento da análise — auditoria |

### `outbox_messages`
Tabela de infraestrutura gerenciada exclusivamente pela camada Infrastructure. Não é acessada pelo Domain.

| Coluna | Tipo | Descrição |
|---|---|---|
| `id` | `uuid` | PK |
| `aggregate_id` | `uuid` | ID da `credit_request` que originou o evento |
| `event_type` | `varchar(100)` | `credit.approved` ou `credit.rejected` |
| `payload` | `jsonb` | Dados do evento já serializados em JSON |
| `status` | `text` | `Pending` → `Sent` / `Failed` |
| `created_at` | `timestamptz` | |

---

## Engine de Score

A `ScoreEngine` é um domain service — lógica pura, sem dependências de infraestrutura.

```
Score mínimo para aprovação: 500

Classificação de risco:
  Score ≥ 750  →  LOW    →  limite = renda_mensal × 0.8
  Score ≥ 500  →  MEDIUM →  limite = renda_mensal × 0.5
  Score < 500  →  HIGH   →  reprovado, limite = 0

Cache Redis:
  Chave: score:{customer_id}
  TTL:   300 segundos
```

O score é cacheado no Redis por 5 minutos. Na primeira chamada, é calculado e persistido no cache. Chamadas subsequentes do mesmo cliente dentro da janela de 5 minutos retornam o valor cacheado — evita recalcular em cenários de requisições concorrentes do mesmo cliente.

A `IScoreEngine` é compartilhada por `CreateCreditRequestHandler` e `ValidateScoreHandler`. Não há duplicação de regras de negócio.

> **Nota sobre a simulação:** O método `Compute()` usa uma fórmula determinística baseada na renda (`(income % 1000) + 200`) apenas para demonstração. Em produção, substituir pela chamada ao bureau de crédito externo (Serasa, SPC, etc.) sem alterar o restante do sistema.

---

## Outbox Pattern

Garante que o evento de domínio seja publicado no RabbitMQ **exatamente** quando e se a transação for persistida, sem risco de perda por falha do broker.

```
┌─────────────────────────────────────────────────────┐
│  Transação PostgreSQL (CommitAsync)                 │
│                                                     │
│  INSERT credit_requests (status = Approved)         │
│  INSERT risk_analyses                               │
│  INSERT outbox_messages (status = Pending)  ◄───── │
│                            mesmo contexto EF Core   │
│  COMMIT ← atomicidade garantida pelo banco          │
└─────────────────────────────────────────────────────┘
              │
              │  BackgroundService lê a cada 5 segundos
              ▼
┌─────────────────────────────────────────────────────┐
│  OutboxPublisher                                    │
│                                                     │
│  SELECT * FROM outbox_messages WHERE status=Pending │
│  → PublishAsync(exchange, eventType, payload)       │
│  → UPDATE status = Sent   (sucesso)                 │
│     ou   status = Failed  (falha — reprocessável)   │
└─────────────────────────────────────────────────────┘
```

**Por que não publicar diretamente no handler?**

Se o handler publicasse no RabbitMQ logo após o commit, uma falha de rede entre os dois momentos causaria perda definitiva do evento — o banco já foi atualizado, o broker nunca soube. Com o Outbox, a mensagem existe no banco antes de qualquer tentativa de publicação. O broker pode ficar fora por horas; quando voltar, o `OutboxPublisher` reprocessa todas as mensagens com status `Pending`.

**Separação de responsabilidades:**

O handler não conhece `OutboxMessage`. Ele chama `IOutboxWriter.EnqueueAsync(id, eventType, payload)` — uma interface do Domain. O `OutboxService` (Infrastructure) cria o `OutboxMessage` e o adiciona ao `AppDbContext` dentro da mesma transação coordenada pelo `IUnitOfWork`.

---

## Endpoints

Todos os endpoints exigem autenticação JWT. Ver seção [Autenticação](#autenticação).

### `POST /api/credit/request`

Cria uma solicitação de crédito, executa a engine de score e retorna o resultado na mesma chamada.

**Request:**
```json
{
  "document": "12345678901",
  "customerName": "João Silva",
  "monthlyIncome": 5000.00,
  "amount": 10000.00,
  "installments": 24,
  "productType": 0
}
```

`productType`: `0` = PersonalLoan · `1` = ConsignedLoan · `2` = ReceivablesAnticipation

**Response `201 Created`:**
```json
{
  "requestId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Approved",
  "approvedLimit": 4000.00,
  "rejectionReason": null
}
```

---

### `GET /api/credit/{id}/status`

Consulta o status completo de uma solicitação, incluindo os dados da análise de risco.

**Response `200 OK`:**
```json
{
  "requestId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerName": "João Silva",
  "document": "12345678901",
  "amount": 10000.00,
  "installments": 24,
  "status": "Approved",
  "productType": "PersonalLoan",
  "approvedLimit": 4000.00,
  "rejectionReason": null,
  "riskAnalysis": {
    "score": 700,
    "riskLevel": "Medium",
    "approvedLimit": 4000.00,
    "analysedAt": "2026-06-27T19:22:06Z",
    "engineVersion": "1.0.0"
  },
  "createdAt": "2026-06-27T19:22:06Z"
}
```

---

### `POST /api/credit/validate-score`

Consulta o score de um cliente sem criar uma solicitação de crédito. Útil para simulações e pré-análises.

**Request:**
```json
{
  "document": "12345678901",
  "monthlyIncome": 5000.00
}
```

**Response `200 OK`:**
```json
{
  "document": "12345678901",
  "score": 700,
  "riskLevel": "Medium",
  "approvedLimit": 2500.00,
  "isEligible": true
}
```

---

### `POST /api/credit/receivables`

Calcula a antecipação de recebíveis com base na taxa diária e no número de dias.

**Request:**
```json
{
  "document": "12345678901",
  "totalReceivables": 8000.00,
  "anticipationDays": 30
}
```

**Response `200 OK`:**
```json
{
  "document": "12345678901",
  "totalReceivables": 8000.00,
  "fee": 72.00,
  "netAmount": 7928.00,
  "anticipationDays": 30
}
```

---

## Autenticação

A API usa **JWT Bearer**. Inclua o header em todas as requisições:

```
Authorization: Bearer <token>
```

**Parâmetros do token:**

| Claim | Valor (padrão Docker) |
|---|---|
| Issuer | `fts-credit-api` |
| Audience | `fts-credit-clients` |
| Secret | `fts-credit-super-secret-key-minimum-32-chars!!` |

No Swagger UI (`/swagger`), clique em **Authorize** e informe `Bearer <token>`.

> Em produção, implemente um endpoint de emissão de tokens com validação de credenciais. O `Jwt:Secret` deve vir de um gerenciador de segredos (Azure Key Vault, AWS Secrets Manager) — nunca hardcoded em `appsettings.json`.

---

## Executando com Docker

Pré-requisito: Docker Desktop.

```bash
# Sobe API + PostgreSQL + Redis + RabbitMQ
docker compose up -d

# Acompanha logs da API
docker compose logs -f api

# Para e remove containers (preserva volume do Postgres)
docker compose down

# Remove tudo incluindo dados do banco
docker compose down -v
```

| Serviço | URL |
|---|---|
| API | http://localhost:8080 |
| Swagger | http://localhost:8080/swagger |
| RabbitMQ Management | http://localhost:15672 (guest / guest) |
| PostgreSQL | localhost:5432 — banco `fts_credit`, user `postgres`, senha `postgres` |
| Redis | localhost:6379 |

A API aguarda os health checks de PostgreSQL, Redis e RabbitMQ antes de iniciar (`depends_on: condition: service_healthy`). As migrations EF Core são aplicadas automaticamente no startup.

---

## Executando Localmente

Pré-requisitos: .NET 9 SDK, PostgreSQL e Redis rodando localmente.

```bash
# Sobe apenas a infraestrutura via Docker
docker compose up postgres redis rabbitmq -d

# Restaura dependências
dotnet restore

# Aplica migrations
dotnet ef database update --project src/FtsCredit.Api

# Roda a API
dotnet run --project src/FtsCredit.Api
```

---

## Testes

```bash
# Executa todos os testes
dotnet test

# Com output detalhado
dotnet test --verbosity normal
```

Os testes unitários cobrem os dois handlers com foco nas **regras de negócio**. Nenhuma infraestrutura real é necessária:

- Repositórios: mockados com Moq (`ICustomerRepository`, `ICreditRequestRepository`, `IOutboxWriter`)
- `IScoreEngine`: instância real de `ScoreEngine` — testa a lógica de domínio sem mock
- Cache: `NullCacheService` inline (retorna sempre nulo, simula cache frio)

**Cenários cobertos — `CreateCreditRequestHandler` (6 testes):**
- Cliente novo com score ≥ 500 → aprovado com limite calculado
- Cliente novo com score < 500 → rejeitado com motivo
- Cliente existente → renda atualizada antes do cálculo
- Mensagem de rejeição contém o score mínimo exigido (500)
- Aprovação → evento `credit.approved` enfileirado no `IOutboxWriter`
- Rejeição → evento `credit.rejected` enfileirado no `IOutboxWriter`

**Cenários cobertos — `ValidateScoreHandler` (7 testes):**
- Três faixas de risco via `[Theory]`: LOW (elegível), MEDIUM (elegível), HIGH (não elegível)
- Limite LOW = 80% da renda
- Limite MEDIUM = 50% da renda
- Limite HIGH = 0
- Documento presente na resposta

---

## CI/CD

O pipeline em `.github/workflows/ci.yml` executa em todo push e PR para `main`:

1. **Checkout** do código
2. **Setup .NET** (SDK 8.x)
3. **Restore** dependências
4. **Build** em modo Release
5. **Test** com PostgreSQL provisionado como service container do Actions

O PostgreSQL de CI usa a mesma imagem (`postgres:16-alpine`) do ambiente de produção — garante que migrations e queries testadas em CI são as mesmas que rodam em produção.

---

## Variáveis de Ambiente

| Variável | Descrição | Padrão local |
|---|---|---|
| `ConnectionStrings__Postgres` | Connection string PostgreSQL | `Host=localhost;Port=5432;Database=fts_credit;Username=postgres;Password=postgres` |
| `ConnectionStrings__Redis` | Host e porta Redis | `localhost:6379` |
| `Jwt__Secret` | Chave HMAC-SHA256 (mínimo 32 chars) | — |
| `Jwt__Issuer` | Issuer do JWT | `fts-credit-api` |
| `Jwt__Audience` | Audience do JWT | `fts-credit-clients` |
| `RabbitMQ__Host` | Host do RabbitMQ | `localhost` |
| `RabbitMQ__Port` | Porta AMQP | `5672` |
| `RabbitMQ__Username` | Usuário RabbitMQ | `guest` |
| `RabbitMQ__Password` | Senha RabbitMQ | `guest` |

> Use `dotnet user-secrets` em desenvolvimento local. Nunca commite credenciais reais.
