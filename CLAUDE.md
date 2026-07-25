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

## Autenticação (conta local + login social)

Login **obrigatório** para fazer simulado. Dois caminhos, ambos chegando na mesma entidade `Usuario`: **conta local** (e-mail + senha, com cadastro em `/conta/cadastrar`) e **provedor externo** — Google, LinkedIn, GitHub. Na tela de login o formulário de e-mail/senha vem acima dos botões sociais e o link de cadastro abaixo deles.

- **Sem ASP.NET Core Identity**, inclusive para a conta local. Cookie de autenticação + handlers OAuth + hash de senha próprio, tudo mapeando para a entidade `Usuario` do Domain. Evita as ~7 tabelas do Identity e mantém o schema no padrão do projeto.
- **Senha nunca em texto**: só `PasswordHash` (nulo em conta social) e via `IHasherDeSenha`/`HasherDeSenhaPbkdf2` — PBKDF2-HMAC-SHA256, 600k iterações, salt por senha, comparação em tempo constante. O hash carrega os próprios parâmetros (`pbkdf2-sha256$iteracoes$salt$hash`), então subir o fator de trabalho (ou trocar o algoritmo) não invalida senha já cadastrada. `PoliticaDeSenha` no Domain é a única fonte do mínimo/máximo, lida por Application e pelas anotações da ViewModel.
- **Falha de login nunca distingue** e-mail inexistente, senha errada, conta bloqueada e conta que só existe num provedor social — todas devolvem `CredenciaisInvalidas` e a mesma mensagem, e os caminhos que não chegam a conferir o hash real ainda verificam um hash de referência para não vazar a resposta pelo tempo. Cadastro é a exceção: aí a duplicata é inevitável, então a mensagem diz o que fazer.
- **Duas camadas contra força bruta, de propósito.** (1) `LimiteDeTentativasSetup`: 10 requisições por IP a cada 5 min nos POSTs de login/cadastro/esqueci-senha/redefinir-senha, via rate limiter nativo; `OnRejected` manda `Retry-After` e redireciona para `/conta/login?limite=1` (redirect e não corpo 429 porque são formulários de navegador). ⚠️ O contador é **em memória, por instância** — mesmo gatilho do PostgreSQL: em multi-instância a cota se multiplica pelas réplicas. (2) `PoliticaDeTentativasDeLogin`: 5 falhas consecutivas bloqueiam a conta por 15 min. A primeira camada não segura ataque distribuído contra uma conta conhecida; a segunda não segura quem varre muitas contas. O aviso visível ("muitas tentativas") é só o da camada de IP, que é por máquina e não revela cadastro; o bloqueio por conta é **silencioso**, senão viraria confirmação de que aquele e-mail tem conta.
- **Bloqueio expira sozinho e nunca é permanente** — bloqueio longo viraria arma para trancar a conta de outra pessoa só errando a senha de propósito. Acerto zera o contador (o limite é de falhas *consecutivas*) e redefinir a senha libera o bloqueio, porque quem provou controlar o e-mail é o dono.
- **Redefinição de senha por link de uso único** (`/conta/esqueci-senha` → `/conta/redefinir-senha?token=…`): token de 256 bits em Base64Url, guardado como **hash SHA-256** (`IGeradorDeTokenSeguro`), validade de 1h (`PoliticaDeRedefinicaoDeSenha`), invalidado ao ser usado, ao pedir um link novo e ao concluir a troca. SHA-256 puro aqui é o certo, ao contrário da senha: token aleatório não tem dicionário a resistir, e o hash **precisa** ser determinístico para servir de chave de busca. A tela responde igual exista ou não conta com aquele e-mail, e o `GET` valida o link antes de mostrar o formulário. Não autentica automaticamente depois da troca (ao contrário do cadastro): o cookie não tem selo de segurança, então sessões antigas em outros dispositivos **continuam válidas** — dívida conhecida.
- **E-mail via `IEnviadorDeEmail`**, registrado conforme a seção `Email` do config: com `Email:SmtpHost` sai por SMTP (`EnviadorDeEmailSmtp`); sem ele, `EnviadorDeEmailParaLog` grava a mensagem inteira no log em nível Warning — é assim que se testa "esqueci minha senha" em desenvolvimento, copiando o link do console. Falha de envio é registrada e engolida, porque propagá-la mudaria a resposta só no caminho em que o e-mail existe.
- **Dois esquemas de cookie** (`EsquemasDeAutenticacao`): `Aplicacao` é a sessão; `Externo` é temporário e só existe entre o callback do provedor e a criação da sessão. É esse intervalo que permite trocar as claims externas por um `Usuario` local.
- **Chave natural da identidade é o par (`Provider`, `ProviderKey`)**, nunca o e-mail — o GitHub pode não devolver e-mail, e a mesma pessoa pode ter o mesmo e-mail em dois provedores. Na conta local o `ProviderKey` é o **e-mail normalizado** (`Usuario.NormalizarEmail`: trim + minúsculas), então "uma conta local por e-mail" cai no mesmo índice único, sem regra nova — e um e-mail que também aparece numa conta Google segue sendo outro usuário, exatamente como já valia entre Google e GitHub. Vincular contas (ou somar senha a uma conta social) é recurso à parte, ainda não implementado.
- **`NameIdentifier` no cookie da aplicação é o Id LOCAL do `Usuario`**, não o id do provedor. `IUsuarioAtual` lê exatamente essa claim.
- **Posse da tentativa é imposta na Application**, não no controller: `SessaoDeProvaService` resolve toda tentativa por `ObterTentativaDoUsuarioAsync`, que devolve `null` para tentativa de outro dono (o Web responde 404 e não confirma que o id existe). O controller não tem como esquecer de checar porque não é ele quem checa.
- **Política padrão exige autenticação** (`SetFallbackPolicy`); o que é público leva `[AllowAnonymous]` explícito — hoje login/callback, cadastro e a página de erro.
- **`/conta/entrar` é POST com antiforgery**, não GET: um GET permitiria login CSRF, prendendo a pessoa numa conta que não é dela. Vale igual para `/conta/entrar-com-senha` e `/conta/cadastrar`. `returnUrl` passa por `Url.IsLocalUrl` para barrar open redirect.
- **Validação de senha é server-side**, sem jQuery validation: a tela reexibe com as mensagens do `ModelState`. As telas de login/cadastro não carregam script nenhum, no mesmo espírito do resto do projeto.
- **Credenciais nunca no appsettings versionado** — usar `dotnet user-secrets` em `Authentication:{Google,GitHub,LinkedIn}:{ClientId,ClientSecret}`. Cada provedor só é registrado se tiver credencial, então a app sobe e a tela de login funciona com apenas um deles configurado — ou com nenhum, já que a conta local não depende de credencial externa (o bloco "ou continue com" simplesmente não aparece).
- **LinkedIn usa OpenID Connect** ("Sign In with LinkedIn using OpenID Connect", escopos `openid`/`profile`/`email`, userinfo em `/v2/userinfo`) — é preciso habilitar esse produto no app do LinkedIn, senão o callback volta com `unauthorized_scope`. **GitHub exige o escopo `user:email`**, sem ele não vem e-mail nenhum.

## Banco de dados

SQLite via EF Core Migrations. Arquivo em `src/AzurePrep.Web/App_Data/azureprep.db` (ajustável). Evitar recursos específicos de um único provider na modelagem — se o projeto crescer, a migração pra PostgreSQL deve ser barata.

**Decisão (mantida com login social):** seguir no SQLite, com **WAL habilitado** no startup (leitores param de bloquear o escritor). O que quebraria o SQLite não é volume nem autenticação, é **deploy multi-instância ou disco efêmero** — esse é o gatilho para migrar pro PostgreSQL, não uma data. É a disciplina provider-agnostic acima que mantém essa migração barata.

> ⚠️ Dívida conhecida: `ScorePercent` é `decimal` e o SQLite não tem tipo decimal nativo, então ordenação/comparação numérica é imprecisa. Hoje não morde porque ninguém ordena por nota — vai morder quando existir histórico ordenado ou ranking.

## Docker

`Dockerfile` (multi-stage: SDK 10 compila, `aspnet:10.0` roda) + `docker-compose.yml`. A imagem roda como usuário sem privilégio (`USER $APP_UID`), escuta na **8080** e se auto-inicializa: as migrations e o seed rodam no startup, então subir com volume vazio já cria o banco.

```bash
cp .env.exemplo .env          # credenciais de OAuth/SMTP; funciona vazio
docker compose up --build
# http://localhost:8080
```

- **`App_Data` é o único estado que precisa sobreviver** e é onde o volume nomeado `dados` monta: banco SQLite **e** chaves de Data Protection. Container tem disco efêmero — que é exatamente o gatilho de migração para PostgreSQL citado acima; o volume neutraliza o gatilho enquanto for **uma instância só**. Escalar réplicas continua sendo o momento de trocar de banco (e o limitador de tentativas, que é em memória, tem o mesmo limite).
- **As chaves de Data Protection são persistidas de propósito** (`PersistKeysToFileSystem` em `Program.cs`). Sem isso, cada restart geraria chaves novas: todos os cookies de sessão invalidados e os formulários quebrando com "the antiforgery token could not be decrypted". É o erro mais comum ao containerizar ASP.NET Core e não aparece em teste rápido, só depois do primeiro deploy.
- O aviso `No XML encryptor configured` no boot é esperado: em Linux não há DPAPI, então as chaves ficam em claro **dentro do volume**. Quem protege é o acesso ao volume; cifrá-las exigiria certificado, o que só faz sentido com um deploy real definido.
- **Volume nomeado, não bind mount.** O volume herda o dono do diretório na imagem (o usuário sem privilégio); uma pasta do host entraria com o UID do host e o processo não conseguiria gravar.
- **`dotnet user-secrets` não existe no container** — é ferramenta de desenvolvimento. No container tudo vem de variável de ambiente, com `__` no lugar de `:` (`Authentication__Google__ClientId`). O `.env` é gitignored; o modelo versionado é `.env.exemplo`.
- **Callback de OAuth tem de casar com a porta publicada** (`http://localhost:8080/signin-google` etc.). Trocar host/porta exige recadastrar no painel de cada provedor.
- `/health` responde `200 ok` sem tocar no banco e sem exigir login. Não há `HEALTHCHECK` no Dockerfile porque a imagem de runtime não traz curl/wget — quem orquestrar aponta para o endpoint.
- O aviso `Failed to determine the https port for redirect` também é esperado: o container serve HTTP e o TLS termina no proxy à frente. ⚠️ **Quando entrar um reverse proxy, é obrigatório configurar `UseForwardedHeaders`** — sem isso o ASP.NET monta os `redirect_uri` do OAuth com `http://` e os provedores recusam. Não está feito porque depende de saber qual é o proxy (`KnownProxies`), e middleware meio configurado aqui é risco de spoofing de IP — que também afetaria o limitador por IP.
- A imagem **não roda os testes** no build. `dotnet test` fica no fluxo local/CI, para o build da imagem não pagar esse tempo a cada deploy.

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

# SMTP para o e-mail de redefinição de senha (opcional) — ver a seção "Envio de e-mail".
dotnet user-secrets set "Email:SmtpHost"           "smtp.gmail.com"
dotnet user-secrets set "Email:SmtpPort"           "587"
dotnet user-secrets set "Email:Usuario"            "seu-endereco@gmail.com"
dotnet user-secrets set "Email:Senha"              "senha-de-app-de-16-caracteres"
dotnet user-secrets set "Email:RemetenteEndereco"  "seu-endereco@gmail.com"
```

### Envio de e-mail: três modos, do mais simples ao de produção

**1. Nada configurado (padrão) — link no log.** Sem `Email:SmtpHost` a app usa
`EnviadorDeEmailParaLog`: a mensagem inteira aparece no console em nível Warning e você copia o
link do terminal. É o suficiente para desenvolver e não exige conta em lugar nenhum.

**2. Servidor SMTP local de teste — vê o envio real, sem conta.** `tools/smtp-de-teste.py` é um
servidor SMTP mínimo (só stdlib) que imprime o e-mail decodificado e destaca o link:

```bash
python3 tools/smtp-de-teste.py           # terminal 1

# terminal 2 — vale só nesta execução, não grava nada:
Email__SmtpHost=127.0.0.1 Email__SmtpPort=1025 Email__UsarSsl=false \
    dotnet run --project src/AzurePrep.Web
```

Serve para exercitar `EnviadorDeEmailSmtp` de verdade (conexão, `MAIL FROM`, `DATA`) antes de
apontar para um provedor. Só escuta em `127.0.0.1` e não valida nada — desenvolvimento apenas.

**3. Gmail com senha de app — e-mail que chega mesmo, sem domínio próprio.** O Gmail não exige
domínio: o remetente é o próprio endereço da conta. Passos:

1. Ativar **verificação em duas etapas** na conta Google (sem isso o Google não oferece senha de
   app, e o acesso por senha comum foi descontinuado — não há como contornar).
2. Gerar uma **senha de app** em `myaccount.google.com/apppasswords` (16 caracteres, sem espaços).
3. Rodar os `dotnet user-secrets set` do bloco acima, com `Email:Usuario` **e**
   `Email:RemetenteEndereco` iguais ao endereço da conta — o Gmail recusa remetente que não seja
   a conta autenticada.

Limites que importam: ~500 mensagens/dia, e é conta pessoal — serve para desenvolvimento,
portfólio e uso próprio, não para base real de usuários. Quando houver deploy e usuários de
verdade, o caminho é um serviço transacional (Brevo, Resend, SES); a troca é só a classe
`EnviadorDeEmailSmtp`, porque o resto do código conhece apenas `IEnviadorDeEmail`.

Callback a cadastrar em cada provedor (ajuste host/porta): `/signin-google`, `/signin-github`, `/signin-linkedin`.

## Fora de escopo por agora

- Deploy em nuvem (a imagem Docker existe e roda local; publicar num provedor é outro passo)
- Outros exames além do AZ-900 (mas o modelo de dados já deve suportar)