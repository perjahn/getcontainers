FROM mcr.microsoft.com/dotnet/sdk:9.0 as build

WORKDIR /app

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY . .

RUN dotnet publish -c Release


FROM mcr.microsoft.com/dotnet/runtime:9.0 as runtime

WORKDIR /app

COPY --from=build /app/bin/Release/*/publish .

ENTRYPOINT ["./getcontainers"]
