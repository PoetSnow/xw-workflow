# 构建上下文：仓库根目录
# docker build -f src/Services/XW.Agent.Host/Dockerfile -t xw-agent:local .
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props XW.sln ./
COPY src/ ./src/

RUN dotnet restore "src/Services/XW.Agent.Host/XW.Agent.Host.csproj"
RUN dotnet publish "src/Services/XW.Agent.Host/XW.Agent.Host.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "XW.Agent.Host.dll"]
