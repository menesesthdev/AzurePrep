using AzurePrep.Domain.Entidades;
using AzurePrep.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace AzurePrep.Infrastructure.Persistence;

/// <summary>
/// Popula o banco com o exame AZ-900 e com as questões dos arquivos de seed embutidos.
///
/// IMPORTANTE (regra não negociável do projeto): nenhuma questão do banco é ou pode ser copiada
/// de dumps/vazamentos de prova. Todas são escritas a partir do Skills Measured outline público,
/// com engenharia de distrator e explicação por distrator — ver <c>docs/formato-questoes.md</c>.
///
/// O seed é <b>idempotente e incremental</b>: roda a cada startup, insere o que é novo e
/// atualiza o que mudou, casando pelo <c>ExternalId</c> do arquivo. Adicionar um lote de questões
/// é adicionar um JSON — nada aqui muda.
/// </summary>
public static class AzurePrepDbSeeder
{
    private const string CodigoDoExame = "AZ-900";

    /// <summary>
    /// Definição do exame e dos seus domínios, com os pesos do Skills Measured outline oficial.
    /// As áreas são identificadas pelo slug, que é o que os arquivos de questões referenciam.
    /// </summary>
    private static readonly (string Key, string Name, decimal Weight)[] Areas =
    {
        ("conceitos-de-nuvem", "Descrever conceitos de nuvem", 27.5m),
        ("arquitetura", "Descrever arquitetura e serviços do Azure", 37.5m),
        ("governanca", "Descrever gestão e governança do Azure", 32.5m)
    };

    public static async Task SemearAsync(AzurePrepDbContext db, CancellationToken cancellationToken = default)
    {
        var exam = await db.Exams
            .Include(e => e.SkillAreas)
            .FirstOrDefaultAsync(e => e.Code == CodigoDoExame, cancellationToken);

        if (exam is null)
        {
            exam = CriarExame();
            db.Exams.Add(exam);
            await db.SaveChangesAsync(cancellationToken);
        }

        await AplicarQuestoesAsync(db, exam, cancellationToken);
    }

    private static Exame CriarExame()
    {
        // 40 itens em 45 minutos espelha a entrega real do AZ-900. TotalQuestions deixou de ser
        // "o tamanho do banco" e passou a ser o tamanho da PROVA: o sorteio escolhe 40 entre
        // centenas a cada tentativa.
        var exam = new Exame(
            code: CodigoDoExame,
            name: "Microsoft Azure Fundamentals",
            timeLimitMinutes: 45,
            passingScorePercent: 70,
            totalQuestions: 40);

        foreach (var (key, name, weight) in Areas)
        {
            exam.AdicionarAreaDeHabilidade(key, name, weight);
        }

        return exam;
    }

    /// <summary>
    /// Aplica todos os lotes embutidos. Questão nova entra; questão já conhecida tem enunciado,
    /// explicação e alternativas reescritos no lugar, preservando o Id — é isso que permite
    /// corrigir um erro de conteúdo sem invalidar as tentativas que já responderam aquele item.
    /// </summary>
    private static async Task AplicarQuestoesAsync(
        AzurePrepDbContext db,
        Exame exam,
        CancellationToken cancellationToken)
    {
        var arquivos = CatalogoDeQuestoesDeSeed.Carregar()
            .Where(a => string.Equals(a.ExameCode, exam.Code, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (arquivos.Count == 0)
        {
            return;
        }

        var areasPorKey = exam.SkillAreas.ToDictionary(a => a.Key, StringComparer.OrdinalIgnoreCase);

        var problemas = CatalogoDeQuestoesDeSeed.Validar(arquivos, areasPorKey.Keys.ToList());
        if (problemas.Count > 0)
        {
            // Fail fast: subir com banco de questões inválido produziria prova com gabarito
            // errado, que é pior que não subir. O teste de integridade pega isso antes do deploy.
            throw new InvalidOperationException(
                "Banco de questões inválido:" + Environment.NewLine + string.Join(Environment.NewLine, problemas));
        }

        // O Guid vem do ExternalId, então buscar por Id já cobre a colisão de chave entre exames —
        // não é preciso uma segunda consulta por ExternalId. As questões deste exame entram na
        // busca mesmo sem estar nos arquivos: são elas que podem precisar ser aposentadas.
        var idsDosArquivos = arquivos
            .SelectMany(a => a.Questoes)
            .Select(q => GuidDeterministico.DeChave(q.Id))
            .ToHashSet();

        var existentes = await db.Questions
            .Include(q => q.Options)
            .Where(q => q.ExamId == exam.Id || idsDosArquivos.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, cancellationToken);

        foreach (var arquivo in arquivos)
        {
            var area = areasPorKey[arquivo.Area];

            foreach (var seed in arquivo.Questoes)
            {
                CatalogoDeQuestoesDeSeed.TentarConverterTipo(seed.Tipo, out var tipo);
                var id = GuidDeterministico.DeChave(seed.Id);

                if (existentes.TryGetValue(id, out var questao))
                {
                    if (questao.ExamId != exam.Id)
                    {
                        // A chave de seed é global (índice único em ExternalId) e o Guid deriva
                        // dela. Sem esta checagem, o segundo exame a usar a mesma chave quebraria
                        // no SaveChanges com uma violação de índice sem explicação nenhuma.
                        throw new InvalidOperationException(
                            $"A chave de questão '{seed.Id}' já pertence a outro exame. " +
                            "Chaves de seed são globais — prefixe-as com o código do exame.");
                    }

                    AtualizarQuestao(db, questao, area.Id, seed, tipo);
                }
                else
                {
                    var nova = exam.AdicionarQuestao(area.Id, seed.Id, seed.Enunciado, tipo, seed.Explicacao, seed.Topico, id);
                    for (var i = 0; i < seed.Opcoes.Count; i++)
                    {
                        nova.AdicionarOpcao(seed.Opcoes[i].Texto, seed.Opcoes[i].Correta, i, GuidDeterministico.DeOpcao(seed.Id, i));
                    }

                    // Add explícito: a chave já vem preenchida do domínio, então o EF não tem como
                    // inferir sozinho que é inserção — mesmo motivo do Add de respostas no repositório.
                    db.Questions.Add(nova);
                }
            }
        }

        SincronizarAtivas(existentes.Values, exam.Id, idsDosArquivos);

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Alinha o pool ao conteúdo dos arquivos: o que saiu deles é aposentado, o que voltou é
    /// reativado. É o que faltava para os arquivos serem de fato a fonte única da verdade — antes
    /// disso, apagar uma questão do JSON não a tirava do sorteio, e não havia como retirar de
    /// circulação uma questão com gabarito errado.
    /// </summary>
    /// <remarks>
    /// Aposentar, nunca apagar: as tentativas já feitas apontam para a questão, e o FK
    /// <c>Restrict</c> de <c>ExamAttemptQuestions</c> existe justamente para impedir o DELETE.
    /// ⚠️ Isso alcança também as questões do seed hardcoded antigo (marcadas como
    /// <c>az900-legado-*</c> pela migration <c>SorteioDeQuestoes</c>): como não têm chave em
    /// arquivo nenhum, saem do sorteio na primeira execução. É o resultado pretendido — questão
    /// que não está num arquivo não tem como ser corrigida nem revisada. Para manter alguma
    /// delas, basta reescrevê-la num JSON.
    /// </remarks>
    private static void SincronizarAtivas(
        IEnumerable<Questao> conhecidas,
        Guid examId,
        IReadOnlySet<Guid> idsDosArquivos)
    {
        foreach (var questao in conhecidas.Where(q => q.ExamId == examId))
        {
            var estaNosArquivos = idsDosArquivos.Contains(questao.Id);

            if (!estaNosArquivos && questao.IsActive)
            {
                questao.Aposentar();
            }
            else if (estaNosArquivos && !questao.IsActive)
            {
                questao.Reativar();
            }
        }
    }

    private static void AtualizarQuestao(
        AzurePrepDbContext db,
        Questao questao,
        Guid skillAreaId,
        QuestaoDeSeed seed,
        Domain.Enums.TipoDeQuestao tipo)
    {
        questao.Atualizar(skillAreaId, seed.Enunciado, tipo, seed.Explicacao, seed.Topico);

        for (var i = 0; i < seed.Opcoes.Count; i++)
        {
            var opcao = questao.OpcaoNaPosicao(i);
            if (opcao is null)
            {
                db.AnswerOptions.Add(new OpcaoDeResposta(
                    questao.Id,
                    seed.Opcoes[i].Texto,
                    seed.Opcoes[i].Correta,
                    i,
                    GuidDeterministico.DeOpcao(seed.Id, i)));
            }
            else
            {
                opcao.Atualizar(seed.Opcoes[i].Texto, seed.Opcoes[i].Correta);
            }
        }

        // Alternativas que sobraram de uma versão anterior do arquivo saem — sem isso, reduzir
        // uma questão de 5 para 4 alternativas deixaria a quinta viva e selecionável.
        var sobrando = questao.Options.Where(o => o.OrderIndex >= seed.Opcoes.Count).ToList();
        if (sobrando.Count > 0)
        {
            db.AnswerOptions.RemoveRange(sobrando);
        }
    }
}
