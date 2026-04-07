# Tahap 1: Build aplikasi menggunakan .NET SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Salin file .csproj spesifik untuk Latihan2 agar restore lebih efisien
COPY ["Latihan2/Latihan2.csproj", "Latihan2/"]
RUN dotnet restore "Latihan2/Latihan2.csproj"

# Salin semua sisa source code
COPY . .

# Publish proyek Latihan2 (Portal Guru)
WORKDIR "/src/Latihan2"
RUN dotnet publish "Latihan2.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Tahap 2: Jalankan aplikasi menggunakan .NET Runtime (Lebih ringan)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Latihan2.dll"]