# 构建上下文：仓库根目录
# docker build -f src/XW.Host.API/Dockerfile -t xw-host-api:local .
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 先拷贝工程文件以利用 restore 层缓存
COPY Directory.Build.props Directory.Packages.props XW.sln ./
COPY src/ ./src/

RUN dotnet restore "src/XW.Host.API/XW.Host.API.csproj"
RUN dotnet publish "src/XW.Host.API/XW.Host.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "XW.Host.API.dll"]
