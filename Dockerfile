FROM mcr.microsoft.com/dotnet/sdk:latest AS build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /app

RUN apt-get update && \
    apt-get -y upgrade && \
    apt-get -y autoremove

COPY *.cs* ./

RUN dotnet publish -c Release


FROM mcr.microsoft.com/dotnet/runtime:latest AS runtime
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /app

RUN apt-get update && \
    apt-get -y upgrade && \
    apt-get -y autoremove

RUN apt-get -y install python3 curl apt-transport-https ca-certificates gnupg

RUN echo "deb [signed-by=/usr/share/keyrings/cloud.google.gpg] https://packages.cloud.google.com/apt cloud-sdk main" | tee /etc/apt/sources.list.d/google-cloud-sdk.list && \
    curl -s https://packages.cloud.google.com/apt/doc/apt-key.gpg -o /usr/share/keyrings/cloud.google.gpg && \
    apt-get update && \
    apt-get -y install google-cloud-sdk

RUN echo "deb [signed-by=/usr/share/keyrings/kubernetes-archive-keyring.gpg] https://apt.kubernetes.io/ kubernetes-xenial main" | tee /etc/apt/sources.list.d/kubernetes.list && \
    curl -s https://packages.cloud.google.com/apt/doc/apt-key.gpg -o /usr/share/keyrings/kubernetes-archive-keyring.gpg && \
    apt-get update && \
    apt-get -y install kubectl

COPY --from=build /app/bin/Release/*/publish /app/

RUN ls | xargs sha256sum

ENTRYPOINT ["./getcontainers"]
