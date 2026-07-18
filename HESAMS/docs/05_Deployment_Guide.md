# HESAMS — Deployment Guide

## 1. Prerequisites
- .NET 8 SDK · Node.js 20+ · SQL Server 2019+ · Docker (optional) · Arduino IDE or PlatformIO for gate units

## 2. Database
```bash
sqlcmd -S localhost -i database/HESAMS_Database_Schema.sql
sqlcmd -S localhost -d HESAMS -i database/HESAMS_Seed_Data.sql
```
Or with EF Core migrations:
```bash
cd backend/HESAMS.Api
dotnet tool install --global dotnet-ef
dotnet ef migrations add Initial
dotnet ef database update
```

## 3. API
```bash
cd backend/HESAMS.Api
dotnet restore && dotnet run
# Swagger: https://localhost:5001/swagger
```
Set `ConnectionStrings:Default` and `Jwt:Key` via environment variables or Azure Key Vault. **Never ship the default key.**
```bash
export ConnectionStrings__Default="Server=...;Database=HESAMS;User Id=...;Password=..."
export Jwt__Key="$(openssl rand -base64 48)"
```

## 4. Frontend
```bash
cd frontend
npm install && npm run dev     # http://localhost:3000
npm run build && npm start     # production
```
Set `NEXT_PUBLIC_API_BASE=https://hesams.company.com`.

## 5. Docker
```bash
docker compose up -d --build
```
Brings up SQL Server, the API, and the web app on one network. Data persists in the `mssql-data` volume.

## 6. Azure
```bash
az group create -n hesams-rg -l uaenorth
az sql server create -g hesams-rg -n hesams-sql -u hesamsadmin -p "<strong>"
az sql db create -g hesams-rg -s hesams-sql -n HESAMS --service-objective S2
az appservice plan create -g hesams-rg -n hesams-plan --sku P1V3 --is-linux
az webapp create -g hesams-rg -p hesams-plan -n hesams-api --runtime "DOTNETCORE:8.0"
az keyvault create -g hesams-rg -n hesams-kv
az keyvault secret set --vault-name hesams-kv -n JwtKey --value "$(openssl rand -base64 48)"
```
Enable the App Service managed identity, grant it Key Vault get/list, and reference secrets as `@Microsoft.KeyVault(SecretUri=...)`. Restrict the SQL firewall to the App Service outbound IPs.

## 7. IIS
Publish with `dotnet publish -c Release -o ./publish`, install the ASP.NET Core Hosting Bundle, create a site with an **No Managed Code** app pool, bind the TLS certificate, and point the physical path at `publish`.

## 8. Gate units (ESP32)
1. Register the machine in HESAMS and set its `RfidReaderId` (e.g. `RDR-A1`).
2. In `esp32_rfid_gate.ino` set `WIFI_SSID`, `WIFI_PASSWORD`, `API_BASE`, `READER_ID`, `DEVICE_TOKEN`, and paste the server root CA into `ROOT_CA`.
3. Libraries: `MFRC522`, `ArduinoJson`, `Preferences` (bundled), `WiFiClientSecure` (bundled).
4. Flash at 115200 baud, board `ESP32 Dev Module`.
5. Wiring: relay on GPIO26 through an **opto-isolated** driver board, in series with the **starter-enable line only**. Never place the relay in a safety stop circuit. Fit a flyback diode across the relay coil.
6. Verify: tap a known-good card → green LED, single beep, relay closes. Tap an unauthorized card → red LED, triple beep, relay stays open.
7. Pull the network cable and re-tap to confirm the offline cache path.

## 9. Power BI
Connect to the SQL Server with DirectQuery on `AccessEvents`, `UsageSessions`, `MaintenanceOrders`, and `Equipment`. Set the refresh schedule to 15 minutes for operational tiles.

## 10. Hardening checklist
- [ ] `Jwt:Key` from Key Vault, never in `appsettings.json`
- [ ] TLS 1.2+ enforced; HTTP → HTTPS redirect on
- [ ] Per-device tokens rotated quarterly
- [ ] SQL TDE enabled; column encryption for PII
- [ ] Nightly full backup + 15-minute log backups, restore tested
- [ ] Audit log write access revoked for application accounts
- [ ] CORS restricted to the production web origin
- [ ] Rate limiting on `/auth/login` and `/access/validate`
