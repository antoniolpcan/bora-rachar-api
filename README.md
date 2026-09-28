# BoraRachar

API ASP.NET Core 8 para grupos de despesas em reais, com MongoDB.

## Configuração local

A conexão continua em `BoraRachar/appsettings.json`, na seção `MongoDbSettings`, conforme a configuração escolhida para este projeto. Não é necessário configurar User Secrets.

```powershell
dotnet run --project .\BoraRachar --launch-profile http
```

O Swagger fica em `/swagger` no ambiente Development. CORS é configurável por `Cors:AllowedOrigins` (padrão: `http://localhost:5173`). Configure HTTPS na implantação.