# AzurePrep — Contexto do Projeto

## O que é

Simulado do exame **AZ-900 (Microsoft Azure Fundamentals)** que replica fielmente a experiência real da prova: interface, timer, navegação entre questões, marcação para revisão. O diferencial não é ter "mais um banco de questões" — é a fidelidade à experiência real de prova (estilo Pearson VUE), algo que o simulado oficial da Microsoft não oferece.

Roadmap futuro: expandir para outras certificações Microsoft (AZ-104, AI-900, etc.). O modelo de dados deve ser desenhado pensando nisso desde já — nada de hardcoded só pro AZ-900.

## ⚠️ Regra crítica de conteúdo (não negociável)

**Nunca usar, buscar ou reproduzir questões reais vazadas ("dumps") de provas Microsoft.** Isso viola o NDA que se assina ao fazer a certificação e pode levar à revogação do certificado. Todas as questões do banco devem ser **originais**, escritas com base no **Skills Measured outline** público do exame (documento oficial da Microsoft Learn, que lista tópicos e pesos de cada domínio). Mesmo estilo, mesmo formato, mesma dificuldade — conteúdo próprio.

Se em algum momento o Claude Code (ou eu) sugerir "buscar questões que caíram na prova" ou importar de sites de dump, a resposta é não.

## Stack

- .NET 10, ASP.NET Core MVC
- EF Core + SQLite (`Microsoft.Data.Sqlite`)
- xUnit para testes — **prioridade desde o início**, não deixar pra depois (gap conhecido a corrigir neste projeto)
- UI: priorizar fidelidade visual ao ambiente de prova real (timer fixo no topo, navegação linear + tela de revisão, cores neutras) sobre qualquer frescura visual

## Arquitetura

Clean Architecture, seguindo o padrão já usado em outros projetos (ex: WebAppClinicaMedica):

```
AzurePrep.sln
├── src/
│   ├── AzurePrep.Domain          # Entidades, Value Objects, regras de negócio puras
│   ├── AzurePrep.Application     # Casos de uso, interfaces, DTOs
│   ├── AzurePrep.Infrastructure  # EF Core, DbContext, Repositories, SQLite
│   └── AzurePrep.Web             # ASP.NET Core MVC (Controllers, Views, ViewModels)
└── tests/
    ├── AzurePrep.Domain.Tests
    └── AzurePrep.Application.Tests
```

Dependências: `Web` → `Application` + `Infrastructure`; `Infrastructure` → `Application`; `Application` → `Domain`.

## Modelo de domínio (nomes de tipo em português; propriedades em inglês = colunas do banco)

- **Exame** (`Exam`) — Id, Code (ex: "AZ-900"), Name, TimeLimitMinutes, PassingScorePercent, TotalQuestions
- **AreaDeHabilidade** (`SkillArea`) — Id, ExamId, Name, WeightPercent (ex: "Descrever conceitos de nuvem — 25-30%")
- **Questao** (`Question`) — Id, ExamId, SkillAreaId, Text, Type (`TipoDeQuestao`: EscolhaUnica, EscolhaMultipla, SimNao), Explanation
- **OpcaoDeResposta** (`AnswerOption`) — Id, QuestionId, Text, IsCorrect, OrderIndex
- **TentativaDeProva** (`ExamAttempt`) — Id, ExamId, StartedAt, FinishedAt, ScorePercent, Passed
- **RespostaDaTentativa** (`ExamAttemptAnswer`) — Id, ExamAttemptId, QuestionId, SelectedOptionIds, IsFlaggedForReview, TimeSpentSeconds

> Nota (nomes entre parênteses): o modelo foi traduzido para português nos **tipos e métodos**; as **propriedades** e o **schema do banco** (nomes de tabela/coluna, fixados por `ToTable`/convenção) seguem em inglês. Correção: `CorretorDeProva` (era `ExamGrader`); placares: `PlacarDaProva`/`PlacarPorArea`. Serviços: `CatalogoDeExamesService`, `SessaoDeProvaService`. Repositórios: `IExameRepository`, `ITentativaDeProvaRepository`.

> **Nota exibida ≠ percentual armazenado.** `PassingScorePercent` continua sendo a regra de negócio (é o que define `Passed`), mas nunca aparece na UI. `EscalaDeNota` converte o percentual para a escala 1–1000 ancorando o percentual de corte do exame em exatamente 700 — assim a nota mostrada e o veredito jamais se contradizem. A escala real da Microsoft é derivada de Teoria de Resposta ao Item e nunca é divulgada; a nossa é uma aproximação linear por partes, deliberadamente documentada como tal.

## Fluxo da prova (o coração do diferencial)

1. Tela inicial → botão "Iniciar Simulado"
2. Tela de questão: timer regressivo fixo, indicador "Item 12 de 40", navegação **linear** (Anterior / Próxima), toggle "Marcar para revisão", botão "Comentários"
3. Tela de revisão (acessível a qualquer momento): tabela de todos os itens com status Completo / Incompleto / Não visto + coluna Marcado, filtros por categoria, clique na linha volta ao item
4. Ao zerar o tempo ou encerrar manualmente → **score report** na escala 1–1000 (corte 700) com desempenho por domínio
5. A revisão questão a questão com gabarito e explicação é uma **tela separada**, acessada a partir do score report — não faz parte da simulação

## Metodologia de criação de questões (o que dá a dificuldade real)

O objetivo é uma questão que **quem só decorou termo erra, e quem entende o conceito acerta** — isso não vem de "questão autêntica vazada", vem de boa engenharia de distrator. Regras pra toda questão nova:

- **Cenário aplicado, não definição.** Nunca "O que é um Resource Group?". Sempre algo como "Uma empresa precisa de X restrição, qual serviço/abordagem atende?" — obriga a aplicar o conceito, não só reconhecer o termo.
- **Distratores plausíveis, não bobos.** Cada alternativa errada deve ser a resposta certa de uma pergunta *ligeiramente diferente* — ex: confundir Availability Zone com Availability Set, Azure Policy com RBAC, Reserved Instances com Spot Instances, o limite entre IaaS/PaaS/SaaS, Cost Management com Advisor. Nada de alternativa absurda que se elimina por eliminação óbvia.
- **Questões negativas ocasionais** ("Qual das opções NÃO é..."), pra quebrar padrão de pattern-matching.
- **Síntese de mais de um tópico do Skills Measured na mesma questão** quando fizer sentido (ex: shared responsibility model + compliance no mesmo cenário) — isso é o que mais separa quem entende de quem decorou frase solta.
- **Mix de formatos**: single choice, multiple response ("selecione duas"), e statement-based (afirmação + Verdadeiro/Falso ou Sim/Não) — são formatos publicamente documentados pela Microsoft como parte do formato de prova, então replicar o formato é ok; o que não pode é replicar o conteúdo real.
- **Explicação de cada distrator, não só da resposta certa.** A explicação da questão deve dizer por que cada alternativa errada está errada — é isso que ensina, não o gabarito sozinho.
- Calibrar dificuldade contra o **Skills Measured outline oficial** (pesos por domínio) — não inventar peso de tópico.

## Especificação da interface de prova (fidelidade é o produto)

Isso não é "nice to have", é o diferencial do AzurePrep. A referência de calibração é o **exam sandbox oficial da Microsoft** (`aka.ms/examdemo`) — demo pública da interface de entrega, não é dump de conteúdo. Elementos obrigatórios:

- **Barra superior fixa**: código/nome do exame à esquerda, "Tempo restante" + relógio HH:MM:SS à direita. Fundo claro (cinza), texto escuro — o chrome da prova real é discreto, não uma faixa colorida
- **Indicador de posição**: "Item 12 de 40", no topo da área da questão
- **Opções de resposta**: radio pra single choice, checkbox pra multiple response. Sem "cartão"/borda por alternativa — é radio + texto, com hover e destaque de selecionada
- **Fraseado padronizado**, derivado do modelo e não hardcoded: "Escolha duas." (quantidade vem de `RequiredSelections`), "OBSERVAÇÃO: Cada seleção correta vale um ponto." em múltipla resposta, e a instrução Sim/Não nos itens de afirmação
- **Barra de ações fixa no rodapé**: à esquerda "Marcar para revisão" e "Comentários"; à direita "Tela de revisão", "Anterior", "Próxima". No último item, "Próxima" dá lugar a "Encerrar prova"
- ⚠️ **Sem painel de navegação lateral.** A prova real não tem grid de questões — a navegação é linear e o único jeito de saltar entre itens é pela tela de revisão. Não reintroduzir
- **Tela de revisão**: substitui a área da questão (não é modal). Tabela Item / Status / Marcado, com status **Completo, Incompleto, Não visto** — múltipla resposta parcialmente marcada é Incompleto. Filtros "Revisar todos / incompletos / marcados". Clique na linha volta ao item. Modal de confirmação em "Encerrar prova", com aviso de que não dá pra voltar
- **Score report**: nota na **escala 1–1000, corte em 700** (nunca percentual), veredito aprovado/reprovado, régua da escala e barras por domínio **sem números** — igual ao relatório real, que não revela contagem de acertos nem quais itens foram errados
- **Revisão de estudo**: tela à parte (`exam/{id}/review`), com gabarito, explicação por distrator e números por domínio. Deve deixar explícito que não existe na prova real
- **Estilo visual**: neutro e sério — tons de azul/cinza/branco, nada de gamificação, emoji ou cor vibrante. Tem que parecer ambiente de prova, não app de quiz
- **Timer zerado = submissão automática**, sem exceção
- Fluxo deve se comportar como SPA (AJAX/partial views no MVC) — sem recarregar a página inteira a cada navegação de questão, pra não quebrar a imersão

## Convenções de código

- **Idioma (decisão do projeto):** o domínio é em **português** — nomes de **tipos** (classes/interfaces/enums/records), **métodos**, **arquivos, pastas e namespaces de negócio**. Comentários também em português.
  - **Mantém-se em inglês:** termos de framework/convenção .NET (`Controller`, `DbContext`, `Repository`, `Service`, `Dto`, `Request`, `ViewModel`, `Configuration`, `UnitOfWork`, `Entity`, `Guard`), as pastas de convenção do MVC (`Controllers`, `Views`, `Models`) e de infra (`Persistence`, `Repositories`, `Configurations`), as **propriedades das entidades** e o **schema do banco** (tabelas/colunas) — para não poluir com `HasColumnName` e manter portabilidade.
  - Sufixo técnico + raiz de negócio, ex.: `IExameRepository`, `SessaoDeProvaService`, `QuestaoDto`, `ExameController`, `RealizarProvaViewModel`.
- SOLID; Clean Architecture + DDD leve, mesmo padrão dos outros projetos .NET
- AutoMapper entre Domain e DTOs/ViewModels quando fizer sentido
- Testes unitários (xUnit) desde a primeira etapa — objetivo explícito do projeto, não é opcional

## Autenticação (login social, sem senha)

Login **obrigatório** para fazer simulado. Não existe senha local nem cadastro: a identidade vem sempre de um provedor externo — **Google, LinkedIn, GitHub**.

- **Sem ASP.NET Core Identity.** Cookie de autenticação + handlers OAuth, mapeando para a entidade `Usuario` do Domain. Evita as ~7 tabelas do Identity e mantém o schema no padrão do projeto.
- **Dois esquemas de cookie** (`EsquemasDeAutenticacao`): `Aplicacao` é a sessão; `Externo` é temporário e só existe entre o callback do provedor e a criação da sessão. É esse intervalo que permite trocar as claims externas por um `Usuario` local.
- **Chave natural da identidade é o par (`Provider`, `ProviderKey`)**, nunca o e-mail — o GitHub pode não devolver e-mail, e a mesma pessoa pode ter o mesmo e-mail em dois provedores. Vincular contas de provedores diferentes é recurso à parte, ainda não implementado.
- **`NameIdentifier` no cookie da aplicação é o Id LOCAL do `Usuario`**, não o id do provedor. `IUsuarioAtual` lê exatamente essa claim.
- **Posse da tentativa é imposta na Application**, não no controller: `SessaoDeProvaService` resolve toda tentativa por `ObterTentativaDoUsuarioAsync`, que devolve `null` para tentativa de outro dono (o Web responde 404 e não confirma que o id existe). O controller não tem como esquecer de checar porque não é ele quem checa.
- **Política padrão exige autenticação** (`SetFallbackPolicy`); o que é público leva `[AllowAnonymous]` explícito — hoje só login/callback e a página de erro.
- **`/conta/entrar` é POST com antiforgery**, não GET: um GET permitiria login CSRF, prendendo a pessoa numa conta que não é dela. `returnUrl` passa por `Url.IsLocalUrl` para barrar open redirect.
- **Credenciais nunca no appsettings versionado** — usar `dotnet user-secrets` em `Authentication:{Google,GitHub,LinkedIn}:{ClientId,ClientSecret}`. Cada provedor só é registrado se tiver credencial, então a app sobe e a tela de login funciona com apenas um deles configurado.
- **LinkedIn usa OpenID Connect** ("Sign In with LinkedIn using OpenID Connect", escopos `openid`/`profile`/`email`, userinfo em `/v2/userinfo`) — é preciso habilitar esse produto no app do LinkedIn, senão o callback volta com `unauthorized_scope`. **GitHub exige o escopo `user:email`**, sem ele não vem e-mail nenhum.

## Banco de dados

SQLite via EF Core Migrations. Arquivo em `src/AzurePrep.Web/App_Data/azureprep.db` (ajustável). Evitar recursos específicos de um único provider na modelagem — se o projeto crescer, a migração pra PostgreSQL deve ser barata.

**Decisão (mantida com login social):** seguir no SQLite, com **WAL habilitado** no startup (leitores param de bloquear o escritor). O que quebraria o SQLite não é volume nem autenticação, é **deploy multi-instância ou disco efêmero** — esse é o gatilho para migrar pro PostgreSQL, não uma data. É a disciplina provider-agnostic acima que mantém essa migração barata.

> ⚠️ Dívida conhecida: `ScorePercent` é `decimal` e o SQLite não tem tipo decimal nativo, então ordenação/comparação numérica é imprecisa. Hoje não morde porque ninguém ordena por nota — vai morder quando existir histórico ordenado ou ranking.

## Comandos úteis

```bash
dotnet build
dotnet run --project src/AzurePrep.Web
dotnet ef migrations add NomeDaMigration --project src/AzurePrep.Infrastructure --startup-project src/AzurePrep.Web
dotnet ef database update --project src/AzurePrep.Infrastructure --startup-project src/AzurePrep.Web
dotnet test

# Credenciais OAuth (nunca commitar — ficam fora do repositório)
cd src/AzurePrep.Web
dotnet user-secrets init
dotnet user-secrets set "Authentication:Google:ClientId"     "..."
dotnet user-secrets set "Authentication:Google:ClientSecret" "..."
dotnet user-secrets set "Authentication:GitHub:ClientId"     "..."
dotnet user-secrets set "Authentication:GitHub:ClientSecret" "..."
dotnet user-secrets set "Authentication:LinkedIn:ClientId"     "..."
dotnet user-secrets set "Authentication:LinkedIn:ClientSecret" "..."
```

Callback a cadastrar em cada provedor (ajuste host/porta): `/signin-google`, `/signin-github`, `/signin-linkedin`.

## Fora de escopo por agora

- Deploy em nuvem
- Outros exames além do AZ-900 (mas o modelo de dados já deve suportar)