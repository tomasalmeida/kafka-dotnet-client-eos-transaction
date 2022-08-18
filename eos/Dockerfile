FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["*", "eos/"]
RUN dotnet restore "eos/eos.csproj"
COPY . .
WORKDIR "/src/eos"
RUN dotnet build "eos.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "eos.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "eos.dll"]
