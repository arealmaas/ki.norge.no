FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["KiNorge.Cms.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Run as root to avoid permission issues
USER root

# Install Litestream for SQLite replication to Azure Blob Storage
ADD https://github.com/benbjohnson/litestream/releases/download/v0.3.14/litestream-v0.3.14-linux-amd64.tar.gz /tmp/litestream.tar.gz
RUN tar -C /usr/local/bin -xzf /tmp/litestream.tar.gz && rm /tmp/litestream.tar.gz

# Ensure directories exist with correct permissions
RUN mkdir -p /app/umbraco/Data /app/umbraco/Logs /app/umbraco/mediacache /app/wwwroot/media && \
    chmod -R 777 /app/umbraco /app/wwwroot/media

# Copy Litestream config and entrypoint
COPY litestream.yml /etc/litestream.yml
COPY entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

CMD ["/app/entrypoint.sh"]
