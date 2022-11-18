FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS base
RUN apk add --no-cache rsync ca-certificates
ENV CONFIG_BACKUP__TEMPDIRECTORY="/tmp"
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build
WORKDIR /Build
COPY ["EventStoreBackup.csproj", "./"]
RUN dotnet restore "EventStoreBackup.csproj"
COPY . .
RUN dotnet build "EventStoreBackup.csproj" -c Release

FROM build AS publish
RUN dotnet publish "EventStoreBackup.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EventStoreBackup.dll"]
