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
- UI: priorizar fidelidade visual ao ambiente de prova real (timer fixo no topo, painel de navegação lateral com status por questão, cores neutras) sobre qualquer frescura visual

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

## Modelo de domínio (rascunho inicial — ajustar durante o desenvolvimento)

- **Exam** — Id, Code (ex: "AZ-900"), Name, TimeLimitMinutes, PassingScorePercent, TotalQuestions
- **SkillArea** — Id, ExamId, Name, WeightPercent (ex: "Descrever conceitos de nuvem — 25-30%")
- **Question** — Id, ExamId, SkillAreaId, Text, Type (SingleChoice, MultipleChoice, YesNo), Explanation
- **AnswerOption** — Id, QuestionId, Text, IsCorrect, OrderIndex
- **ExamAttempt** — Id, ExamId, StartedAt, FinishedAt, ScorePercent, Passed
- **ExamAttemptAnswer** — Id, ExamAttemptId, QuestionId, SelectedOptionIds, IsFlaggedForReview, TimeSpentSeconds

## Fluxo da prova (o coração do diferencial)

1. Tela inicial → botão "Iniciar Simulado"
2. Tela de questão: timer regressivo fixo, número da questão (ex: "12 de 40"), painel lateral com status de cada questão (não respondida / respondida / marcada para revisão), botão "marcar para revisão", navegação anterior/próxima
3. Ao zerar o tempo ou finalizar manualmente → tela de resultado com score, aprovado/reprovado (baseado no `PassingScorePercent`), revisão questão a questão com explicação

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

Isso não é "nice to have", é o diferencial do AzurePrep. Elementos obrigatórios:

- **Header fixo**: código/nome do exame + timer regressivo (formato MM:SS ou HH:MM:SS), sempre visível, sem precisar rolar a página
- **Indicador de posição**: "Questão 12 de 40"
- **Opções de resposta**: radio button pra single choice; checkbox pra multiple response, com texto "(Selecione duas)" explícito acima das opções
- **Painel de navegação lateral**: grid com o número de todas as questões, cor/ícone por status — não respondida, respondida, marcada para revisão. Clicar em qualquer número pula direto pra aquela questão
- **Barra inferior**: botões Anterior / Próxima, toggle "Marcar para revisão", botão "Ir para revisão"
- **Tela de revisão final** (antes de submeter): lista de todas as questões com status, permite voltar e alterar qualquer resposta, modal de confirmação em "Finalizar prova"
- **Tela de resultado**: score %, aprovado/reprovado, breakdown por skill area (como um score report real), modo de revisão questão a questão com explicação
- **Estilo visual**: neutro e sério — tons de azul/cinza/branco, nada de gamificação, emoji ou cor vibrante. Tem que parecer ambiente de prova, não app de quiz
- **Timer zerado = submissão automática**, sem exceção
- Fluxo deve se comportar como SPA (AJAX/partial views no MVC) — sem recarregar a página inteira a cada navegação de questão, pra não quebrar a imersão

## Convenções de código

- SOLID; nomes de classes/métodos/variáveis em inglês; comentários em português quando ajudarem
- Clean Architecture + DDD leve, mesmo padrão dos outros projetos .NET
- AutoMapper entre Domain e DTOs/ViewModels quando fizer sentido
- Testes unitários (xUnit) desde a primeira etapa — objetivo explícito do projeto, não é opcional

## Banco de dados

SQLite via EF Core Migrations. Arquivo em `src/AzurePrep.Web/App_Data/azureprep.db` (ajustável). Evitar recursos específicos de um único provider na modelagem — se o projeto crescer, a migração pra PostgreSQL deve ser barata.

## Comandos úteis

```bash
dotnet build
dotnet run --project src/AzurePrep.Web
dotnet ef migrations add NomeDaMigration --project src/AzurePrep.Infrastructure --startup-project src/AzurePrep.Web
dotnet ef database update --project src/AzurePrep.Infrastructure --startup-project src/AzurePrep.Web
dotnet test
```

## Fora de escopo por agora

- Autenticação/multi-usuário
- Deploy em nuvem
- Outros exames além do AZ-900 (mas o modelo de dados já deve suportar)