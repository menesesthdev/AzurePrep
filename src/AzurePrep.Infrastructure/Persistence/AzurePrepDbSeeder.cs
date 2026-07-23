using AzurePrep.Domain.Entidades;
using AzurePrep.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AzurePrep.Infrastructure.Persistence;

/// <summary>
/// Popula o banco com o exame AZ-900 e um conjunto inicial de questões ORIGINAIS.
///
/// IMPORTANTE (regra não negociável do projeto): nenhuma questão aqui é ou pode ser
/// copiada de dumps/vazamentos de prova. Todas foram escritas a partir do Skills Measured
/// outline público do AZ-900, usando engenharia de distrator (cada alternativa errada é a
/// resposta certa de uma pergunta ligeiramente diferente) e explicação por distrator.
/// </summary>
public static class AzurePrepDbSeeder
{
    public static async Task SemearAsync(AzurePrepDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Exams.AnyAsync(cancellationToken))
        {
            return; // já semeado
        }

        // Domínios e pesos vêm do Skills Measured outline oficial do AZ-900 (não inventados).
        var exam = new Exame(
            code: "AZ-900",
            name: "Microsoft Azure Fundamentals",
            timeLimitMinutes: 45,
            passingScorePercent: 70,
            totalQuestions: 8);

        var cloudConcepts = exam.AdicionarAreaDeHabilidade("Descrever conceitos de nuvem", 27.5m);
        var architecture = exam.AdicionarAreaDeHabilidade("Descrever arquitetura e serviços do Azure", 37.5m);
        var governance = exam.AdicionarAreaDeHabilidade("Descrever gestão e governança do Azure", 32.5m);

        MontarQ1SharedResponsibility(exam, cloudConcepts);
        MontarQ2CapexOpex(exam, cloudConcepts);
        MontarQ3ScaleOut(exam, cloudConcepts);
        MontarQ4AvailabilityZones(exam, architecture);
        MontarQ5ServerlessContainers(exam, architecture);
        MontarQ6StorageTier(exam, architecture);
        MontarQ7AzurePolicy(exam, governance);
        MontarQ8PricingCalculator(exam, governance);

        // Add(exam) insere em cascata skill areas, questões e opções (navegações mapeadas).
        db.Exams.Add(exam);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Questao MontarQ1SharedResponsibility(Exame exam, AreaDeHabilidade area)
    {
        var q = exam.AdicionarQuestao(area.Id,
            "Uma empresa migrou uma aplicação para máquinas virtuais (modelo IaaS) no Azure. " +
            "Pelo modelo de responsabilidade compartilhada, qual tarefa continua sendo " +
            "responsabilidade do CLIENTE — e não da Microsoft?",
            TipoDeQuestao.EscolhaUnica,
            "Em IaaS, o cliente é responsável pelo sistema operacional convidado, o que inclui " +
            "aplicar patches (correta). Segurança física dos data centers, substituição de hardware " +
            "com falha e redundância de energia/refrigeração são sempre responsabilidade da Microsoft, " +
            "independentemente do modelo de serviço — por isso as demais estão erradas.");
        q.AdicionarOpcao("Aplicar patches de segurança no sistema operacional convidado das VMs.", true, 0);
        q.AdicionarOpcao("Manter a segurança física dos data centers onde as VMs rodam.", false, 1);
        q.AdicionarOpcao("Substituir hardware de servidor com falha no data center.", false, 2);
        q.AdicionarOpcao("Garantir a redundância de energia e refrigeração do data center.", false, 3);
        return q;
    }

    private static Questao MontarQ2CapexOpex(Exame exam, AreaDeHabilidade area)
    {
        var q = exam.AdicionarQuestao(area.Id,
            "Uma startup quer lançar um produto sem fazer um grande investimento inicial em servidores, " +
            "pagando apenas pelo que consumir mês a mês. Qual característica do modelo de nuvem atende " +
            "DIRETAMENTE a esse requisito?",
            TipoDeQuestao.EscolhaUnica,
            "Pagar pelo consumo mensal, sem investimento inicial, é despesa operacional (OpEx) no modelo " +
            "pay-as-you-go (correta). CapEx é justamente o investimento inicial que a startup quer evitar. " +
            "SLA trata de disponibilidade garantida, não de forma de pagamento. Elasticidade resolve variação " +
            "de carga e é atraente, mas o requisito central — evitar investimento inicial — é definido por OpEx.");
        q.AdicionarOpcao("Despesa operacional (OpEx) com modelo de consumo pay-as-you-go.", true, 0);
        q.AdicionarOpcao("Despesa de capital (CapEx) para adquirir a infraestrutura antecipadamente.", false, 1);
        q.AdicionarOpcao("Alta disponibilidade garantida por SLA.", false, 2);
        q.AdicionarOpcao("Elasticidade para escalar automaticamente sob demanda.", false, 3);
        return q;
    }

    private static Questao MontarQ3ScaleOut(Exame exam, AreaDeHabilidade area)
    {
        var q = exam.AdicionarQuestao(area.Id,
            "Afirmação: \"Adicionar mais instâncias de uma máquina virtual para distribuir a carga é " +
            "um exemplo de escalabilidade vertical (scale up).\" Essa afirmação é verdadeira?",
            TipoDeQuestao.SimNao,
            "A afirmação é FALSA. Adicionar mais instâncias é escalabilidade horizontal (scale out). " +
            "Escalabilidade vertical (scale up) é aumentar os recursos de uma única instância (mais CPU/RAM). " +
            "Confundir scale out com scale up é o erro que a questão testa.");
        q.AdicionarOpcao("Verdadeiro", false, 0);
        q.AdicionarOpcao("Falso", true, 1);
        return q;
    }

    private static Questao MontarQ4AvailabilityZones(Exame exam, AreaDeHabilidade area)
    {
        var q = exam.AdicionarQuestao(area.Id,
            "Uma aplicação roda em várias VMs em uma única região do Azure. A equipe precisa proteger a " +
            "aplicação contra a falha de um data center INTEIRO dentro dessa região, mantendo o SLA mais alto " +
            "possível. Qual recurso atende a esse requisito?",
            TipoDeQuestao.EscolhaUnica,
            "Availability Zones são data centers fisicamente separados dentro de uma região; distribuir as VMs " +
            "entre zonas protege contra a falha de um data center inteiro (correta). Um Availability Set protege " +
            "contra falhas de rack/host DENTRO de um mesmo data center, não contra a perda do data center todo. " +
            "Um par de regiões protege contra falha de toda a região, mas o requisito é resiliência dentro da " +
            "mesma região. Backup ajuda na recuperação de dados, não em manter a aplicação disponível.");
        q.AdicionarOpcao("Distribuir as VMs entre Availability Zones.", true, 0);
        q.AdicionarOpcao("Colocar as VMs em um Availability Set.", false, 1);
        q.AdicionarOpcao("Usar um par de regiões (region pair).", false, 2);
        q.AdicionarOpcao("Habilitar backup automático das VMs.", false, 3);
        return q;
    }

    private static Questao MontarQ5ServerlessContainers(Exame exam, AreaDeHabilidade area)
    {
        var q = exam.AdicionarQuestao(area.Id,
            "Uma equipe quer executar containers sem provisionar nem gerenciar máquinas virtuais ou um cluster " +
            "de orquestração, pagando apenas pela execução. Selecione DUAS opções que atendem a esse requisito.",
            TipoDeQuestao.EscolhaMultipla,
            "Azure Container Instances executa containers isolados sob demanda, sem gerenciar infraestrutura " +
            "(correta). Azure Container Apps é serverless para containers, também sem administrar VMs ou cluster " +
            "(correta). Azure Kubernetes Service é Kubernetes gerenciado, mas você ainda administra e paga pelos " +
            "nós (VMs) do cluster. Azure Virtual Machines contraria o requisito, pois exige provisionar e gerenciar " +
            "a máquina.");
        q.AdicionarOpcao("Azure Container Instances (ACI).", true, 0);
        q.AdicionarOpcao("Azure Container Apps.", true, 1);
        q.AdicionarOpcao("Azure Kubernetes Service (AKS).", false, 2);
        q.AdicionarOpcao("Azure Virtual Machines.", false, 3);
        return q;
    }

    private static Questao MontarQ6StorageTier(Exame exam, AreaDeHabilidade area)
    {
        var q = exam.AdicionarQuestao(area.Id,
            "Uma empresa precisa armazenar logs de auditoria que quase nunca são acessados, mas devem ser " +
            "retidos por 7 anos ao MENOR custo de armazenamento possível. Qual camada de acesso do Azure Blob " +
            "Storage é a mais adequada?",
            TipoDeQuestao.EscolhaUnica,
            "A camada Archive tem o menor custo de armazenamento e é ideal para dados raramente acessados e " +
            "retidos por longos períodos, aceitando maior latência para recuperar (correta). A camada Cool serve " +
            "para dados infrequentes com acesso ocasional, custando mais que a Archive. A camada Hot é para acesso " +
            "frequente, com o maior custo de armazenamento. Premium prioriza desempenho e baixa latência — o " +
            "oposto de 'menor custo'.");
        q.AdicionarOpcao("Archive", true, 0);
        q.AdicionarOpcao("Cool", false, 1);
        q.AdicionarOpcao("Hot", false, 2);
        q.AdicionarOpcao("Premium", false, 3);
        return q;
    }

    private static Questao MontarQ7AzurePolicy(Exame exam, AreaDeHabilidade area)
    {
        var q = exam.AdicionarQuestao(area.Id,
            "Uma organização quer IMPEDIR que qualquer usuário crie recursos fora da região Brazil South, " +
            "garantindo conformidade em todas as assinaturas. Qual serviço do Azure atende diretamente a esse " +
            "requisito?",
            TipoDeQuestao.EscolhaUnica,
            "Azure Policy impõe regras sobre as propriedades dos recursos, como restringir as regiões permitidas " +
            "na criação (correta). RBAC define QUEM pode executar ações, mas não limita atributos como a região do " +
            "recurso. Microsoft Entra ID cuida de identidade e autenticação, não de conformidade de recursos. " +
            "Resource locks evitam exclusão ou modificação acidental, mas não controlam em qual região um recurso " +
            "pode ser criado.");
        q.AdicionarOpcao("Azure Policy", true, 0);
        q.AdicionarOpcao("Controle de acesso baseado em função (RBAC)", false, 1);
        q.AdicionarOpcao("Microsoft Entra ID (Azure AD)", false, 2);
        q.AdicionarOpcao("Resource locks", false, 3);
        return q;
    }

    private static Questao MontarQ8PricingCalculator(Exame exam, AreaDeHabilidade area)
    {
        var q = exam.AdicionarQuestao(area.Id,
            "Antes de migrar, uma empresa quer ESTIMAR quanto pagará por um conjunto específico de serviços do " +
            "Azure (VMs, storage e banco de dados) que ainda NÃO provisionou. Qual ferramenta é a mais adequada?",
            TipoDeQuestao.EscolhaUnica,
            "O Pricing Calculator estima o custo de serviços do Azure ainda não provisionados, montando um " +
            "orçamento por serviço (correta). O Microsoft Cost Management monitora e controla gastos de recursos " +
            "que já existem, não serve para estimar antes de criar. O Azure Advisor recomenda otimizações de " +
            "recursos existentes. O TCO Calculator compara o custo total de propriedade entre on-premises e Azure, " +
            "e não a estimativa de uma lista específica de serviços a provisionar.");
        q.AdicionarOpcao("Azure Pricing Calculator", true, 0);
        q.AdicionarOpcao("Microsoft Cost Management", false, 1);
        q.AdicionarOpcao("Azure Advisor", false, 2);
        q.AdicionarOpcao("TCO Calculator", false, 3);
        return q;
    }
}
