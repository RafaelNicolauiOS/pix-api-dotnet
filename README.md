# pix-api-dotnet

[![CI](https://github.com/RafaelNicolauiOS/pix-api-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/RafaelNicolauiOS/pix-api-dotnet/actions/workflows/ci.yml)

Versão em **.NET 8 / ASP.NET Core** do [pix-api](https://github.com/RafaelNicolauiOS/pix-api)
(Java 17 / Spring Boot), com **o mesmo contrato de API**: mesmas rotas, payloads, status HTTP,
erros RFC 9457, cabeçalho `X-Admin-Token` e o mesmo arquivo de largura fixa do batch COBOL
(`MOVIMENTO.cpy`). A ferramenta de carga em Go do projeto Java rodou contra esta versão sem nenhuma
mudança; o front-end e o job COBOL usam as mesmas rotas e o mesmo layout, mas ainda não foram
executados contra ela.

| | |
|---|---|
| Linguagem / runtime | C# 12, .NET 8 (LTS) |
| Web | ASP.NET Core Web API com controllers, Swagger/OpenAPI (Swashbuckle) |
| Persistência | EF Core 8 + PostgreSQL (Npgsql) com migrations; SQLite nos testes e no desenvolvimento |
| Mensageria | `IPublicadorDeEventos` com dois adaptadores: log (padrão) e RabbitMQ |
| Testes | xUnit + Moq + `WebApplicationFactory`, escritos em TDD (vermelho → verde) |
| Entrega | Dockerfile multi-stage sem root, GitHub Actions rodando `dotnet test` com PostgreSQL e RabbitMQ |

## Sumário

1. [Instalação do .NET 8](#1-instalação-do-net-8)
2. [Arquitetura em camadas e o paralelo com o Java](#2-arquitetura-em-camadas-e-o-paralelo-com-o-java)
3. [Contrato da API](#3-contrato-da-api)
4. [Persistência: EF Core, migrations e dinheiro em decimal](#4-persistência-ef-core-migrations-e-dinheiro-em-decimal)
5. [Transferência atômica com lock pessimista](#5-transferência-atômica-com-lock-pessimista)
6. [Eventos depois do commit e RabbitMQ](#6-eventos-depois-do-commit-e-rabbitmq)
7. [Swagger](#7-swagger)
8. [Testes em TDD](#8-testes-em-tdd)
9. [Docker e GitHub Actions](#9-docker-e-github-actions)
10. [Teste de carga com a ferramenta em Go](#10-teste-de-carga-com-a-ferramenta-em-go)
11. [Como rodar](#como-rodar) · [Configuração](#configuração) · [Para a entrevista](#para-a-entrevista) · [Limitações](#limitações-e-próximos-passos)

## 1. Instalação do .NET 8

```bash
brew install dotnet@8
```

A fórmula `dotnet@8` é *keg-only* (não entra no `PATH` sozinha, para não conflitar com o `dotnet`
mais novo). No `~/.zshrc`:

```bash
export DOTNET_ROOT="/opt/homebrew/opt/dotnet@8/libexec"
export PATH="/opt/homebrew/opt/dotnet@8/bin:$PATH"
```

```bash
dotnet --info
```

Saída relevante na máquina onde o projeto foi feito: `.NET SDK 8.0.133`, runtimes
`Microsoft.AspNetCore.App 8.0.33` e `Microsoft.NETCore.App 8.0.33`, `RID: osx-arm64`.

> Por que a fórmula e não o cask `dotnet-sdk@8`? O cask instala um `.pkg` (pede senha de
> administrador); a fórmula vem pré-compilada do Homebrew e instala sem `sudo`.

O [`global.json`](global.json) fixa o SDK 8.0 (com `rollForward: latestFeature`): quem clonar o
repositório com um SDK 9 instalado ainda compila com o 8, como o `.java-version` / Maven Wrapper.

## 2. Arquitetura em camadas e o paralelo com o Java

```mermaid
flowchart LR
    Cliente["Front-end · curl · carga (Go) · COBOL"] -- HTTP/JSON --> Api
    subgraph Solution["PixApi.sln"]
        Api["PixApi.Api<br/>controllers, DTOs, validação,<br/>ProblemDetails, CORS, Swagger"]
        App["PixApi.Application<br/>casos de uso + portas (interfaces)"]
        Dom["PixApi.Domain<br/>Conta, Lancamento"]
        Inf["PixApi.Infrastructure<br/>EF Core, Npgsql, RabbitMQ, logs"]
        Api --> App
        Api --> Inf
        Inf --> App
        App --> Dom
    end
    Inf --> PG[("PostgreSQL")]
    Inf -. "Pix:Mensageria=rabbitmq" .-> MQ[["RabbitMQ<br/>exchange pix.transferencias"]]
```

As setas são **referências de projeto** (`<ProjectReference>`), e o compilador as impõe: o Domain
não referencia nada, e a Application não referencia o EF Core. Por isso é impossível usar um
`DbContext` dentro de um caso de uso por engano. No Java, os pacotes eram só convenção: nada
impedia o `controller` de chamar o `repository` direto.

**Inversão de dependência:** a Application declara as *portas* (`IContaRepositorio`,
`IUnidadeDeTrabalho`, `IPublicadorDeEventos`) e a Infrastructure as implementa. O caso de uso
depende da interface, nunca do EF ou do RabbitMQ. Em Swift seria um `protocol` na camada de
domínio com a implementação injetada no `init`.

| Camada .NET | Pacote no pix-api (Java) | Paralelo em Swift/iOS | O que tem |
|---|---|---|---|
| `PixApi.Api` | `controller`, `controller.dto`, `config` | Camada de rede, `Codable` | Controllers, DTOs, `ApiExceptionFilter`, CORS, `DadosIniciais`, `Program.cs` |
| `PixApi.Application` | `service`, `batch` | UseCase / ViewModel | `ContaService`, `TransferenciaService`, `ExportacaoMovimentoService`, `LayoutMovimento`, portas, eventos |
| `PixApi.Domain` | `domain` | Model | `Conta` (debitar/creditar), `Lancamento` (razão), `TipoLancamento` |
| `PixApi.Infrastructure` | `repository`, `mensageria`, `monitoramento` | DataSource, adapters de um `protocol` | `PixDbContext`, repositórios, migrations, adaptadores log/RabbitMQ, logs em memória |

### Tradução de conceitos Spring → ASP.NET Core

| Spring Boot (Java) | ASP.NET Core (.NET) | Observação |
|---|---|---|
| `@SpringBootApplication` + `main` | [`Program.cs`](src/PixApi.Api/Program.cs) | Registra serviços e monta o pipeline de middlewares |
| `@Service` / `@Component` (component scan) | `builder.Services.AddScoped<T>()` | Registro explícito: dá para ler no `Program.cs` tudo o que existe |
| Injeção pelo construtor | *Primary constructor* `class X(IDep dep)` | C# 12; os parâmetros viram campos |
| Bean singleton | `AddSingleton` / `AddScoped` (1 por requisição) / `AddTransient` | O `DbContext` é *Scoped*: um por requisição, como o `EntityManager` |
| `@ConditionalOnProperty` | Fábrica no DI lendo `Pix:Mensageria` + *keyed service* | Ver [`DependencyInjection.cs`](src/PixApi.Infrastructure/DependencyInjection.cs) |
| `@RestController` + `@Valid` | `[ApiController]` | Valida e responde 400 antes de entrar no método |
| Bean Validation (`@NotBlank`, `@Digits`) | DataAnnotations (`[Required]`, atributos próprios) | Mesmas mensagens em pt-BR |
| `@RestControllerAdvice` | `IExceptionFilter` ([`ApiExceptionFilter`](src/PixApi.Api/Erros/ApiExceptionFilter.cs)) + `IExceptionHandler` para 500 | |
| `ProblemDetail` | `ProblemDetails` | Ajustado para não incluir `type`/`traceId` (contrato idêntico) |
| Spring Data JPA / Hibernate | EF Core | `DbContext` ≈ `EntityManager`; `ChangeTracker` ≈ *dirty checking* |
| `@Entity`, `@Column` na entidade | `IEntityTypeConfiguration<T>` fora do domínio | A `Conta` não sabe que existe banco |
| Flyway (`V1__*.sql`) | EF Core Migrations (`__EFMigrationsHistory`) | Geradas em C#, aplicadas na subida |
| `@Transactional` | `IUnidadeDeTrabalho` + `await using` transação | Explícito: dá para ver onde começa e termina |
| `@Lock(PESSIMISTIC_WRITE)` | `FromSql("... FOR UPDATE")` | O EF Core não tem anotação de lock |
| `@TransactionalEventListener(AFTER_COMMIT)` | Publicar **depois** do `ConfirmarAsync` | Ver seção 6 |
| `BigDecimal` | `decimal` | Tipo nativo: `+`, `<`, `==` funcionam direto |
| `Instant` | `DateTimeOffset` em UTC + `TimeProvider` | `TimeProvider` (BCL do .NET 8) torna o relógio injetável |
| `record` | `record` | Igualdade por valor nos dois |
| JUnit 5 / Mockito / MockMvc | xUnit / Moq / `WebApplicationFactory` | |
| `@SpringBootTest` + H2 | `WebApplicationFactory<Program>` + SQLite em memória | Sobe a aplicação inteira, sem abrir porta |
| `@EmbeddedKafka` | RabbitMQ real (service no CI) | Não existe RabbitMQ embutido para .NET |
| Logback appender | `ILoggerProvider` ([`LogsEmMemoria`](src/PixApi.Infrastructure/Monitoramento/LogsEmMemoria.cs)) | |
| Maven Wrapper + `pom.xml` | `global.json` + `.csproj` + `Directory.Build.props` | `Directory.Build.props` ≈ POM pai |

<details>
<summary>Estrutura de arquivos</summary>

```
pix-api-dotnet/
├── PixApi.sln, global.json, Directory.Build.props, Dockerfile
├── .config/dotnet-tools.json         dotnet-ef fixado (dotnet tool restore)
├── .github/workflows/ci.yml          testes com PostgreSQL + RabbitMQ, build da imagem
├── src/
│   ├── PixApi.Domain/                Conta, Lancamento, TipoLancamento, SaldoInsuficienteException
│   ├── PixApi.Application/
│   │   ├── Portas/                   IContaRepositorio, ILancamentoRepositorio, IUnidadeDeTrabalho, IPublicadorDeEventos
│   │   ├── Contas/                   ContaService + exceções
│   │   ├── Transferencias/           TransferenciaService, ComprovanteTransferencia
│   │   ├── Batch/                    LayoutMovimento (copybook), ExportacaoMovimentoService
│   │   ├── Eventos/                  TransferenciaRealizadaEvent
│   │   └── Monitoramento/            HistoricoDeEventos
│   ├── PixApi.Infrastructure/
│   │   ├── Persistencia/             PixDbContext, configurações, repositórios, UnidadeDeTrabalhoEf, Migrations/
│   │   ├── Mensageria/               adaptadores log/RabbitMQ, decorator, consumidor idempotente, JSON
│   │   ├── Monitoramento/            LogsEmMemoria (GET /logs)
│   │   └── DependencyInjection.cs
│   └── PixApi.Api/
│       ├── Controllers/              Contas, Transferencias, Eventos, Logs, Batch
│       ├── Dtos/                     requests/responses + atributos de validação
│       ├── Erros/                    ApiExceptionFilter, ErroInesperadoHandler, RespostaDeValidacao
│       ├── Configuracao/             Cors, DadosIniciais
│       └── Program.cs
└── tests/PixApi.Tests/               Domain/, Application/, Api/, Mensageria/, Integracao/, Suporte/
```
</details>

## 3. Contrato da API

Idêntico ao [pix-api](https://github.com/RafaelNicolauiOS/pix-api#endpoints):

| Método | Rota | Corpo | Respostas |
|---|---|---|---|
| `GET` | `/contas` | — | 200 (ordenado por id) |
| `GET` | `/contas/{id}` | — | 200 · 404 |
| `POST` | `/contas` | `{titular, cpf, chavePix, saldo}` | 201 + `Location` · 400 · 409 |
| `PUT` | `/contas/{id}` | `{titular, cpf, chavePix, saldo}` (altera titular e chave) | 200 · 400 · 404 · 409 |
| `DELETE` | `/contas/{id}` | — | 204 · 404 |
| `POST` | `/transferencias` | `{chaveOrigem, chaveDestino, valor}` | 200 · 400 · 404 · 422 |
| `GET` | `/eventos` | — | 200: últimas 100 transferências confirmadas |
| `GET` | `/logs?limite=200` | cabeçalho `X-Admin-Token` | 200 (máx. 500 linhas) · 401 · 404 se desativado |
| `GET` | `/batch/movimento?data=AAAA-MM-DD` | — | 200 `text/plain` largura fixa (padrão: ontem) · 400 |

Respostas reais desta versão (capturadas com `curl` contra a API rodando com PostgreSQL):

```json
{"chaveOrigem":"ana@pix.com","chaveDestino":"bruno@pix.com","valor":100.50,"saldoOrigem":899.50,"realizadaEm":"2026-10-30T20:37:21.870979Z"}
```

```json
{"title":"Saldo insuficiente","status":422,"detail":"Saldo insuficiente na chave +5511999990000 para transferir R$ 999","instance":"/transferencias"}
```

```json
{"title":"Bad Request","status":400,"detail":"Dados inválidos","instance":"/contas","erros":{"chavePix":"não deve estar em branco","cpf":"deve conter 11 dígitos","saldo":"deve ser maior ou igual a 0","titular":"não deve estar em branco"}}
```

**Detalhes que exigiram trabalho para o contrato ficar idêntico** (cada um tem teste):

- **Datas com `Z`.** O `System.Text.Json` escreve `+00:00`; o `Instant` do Java escreve `Z`. Um
  `JsonConverter` ([`InstanteUtcJsonConverter`](src/PixApi.Infrastructure/Mensageria/JsonPix.cs))
  resolve isso para a API e para as mensagens.
- **400 de validação.** O padrão do ASP.NET é `"errors": {"Cpf": ["..."]}`.
  [`RespostaDeValidacao`](src/PixApi.Api/Erros/RespostaDeValidacao.cs) gera `"erros": {"cpf": "..."}`,
  e o `SystemTextJsonValidationMetadataProvider` faz as chaves usarem o nome do JSON (`chavePix`), não
  o da propriedade C# (`ChavePix`).
- **ProblemDetails "limpo".** O `ControllerBase.Problem()` acrescentaria `type` e `traceId`.
  [`Problemas`](src/PixApi.Api/Erros/Problemas.cs) monta só `title/status/detail/instance`.
- **Campos ausentes.** Os DTOs usam `decimal?` e `string?`. Com `decimal`, um `saldo` ausente viraria
  `0` e passaria pela validação calado.
- **`?data=` só em ISO.** Com `DateOnly` direto no parâmetro, o ASP.NET aceitaria também `10/26/2026`
  (cultura invariante = formato americano). A data chega como texto e é lida com `TryParseExact`.
- **Saldo com 2 casas.** O SQLite devolve decimal como texto (`1000.0`), e o JSON perdia o zero que o
  `NUMERIC(15,2)` do PostgreSQL e do H2 mantêm (`1000.00`). Achado no job de Docker do CI; um
  conversor só para SQLite devolve a escala 2.
- **Mensagens com `R$ 50.01`.** `decimal.ToString()` usa a cultura da máquina, e em pt-BR seria
  `50,01`. O domínio usa `InvariantCulture`.

O arquivo do batch segue o copybook [`MOVIMENTO.cpy`](https://github.com/RafaelNicolauiOS/pix-api/blob/main/cobol/copybooks/MOVIMENTO.cpy)
byte a byte: registros de 150 posições, `9(13)V99` com vírgula implícita, ASCII puro. O
[`LayoutMovimento`](src/PixApi.Application/Batch/LayoutMovimento.cs) remove acentos com
`Normalize(FormD)` e percorre o texto por `Rune` (code point). Um emoji ocupa 2 `char` em UTF-16 e
viraria `??`, deslocando as colunas; no Java a regex já trabalha por code point.

## 4. Persistência: EF Core, migrations e dinheiro em decimal

- **Mapeamento fora do domínio.** [`ContaConfiguration`](src/PixApi.Infrastructure/Persistencia/ContaConfiguration.cs)
  e [`LancamentoConfiguration`](src/PixApi.Infrastructure/Persistencia/LancamentoConfiguration.cs)
  produzem **o mesmo schema do Flyway do Java**: `conta`, `chave_pix`, `NUMERIC(15,2)`,
  `timestamptz`, `uuid`, `uk_conta_chave_pix`, os mesmos índices e nenhuma FK de `lancamento` para
  `conta` (o razão sobrevive ao encerramento da conta).
- **Migrations.** [`Persistencia/Migrations`](src/PixApi.Infrastructure/Persistencia/Migrations)
  são geradas com `dotnet ef` e aplicadas na subida por
  [`InicializacaoDoBanco`](src/PixApi.Infrastructure/Persistencia/InicializacaoDoBanco.cs), como o
  Flyway no Spring Boot. O CI roda `dotnet ef migrations has-pending-model-changes` e **falha se
  alguém alterar o modelo e esquecer a migration**.

  ```bash
  ConnectionStrings__Pix="Host=localhost;Database=pix" dotnet ef migrations add NomeDaMudanca -p src/PixApi.Infrastructure -s src/PixApi.Api -o Persistencia/Migrations
  ```

- **`decimal` para dinheiro.** Base 10, 28–29 dígitos: `0.1m + 0.2m == 0.3m`. Diferente do
  `BigDecimal`, é um tipo nativo (sem `.add()`). Mapeado para `NUMERIC(15,2)`.
- **SQLite nos testes.** Rápido e sem Docker. Duas concessões, isoladas na Infrastructure: o
  schema vem de `EnsureCreated` (as migrations são SQL de PostgreSQL), e `DateTimeOffset` é gravado
  como número, porque o SQLite não sabe ordenar datas com fuso. **O que o SQLite não prova (`FOR
  UPDATE`, migrations) roda no PostgreSQL real**, nos testes `[PostgresFact]` e no CI.
- **Corrida na chave Pix.** Duas requisições com a mesma chave podem passar juntas pelo
  `ExisteChavePix`. O `UNIQUE` do banco barra a segunda; a Infrastructure traduz o erro `23505` em
  `ConflitoDeChaveUnicaException`, e a Application, em **409**. (No Java essa corrida virava 500.)

## 5. Transferência atômica com lock pessimista

[`TransferenciaService.TransferirAsync`](src/PixApi.Application/Transferencias/TransferenciaService.cs):

```csharp
await using (var transacao = await uow.IniciarTransacaoAsync(ct: ct))       // BEGIN
{
    // trava em ordem determinística: A→B e B→A pegam os locks na mesma ordem
    if (string.CompareOrdinal(chaveOrigem, chaveDestino) < 0) { origem = lock(origem); destino = lock(destino); }
    else                                                      { destino = lock(destino); origem = lock(origem); }

    origem.Debitar(valor);                 // SaldoInsuficienteException → sai sem commit = ROLLBACK
    destino.Creditar(valor);
    lancamentos.Adicionar(PixEnviado(...)); lancamentos.Adicionar(PixRecebido(...));   // razão
    await uow.SalvarAsync(ct);             // 2 UPDATE + 2 INSERT
    await transacao.ConfirmarAsync(ct);    // COMMIT: libera os locks
}
await publicador.PublicarAsync(evento);    // só depois do commit
```

1. **`SELECT ... FOR UPDATE`** ([`ContaRepositorio`](src/PixApi.Infrastructure/Persistencia/ContaRepositorio.cs)):
   a segunda transação que tocar a mesma conta **espera** e depois lê o saldo já atualizado.
   Sem isso, as duas leem R$ 100, as duas debitam R$ 80 e o banco aceita (*lost update*). O
   `FromSql($"... {chave} ...")` com string interpolada vira **parâmetro** `$1`, não concatenação:
   não há SQL injection.
2. **Ordem determinística** evita *deadlock*: se A→B travasse A e depois B, e B→A travasse B e
   depois A, cada uma esperaria a outra para sempre. **`string.CompareOrdinal`**, e não
   `string.Compare`: o padrão do .NET compara pela **cultura** do servidor, então a ordem poderia
   mudar de uma máquina para outra. O `compareTo` do Java já é ordinal.
3. **Razão na mesma transação.** Débito, crédito e as duas partidas do razão gravam juntos ou
   nenhum grava. Invariante testado: **soma do razão de cada conta = saldo da conta**.
4. **`await using`** chama `DisposeAsync` ao sair do bloco, inclusive por exceção. Sem
   `ConfirmarAsync`, o dispose faz **rollback**. É o `defer` do Swift aplicado à transação.

**Prova:** o teste [`TransferenciasConcorrentes_NaoCriamNemPerdemDinheiro`](tests/PixApi.Tests/Integracao/PostgresTests.cs)
dispara 300 transferências, 30 simultâneas, em todos os sentidos, contra o PostgreSQL. Ao **remover
o `FOR UPDATE` de propósito** (teste de mutação), ele reprova: o saldo esperado de uma conta era
R$ 355,61 e o gravado foi R$ 146,78.

## 6. Eventos depois do commit e RabbitMQ

```mermaid
sequenceDiagram
    participant S as TransferenciaService
    participant DB as PostgreSQL
    participant D as PublicadorComHistorico (decorator)
    participant A as Adaptador (log | RabbitMQ)
    participant X as exchange pix.transferencias
    participant C as NotificacaoConsumer
    S->>DB: BEGIN · FOR UPDATE · UPDATE/INSERT
    S->>DB: COMMIT
    S->>D: PublicarAsync(evento)
    D->>D: registra no histórico (GET /eventos)
    D->>A: PublicarAsync(evento)
    alt Pix:Mensageria = rabbitmq
        A->>X: JSON persistente, MessageId = id, publisher confirm
        X->>C: fila pix.notificacoes (ack manual)
        C->>C: ignora id repetido e gera a notificação
    else Pix:Mensageria = log (padrão)
        A->>A: escreve no log
    end
```

- **Só depois do commit.** No Spring, `@TransactionalEventListener(AFTER_COMMIT)` segurava o evento.
  Aqui o caso de uso publica **depois** de `ConfirmarAsync`, de forma explícita. Saldo insuficiente,
  chave inexistente ou qualquer exceção saem antes, e nada é publicado.
- **Falha ao publicar não vira erro HTTP.** O dinheiro já mudou de conta; responder 500 faria o
  cliente tentar de novo e pagar duas vezes. A falha é registrada no log. O evento se perde nesse
  caso: o **Transactional Outbox** resolve (ver Limitações).
- **Porta e adaptadores.** `IPublicadorDeEventos` está na Application. Os dois adaptadores,
  [`LogPublicadorDeEventos`](src/PixApi.Infrastructure/Mensageria/LogPublicadorDeEventos.cs) e
  [`RabbitMqPublicadorDeEventos`](src/PixApi.Infrastructure/Mensageria/RabbitMqPublicadorDeEventos.cs),
  implementam a mesma interface e são escolhidos por `Pix:Mensageria`. No modo `log`, nada do
  RabbitMQ é criado nem conectado.
- **Decorator.** [`PublicadorComHistorico`](src/PixApi.Infrastructure/Mensageria/PublicadorComHistorico.cs)
  também é um `IPublicadorDeEventos`: registra no histórico do `/eventos` e repassa ao adaptador,
  que é resolvido como *keyed service* (.NET 8). O caso de uso não sabe que existe histórico.
- **RabbitMQ × Kafka.** O Java usava um tópico Kafka com a chave Pix como *key* (ordem por
  partição). Aqui: exchange `topic` durável `pix.transferencias`, routing key
  `transferencia.realizada` e a fila `pix.notificacoes` ligada a ela. Outros sistemas (antifraude,
  extrato) ligariam as próprias filas, sem mudar o publicador. Mensagens persistentes e *publisher
  confirms* (o broker confirma que gravou; sem confirmação em 5 s, exceção).
- **Consumidor idempotente.** O broker entrega "pelo menos uma vez". O
  [`NotificacaoConsumer`](src/PixApi.Infrastructure/Mensageria/NotificacaoConsumer.cs) guarda os ids
  processados e ignora repetidos. O [`RabbitMqConsumidor`](src/PixApi.Infrastructure/Mensageria/RabbitMqConsumidor.cs)
  (um `BackgroundService`) faz *ack* manual **depois** de processar; mensagem ilegível recebe
  `nack` sem reenvio (*poison message*) para não travar a fila. Se o broker estiver fora do ar na
  subida, tenta de novo a cada 5 s em vez de derrubar a API.

## 7. Swagger

- Interface: http://localhost:8080/swagger
- Documento OpenAPI: http://localhost:8080/swagger/v1/swagger.json

Os status possíveis de cada rota estão declarados com `[ProducesResponseType]`, inclusive os
`ProblemDetails` em `application/problem+json`. O `SwaggerTests` garante que as seis rotas estão no
documento. Fica habilitado em todos os ambientes por ser um projeto de estudo; num sistema real,
ficaria atrás de autenticação ou só fora de produção.

## 8. Testes em TDD

```bash
dotnet test
```

**90 testes xUnit.** Cada etapa foi escrita **antes** da implementação, e o histórico do git
mostra o ciclo: um commit "(vermelho)" com os testes falhando pelo motivo certo
(`NotImplementedException` ou rota inexistente) e o commit "(verde)" seguinte com a implementação.

```text
fff157e Cria solution em camadas e testes do domínio e do layout MOVIMENTO (vermelho)   16 falhando
b1a4d56 Implementa Conta, Lancamento e LayoutMovimento (verde)                          16/16
f94af2d Define portas da Application e testes dos casos de uso com Moq (vermelho)       24 falhando
8217bf0 Implementa casos de uso de conta, transferência, exportação e histórico (verde) 40/40
726d801 Testes de integração HTTP, razão, mensageria e PostgreSQL (vermelho)            43 falhando
69b463e Infrastructure (EF Core, Npgsql, SQLite, RabbitMQ) e Api ... (verde)            85/85 (+4 ignorados)
512f9ca Corrige fila do RabbitMQ 4 e cultura no teste de concorrência ...               89/89
a43f6e8 Teste: saldo sai com 2 casas no JSON também no SQLite (vermelho)              1 falhando
(seguinte) Devolve a escala 2 do decimal no SQLite (verde)                             90/90 no CI
```

| Pasta / classe | Tipo | Qtd. | O que cobre |
|---|---|---|---|
| `Domain/ContaTests` | Unidade pura | 5 | Débito, crédito (`0.1 + 0.2`), saldo insuficiente, atualização cadastral |
| `Domain/LancamentoTests` | Unidade pura | 3 | Sinal de crédito/débito, fábricas, id da transferência |
| `Application/LayoutMovimentoTests` | Unidade pura | 8 | Posição a posição de H/C/L/T, acentos, emoji, truncamento, recusa de 3 casas |
| `Application/ContaServiceTests` | Moq | 11 | CRUD, abertura/encerramento no razão, chave duplicada (inclusive corrida no UNIQUE) |
| `Application/TransferenciaServiceTests` | Moq + fake | 8 | Débito/crédito, partidas dobradas, **ordem salvar → confirmar → publicar**, ordem dos locks, nada publicado em erro, falha no broker não desfaz |
| `Application/ExportacaoMovimentoServiceTests` | Moq + `FakeTimeProvider` | 3 | "Ontem" em Brasília (não UTC), data futura, `REPEATABLE READ`, totais do trailer |
| `Application/HistoricoDeEventosTests` | Unidade pura | 2 | Ordem e capacidade |
| `Api/ContasApiTests` | `WebApplicationFactory` + SQLite | 11 | 201 + `Location`, 400 com `erros`, JSON malformado, 409, 404, PUT, DELETE, contas de exemplo, saldo com 2 casas no JSON |
| `Api/TransferenciasApiTests` | idem | 7 | 200 + comprovante (`Z`), 422, 404, origem = destino, 400 para `0.001`, `0`, `-1` |
| `Api/BatchApiTests` | idem | 6 | Arquivo consistente com o trailer, `Content-Disposition`, data futura/inválida (inclusive `10/26/2026`), padrão = ontem |
| `Api/LogsApiTests` | idem, 2 fábricas | 5 | 401 sem/errado, 200 com token, limite, 404 sem token configurado |
| `Api/CorsTests` | idem | 5 | GitHub Pages e `localhost:*` liberados com `X-Admin-Token`; `localhost.evil.com` bloqueado |
| `Api/EventosApiTests`, `Api/SwaggerTests` | idem | 2 | Confirmada aparece em `/eventos` e recusada não; rotas no OpenAPI |
| `Mensageria/PublicacaoAposCommitTests` | idem + espião | 3 | Evento só após commit; nada com saldo insuficiente; **falha no meio da transação → rollback real e nada publicado** |
| `Mensageria/EventoJsonTests` | Unidade | 2 | JSON da mensagem igual ao do Java |
| `Mensageria/NotificacaoConsumerTests` | Unidade | 3 | Notificação em R$ pt-BR, duplicata ignorada |
| `Mensageria/RabbitMqTests` | **RabbitMQ real** | 2 | Transferência → exchange → fila → notificação; mensagem duplicada no broker notificada uma vez |
| `Integracao/RazaoTests` | `WebApplicationFactory` | 2 | Soma do razão = saldo; conta encerrada com razão zerado |
| `Integracao/PostgresTests` | **PostgreSQL real** | 2 | Migrations aplicadas e nenhuma pendente; 300 transferências concorrentes sem criar nem perder dinheiro |

Os testes `[PostgresFact]` e `[RabbitMqFact]` precisam dos serviços de verdade. Sem as variáveis
abaixo, o xUnit os mostra como **Ignorado**, com o motivo, em vez de falhar. No CI elas estão
sempre definidas.

```bash
PIX_TESTE_POSTGRES="Host=localhost;Port=5432;Username=pix;Password=pix" PIX_TESTE_RABBITMQ="amqp://guest:guest@localhost:5672" dotnet test
```

**Bugs que só os testes contra serviços reais pegaram** (bons exemplos para entrevista):

1. **RabbitMQ 4 recusa fila transitória compartilhada** (`transient_nonexcl_queues` está
   descontinuado). A fila temporária dos testes passou a ser durável com `x-expires`.
2. **Cultura no teste de concorrência.** O primeiro run deu 400 em 299 de 300 requisições: o
   `{valor}` interpolado numa máquina em pt-BR virava `12,34`, que é JSON inválido. O código de
   produção já usava `InvariantCulture`; o teste não.

## 9. Docker e GitHub Actions

[`Dockerfile`](Dockerfile), multi-stage:

1. **build** (`mcr.microsoft.com/dotnet/sdk:8.0`): copia primeiro só os `.csproj` e roda
   `dotnet restore`. Enquanto eles não mudarem, a camada com os pacotes NuGet é reaproveitada. Depois
   `dotnet publish -c Release`.
2. **runtime** (`mcr.microsoft.com/dotnet/aspnet:8.0`): só o runtime, sem SDK. **`USER $APP_UID`**:
   as imagens .NET 8 já trazem o usuário `app` (UID 1654), então nada roda como root. Imagem Debian,
   e não Alpine, porque precisa de ICU (`R$ 1.234,50` em pt-BR) e tzdata (`America/Sao_Paulo`).

```bash
docker build -t pix-api-dotnet .
```

```bash
docker run --rm -p 8080:8080 -e Pix__Banco__Provedor=sqlite -e ConnectionStrings__Pix="Data Source=/tmp/pix.db" pix-api-dotnet
```

[`.github/workflows/ci.yml`](.github/workflows/ci.yml), a cada push e PR:

- **testes:** sobe `postgres:17` e `rabbitmq:4` como *services*, faz `dotnet restore --locked-mode`
  (versões exatas dos `packages.lock.json`), build Release, **`dotnet test`** (os 90, inclusive os de
  PostgreSQL e RabbitMQ: no CI nenhum fica ignorado), verifica se há migration pendente e publica o `.trx` como artefato.
- **docker:** builda a imagem, sobe o container, confere que `/contas` responde com as contas de
  exemplo e que o processo **não** roda com UID 0.

## 10. Teste de carga com a ferramenta em Go

A ferramenta [`tools/carga`](https://github.com/RafaelNicolauiOS/pix-api/tree/main/tools/carga) do
pix-api rodou **sem nenhuma alteração** contra esta versão: o contrato é o mesmo. Ela cria 5 contas,
dispara 2.000 transferências com 100 simultâneas (inclusive A→B e B→A ao mesmo tempo) e verifica,
conta a conta, saldo final = inicial − enviado + recebido.

Ambiente: MacBook (Apple Silicon), PostgreSQL 17 local, API em Release (`dotnet publish`), modo de
mensageria `log`, semente `2026`. Cada linha é a mediana de 3 rodadas depois do aquecimento.

```bash
cd ../pix-api/tools/carga && go run . -transferencias 2000 -concorrencia 100 -semente 2026
```

| Versão | Pool de conexões | Transferências/s | p50 | p95 | p99 | Consistência |
|---|---|---|---|---|---|---|
| **.NET 8 (atual, `Maximum Pool Size=5`)** | 5 | **~1.800** | 54 ms | **68 ms** | 72 ms | ✅ APROVADO, R$ 5.000,00 |
| .NET 8 com o padrão do Npgsql | 100 | ~800 | 9 ms | 581 ms | 1,1 s | ✅ APROVADO |
| Java/Spring Boot (pix-api), mesmo PostgreSQL | 5 (Hikari) | ~1.900 | 51 ms | 66 ms | 90 ms | ✅ APROVADO |

Saída de uma rodada (.NET, pool 5):

```text
Resultados (1.11 s · 1797 transferências/s)
  latência p50 54.1ms · p95 64.8ms · p99 69.2ms · máx 72.8ms
Verificação de consistência
  ✅ 5 contas: saldo final = inicial − enviado + recebido
  ✅ nenhum saldo negativo
  ✅ soma dos saldos inalterada: R$ 5.000,00
Resultado: APROVADO — nenhum centavo criado, perdido ou gasto duas vezes.
```

**O achado: o pool de conexões pesa mais do que a linguagem.** Com o padrão do Npgsql (100
conexões), 100 transações disputam o lock das **mesmas 5 linhas** dentro do PostgreSQL. A latência
fica bimodal: p50 de 9 ms para quem pega o lock logo e p95 de 600 ms para quem entra no "comboio"
de espera. Com pool 5, as outras 95 requisições esperam na **fila da aplicação**, que é barata, e o
banco só vê 5 transações por vez. A vazão dobra, o p95 cai ~9×, e o resultado fica no mesmo
patamar do Java, cujo perfil `prod` já usava 5 conexões. O padrão do
[`appsettings.json`](src/PixApi.Api/appsettings.json) passou a ser `Maximum Pool Size=5`.

> Os ~1.550/s do README do Java foram medidos com H2 em memória; a comparação justa é a da tabela
> acima, no mesmo PostgreSQL. A prova de que a ferramenta detecta falhas de concorrência está no
> teste de mutação da seção 5.

## Como rodar

Desenvolvimento (SQLite em arquivo, sem nada instalado além do .NET; `appsettings.Development.json`):

```bash
dotnet run --project src/PixApi.Api
```

API em http://localhost:8080 e Swagger em http://localhost:8080/swagger. O front-end do pix-api
(`python3 -m http.server 8000 --directory ../pix-api/docs`) aponta para `localhost:8080` e usa as
mesmas rotas; a origem `localhost:*` já está liberada no CORS.

Com PostgreSQL (igual à produção). Antes, crie o banco `pix` e o usuário `pix`:

```bash
ASPNETCORE_ENVIRONMENT=Production dotnet run --project src/PixApi.Api
```

Com RabbitMQ:

```bash
ASPNETCORE_ENVIRONMENT=Production Pix__Mensageria=rabbitmq dotnet run --project src/PixApi.Api
```

A cada transferência aparecem no log `Evento ... publicado em pix.transferencias` e
`Notificação para <chave>: você recebeu um Pix de R$ ...`.

## Configuração

O ASP.NET lê `appsettings.json`, `appsettings.{Ambiente}.json` e variáveis de ambiente, nessa ordem.
`:` vira `__` na variável (`Pix:Mensageria` → `Pix__Mensageria`).

| Chave | Variável | Padrão | Uso |
|---|---|---|---|
| `ConnectionStrings:Pix` | `ConnectionStrings__Pix` | PostgreSQL local, pool 5 | String de conexão |
| — | `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER`, `DB_PASSWORD` | — | Mesmas variáveis do Render no Java; têm prioridade |
| `Pix:Banco:Provedor` | `Pix__Banco__Provedor` | `postgres` (`sqlite` em Development) | Banco |
| `Pix:Mensageria` | `Pix__Mensageria` | `log` | `log` ou `rabbitmq` |
| `Pix:RabbitMq:Url` | `Pix__RabbitMq__Url` | `amqp://guest:guest@localhost:5672` | Broker |
| `Pix:Admin:Token` | `PIX_ADMIN_TOKEN` | vazio | Token do `GET /logs`; vazio = desativado (404) |
| `Pix:Cors:Origens` | `PIX_CORS_ORIGENS` | GitHub Pages + `localhost:*` | Origens do CORS |
| `Pix:Seed:Habilitado` | `PIX_SEED_ENABLED` | `true` | Contas de exemplo com o banco vazio |
| — | `PORT` | `8080` | Porta HTTP (o Render define) |

## Para a entrevista

Perguntas prováveis e onde está a resposta no código:

- **"Por que não usar o `TransactionScope` / um `@Transactional` mágico?"** A transação explícita
  (`IUnidadeDeTrabalho` + `await using`) deixa visível onde ela começa, onde termina e **onde o
  evento é publicado em relação ao commit**. É o ponto mais sensível do sistema, e fica testável
  com um fake que registra a ordem `salvar → confirmar → publicar`.
- **"Lock otimista ou pessimista?"** Otimista (coluna de versão, `DbUpdateConcurrencyException`)
  é melhor com pouca disputa. Num Pix, a mesma conta recebe várias operações simultâneas; com
  otimista, a maioria falharia e teria que repetir. O pessimista enfileira no banco e acerta de
  primeira. O teste de carga mostra o custo e o teste de mutação mostra o risco de não ter lock.
- **"Por que ordem alfabética?"** Evita deadlock com uma ordem global de aquisição de locks. E é
  `CompareOrdinal`, porque a comparação cultural poderia diferir entre servidores.
- **"Por que a Application não referencia o EF Core?"** Para trocar persistência ou testar os casos
  de uso com mocks sem banco; e o compilador impede o atalho.
- **"E se o broker cair depois do commit?"** O evento se perde (*dual write*). Solução: Transactional
  Outbox (abaixo). O consumidor já é idempotente, que é o pré-requisito.
- **"O que você aprendeu no teste de carga?"** Pool de conexões grande piora a latência de cauda
  quando o gargalo é lock de linha. Medir antes de concluir: comparar Java em H2 com .NET em
  PostgreSQL teria levado à conclusão errada.
- **"Swift → C#?"** `protocol` → `interface`; `struct` imutável → `record`; `defer` → `using`;
  `async/await` igual no nome e na ideia (`Task` ≈ `Task` do Swift); `actor` → `lock`;
  `Optional` → tipos anuláveis (`string?`) com avisos tratados como erro (`WarningsAsErrors=nullable`).

## Limitações e próximos passos

- **Transactional Outbox:** gravar o evento numa tabela `outbox` na mesma transação e publicar
  com um `BackgroundService`, para não perder eventos se o broker falhar logo após o commit.
- **Ids processados em memória** no consumidor: em produção, uma tabela com o id `UNIQUE` gravada
  na mesma transação do efeito.
- **Publicador RabbitMQ com um canal e `lock`:** suficiente aqui; com mais volume, um pool de canais.
- **Sem autenticação** (igual ao Java) e **sem idempotência no `POST /transferencias`**
  (`Idempotency-Key`).
- Histórico de eventos e logs em memória: zeram ao reiniciar.
- Imagem Docker testada só no CI (a máquina onde o projeto foi feito não tem Docker). O teste de
  carga rodou localmente, contra PostgreSQL 17.
