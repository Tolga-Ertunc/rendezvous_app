FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Rendezvous.Domain/Rendezvous.Domain.csproj src/Rendezvous.Domain/
COPY src/Rendezvous.Application/Rendezvous.Application.csproj src/Rendezvous.Application/
COPY src/Rendezvous.Infrastructure/Rendezvous.Infrastructure.csproj src/Rendezvous.Infrastructure/
COPY src/Rendezvous.Api/Rendezvous.Api.csproj src/Rendezvous.Api/
RUN dotnet restore src/Rendezvous.Api/Rendezvous.Api.csproj

COPY src/Rendezvous.Domain src/Rendezvous.Domain
COPY src/Rendezvous.Application src/Rendezvous.Application
COPY src/Rendezvous.Infrastructure src/Rendezvous.Infrastructure
COPY src/Rendezvous.Api src/Rendezvous.Api
RUN dotnet publish src/Rendezvous.Api/Rendezvous.Api.csproj -c Release -o /app/publish --no-restore

FROM build AS migrate
RUN dotnet tool install --global dotnet-ef --version 8.0.12
ENV PATH="${PATH}:/root/.dotnet/tools"
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "src/Rendezvous.Infrastructure/Rendezvous.Infrastructure.csproj", "--startup-project", "src/Rendezvous.Api/Rendezvous.Api.csproj"]

FROM runtime AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Rendezvous.Api.dll"]
