# Tahap 1: Build (Menggunakan SDK .NET 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy file .sln dan semua .csproj untuk restore dependensi
# Ini penting agar layer Docker bisa di-cache
COPY ["Sekolah.sln", "./"]
COPY ["Latihan1/Latihan1.csproj", "Latihan1/"]
COPY ["Latihan2/Latihan2.csproj", "Latihan2/"]
COPY ["Latihan3/Latihan3.csproj", "Latihan3/"]

RUN dotnet restore "Latihan1/Latihan1.csproj"

# Copy seluruh source code
COPY . .

# Build dan Publish
WORKDIR "/src/Latihan1"
RUN dotnet publish "Latihan1.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Tahap 2: Runtime (Image yang lebih kecil)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render menggunakan port 80 secara default untuk HTTP
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "Latihan1.dll"]