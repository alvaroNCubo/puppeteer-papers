# syntax=docker/dockerfile:1
#
# Tetris Increment C2 — runtime image for TetrisStageCluster (one StageV2 node
# per container). Mirrors Paper 7's Dockerfile.host: the container does NOT
# compile inside Docker. The engine (a Puppeteer checkout, $env:PuppeteerEngine) lives on the host by an
# absolute/relative project path, so the operator pre-publishes on the host and
# the image just copies the framework-dependent artifacts in:
#
#   dotnet publish sm-cluster/TetrisStageCluster.csproj -c Release -o docker/build/host
#   cd docker && docker compose build      # (docker/run-demo.sh does both)
#
# Choreography's StageManager uses Kestrel for the real TLS peer transport, so
# the ASP.NET Core shared framework must be present at runtime — the aspnet:9.0
# base image carries it (the plain runtime:9.0 image would not).

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY build/host/ ./

ENV TETRIS_DATA_DIR=/data \
    TETRIS_BOOTSTRAP_DIR=/bootstrap

RUN mkdir -p /data /bootstrap

# 5443 = TLS peer transport. It is advertised to the other containers on the
# compose bridge network (PUPPETEER_ADVERTISE_URL=https://tetris-<id>:5443/) and
# is NOT published to the host — peer traffic stays on the internal network.
EXPOSE 5443

ENTRYPOINT ["dotnet", "/app/TetrisStageCluster.dll"]
