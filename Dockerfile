# 1. Build Aşaması (.NET 10 SDK)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Proje dosyalarını kopyala ve restore et
COPY ["core/core.csproj", "core/"]
COPY ["data/data.csproj", "data/"]
COPY ["business/business.csproj", "business/"]
COPY ["WebApi/WebApi.csproj", "WebApi/"]

RUN dotnet restore "WebApi/WebApi.csproj"

# Tüm kaynak kodları ve wwwroot frontend dosyalarını kopyala
COPY . .

WORKDIR "/src/WebApi"
RUN dotnet publish "WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Çalışma Zamanı (Runtime) Aşaması
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# SkiaSharp ve QuestPDF için Linux grafik font kütüphaneleri ve Healthcheck için curl
RUN apt-get update && apt-get install -y --no-install-recommends \
    libfontconfig1 \
    libfreetype6 \
    curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5144
ENV ASPNETCORE_ENVIRONMENT=Development

EXPOSE 5144

ENTRYPOINT ["dotnet", "WebApi.dll"]
