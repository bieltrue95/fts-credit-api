FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/FtsCredit.Api/FtsCredit.Api.csproj", "src/FtsCredit.Api/"]
RUN dotnet restore "src/FtsCredit.Api/FtsCredit.Api.csproj"
COPY . .
WORKDIR "/src/src/FtsCredit.Api"
RUN dotnet build "FtsCredit.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FtsCredit.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FtsCredit.Api.dll"]
