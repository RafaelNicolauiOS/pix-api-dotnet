# ---- Estágio 1: build ----
# Imagem com o SDK completo (compilador, NuGet) só para gerar a aplicação publicada.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Primeiro só os .csproj: enquanto eles não mudarem, o Docker reaproveita a camada com os pacotes
# NuGet já baixados (o mesmo truque do "dependency:go-offline" no Dockerfile do Java).
COPY global.json Directory.Build.props PixApi.sln ./
COPY src/PixApi.Domain/PixApi.Domain.csproj src/PixApi.Domain/
COPY src/PixApi.Application/PixApi.Application.csproj src/PixApi.Application/
COPY src/PixApi.Infrastructure/PixApi.Infrastructure.csproj src/PixApi.Infrastructure/
COPY src/PixApi.Api/PixApi.Api.csproj src/PixApi.Api/
RUN dotnet restore src/PixApi.Api/PixApi.Api.csproj

COPY src/ src/
# Os testes rodam no CI (GitHub Actions); aqui só compilamos em Release.
RUN dotnet publish src/PixApi.Api/PixApi.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ---- Estágio 2: runtime ----
# Só o runtime do ASP.NET (sem SDK nem compilador): imagem menor e com menos superfície de ataque.
# Debian (e não Alpine) porque traz ICU e tzdata: R$ em pt-BR e o fuso America/Sao_Paulo.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# Não rodar como root: as imagens .NET 8 já trazem o usuário "app" (UID 1654 em $APP_UID).
USER $APP_UID

# As imagens .NET 8 escutam na 8080 por padrão (o Render sobrescreve com a variável PORT).
EXPOSE 8080
ENTRYPOINT ["dotnet", "PixApi.Api.dll"]
