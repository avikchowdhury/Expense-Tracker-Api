# ASP.NET Core backend Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ExpenseTracker.Api.csproj .
RUN dotnet restore "ExpenseTracker.Api.csproj"
COPY . .
WORKDIR "/src/ExpenseTracker.Api"
RUN dotnet build "ExpenseTracker.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ExpenseTracker.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Ensure avatars directory exists and is writable
RUN mkdir -p /app/avatars && chmod -R 777 /app/avatars
ENTRYPOINT ["dotnet", "ExpenseTracker.Api.dll"]
