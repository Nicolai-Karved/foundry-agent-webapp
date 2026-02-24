FROM mcr.microsoft.com/dotnet/sdk:9.0

WORKDIR /src

# Azure CLI is required for AzureCliCredential in local Docker development.
RUN apt-get update \
	&& apt-get install -y --no-install-recommends ca-certificates curl apt-transport-https lsb-release gnupg \
	&& curl -sL https://aka.ms/InstallAzureCLIDeb | bash \
	&& rm -rf /var/lib/apt/lists/*

# Pre-restore project dependencies for faster dev startup
COPY backend/WebApp.sln ./backend/
COPY backend/WebApp.Api/WebApp.Api.csproj ./backend/WebApp.Api/
COPY backend/WebApp.ServiceDefaults/WebApp.ServiceDefaults.csproj ./backend/WebApp.ServiceDefaults/
RUN dotnet restore ./backend/WebApp.Api/WebApp.Api.csproj

WORKDIR /src/backend/WebApp.Api

EXPOSE 8089

ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://0.0.0.0:8089
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV AZURE_CONFIG_DIR=/root/.azure

CMD ["dotnet", "run", "--urls", "http://0.0.0.0:8089"]
