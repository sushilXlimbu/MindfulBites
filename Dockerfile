# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, using only the project file, so the restore layer is cached
# until package references change.
COPY FoodJournalApp/FoodJournalApp.csproj FoodJournalApp/
RUN dotnet restore FoodJournalApp/FoodJournalApp.csproj

# Copy the rest of the source and publish a Release build.
COPY FoodJournalApp/ FoodJournalApp/
RUN dotnet publish FoodJournalApp/FoodJournalApp.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish .

# Program.cs binds to 0.0.0.0:$PORT when Railway sets PORT.
# Without PORT (e.g. local docker run) the image default of port 8080 applies.
EXPOSE 8080

# Run as the non-root user that ships with the official .NET images.
USER $APP_UID

ENTRYPOINT ["dotnet", "FoodJournalApp.dll"]
