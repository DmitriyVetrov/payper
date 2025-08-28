# Multi-stage Dockerfile for ReceiptBot
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["ReceiptBot/ReceiptBot.csproj", "ReceiptBot/"]
RUN dotnet restore "ReceiptBot/ReceiptBot.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/ReceiptBot"
RUN dotnet publish "ReceiptBot.csproj" -c Release -o /app/publish -p:UseAppHost=false

# Runtime stage - Use ASP.NET Core runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Install sqlite3
RUN apk add --no-cache sqlite

# Create non-root user
RUN adduser -D -s /bin/sh appuser

# Copy published app
COPY --from=build /app/publish .

# Create data directory and set permissions
RUN mkdir -p /app/data && \
    chown -R appuser:appuser /app && \
    chmod -R 755 /app

# Switch to non-root user
USER appuser

# Environment variables
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DATABASE_CONNECTION_STRING="Data Source=/app/data/receipts.db"

ENTRYPOINT ["dotnet", "ReceiptBot.dll"]