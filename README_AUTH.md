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
    "userId": "00000000-0000-0000-0000-000000000000",
    "name": "Maria Silva",
    "email": "maria@email.com"
  }
}
```

O refresh token e enviado por cookie `HttpOnly`, `Secure`, `SameSite=Lax` chamado
`__Host-farol_refresh`. Ele nao aparece no corpo da resposta.

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

O body e aceito para compatibilidade. O fluxo preferencial do frontend e renovar usando
o cookie `HttpOnly` definido pelo backend.

Sucesso:

```json
{
  "data": {
    "accessToken": "novo-jwt",
    "userId": "00000000-0000-0000-0000-000000000000",
    "name": "Maria Silva",
    "email": "maria@email.com"
  }
}
```

Regras:

- o refresh token antigo e revogado
- o refresh token novo e enviado por cookie `HttpOnly`
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
  2. substituir o `accessToken` em memoria
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
    "message": "If the email exists, password reset instructions have been sent."
  }
}
```

Regras:

- a resposta e sempre generica para nao revelar se o email existe
- o token de reset nao e retornado na resposta publica
- em desenvolvimento, o email e enviado via SMTP para Mailpit quando ele estiver disponivel
- falha de entrega nao expoe detalhe ao cliente

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

## Email transacional

O backend possui uma abstracao `IEmailService` para emails transacionais. O fluxo ativo
nesta etapa e recuperacao de senha.

Implementacoes disponiveis:

- `SmtpEmailService`: uso local/homologacao tecnica com Mailpit ou outro SMTP
- `ResendEmailService`: provider externo para producao/homologacao real
- `PasswordResetEmailTemplate`: template simples para o email de recuperacao de senha

Configuracao base:

- `Email__Mode`: `Smtp` ou `Resend`
- `Email__FromAddress`
- `Email__FromName`
- `Email__PublicBaseUrl`
- `Email__PasswordResetPath`
- `Email__ResetTokenMinutes`

SMTP/Mailpit:

- `Email__Smtp__Host`
- `Email__Smtp__Port`
- `Email__Smtp__UseTls`
- `Email__Smtp__Username`
- `Email__Smtp__Password`

Resend:

- `Email__Resend__ApiUrl`
- `Email__Resend__ApiKey`
- `Email__Resend__TimeoutSeconds`

Validacoes em producao:

- `Email__FromAddress` e obrigatorio
- `Email__PublicBaseUrl` deve ser uma URL absoluta HTTPS
- `Email__Mode=Resend` exige `Email__Resend__ApiKey` e `Email__Resend__ApiUrl` HTTPS
- `Email__Mode=Smtp` exige host externo, porta valida e TLS ligado

Logs:

- podem registrar evento, provider, `UserId`, timestamp e id de mensagem do provider
- nao devem registrar token de reset, senha, chave de API, corpo do email ou email completo

Status de outros emails de autenticacao:

- email de recuperacao de senha: implementado
- email de boas-vindas: pendente; nao ha necessidade no fluxo atual
- confirmacao de conta: pendente; nao existe endpoint/estado de confirmacao nesta fase

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
  - `Email:*`
- variavel de ambiente:
  - `FAROL_ENVIRONMENT`
  - `FAROL_INTERNAL_API_KEY`
  - `Email__Resend__ApiKey` quando `Email__Mode=Resend`

Subida local:

1. garantir PostgreSQL em `localhost:5432`
2. garantir Mailpit SMTP em `localhost:1025` para testar email localmente
3. exportar `FAROL_ENVIRONMENT` e `FAROL_INTERNAL_API_KEY`
4. subir o servico Python em `127.0.0.1:8000`
5. rodar:

```powershell
$env:DOTNET_CLI_HOME='c:\Users\masuc\Desktop\PensarNoNome\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:FAROL_ENVIRONMENT='Development'
$env:FAROL_INTERNAL_API_KEY='dev-internal-key'
dotnet run --project src/Farol.Api --launch-profile http
```

6. validar:

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:5258/health
```
