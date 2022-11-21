FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS base
RUN apk add --no-cache rsync ca-certificates
EXPOSE 80/tcp
EXPOSE 443/tcp

ARG UID=1000
ARG GID=1000

#Setup user 1000/eventstore
#This is the user that eventstore runs under as well
RUN addgroup --gid ${GID} "eventstore" && \
    adduser \
    --disabled-password \
    --gecos "" \
    --ingroup "eventstore" \
    --no-create-home \
    --uid ${UID} \
    "eventstore"

#Ensure temporary directory is set
ENV CONFIG_BACKUP__TEMPDIRECTORY="/tmp"

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
