# ASP.NET Core backend Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ExpenseTracker.Api.csproj .
RUN dotnet restore "ExpenseTracker.Api.csproj"
COPY . .
RUN dotnet build "ExpenseTracker.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ExpenseTracker.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV Storage__RootPath=/app_data
RUN mkdir -p /app_data/avatars /app_data/receipts && chmod -R 777 /app_data
VOLUME ["/app_data"]
ENTRYPOINT ["dotnet", "ExpenseTracker.Api.dll"]
