# Tahap 1: Build aplikasi menggunakan .NET SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Salin file .csproj spesifik untuk Latihan3 agar restore lebih efisien
COPY ["Latihan3/Latihan3.csproj", "Latihan3/"]
RUN dotnet restore "Latihan3/Latihan3.csproj"

# Salin semua sisa source code
COPY . .

# Publish proyek Latihan3 (Portal Absensi)
WORKDIR "/src/Latihan3"
RUN dotnet publish "Latihan3.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Tahap 2: Jalankan aplikasi menggunakan .NET Runtime (Lebih ringan)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Latihan3.dll"]