# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore dependencies first (dedicated layer — only re-runs when .csproj changes)
COPY backend/src/TaskMeisterAPI/TaskMeisterAPI.csproj backend/src/TaskMeisterAPI/
RUN dotnet restore backend/src/TaskMeisterAPI/TaskMeisterAPI.csproj

# Copy source and publish a self-contained release build
COPY backend/src/TaskMeisterAPI/ backend/src/TaskMeisterAPI/
RUN dotnet publish backend/src/TaskMeisterAPI/TaskMeisterAPI.csproj \
      --configuration Release \
      --output /app/publish \
      --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Bind on all interfaces so Docker's bridge network can reach it
ENV ASPNETCORE_URLS=http://+:5276
EXPOSE 5276

ENTRYPOINT ["dotnet", "TaskMeisterAPI.dll"]
