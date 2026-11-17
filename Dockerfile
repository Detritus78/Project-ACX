FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
ARG TARGETARCH
WORKDIR /Source

# copy csproj and restore as distinct layers
COPY ./Source/*.sln ./
COPY ./Source/ACE.Adapter/*.csproj ./ACE.Adapter/
COPY ./Source/ACE.Common/*.csproj ./ACE.Common/
COPY ./Source/ACE.Database/*.csproj ./ACE.Database/
COPY ./Source/ACE.Database.Tests/*.csproj ./ACE.Database.Tests/
COPY ./Source/ACE.DatLoader/*.csproj ./ACE.DatLoader/
COPY ./Source/ACE.DatLoader.Tests/*.csproj ./ACE.DatLoader.Tests/
COPY ./Source/ACE.Entity/*.csproj ./ACE.Entity/
COPY ./Source/ACE.Server/*.csproj ./ACE.Server/
COPY ./Source/ACE.Server.Tests/*.csproj ./ACE.Server.Tests/

RUN dotnet restore -a $TARGETARCH

# copy and publish app and libraries
COPY . ../.
RUN dotnet publish ./ACE.Server/ACE.Server.csproj -a $TARGETARCH -c release -o /ace --no-restore

# final stage/image
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble
ARG DEBIAN_FRONTEND="noninteractive"
WORKDIR /ace

# install net-tools (netstat for health check) & cleanup
RUN apt-get update && \
    apt-get install --no-install-recommends -y \
    net-tools && \
    apt-get clean && \
    rm -rf \
    /tmp/* \
    /var/lib/apt/lists/* \
    /var/tmp/*

# add app from build
COPY --from=build /ace .

# Project ACX: run as the image's own non-root user (app, APP_UID 1654), which has no account on the host (JJ 2026-11-17:
# "lets do the non root container fix"). The only writes in /ace itself: at start ACE copies Config/Config.js and
# Config/log4net.config over these two, and keeps applied_updates.txt under DatabaseSetupScripts/Updates/<db> (the rest goes to
# the mounted folders; an admin's /recordcast log would also land in /ace, and fails).
RUN touch Config.js log4net.config && chown -R $APP_UID:$APP_UID Config.js log4net.config DatabaseSetupScripts/Updates
USER $APP_UID

# run app
ENTRYPOINT ["dotnet", "ACE.Server.dll"]

# ports and volumes
EXPOSE 9000-9001/udp
VOLUME /ace/Config /ace/Content /ace/Dats /ace/Logs /ace/Mods

# health check
HEALTHCHECK --start-period=5m --interval=1m --timeout=3s \
  CMD netstat -an | grep 9000 > /dev/null; if [ 0 != $? ]; then exit 1; fi;
