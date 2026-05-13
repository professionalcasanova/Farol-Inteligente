# Auth API

## Base DEV

- API: `http://localhost:5258`
- Prefixo auth: `http://localhost:5258/api/auth`

## Formato de resposta

Sucesso:

```json
{
  "data": {}
}
```

Erro:

```json
{
  "error": {
    "code": "string_curto",
    "message": "mensagem clara"
  }
}
```

## Como autenticar

### Login

`POST /api/auth/login`

Body:

```json
{
  "email": "maria@email.com",
  "password": "Password123"
}
```

Sucesso:

```json
{
  "data": {
    "accessToken": "jwt",
    "refreshToken": "refresh-token",
    "userId": "00000000-0000-0000-0000-000000000000",
    "name": "Maria Silva",
    "email": "maria@email.com"
  }
}
```

Uso do token:

```http
Authorization: Bearer {accessToken}
```

## Como renovar token

### Refresh

`POST /api/auth/refresh`

Body:

```json
{
  "refreshToken": "token-atual"
}
```

Sucesso:

```json
{
  "data": {
    "accessToken": "novo-jwt",
    "refreshToken": "novo-refresh-token",
    "userId": "00000000-0000-0000-0000-000000000000",
    "name": "Maria Silva",
    "email": "maria@email.com"
  }
}
```

Regras:

- o refresh token antigo e revogado
- o refresh token novo passa a ser o unico valido da sessao
- se o token estiver invalido, expirado ou revogado, o endpoint retorna `400`

## Sessoes do usuario

### Listar sessoes

`GET /api/auth/sessions`

Auth:

```http
Authorization: Bearer {accessToken}
```

Sucesso:

```json
{
  "data": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "createdAt": "2026-04-26T10:00:00+00:00",
      "expiresAt": "2026-05-03T10:00:00+00:00",
      "revoked": false
    }
  ]
}
```

Observacao: o refresh token puro nunca e retornado.

### Revogar sessao

`DELETE /api/auth/sessions/{sessionId}`

Auth:

```http
Authorization: Bearer {accessToken}
```

Sucesso:

```json
{
  "data": {
    "message": "Session revoked successfully."
  }
}
```

Regras:

- so revoga sessao do usuario autenticado
- tentativa de revogar sessao de outro usuario retorna `404`

## Como lidar com expiracao

- o `accessToken` e curto e deve ser renovado com `refresh`
- ao receber `401` por token expirado/invalido:
  1. chamar `POST /api/auth/refresh`
  2. substituir `accessToken` e `refreshToken`
  3. repetir a chamada protegida
- se o refresh falhar, o cliente deve voltar para login

## Logout

`POST /api/auth/logout`

Body:

```json
{
  "refreshToken": "token-atual"
}
```

Sucesso:

```json
{
  "data": {
    "message": "Logged out successfully."
  }
}
```

## Endpoints de senha

### Forgot Password

`POST /api/auth/forgot-password`

Body:

```json
{
  "email": "maria@email.com"
}
```

Resposta:

```json
{
  "data": {
    "message": "If the email exists, a password reset token has been generated."
  }
}
```

### Reset Password

`POST /api/auth/reset-password`

Body:

```json
{
  "token": "reset-token",
  "password": "Password456"
}
```

Sucesso:

```json
{
  "data": {
    "message": "Password has been reset successfully."
  }
}
```

### Change Password

`POST /api/auth/change-password`

Auth:

```http
Authorization: Bearer {accessToken}
```

Body:

```json
{
  "currentPassword": "Password123",
  "newPassword": "Password456",
  "confirmNewPassword": "Password456"
}
```

Sucesso:

```json
{
  "data": {
    "message": "Password changed successfully."
  }
}
```

## Politica de senha

- minimo 8 caracteres
- pelo menos 1 letra
- pelo menos 1 numero
- nao aceita somente espacos

Mensagem padrao:

```text
Password must be at least 8 characters long, contain at least 1 letter and 1 number, and cannot be only spaces.
```

## Respostas comuns

### Credenciais invalidas

`401`

```json
{
  "error": {
    "code": "invalid_credentials",
    "message": "Invalid email or password."
  }
}
```

### Refresh invalido

`400`

```json
{
  "error": {
    "code": "invalid_refresh_token",
    "message": "Refresh token is invalid or expired."
  }
}
```

### Rate limit no login

`429`

```json
{
  "error": {
    "code": "rate_limited",
    "message": "Muitas tentativas. Tente novamente em alguns instantes."
  }
}
```

## Instrucoes rapidas DEV

Variaveis/configuracao:

- `src/Farol.Api/appsettings.Development.json`
  - `ConnectionStrings:DefaultConnection`
  - `Jwt:*`
  - `FinancialIntelligence:*`
- variavel de ambiente:
  - `FAROL_ENVIRONMENT`
  - `FAROL_INTERNAL_API_KEY`

Subida local:

1. garantir PostgreSQL em `localhost:5432`
2. exportar `FAROL_ENVIRONMENT` e `FAROL_INTERNAL_API_KEY`
3. subir o servico Python em `127.0.0.1:8000`
4. rodar:

```powershell
$env:DOTNET_CLI_HOME='c:\Users\masuc\Desktop\PensarNoNome\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:FAROL_ENVIRONMENT='Development'
$env:FAROL_INTERNAL_API_KEY='dev-internal-key'
dotnet run --project src/Farol.Api --launch-profile http
```

5. validar:

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:5258/health
```
