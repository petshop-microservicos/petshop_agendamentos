# Serviço Agendamentos

Documentação do serviço de agendamentos do sistema de gerenciamento de petshop (trabalho de Microsserviços). Versão 1.0.

## Sumário

1. [Identificação do serviço](#1-identificação-do-serviço)
2. [Pré-requisitos](#2-pré-requisitos)
3. [Configuração e execução](#3-configuração-e-execução)
4. [Estrutura do projeto](#4-estrutura-do-projeto)
5. [Database](#5-database)
6. [Endpoints](#6-endpoints)

---

## 1. Identificação do serviço

| Item | Descrição |
|---|---|
| Nome | Agendamentos (projeto `Agendamentos.Api`) |
| Finalidade | Gerenciar os serviços oferecidos, os profissionais (disponibilidade semanal e serviços que executam) e os agendamentos do petshop |
| Depende de | Serviço AnimaisTutores (valida a existência do animal ao criar um agendamento) |
| Prefixo das rotas | `/api` |
| Formato | JSON (`application/json`, UTF-8) |

**Responsabilidades**

- Cadastrar e manter serviços (nome, descrição, preço e duração em minutos), com inativação.
- Cadastrar e manter profissionais, com inativação.
- Definir a disponibilidade semanal recorrente de cada profissional.
- Definir quais serviços cada profissional executa.
- Criar, consultar, remarcar e acompanhar o status dos agendamentos, aplicando as regras de negócio (seção 6.8).

**Fora do escopo:** autenticação e autorização (serviço de usuários, via JWT), cadastro de animais e tutores (serviço AnimaisTutores), movimentação de estoque (o consumo de produtos nos serviços é lançado manualmente, RN09) e financeiro.

**Tecnologias utilizadas**

| Tecnologia | Uso |
|---|---|
| C# e .NET 10 | Linguagem e plataforma |
| ASP.NET Core (Controllers) | API REST |
| Entity Framework Core 10 | ORM e migrations |
| SQLite | Banco de dados (arquivo local, exclusivo do serviço) |
| `HttpClient` tipado | Chamada ao serviço AnimaisTutores |
| Microsoft.AspNetCore.OpenApi | Documento OpenAPI em `/openapi/v1.json` (somente em Development) |
| DataAnnotations | Validação dos DTOs |

---

## 2. Pré-requisitos

| Ferramenta | Versão | Observação |
|---|---|---|
| .NET SDK | 10.x | Confira com `dotnet --version` |
| dotnet-ef (ferramenta global) | 10.0.x | `dotnet tool install --global dotnet-ef --version 10.0.*` |
| Git | Recente | Para clonar o repositório |
| SQLite | Nenhuma instalação | Embutido no pacote NuGet do EF Core |
| Serviço AnimaisTutores | Em execução | Necessário para criar agendamentos (seção 6.9) |
| Cliente HTTP | Opcional | VS Code com REST Client, Visual Studio ou Rider (arquivo `.http`) |
| Visualizador de SQLite | Opcional | DB Browser for SQLite ou a extensão SQLite Viewer do VS Code |

**Pacotes NuGet** (todos na mesma linha 10.x do .NET 10):

| Pacote | Versão |
|---|---|
| `Microsoft.AspNetCore.OpenApi` | 10.0.* |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.* |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.* |

Sem fixar a versão, o NuGet instala a 11.x, que não é compatível com `net10.0` (erro NU1202). Sempre use `--version 10.0.*` ao adicionar pacotes do EF Core.

---

## 3. Configuração e execução

### 3.1 Clonagem e dependências

```powershell
git clone <url-do-repositorio>
cd Agendamentos.Api
dotnet restore
```

Em um projeto novo, os pacotes são adicionados com:

```powershell
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.*
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.*
```

### 3.2 Variáveis de ambiente e configuração

A configuração fica em `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=agendamentos.db"
  },
  "Servicos": {
    "AnimaisTutores": {
      "BaseUrl": "http://localhost:5123/"
    }
  }
}
```

| Chave | Padrão | Descrição |
|---|---|---|
| `ConnectionStrings:Default` | `Data Source=agendamentos.db` | Caminho do arquivo SQLite, relativo ao diretório de execução |
| `Servicos:AnimaisTutores:BaseUrl` | `http://localhost:5123/` | URL base do serviço AnimaisTutores. **A barra final é obrigatória** |
| `ASPNETCORE_ENVIRONMENT` | `Development` (no `dotnet run`) | Em Development o OpenAPI fica habilitado |
| `ASPNETCORE_URLS` | Definido no `launchSettings.json` (`http://localhost:5124`) | URLs e porta da API |

Se `Servicos:AnimaisTutores:BaseUrl` não estiver configurada, a aplicação falha ao iniciar. A chamada ao AnimaisTutores tem timeout de 5 segundos.

Precedência (da menor para a maior): `appsettings.json`, `appsettings.{Ambiente}.json`, variáveis de ambiente, argumentos de linha de comando. Em variável de ambiente, use `__` no lugar de `:`:

```powershell
$env:Servicos__AnimaisTutores__BaseUrl = "http://localhost:5123/"
$env:ConnectionStrings__Default = "Data Source=agendamentos.db"
```

Não versione segredos. Os arquivos `*.db`, `*.db-shm` e `*.db-wal` devem estar no `.gitignore`.

### 3.3 Migrations

Ao iniciar, o `Program.cs` prepara o banco:

- **Sem migrations no projeto:** cria o schema com `EnsureCreated`.
- **Com migrations commitadas:** aplica as pendentes com `Migrate`.

Para versionar o schema, gere a migration inicial uma vez e faça commit da pasta `Migrations/`:

```powershell
dotnet ef migrations add Inicial
```

- Se o serviço já tiver rodado com `EnsureCreated`, apague o `agendamentos.db` antes de gerar a migration e subir de novo.
- Mudou uma entidade? Gere uma nova migration (`dotnet ef migrations add <Nome>`); ela é aplicada na próxima inicialização.
- Em desenvolvimento, para recomeçar do zero: apague o `.db` e a pasta `Migrations/` e repita o comando.

### 3.4 Inicialização

```powershell
dotnet run
```

A API sobe em `http://localhost:5124` (porta do `launchSettings.json`). Em Development, o documento OpenAPI fica em `/openapi/v1.json`.

### 3.5 Testando a API

O arquivo `Agendamentos.Api.http` cobre todos os endpoints e os casos de erro, com o status esperado em cada título. Para rodá-lo:

1. Suba o AnimaisTutores e confira a `BaseUrl` do passo 3.2.
2. Apague o `agendamentos.db`: os ids do arquivo assumem a ordem de criação.
3. Rode as requisições **de cima para baixo**. O bloco 0 cria tutor e animais no AnimaisTutores.

### 3.6 Problemas comuns

| Sintoma | Causa e solução |
|---|---|
| `NU1202` ao adicionar pacote do EF Core | O NuGet pegou a 11.x. Use `--version 10.0.*` |
| Falha ao iniciar com `Servicos:AnimaisTutores:BaseUrl não configurado` | Falta a chave no `appsettings.json` ou na variável de ambiente |
| `POST /api/agendamentos` retorna 503 | O AnimaisTutores está fora do ar, a `BaseUrl` está errada (confira a porta e a barra final) ou ele demorou mais de 5 segundos |
| `POST /api/agendamentos` retorna 404 para um animal que existe | A `BaseUrl` aponta para outra instância, ou o animal foi criado em outro banco |
| 400 com `Informe o horário local, sem fuso horário` | `inicio` foi enviado com `Z` ou offset. Use `2026-11-23T09:00:00` |
| 400 em campos de enum (`status`, `diaSemana`) | Os enums são texto, com a grafia exata da seção 6.1 (ex.: `EmAndamento`, `Terca`) |
| 500 com `no such table` | O `.db` é de uma versão anterior do schema. Apague o `.db` ou aplique a migration |
| 500 com `Unable to resolve service` | Falta registrar algum Service ou o `AppDbContext` no `Program.cs` |

---

## 4. Estrutura do projeto

```
Agendamentos.Api/
├── Controllers/
│   ├── ApiControllerBase.cs
│   ├── AgendamentosController.cs
│   ├── ProfissionaisController.cs
│   └── ServicosController.cs
├── Services/
│   ├── AgendamentosService.cs
│   ├── ProfissionaisService.cs
│   ├── ServicosService.cs
│   └── AnimaisTutoresClient.cs
├── DTOs/
│   ├── AgendamentosDtos.cs
│   ├── ProfissionaisDtos.cs
│   ├── ServicosDtos.cs
│   └── Comum.cs
├── Data/
│   ├── AppDbContext.cs
│   ├── Agendamento.cs
│   ├── Servico.cs
│   ├── Profissional.cs
│   ├── DisponibilidadeProfissional.cs
│   ├── ProfissionalServico.cs
│   ├── StatusAgendamento.cs
│   └── DiaSemana.cs
├── Migrations/
├── Properties/
│   └── launchSettings.json
├── appsettings.json
├── appsettings.Development.json
├── Agendamentos.Api.csproj
├── Agendamentos.Api.http
└── Program.cs
```

| Pasta ou arquivo | Responsabilidade |
|---|---|
| `Controllers/` | Camada HTTP: rotas, verbos, status e corpo das respostas. Não contém regra de negócio. `ApiControllerBase` traduz o resultado dos Services em `ProblemDetails` |
| `Services/` | Regras de negócio e acesso ao `AppDbContext`. Cada Service tem sua interface (`IServicosService`, `IProfissionaisService`, `IAgendamentosService`), registrada por injeção de dependência. `AnimaisTutoresClient` é o cliente HTTP do serviço AnimaisTutores |
| `DTOs/` | Contratos de entrada e saída, com validação por DataAnnotations. `Comum.cs` reúne `PagedResult<T>`, `PaginacaoDto`, `StatusResultado`, `Resultado<T>` e `AlterarAtivoDto` |
| `Data/` | Entidades, enums (`StatusAgendamento`, `DiaSemana`) e o `AppDbContext` com a configuração do modelo |
| `Migrations/` | Histórico de schema, gerado pelo `dotnet ef` |
| `Properties/launchSettings.json` | Perfil de execução local (URL, porta, ambiente). Não vai para container nem produção |
| `appsettings*.json` | Configuração da aplicação (connection string, URL do AnimaisTutores, logging) |
| `Agendamentos.Api.http` | Requisições de teste manual de todos os endpoints |
| `Program.cs` | Composição da aplicação: DI, `HttpClient`, JSON, OpenAPI, criação do banco e pipeline |
| `bin/` e `obj/` | Saída de build; recriadas a cada compilação e fora do Git |

Fluxo de uma requisição: `Controller → Service → AppDbContext → SQLite`. Na criação de agendamento, o Service também chama `AnimaisTutoresClient → AnimaisTutores`.

**Convenção de retorno dos Services** (não há exceções de domínio nem handlers globais):

- `null` quando o recurso não existe (o controller responde 404).
- `Resultado<T>` quando há mais de uma falha possível. O `ApiControllerBase` traduz o status: `NaoEncontrado` em 404, `Conflito` em 409 e `Indisponivel` em 503.

---

## 5. Database

**Tecnologia:** SQLite, acessado pelo Entity Framework Core. O banco é um arquivo (`agendamentos.db`) exclusivo deste serviço, o que atende o requisito de banco isolado por microsserviço.

**Entidades e tabelas**

| Entidade (C#) | Tabela | DbSet |
|---|---|---|
| `Servico` | `Servicos` | `AppDbContext.Servicos` |
| `Profissional` | `Profissionais` | `AppDbContext.Profissionais` |
| `DisponibilidadeProfissional` | `Disponibilidades` | `AppDbContext.Disponibilidades` |
| `ProfissionalServico` | `ProfissionaisServicos` | `AppDbContext.ProfissionaisServicos` |
| `Agendamento` | `Agendamentos` | `AppDbContext.Agendamentos` |

### 5.1 Tabela Servicos

| Coluna | Tipo SQLite | Restrições |
|---|---|---|
| `Id` | INTEGER | Chave primária, autoincremento |
| `Nome` | TEXT | Obrigatório, máx. 100, collation `NOCASE`, índice único `IX_Servicos_Nome` |
| `Descricao` | TEXT | Opcional, máx. 300 |
| `Preco` | TEXT | Obrigatório; `decimal(10,2)` |
| `DuracaoMinutos` | INTEGER | Obrigatório |
| `Ativo` | INTEGER | Obrigatório; booleano (0 ou 1) |

### 5.2 Tabela Profissionais

| Coluna | Tipo SQLite | Restrições |
|---|---|---|
| `Id` | INTEGER | Chave primária, autoincremento |
| `Nome` | TEXT | Obrigatório, máx. 150 |
| `Telefone` | TEXT | Obrigatório, máx. 11 |
| `Email` | TEXT | Opcional, máx. 150 |
| `Especialidade` | TEXT | Opcional, máx. 80 |
| `Ativo` | INTEGER | Obrigatório; booleano (0 ou 1) |

### 5.3 Tabela Disponibilidades

Cada linha é uma faixa da grade semanal recorrente do profissional.

| Coluna | Tipo SQLite | Restrições |
|---|---|---|
| `Id` | INTEGER | Chave primária, autoincremento |
| `ProfissionalId` | INTEGER | Obrigatório; chave estrangeira para `Profissionais(Id)` com `ON DELETE CASCADE` |
| `DiaSemana` | INTEGER | Obrigatório; 0 = Domingo, 1 = Segunda, ..., 6 = Sábado |
| `HoraInicio` | TEXT | Obrigatório; `TimeOnly` gravado como `HH:mm:ss` |
| `HoraFim` | TEXT | Obrigatório; `TimeOnly` gravado como `HH:mm:ss` |

Índice composto em (`ProfissionalId`, `DiaSemana`).

### 5.4 Tabela ProfissionaisServicos

Vínculo N:N entre profissionais e os serviços que eles executam.

| Coluna | Tipo SQLite | Restrições |
|---|---|---|
| `ProfissionalId` | INTEGER | Chave primária composta; chave estrangeira para `Profissionais(Id)` com `ON DELETE CASCADE` |
| `ServicoId` | INTEGER | Chave primária composta; chave estrangeira para `Servicos(Id)` com `ON DELETE CASCADE` |

### 5.5 Tabela Agendamentos

| Coluna | Tipo SQLite | Restrições |
|---|---|---|
| `Id` | INTEGER | Chave primária, autoincremento |
| `AnimalId` | INTEGER | Obrigatório; **sem chave estrangeira** (o animal pertence ao serviço AnimaisTutores) |
| `ProfissionalId` | INTEGER | Obrigatório; chave estrangeira para `Profissionais(Id)` com `ON DELETE RESTRICT` |
| `ServicoId` | INTEGER | Obrigatório; chave estrangeira para `Servicos(Id)` com `ON DELETE RESTRICT` |
| `Inicio` | TEXT | Obrigatório; `DateTime` em horário local do petshop, sem fuso |
| `Fim` | TEXT | Obrigatório; `Inicio` + `DuracaoMinutos` |
| `DuracaoMinutos` | INTEGER | Obrigatório; copiada do serviço na criação (RN05) |
| `Preco` | TEXT | Obrigatório; `decimal(10,2)`; copiado do serviço na criação (RN05) |
| `Status` | TEXT | Obrigatório, máx. 20; nome do enum (`Agendado`, `EmAndamento`, `Concluido`, `Cancelado`, `Faltou`) |
| `CriadoEm` | TEXT | Obrigatório; `DateTime` em UTC |

Índices: (`ProfissionalId`, `Inicio`), (`AnimalId`, `Inicio`) e `Status`, que apoiam a checagem de sobreposição e as listagens, além dos índices das chaves estrangeiras.

### 5.6 Relacionamentos

| Relação | Cardinalidade | Observação |
|---|---|---|
| Profissional → Disponibilidade | 1:N | A grade semanal do profissional |
| Profissional ↔ Serviço | N:N | Pela tabela `ProfissionaisServicos` |
| Profissional → Agendamento | 1:N | `ON DELETE RESTRICT` |
| Serviço → Agendamento | 1:N | `ON DELETE RESTRICT` |
| Agendamento → Animal | N:1 (externo) | Só o `AnimalId` é guardado; validado via HTTP no AnimaisTutores |

### 5.7 Persistência

- **Sem exclusão de serviços e profissionais:** são inativados (`Ativo = false`), e a inativação não altera agendamentos já criados (RN29). Por isso as chaves estrangeiras de `Agendamentos` são `RESTRICT`.
- **Sem exclusão de agendamentos:** o histórico é mantido; o cancelamento é uma mudança de status.
- **Snapshot (RN05):** preço e duração ficam gravados no agendamento. Alterar o serviço depois não muda agendamentos existentes.
- **Datas:** `Inicio` e `Fim` são hora local sem fuso; `CriadoEm` é UTC.
- **Limites de tamanho:** o SQLite não impõe `HasMaxLength`; o limite é garantido pela validação dos DTOs.
- **Decimais:** o SQLite não ordena nem agrega colunas `decimal` no banco, então as listagens ordenam por nome ou por início, nunca por preço.
- **Chaves estrangeiras:** o EF Core ativa `PRAGMA foreign_keys` em cada conexão, então as restrições valem.
- **Dados iniciais:** não há seed; o banco começa vazio.
- **Arquivo:** em container, monte um volume no diretório do `.db`, senão os dados somem ao recriar o container.
- **Evolução do schema:** por migrations.

---

## 6. Endpoints

### 6.1 Convenções

- **Rotas:** prefixo `/api`; `{id}` é inteiro (uma rota como `/api/servicos/abc` retorna 404).
- **Campos:** `camelCase` no JSON.
- **Enums como texto:**
  - `status`: `Agendado`, `EmAndamento`, `Concluido`, `Cancelado`, `Faltou`.
  - `diaSemana`: `Domingo`, `Segunda`, `Terca`, `Quarta`, `Quinta`, `Sexta`, `Sabado`.
- **Data e hora (`inicio`, `fim`, `de`, `ate`):** `yyyy-MM-ddTHH:mm:ss`, em **horário local do petshop, sem fuso** (ex.: `2026-11-23T09:00:00`). Valores com `Z` ou offset retornam 400, para não converter o horário em silêncio.
- **Hora do dia (`horaInicio`, `horaFim`):** `HH:mm:ss` (ex.: `08:00:00`).
- **`criadoEm`:** UTC. Na resposta do `POST` aparece com `Z`; nas consultas, sem o `Z`.
- **Paginação:** as listagens aceitam `page` (padrão 1) e `pageSize` (padrão 20, máximo 100) e respondem com `PagedResult<T>`:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalItems": 57,
  "totalPages": 3
}
```

### 6.2 Resumo

| Método | Rota | Descrição | Sucesso |
|---|---|---|---|
| POST | `/api/servicos` | Cadastra um serviço | 201 |
| GET | `/api/servicos` | Lista serviços (paginado, com filtros) | 200 |
| GET | `/api/servicos/{id}` | Obtém um serviço | 200 |
| PUT | `/api/servicos/{id}` | Atualiza um serviço | 200 |
| PATCH | `/api/servicos/{id}/ativo` | Inativa ou reativa um serviço | 200 |
| POST | `/api/profissionais` | Cadastra um profissional | 201 |
| GET | `/api/profissionais` | Lista profissionais (paginado, com filtros) | 200 |
| GET | `/api/profissionais/{id}` | Obtém um profissional | 200 |
| PUT | `/api/profissionais/{id}` | Atualiza um profissional | 200 |
| PATCH | `/api/profissionais/{id}/ativo` | Inativa ou reativa um profissional | 200 |
| GET | `/api/profissionais/{id}/disponibilidade` | Obtém a grade semanal | 200 |
| PUT | `/api/profissionais/{id}/disponibilidade` | Substitui a grade semanal | 200 |
| GET | `/api/profissionais/{id}/servicos` | Lista os serviços que o profissional executa | 200 |
| PUT | `/api/profissionais/{id}/servicos` | Substitui os serviços que o profissional executa | 200 |
| POST | `/api/agendamentos` | Cria um agendamento | 201 |
| GET | `/api/agendamentos` | Lista agendamentos (paginado, com filtros) | 200 |
| GET | `/api/agendamentos/{id}` | Obtém um agendamento | 200 |
| PATCH | `/api/agendamentos/{id}/remarcar` | Remarca um agendamento | 200 |
| PATCH | `/api/agendamentos/{id}/status` | Altera o status de um agendamento | 200 |

Não há `DELETE`: serviços e profissionais são inativados, e agendamentos são cancelados por status.

### 6.3 DTOs

| DTO | Campos | Uso |
|---|---|---|
| `ServicoCreateDto` / `ServicoUpdateDto` | `nome`, `descricao`, `preco`, `duracaoMinutos` | Corpo do `POST` e do `PUT` de serviços |
| `ServicoResponseDto` | `id`, `nome`, `descricao`, `preco`, `duracaoMinutos`, `ativo` | Resposta de serviço |
| `ServicoFiltroDto` | `page`, `pageSize`, `nome`, `ativo` | Query string de `GET /api/servicos` |
| `ProfissionalCreateDto` / `ProfissionalUpdateDto` | `nome`, `telefone`, `email`, `especialidade` | Corpo do `POST` e do `PUT` de profissionais |
| `ProfissionalResponseDto` | `id`, `nome`, `telefone`, `email`, `especialidade`, `ativo` | Resposta de profissional |
| `ProfissionalFiltroDto` | `page`, `pageSize`, `nome`, `especialidade`, `ativo` | Query string de `GET /api/profissionais` |
| `AlterarAtivoDto` | `ativo` | Corpo dos `PATCH .../ativo` |
| `DefinirDisponibilidadeDto` | `faixas[]` (`diaSemana`, `horaInicio`, `horaFim`) | Corpo de `PUT .../disponibilidade` |
| `FaixaDisponibilidadeResponseDto` | `id`, `diaSemana`, `horaInicio`, `horaFim` | Resposta de disponibilidade |
| `DefinirServicosDto` | `servicoIds[]` | Corpo de `PUT .../servicos` |
| `AgendamentoCreateDto` | `animalId`, `profissionalId`, `servicoId`, `inicio` | Corpo do `POST /api/agendamentos` |
| `RemarcarAgendamentoDto` | `inicio` | Corpo de `PATCH .../remarcar` |
| `AlterarStatusAgendamentoDto` | `status` | Corpo de `PATCH .../status` |
| `AgendamentoResponseDto` | `id`, `animalId`, `profissionalId`, `profissionalNome`, `servicoId`, `servicoNome`, `inicio`, `fim`, `duracaoMinutos`, `preco`, `status`, `criadoEm` | Resposta de agendamento |
| `AgendamentoFiltroDto` | `page`, `pageSize`, `animalId`, `profissionalId`, `servicoId`, `status`, `de`, `ate` | Query string de `GET /api/agendamentos` |
| `PagedResult<T>` | `items`, `page`, `pageSize`, `totalItems`, `totalPages` | Envelope das listagens |

**Validações do serviço**

| Campo | Tipo | Obrigatório | Regras |
|---|---|---|---|
| `nome` | string | sim | 2 a 100 caracteres; único, sem diferenciar maiúsculas e minúsculas |
| `descricao` | string | não | Até 300 caracteres |
| `preco` | decimal | sim | De 0 a 100000, no máximo 2 casas decimais (RN23) |
| `duracaoMinutos` | int | sim | De 1 a 1440 (RN23) |

**Validações do profissional**

| Campo | Tipo | Obrigatório | Regras |
|---|---|---|---|
| `nome` | string | sim | 2 a 150 caracteres |
| `telefone` | string | sim | 10 ou 11 dígitos, somente números |
| `email` | string | não | E-mail válido, até 150 caracteres |
| `especialidade` | string | não | Até 80 caracteres |

**Validações da disponibilidade (RN27)**

| Campo | Tipo | Obrigatório | Regras |
|---|---|---|---|
| `faixas` | lista | sim | Pode ser vazia (profissional sem disponibilidade) |
| `faixas[].diaSemana` | enum | sim | `Domingo` a `Sabado` |
| `faixas[].horaInicio` | hora | sim | `HH:mm:ss` |
| `faixas[].horaFim` | hora | sim | Maior que `horaInicio` |

Faixas do mesmo dia não podem se sobrepor. Faixas que se encostam (08:00–12:00 e 12:00–18:00) são válidas.

**Validações do agendamento**

| Campo | Tipo | Obrigatório | Regras |
|---|---|---|---|
| `animalId` | int | sim | Maior que 0; deve existir no AnimaisTutores |
| `profissionalId` | int | sim | Maior que 0 |
| `servicoId` | int | sim | Maior que 0 |
| `inicio` | datetime | sim | Hora local, sem fuso |
| `status` | enum | sim (no `PATCH .../status`) | Um dos valores de `status` (seção 6.1) |

### 6.4 Serviços

#### POST /api/servicos

Cadastra um serviço. Corpo: `ServicoCreateDto`.

```http
POST /api/servicos
Content-Type: application/json

{
  "nome": "Banho",
  "descricao": "Banho com shampoo neutro e secagem",
  "preco": 60.00,
  "duracaoMinutos": 45
}
```

Resposta:

```http
HTTP/1.1 201 Created
Location: /api/servicos/1

{
  "id": 1,
  "nome": "Banho",
  "descricao": "Banho com shampoo neutro e secagem",
  "preco": 60.00,
  "duracaoMinutos": 45,
  "ativo": true
}
```

**Erros:** 400 (validação, incluindo preço e duração obrigatórios), 409 (nome já cadastrado).

#### GET /api/servicos

Lista serviços, ordenados por nome.

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `page` | int | Página (padrão 1) |
| `pageSize` | int | Itens por página (padrão 20, máx. 100) |
| `nome` | string | Filtro por parte do nome |
| `ativo` | bool | Filtro por status. Sem o parâmetro, retorna todos |

```http
GET /api/servicos?ativo=true&nome=ban
```

Resposta `200 OK`: `PagedResult<ServicoResponseDto>`.

#### GET /api/servicos/{id}

Obtém um serviço. Resposta `200 OK`: `ServicoResponseDto`.

**Erros:** 404.

#### PUT /api/servicos/{id}

Substitui os dados do serviço. Corpo: `ServicoUpdateDto`. Agendamentos já criados não são alterados (RN05).

```http
PUT /api/servicos/1
Content-Type: application/json

{
  "nome": "Banho",
  "descricao": "Banho com shampoo neutro e secagem",
  "preco": 80.00,
  "duracaoMinutos": 60
}
```

Resposta `200 OK`: `ServicoResponseDto` atualizado.

**Erros:** 400, 404, 409 (nome pertence a outro serviço).

#### PATCH /api/servicos/{id}/ativo

Inativa ou reativa o serviço. Corpo: `AlterarAtivoDto`.

```http
PATCH /api/servicos/1/ativo
Content-Type: application/json

{
  "ativo": false
}
```

Resposta `200 OK`: `ServicoResponseDto` com o novo `ativo`. Um serviço inativo não pode ser agendado (RN04), e os agendamentos existentes não mudam (RN29).

**Erros:** 400 (`ativo` ausente), 404.

### 6.5 Profissionais

#### POST /api/profissionais

Cadastra um profissional. Corpo: `ProfissionalCreateDto`.

```http
POST /api/profissionais
Content-Type: application/json

{
  "nome": "Ana Ribeiro",
  "telefone": "48999991111",
  "email": "ana@petshop.com",
  "especialidade": "Banho e tosa"
}
```

Resposta:

```http
HTTP/1.1 201 Created
Location: /api/profissionais/1

{
  "id": 1,
  "nome": "Ana Ribeiro",
  "telefone": "48999991111",
  "email": "ana@petshop.com",
  "especialidade": "Banho e tosa",
  "ativo": true
}
```

**Erros:** 400.

#### GET /api/profissionais

Lista profissionais, ordenados por nome.

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `page`, `pageSize` | int | Paginação (seção 6.1) |
| `nome` | string | Filtro por parte do nome |
| `especialidade` | string | Filtro exato por especialidade |
| `ativo` | bool | Filtro por status. Sem o parâmetro, retorna todos |

```http
GET /api/profissionais?ativo=true
```

Resposta `200 OK`: `PagedResult<ProfissionalResponseDto>`.

#### GET /api/profissionais/{id}

Obtém um profissional. Resposta `200 OK`: `ProfissionalResponseDto`.

**Erros:** 404.

#### PUT /api/profissionais/{id}

Substitui os dados do profissional. Corpo: `ProfissionalUpdateDto`. Resposta `200 OK`: `ProfissionalResponseDto` atualizado.

**Erros:** 400, 404.

#### PATCH /api/profissionais/{id}/ativo

Inativa ou reativa o profissional. Corpo: `AlterarAtivoDto` (mesmo formato do serviço). Resposta `200 OK`: `ProfissionalResponseDto`. Um profissional inativo não pode ser agendado (RN04), e os agendamentos existentes não mudam (RN29).

**Erros:** 400, 404.

#### GET /api/profissionais/{id}/disponibilidade

Retorna a grade semanal, ordenada por dia da semana e hora inicial.

```http
GET /api/profissionais/1/disponibilidade
```

Resposta `200 OK`:

```json
[
  { "id": 1, "diaSemana": "Segunda", "horaInicio": "08:00:00", "horaFim": "12:00:00" },
  { "id": 2, "diaSemana": "Segunda", "horaInicio": "12:00:00", "horaFim": "18:00:00" },
  { "id": 3, "diaSemana": "Terca", "horaInicio": "08:00:00", "horaFim": "12:00:00" }
]
```

**Erros:** 404 (profissional não encontrado).

#### PUT /api/profissionais/{id}/disponibilidade

**Substitui a grade semanal inteira.** Corpo: `DefinirDisponibilidadeDto`. Para limpar a disponibilidade, envie `"faixas": []`. Agendamentos já criados não são alterados (RN29).

```http
PUT /api/profissionais/1/disponibilidade
Content-Type: application/json

{
  "faixas": [
    { "diaSemana": "Segunda", "horaInicio": "08:00:00", "horaFim": "12:00:00" },
    { "diaSemana": "Segunda", "horaInicio": "12:00:00", "horaFim": "18:00:00" },
    { "diaSemana": "Terca", "horaInicio": "08:00:00", "horaFim": "12:00:00" }
  ]
}
```

Resposta `200 OK`: a lista de faixas, no formato do `GET`.

**Erros:** 400 (hora final não maior que a inicial, faixas sobrepostas no mesmo dia, dia da semana inválido), 404.

#### GET /api/profissionais/{id}/servicos

Lista os serviços que o profissional executa, ordenados por nome.

```http
GET /api/profissionais/1/servicos
```

Resposta `200 OK`: lista de `ServicoResponseDto`.

**Erros:** 404.

#### PUT /api/profissionais/{id}/servicos

**Substitui o conjunto de serviços** que o profissional executa. Corpo: `DefinirServicosDto`. Ids repetidos são ignorados.

```http
PUT /api/profissionais/1/servicos
Content-Type: application/json

{
  "servicoIds": [1, 2]
}
```

Resposta `200 OK`: lista de `ServicoResponseDto` vinculados.

**Erros:** 400 (`servicoIds` ausente), 404 (profissional ou algum serviço não encontrado).

### 6.6 Agendamentos

#### POST /api/agendamentos

Cria um agendamento. Corpo: `AgendamentoCreateDto`. O serviço calcula o `fim` (`inicio` + duração do serviço) e grava preço e duração do serviço no agendamento.

```http
POST /api/agendamentos
Content-Type: application/json

{
  "animalId": 1,
  "profissionalId": 1,
  "servicoId": 1,
  "inicio": "2026-11-23T09:00:00"
}
```

Resposta:

```http
HTTP/1.1 201 Created
Location: /api/agendamentos/1

{
  "id": 1,
  "animalId": 1,
  "profissionalId": 1,
  "profissionalNome": "Ana Ribeiro",
  "servicoId": 1,
  "servicoNome": "Banho",
  "inicio": "2026-11-23T09:00:00",
  "fim": "2026-11-23T09:45:00",
  "duracaoMinutos": 45,
  "preco": 60.00,
  "status": "Agendado",
  "criadoEm": "2026-11-18T15:30:00Z"
}
```

Validações, na ordem em que são feitas:

1. Serviço e profissional existem (404).
2. Serviço e profissional estão ativos (409, RN04).
3. O profissional executa o serviço (409, RN26).
4. O animal existe no AnimaisTutores (404; 503 se o serviço estiver indisponível; 409 se ele informar `ativo = false`).
5. O intervalo cabe na disponibilidade do profissional (409, RN03).
6. Não há sobreposição para o profissional nem para o animal (409, RN02).

**Erros:** 400 (validação), 404, 409, 503.

#### GET /api/agendamentos

Lista agendamentos, ordenados por início.

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `page`, `pageSize` | int | Paginação (seção 6.1) |
| `animalId` | int | Filtro por animal |
| `profissionalId` | int | Filtro por profissional |
| `servicoId` | int | Filtro por serviço |
| `status` | enum | Filtro por status |
| `de` | datetime | Início do período (inclusivo) |
| `ate` | datetime | Fim do período (exclusivo) |

```http
GET /api/agendamentos?profissionalId=1&status=Agendado&de=2026-11-23&ate=2026-11-24
```

Resposta `200 OK`: `PagedResult<AgendamentoResponseDto>`.

#### GET /api/agendamentos/{id}

Obtém um agendamento. Resposta `200 OK`: `AgendamentoResponseDto`.

**Erros:** 404.

#### PATCH /api/agendamentos/{id}/remarcar

Remarca um agendamento, mudando só o início. O `fim` é recalculado com a duração gravada no agendamento (RN05); animal, profissional e serviço não mudam. Só agendamentos com status `Agendado` podem ser remarcados (RN06). Valida a disponibilidade (RN03) e a sobreposição (RN02, ignorando o próprio agendamento).

```http
PATCH /api/agendamentos/1/remarcar
Content-Type: application/json

{
  "inicio": "2026-11-23T14:00:00"
}
```

Resposta `200 OK`: `AgendamentoResponseDto` atualizado.

**Erros:** 400, 404, 409 (status diferente de `Agendado`, fora da disponibilidade ou sobreposição).

#### PATCH /api/agendamentos/{id}/status

Registra o andamento do agendamento (RN07). Corpo: `AlterarStatusAgendamentoDto`.

```http
PATCH /api/agendamentos/1/status
Content-Type: application/json

{
  "status": "EmAndamento"
}
```

Resposta `200 OK`: `AgendamentoResponseDto` com o novo `status`.

Transições permitidas:

| De | Para |
|---|---|
| `Agendado` | `EmAndamento`, `Cancelado`, `Faltou` |
| `EmAndamento` | `Concluido` |
| `Concluido`, `Cancelado`, `Faltou` | (nenhuma: são status finais) |

Só agendamentos `Agendado` podem ser cancelados (RN06).

**Erros:** 400 (status ausente ou inválido), 404, 409 (transição não permitida).

### 6.7 Tratamento de erros

As respostas de erro seguem o padrão `ProblemDetails` (RFC 9457), com `Content-Type: application/problem+json`.

| Status | Quando ocorre |
|---|---|
| 400 Bad Request | Corpo inválido ou regra de validação violada |
| 404 Not Found | Recurso inexistente (inclusive animal inexistente no AnimaisTutores) |
| 409 Conflict | Regra de negócio violada (nome duplicado, serviço ou profissional inativo, fora da disponibilidade, sobreposição, status inválido) |
| 500 Internal Server Error | Falha inesperada no servidor |
| 503 Service Unavailable | O serviço AnimaisTutores não respondeu (fora do ar, erro ou timeout) |

Exemplo (400, validação):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Inicio": ["Informe o horário local, sem fuso horário (ex.: 2026-11-20T14:00:00)."]
  }
}
```

Exemplo (409, sobreposição):

```json
{
  "title": "Conflito",
  "status": 409,
  "detail": "O profissional já possui um agendamento nesse horário."
}
```

Exemplo (503, AnimaisTutores indisponível):

```json
{
  "title": "Serviço indisponível",
  "status": 503,
  "detail": "Não foi possível contatar o serviço de animais."
}
```

### 6.8 Regras de negócio

| Regra | Descrição | Como é aplicada |
|---|---|---|
| RN01 | Um agendamento ocupa o intervalo [início, início + duração do serviço) | `fim = inicio + duração`; intervalos que se encostam não se sobrepõem |
| RN02 | Profissional e animal não podem ter dois agendamentos ativos sobrepostos | Checagem na criação e na remarcação; só contam `Agendado` e `EmAndamento` |
| RN03 | O agendamento deve estar dentro da disponibilidade do profissional | O intervalo deve caber na grade do dia da semana, no mesmo dia; faixas encostadas valem como uma só |
| RN04 | Animal, serviço e profissional devem estar ativos para novos agendamentos | Serviço e profissional: campo `ativo`. Animal: precisa existir no AnimaisTutores |
| RN05 | O agendamento guarda preço e duração do serviço no momento da criação | Copiados para o agendamento; alterar o serviço não muda agendamentos existentes |
| RN06 | Só agendamentos `Agendado` podem ser remarcados ou cancelados | Validado em `remarcar` e em `status` |
| RN07 | O funcionário registra os status do agendamento | `PATCH .../status` com as transições da seção 6.6 |
| RN09 | O agendamento não movimenta estoque | Nada é feito neste serviço; o consumo é lançado manualmente |
| RN23 | Todo serviço tem preço (maior ou igual a zero) e duração (maior que zero), obrigatórios | Validação dos DTOs de serviço |
| RN26 | O profissional só pode ser agendado para os serviços vinculados a ele | Checado na criação; vínculo definido em `PUT .../servicos` |
| RN27 | A disponibilidade é semanal e recorrente; faixas do mesmo dia não se sobrepõem | Validação de `PUT .../disponibilidade` |
| RN29 | Inativar serviço ou profissional e alterar a disponibilidade não alteram agendamentos já criados | Nenhuma dessas operações toca na tabela `Agendamentos` |

Não validado nesta versão: agendamento em data passada (não consta nas regras).

### 6.9 Integração com outros serviços

| Serviço | Endpoint chamado | Finalidade |
|---|---|---|
| AnimaisTutores | `GET /api/animais/{id}` | Validar que o animal existe (RN04) ao criar um agendamento |

- A chamada usa `HttpClient` tipado, com timeout de 5 segundos e a URL base de `Servicos:AnimaisTutores:BaseUrl`.
- `404` do AnimaisTutores vira `404` aqui; erro, timeout ou falta de conexão vira `503`.
- O campo `ativo` do animal é opcional na resposta do AnimaisTutores. Hoje esse serviço considera "ativo" um animal que existe; se ele passar a expor `ativo = false`, o agendamento é recusado com 409.
- Este serviço guarda só o `animalId` e não mantém cópia dos dados do animal. Se o animal for excluído depois, os agendamentos existentes permanecem.
- Nenhum outro serviço é chamado por este.
