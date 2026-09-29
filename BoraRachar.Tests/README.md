# Testes da API

Execute os comandos a partir da raiz de `bora-rachar-api`, com o SDK do .NET instalado.

## Rate limit e CORS (sem MongoDB)

```powershell
dotnet test .\BoraRachar.Tests\BoraRachar.Tests.csproj --filter "FullyQualifiedName~RateLimitIntegrationTests|FullyQualifiedName~CorsIntegrationTests"
```

Os 12 casos iniciam servidores HTTP locais em portas livres, com um servidor novo por caso. Usam os controllers e as configuracoes reais de `AddBoraRachar` e `AddBoraRacharRateLimiting`, substituindo apenas `IGroupRepository` por `TestGroupRepository`. Nao acessam o MongoDB nem precisam da API iniciada separadamente.

Cobertura:

- Cinco criacoes permitidas; a sexta retorna 429 sem persistir um novo grupo.
- Consultas continuam disponiveis apos esgotar a cota de criacao.
- Tentativas invalidas tambem consomem a cota.
- Cota global de 120 requisicoes compartilhada entre rotas.
- Um X-Forwarded-For enviado pelo cliente nao burla a cota no pipeline atual.
- Resposta 429 com ProblemDetails, Retry-After, no-store e cabecalhos CORS.
- Preflight autorizado para GET, POST, PUT e DELETE, incluindo X-Group-Token.
- Origens nao autorizadas nao recebem Access-Control-Allow-Origin (nao se exige HTTP 403: CORS e aplicado pelo navegador).
- Respostas 401 continuam legiveis pelo front autorizado.
- Preflight nao consome a cota de criacao.

O servidor desses testes reproduz a ordem do pipeline de producao, mas nao executa o `Program.cs` e nao valida TLS nem o proxy do Azure. Ao alterar o pipeline real, mantenha o host de testes alinhado. Nenhuma configuracao de proxy confiavel e simulada aqui.

## Suite completa

```powershell
dotnet test .\BoraRachar.Tests\BoraRachar.Tests.csproj
```

Sem `BORARACHAR_TEST_MONGO`, os testes de integracao com MongoDB sao marcados como ignorados. Os demais, incluindo rate limit e CORS, executam normalmente.

Para executar tambem os testes de persistencia, inicie um MongoDB local de testes e configure:

```powershell
$env:BORARACHAR_TEST_MONGO = 'mongodb://127.0.0.1:27017'
dotnet test .\BoraRachar.Tests\BoraRachar.Tests.csproj
```

A fixture existente cria um banco temporario com nome aleatorio e o exclui ao terminar. Ela rejeita servidores remotos. Use uma instancia local destinada a testes.
