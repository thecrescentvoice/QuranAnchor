# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props QuranAnchor.slnx ./
COPY src/QuranAnchor.Domain/QuranAnchor.Domain.csproj src/QuranAnchor.Domain/
COPY src/QuranAnchor.Application/QuranAnchor.Application.csproj src/QuranAnchor.Application/
COPY src/QuranAnchor.Infrastructure/QuranAnchor.Infrastructure.csproj src/QuranAnchor.Infrastructure/
COPY src/QuranAnchor.Presentation/QuranAnchor.Presentation.csproj src/QuranAnchor.Presentation/
RUN dotnet restore src/QuranAnchor.Presentation/QuranAnchor.Presentation.csproj

COPY src/ src/
RUN dotnet publish src/QuranAnchor.Presentation/QuranAnchor.Presentation.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# QuestPDF (SkiaSharp) needs fontconfig and fonts on Linux.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# Hosts like Railway and Render pass the port in $PORT; default to 8080.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet QuranAnchor.Presentation.dll"]
