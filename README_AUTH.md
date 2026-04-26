# Auth API

## Base DEV

- API: `http://localhost:5258`
- Prefixo auth: `http://localhost:5258/api/auth`

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
  "accessToken": "jwt",
  "refreshToken": "refresh-token",
  "userId": "00000000-0000-0000-0000-000000000000",
  "name": "Maria Silva",
  "email": "maria@email.com"
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
  "accessToken": "novo-jwt",
  "refreshToken": "novo-refresh-token",
  "userId": "00000000-0000-0000-0000-000000000000",
  "name": "Maria Silva",
  "email": "maria@email.com"
}
```

Regras:

- o refresh token antigo é revogado
- o refresh token novo passa a ser o único válido da sessão
- se o token estiver inválido, expirado ou revogado, o endpoint retorna `400`

## Como lidar com expiração

- o `accessToken` é curto e deve ser renovado com `refresh`
- ao receber `401` por token expirado/inválido:
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
  "message": "Logged out successfully."
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
  "message": "If the email exists, a password reset token has been generated."
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
  "message": "Password has been reset successfully."
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
  "message": "Password changed successfully."
}
```

## Política de senha

- mínimo 8 caracteres
- pelo menos 1 letra
- pelo menos 1 número
- não aceita somente espaços

Mensagem padrão:

```text
Password must be at least 8 characters long, contain at least 1 letter and 1 number, and cannot be only spaces.
```

## Respostas comuns

### Credenciais inválidas

`401`

```json
{
  "message": "Invalid email or password."
}
```

### Refresh inválido

`400`

```json
{
  "message": "Refresh token is invalid or expired."
}
```

### Rate limit no login

`429`

```json
{
  "message": "Muitas tentativas. Tente novamente em alguns instantes."
}
```

## Instruções rápidas DEV

Variáveis/configuração:

- `src/Farol.Api/appsettings.Development.json`
  - `ConnectionStrings:DefaultConnection`
  - `Jwt:*`
  - `FinancialIntelligence:*`
- variável de ambiente:
  - `FAROL_INTERNAL_API_KEY`

Subida local:

1. garantir PostgreSQL em `localhost:5432`
2. exportar `FAROL_INTERNAL_API_KEY`
3. subir o serviço Python em `127.0.0.1:8000`
4. rodar:

```powershell
$env:DOTNET_CLI_HOME='c:\Users\masuc\Desktop\PensarNoNome\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:FAROL_INTERNAL_API_KEY='dev-internal-key'
dotnet run --project src/Farol.Api --launch-profile http
```

5. validar:

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:5258/health
```
