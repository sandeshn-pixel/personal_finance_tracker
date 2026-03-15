FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . ./
RUN dotnet restore src/FinanceTracker.Api/FinanceTracker.Api.csproj --configfile NuGet.Config
RUN dotnet publish src/FinanceTracker.Api/FinanceTracker.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "FinanceTracker.Api.dll"]
