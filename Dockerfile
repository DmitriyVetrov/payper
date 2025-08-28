# Ultra-lightweight Dockerfile for cheapest hosting
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /src

# Copy and restore (caching layer)
COPY ["ReceiptBot/ReceiptBot.csproj", "ReceiptBot/"]
RUN dotnet restore "ReceiptBot/ReceiptBot.csproj"

# Copy source and build
COPY . .
WORKDIR "/src/ReceiptBot"
RUN dotnet publish "ReceiptBot.csproj" \
    -c Release \
    -o /app/publish \
    -p:UseAppHost=false \
    --no-restore \
    --verbosity quiet

# Ultra-lightweight runtime
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS runtime
WORKDIR /app

# Install only SQLite (minimal size)
RUN apk add --no-cache sqlite

# Create non-root user (security)
RUN adduser -D -s /bin/sh appuser

# Copy app and set permissions
COPY --from=build /app/publish .
RUN mkdir -p /app/data && \
    chown -R appuser:appuser /app && \
    chmod +x /app/ReceiptBot.dll

# Switch to non-root user
USER appuser

# Minimal environment
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
ENV DATABASE_CONNECTION_STRING="Data Source=/app/data/receipts.db"
ENV ASPNETCORE_ENVIRONMENT=Production

# No health checks or extra ports (save resources)
ENTRYPOINT ["dotnet", "ReceiptBot.dll"]