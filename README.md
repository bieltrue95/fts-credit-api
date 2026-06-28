# fts-credit-api

API de validação e originação de crédito bancário construída com .NET 9, Clean Architecture e um conjunto de infraestrutura adequado para sistemas financeiros de produção.

---

## Sumário

- [Visão Geral](#visão-geral)
- [Decisões Arquiteturais](#decisões-arquiteturais)
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

O `fts-credit-api` recebe pedidos de crédito, avalia o risco do cliente via engine de score, aprova ou rejeita a solicitação e publica eventos de domínio de forma confiável em um broker de mensagens. O sistema foi desenhado para garantir consistência dos dados mesmo diante de falhas de rede ou do broker.

```
Cliente HTTP
    │
    ▼
[JWT Auth] → [CorrelationId Middleware] → [ErrorHandling Middleware]
    │
    ▼
CreditController
    │
    ├─ CreateCreditRequest ──→ Handler ──→ PostgreSQL (transaction)
    │                                           └─ OutboxMessage (mesma tx)
    │                                                   │
    │                                           [BackgroundService - 5s]
    │                                                   └─→ RabbitMQ
    │
    ├─ GetStatus ───────────→ Handler ──→ PostgreSQL (read)
    ├─ ValidateScore ────────→ Handler ──→ Redis (cache) ou Score Engine
    └─ AnticipateReceivables → Handler ──→ lógica de antecipação
```

---

## Decisões Arquiteturais

### Clean Architecture com Feature Folders (Vertical Slice)

A estrutura do projeto organiza o código por **feature**, não por camada técnica. Isso significa que `Create/`, `GetStatus/`, `ValidateScore/` e `Anticipate/` cada uma possui seu próprio Command/Query, Handler, Validator e DTO.

**Por quê?** Em arquiteturas organizadas por camada (`Controllers/`, `Services/`, `Repositories/`), adicionar uma nova feature exige tocar em múltiplos diretórios. Com feature folders, tudo que pertence a um caso de uso vive junto — a coesão é maior e o impacto de mudanças é localizado. Remover uma feature significa deletar uma pasta.

### Repository Pattern + Unit of Work

Cada entidade de domínio tem seu próprio repositório com interface definida em `Domain/Interfaces/`. O `IUnitOfWork` coordena o `CommitAsync()` que salva tudo em uma única transação.

**Por quê?** Isso desacopla o domínio da infraestrutura de persistência. Os handlers conhecem apenas as interfaces — não sabem se estão falando com PostgreSQL, SQL Server ou um in-memory store de testes. Isso é o que permite os testes unitários dos handlers rodarem sem banco de dados real, usando mocks.

### Outbox Pattern (garantia de entrega)

Ao aprovar ou rejeitar um crédito, o evento é gravado em `outbox_messages` **na mesma transação** que persiste a `credit_request`. Um `BackgroundService` lê as mensagens pendentes a cada 5 segundos e publica no RabbitMQ.

**Por quê?** Sem o Outbox, o sistema enfrentaria o *dual-write problem*: gravar no banco e publicar no RabbitMQ são duas operações separadas. Se o broker estiver fora do ar no momento do commit, o evento se perde para sempre. Com o Outbox, a mensagem é parte da transação — ou os dois acontecem, ou nenhum acontece. O broker pode falhar que a mensagem ainda estará em `outbox_messages` com status `PENDING`, esperando a próxima tentativa.

### Correlation ID em todos os logs

Cada requisição recebe um `X-Correlation-ID` (gerado automaticamente se não informado pelo cliente) que é propagado por todo o ciclo de vida no Serilog via `LogContext`.

**Por quê?** Em sistemas com múltiplos serviços e alto volume de requisições, identificar todos os logs de uma requisição específica sem um ID de correlação é inviável. O Correlation ID permite filtrar o log por `CorrelationId` e ver a história completa de uma requisição — do middleware de entrada até a publicação no RabbitMQ.

---

## Stack e Justificativas

| Tecnologia | Motivo da Escolha |
|---|---|
| **.NET 9 / ASP.NET Core** | LTS, performance líder entre frameworks web, excelente suporte a workloads assíncronos |
| **PostgreSQL 16** | Open source, excelente suporte a JSON nativo (`jsonb`) para o payload do Outbox, extensões robustas, behavior previsível em transações |
| **EF Core 9 + Npgsql** | Migrations versionadas no próprio projeto, LINQ type-safe, integração nativa com o Provider Npgsql para features específicas do Postgres |
| **Redis** | Cache em memória distribuído com TTL nativo; evita recalcular o score do mesmo cliente em requisições próximas (TTL de 300s) |
| **RabbitMQ** | Broker de mensagens maduro, suporte a exchanges Direct com routing keys, Management UI para observabilidade em desenvolvimento |
| **JWT Bearer** | Autenticação stateless — o servidor não precisa armazenar sessão. Escalável horizontalmente sem sticky sessions |
| **FluentValidation** | Separação explícita de validação do handler. Declarativo, testável isoladamente, mensagens de erro estruturadas |
| **Mapster** | Alternativa ao AutoMapper com performance superior (usa árvores de expressão compiladas). Configuração centralizada em `MappingConfig.cs` |
| **Serilog** | Logging estruturado (JSON em produção). Suporta sinks intercambiáveis (Console, File, Seq, Elasticsearch) sem mudar o código |
| **Microsoft.Extensions.Http.Resilience** | Retry policies com backoff exponencial via Polly, integrado ao `IHttpClientFactory` sem boilerplate |
| **xUnit + Moq** | xUnit é o padrão de fato no ecossistema .NET moderno. Moq permite criar mocks das interfaces de repositório sem infraestrutura real |

---

## Estrutura do Projeto

```
fts-credit-api/
├── src/
│   └── FtsCredit.Api/
│       ├── Features/                    # Casos de uso organizados por feature
│       │   ├── CreditRequest/
│       │   │   ├── Create/              # Command, Handler, Validator, DTO
│       │   │   └── GetStatus/           # Query, Handler, DTO
│       │   ├── ScoreValidation/
│       │   │   └── ValidateScore/
│       │   └── Receivables/
│       │       └── Anticipate/
│       ├── Domain/                      # Núcleo do domínio — sem dependências externas
│       │   ├── Entities/                # CreditRequest, Customer, RiskAnalysis, OutboxMessage
│       │   ├── Enums/                   # CreditStatus, RiskLevel, ProductType
│       │   └── Interfaces/              # Contratos: IRepository, IUnitOfWork, ICacheService, IEventPublisher
│       ├── Infrastructure/              # Implementações concretas das interfaces do domínio
│       │   ├── Persistence/             # AppDbContext, Migrations, Repositories, UnitOfWork
│       │   ├── Cache/                   # RedisCacheService
│       │   └── Messaging/               # RabbitMqPublisher, OutboxPublisher (BackgroundService)
│       ├── Common/
│       │   ├── Middleware/              # CorrelationId, ErrorHandling, Jwt
│       │   ├── Mapster/                 # MappingConfig centralizado
│       │   └── Extensions/             # ServiceCollectionExtensions (registro de DI)
│       ├── Controllers/
│       │   └── CreditController.cs
│       ├── Program.cs
│       └── appsettings.json
├── tests/
│   └── FtsCredit.Tests/
│       └── Features/
│           ├── CreditRequest/           # CreateCreditRequestHandlerTests
│           └── ScoreValidation/         # ValidateScoreHandlerTests
├── docker-compose.yml
└── .github/workflows/ci.yml
```

---

## Modelo de Dados

### `customers`
| Coluna | Tipo | Descrição |
|---|---|---|
| `id` | `uuid` | PK |
| `document` | `text` | CPF/CNPJ do cliente |
| `name` | `text` | Nome do cliente |
| `monthly_income` | `numeric` | Renda mensal (base para cálculo do limite) |
| `risk_level` | `int` | Enum: Low / Medium / High |
| `created_at` | `timestamptz` | |
| `updated_at` | `timestamptz` | Atualizado a cada nova solicitação |

### `credit_requests`
| Coluna | Tipo | Descrição |
|---|---|---|
| `id` | `uuid` | PK |
| `customer_id` | `uuid` | FK → customers |
| `amount` | `numeric` | Valor solicitado |
| `installments` | `int` | Número de parcelas |
| `status` | `int` | Pending / Approved / Rejected |
| `product_type` | `int` | PersonalLoan / ConsignedLoan / ReceivablesAnticipation |
| `approved_limit` | `numeric?` | Preenchido apenas se aprovado |
| `rejection_reason` | `text?` | Preenchido apenas se rejeitado |
| `created_at` | `timestamptz` | |

### `risk_analyses`
| Coluna | Tipo | Descrição |
|---|---|---|
| `id` | `uuid` | PK |
| `request_id` | `uuid` | FK → credit_requests |
| `score` | `int` | Score calculado (0–900) |
| `approved_limit` | `numeric` | Limite calculado pela engine |
| `risk_level` | `int` | Classificação resultante |
| `analysed_at` | `timestamptz` | |
| `engine_version` | `text` | Versão da engine no momento da análise |

### `outbox_messages`
| Coluna | Tipo | Descrição |
|---|---|---|
| `id` | `uuid` | PK |
| `aggregate_id` | `uuid` | FK → credit_requests (ID do agregado que gerou o evento) |
| `event_type` | `text` | `credit.approved` ou `credit.rejected` |
| `payload` | `jsonb` | Dados do evento serializados em JSON |
| `status` | `int` | Pending / Sent / Failed |
| `created_at` | `timestamptz` | |

---

## Engine de Score

O score determina a elegibilidade e o limite de crédito do cliente.

```
Score mínimo para aprovação: 500

Faixas de risco:
  ≥ 750  →  LOW    →  Limite = renda_mensal × 0.8
  ≥ 500  →  MEDIUM →  Limite = renda_mensal × 0.5
  < 500  →  HIGH   →  Reprovado automaticamente

Cache Redis:
  Chave: score:{customer_id}
  TTL:   300 segundos
```

O score é cacheado no Redis por 5 minutos. Na primeira chamada para um cliente, o score é calculado e persistido no cache. Chamadas subsequentes dentro da janela de 5 minutos retornam o valor cacheado sem recalcular — importante em cenários de alto volume onde múltiplas solicitações do mesmo cliente chegam em sequência.

> **Nota:** A implementação atual usa uma fórmula determinística baseada na renda (`(income % 1000) + 200`) como simulação. Em produção, este ponto seria substituído por chamada a um bureau de crédito externo (Serasa, SPC, etc.).

---

## Outbox Pattern

```
┌─────────────────────────────────────────────────────┐
│  Transação PostgreSQL                                │
│                                                     │
│  INSERT credit_requests (status = Approved)         │
│  INSERT outbox_messages (status = PENDING)          │
│                                                     │
│  COMMIT ◄─── atomicidade garantida pelo banco       │
└─────────────────────────────────────────────────────┘
              │
              │ (máximo 5 segundos depois)
              ▼
┌─────────────────────────────────────────────────────┐
│  OutboxPublisher (BackgroundService)                 │
│                                                     │
│  SELECT * FROM outbox_messages WHERE status=PENDING  │
│  → publish to RabbitMQ (exchange: credit)           │
│  → UPDATE status = SENT ou FAILED                   │
└─────────────────────────────────────────────────────┘
```

**Por que não publicar diretamente no handler?**

Se o handler publicasse no RabbitMQ diretamente após o commit, qualquer falha de rede entre o commit e a publicação resultaria em um evento perdido para sempre — o banco já foi atualizado, mas o broker nunca recebeu a mensagem. O Outbox garante que a mensagem existe no banco antes de qualquer tentativa de publicação, transformando o problema de entrega em uma operação idempotente e reprocessável.

---

## Endpoints

Todos os endpoints requerem autenticação JWT. Ver seção [Autenticação](#autenticação).

### `POST /api/credit/request`
Cria uma solicitação de crédito, executa a engine de score e retorna o resultado.

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
`productType`: `0` = PersonalLoan, `1` = ConsignedLoan, `2` = ReceivablesAnticipation

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
Consulta o status de uma solicitação existente.

**Response `200 OK`:**
```json
{
  "requestId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Approved",
  "approvedLimit": 4000.00,
  "rejectionReason": null,
  "createdAt": "2026-06-27T19:22:06Z"
}
```

---

### `POST /api/credit/validate-score`
Consulta o score de um cliente sem criar uma solicitação de crédito.

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
  "score": 700,
  "riskLevel": "Medium",
  "approvedLimit": 2500.00,
  "eligible": true
}
```

---

### `POST /api/credit/receivables`
Solicita antecipação de recebíveis.

**Request:**
```json
{
  "document": "12345678901",
  "totalReceivables": 8000.00,
  "anticipationRate": 0.02
}
```

**Response `200 OK`:**
```json
{
  "netAmount": 7840.00,
  "fee": 160.00,
  "anticipationRate": 0.02
}
```

---

## Autenticação

A API usa **JWT Bearer**. Para autenticar, inclua o header:

```
Authorization: Bearer <token>
```

**Gerando um token para testes** (exemplo com `dotnet-jwt` ou qualquer gerador JWT):

```json
{
  "issuer": "fts-credit-api",
  "audience": "fts-credit-clients",
  "secret": "fts-credit-super-secret-key-minimum-32-chars!!",
  "expiry": "2026-12-31T23:59:59Z"
}
```

No Swagger UI (`/swagger`), clique em **Authorize** e informe `Bearer <token>`.

> Em produção, implemente um serviço de emissão de tokens (ex: endpoint `/auth/token`) com validação de credenciais. O segredo JWT deve vir de um gerenciador de segredos (Azure Key Vault, AWS Secrets Manager), nunca hardcoded.

---

## Executando com Docker

Pré-requisito: Docker Desktop instalado e rodando.

```bash
# Sobe a API + PostgreSQL + Redis + RabbitMQ
docker compose up -d

# Acompanha os logs da API
docker compose logs -f api

# Derruba tudo (preserva o volume do Postgres)
docker compose down

# Derruba tudo e remove volumes (banco zerado)
docker compose down -v
```

Serviços disponíveis após o `up`:

| Serviço | URL |
|---|---|
| API | http://localhost:8080 |
| Swagger | http://localhost:8080/swagger |
| RabbitMQ Management | http://localhost:15672 (guest/guest) |
| PostgreSQL | localhost:5432 (fts_credit / postgres / postgres) |
| Redis | localhost:6379 |

A API aguarda os health checks do PostgreSQL, Redis e RabbitMQ antes de iniciar. As migrations são aplicadas automaticamente no startup.

---

## Executando Localmente

Pré-requisitos: .NET 9 SDK, PostgreSQL e Redis rodando localmente (ou via `docker compose up postgres redis rabbitmq -d`).

```bash
# Restaura dependências
dotnet restore

# Cria o banco e aplica migrations
dotnet ef database update --project src/FtsCredit.Api

# Roda a API
dotnet run --project src/FtsCredit.Api
```

A API sobe em `https://localhost:5001` / `http://localhost:5000` por padrão.

---

## Testes

```bash
# Executa todos os testes
dotnet test

# Com output detalhado
dotnet test --verbosity normal

# Apenas um projeto
dotnet test tests/FtsCredit.Tests
```

Os testes unitários dos handlers usam Moq para mockar os repositórios e um `NullCacheService` inline — nenhuma infraestrutura real é necessária. O foco é validar as **regras de negócio** da engine de score isoladamente:

- Cliente novo com score ≥ 500 → aprovado com limite calculado
- Cliente novo com score < 500 → rejeitado com motivo de rejeição
- Cliente existente → renda atualizada antes do cálculo
- Mensagem de rejeição deve conter o score mínimo exigido

---

## CI/CD

O pipeline GitHub Actions em `.github/workflows/ci.yml` executa em todo push e PR para `main`:

1. **Checkout** do código
2. **Setup .NET 8** (SDK)
3. **Restore** dependências
4. **Build** em modo Release
5. **Test** com PostgreSQL de serviço provisionado pelo próprio Actions

O PostgreSQL de CI é idêntico ao de produção (imagem `postgres:16-alpine`), garantindo que as queries e migrations testadas em CI sejam as mesmas que rodarão em produção.

---

## Variáveis de Ambiente

| Variável | Descrição | Padrão (dev) |
|---|---|---|
| `ConnectionStrings__Postgres` | Connection string do PostgreSQL | `Host=localhost;Port=5432;Database=fts_credit;Username=postgres;Password=postgres` |
| `ConnectionStrings__Redis` | Host e porta do Redis | `localhost:6379` |
| `Jwt__Secret` | Chave HMAC para assinar tokens JWT (mín. 32 chars) | — |
| `Jwt__Issuer` | Issuer do token JWT | `fts-credit-api` |
| `Jwt__Audience` | Audience do token JWT | `fts-credit-clients` |
| `RabbitMQ__Host` | Host do RabbitMQ | `localhost` |
| `RabbitMQ__Port` | Porta AMQP do RabbitMQ | `5672` |
| `RabbitMQ__Username` | Usuário do RabbitMQ | `guest` |
| `RabbitMQ__Password` | Senha do RabbitMQ | `guest` |

> Nunca commite valores de `Jwt__Secret` ou senhas reais. Use `dotnet user-secrets` em desenvolvimento e um gerenciador de segredos em produção.
