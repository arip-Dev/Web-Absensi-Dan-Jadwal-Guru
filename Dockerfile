# Tahap 1: Build aplikasi menggunakan .NET SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Salin file .csproj spesifik untuk Latihan1 agar restore lebih efisien
COPY ["Latihan1/Latihan1.csproj", "Latihan1/"]
RUN dotnet restore "Latihan1/Latihan1.csproj"

# Salin semua sisa source code
COPY . .

# Publish proyek Latihan1 (Admin Panel)
WORKDIR "/src/Latihan1"
RUN dotnet publish "Latihan1.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Tahap 2: Jalankan aplikasi menggunakan .NET Runtime (Lebih ringan)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Latihan1.dll"]