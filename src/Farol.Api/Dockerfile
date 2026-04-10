FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Farol.sln", "./"]
COPY ["NuGet.Config", "./"]
COPY ["src/Farol.Api/Farol.Api.csproj", "src/Farol.Api/"]
COPY ["src/Farol.Infrastructure/Farol.Infrastructure.csproj", "src/Farol.Infrastructure/"]
COPY ["src/Farol.Domain/Farol.Domain.csproj", "src/Farol.Domain/"]

RUN dotnet restore "src/Farol.Api/Farol.Api.csproj"

COPY . .

RUN dotnet publish "src/Farol.Api/Farol.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000

ENTRYPOINT ["sh", "-c", "dotnet Farol.Api.dll --urls http://0.0.0.0:${PORT:-10000}"]
