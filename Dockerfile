# =============================================================================
# AzurePrep — imagem da aplicação web.
#
# Build em dois estágios: o SDK (grande, com compilador e NuGet) fica no estágio
# descartado, e a imagem final leva só o runtime ASP.NET e os arquivos publicados.
# =============================================================================

# ---- Estágio de build -------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Os .csproj entram ANTES do resto do código de propósito: o restore só reexecuta quando uma
# dependência muda, e não a cada linha editada. Sem isso, todo build baixaria os pacotes de novo.
COPY src/AzurePrep.Domain/AzurePrep.Domain.csproj                 src/AzurePrep.Domain/
COPY src/AzurePrep.Application/AzurePrep.Application.csproj       src/AzurePrep.Application/
COPY src/AzurePrep.Infrastructure/AzurePrep.Infrastructure.csproj src/AzurePrep.Infrastructure/
COPY src/AzurePrep.Web/AzurePrep.Web.csproj                       src/AzurePrep.Web/
RUN dotnet restore src/AzurePrep.Web/AzurePrep.Web.csproj

COPY src/ src/
RUN dotnet publish src/AzurePrep.Web/AzurePrep.Web.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish

# ---- Imagem final -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# 8080 e não 80: a imagem roda como usuário sem privilégio, e porta abaixo de 1024 exigiria root.
# É o padrão das imagens .NET desde a 8.
#
# A segunda porta é só para /metrics (9464 é a convenção do OpenTelemetry). Duas portas, e não
# uma, porque o endpoint de métricas não pede autenticação — o Prometheus não sabe fazer login — e
# expõe números do produto. Separando, basta o compose publicar a 8080 no host para que /metrics
# exista apenas dentro da rede do Docker. É defesa de rede, e não de senha, e é sólida enquanto a
# 9464 não for publicada nem exposta por um proxy reverso.
#
# ⚠️ Esta lista e Observabilidade:PortaDeMetricas precisam concordar. Se discordarem, a aplicação
# sobe igual e avisa no log — /metrics respondendo 404 é a falha esperada.
ENV ASPNETCORE_HTTP_PORTS="8080;9464"

COPY --from=build /app/publish .

# App_Data guarda o banco SQLite e as chaves de Data Protection — é o diretório que recebe o
# volume. Criar e dar posse AQUI é o que permite a aplicação escrever nele depois de trocar de
# usuário: um volume nomeado herda dono e permissão do ponto de montagem na imagem.
#
# APP_UID é definido pelas próprias imagens .NET (o usuário sem privilégio que elas já traçem);
# usar a variável em vez do nome "app" segue a convenção da Microsoft e não depende de o nome
# continuar o mesmo em versões futuras.
RUN mkdir -p /app/App_Data \
    && chown -R $APP_UID:$APP_UID /app/App_Data

# Sem root: se a aplicação for comprometida, o processo não tem privilégio para reescrever a
# imagem nem escalar no host.
USER $APP_UID

EXPOSE 8080
EXPOSE 9464

# Sem HEALTHCHECK aqui de propósito: a imagem de runtime não traz curl nem wget, e instalar um
# só para isso engordaria a imagem. O endpoint /health existe — quem orquestrar (compose com
# curl, proxy, Kubernetes) aponta para ele.
ENTRYPOINT ["dotnet", "AzurePrep.Web.dll"]
