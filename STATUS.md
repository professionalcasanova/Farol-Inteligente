# Backend Status

## Implementado hoje

- Password Reset
  - `POST /api/auth/forgot-password`
  - `POST /api/auth/reset-password`
  - token persistido, expiração, uso único
- Change Password
  - `POST /api/auth/change-password`
  - exige JWT
  - valida senha atual, confirmação e política mínima
- Refresh Token + Logout
  - `POST /api/auth/refresh`
  - `POST /api/auth/logout`
  - rotação de refresh token
- Rate Limiting no login
  - `POST /api/auth/login`
  - 5 tentativas por minuto por IP
  - retorna `429` ao exceder
- Autenticação backend -> Python
  - backend envia `X-Farol-Internal-Key`
  - serviço Python valida `FAROL_INTERNAL_API_KEY`
- Introdução de TDD
  - `AGENTS.md` atualizado
  - testes de auth e integração interna adicionados/ajustados

## Endpoints atuais

| Método | Rota | Auth |
|---|---|---|
| `POST` | `/api/auth/register` | pública |
| `POST` | `/api/auth/login` | pública + rate limit |
| `POST` | `/api/auth/forgot-password` | pública |
| `POST` | `/api/auth/reset-password` | pública |
| `POST` | `/api/auth/refresh` | pública |
| `POST` | `/api/auth/logout` | pública |
| `POST` | `/api/auth/change-password` | `Bearer JWT` |
| `GET` | `/health` | pública |

## Fluxos principais

### Login

1. `POST /api/auth/login`
2. retorna `accessToken` + `refreshToken`
3. `accessToken` deve ser usado no header `Authorization: Bearer {token}`

### Refresh

1. `POST /api/auth/refresh`
2. envia `refreshToken`
3. backend valida, revoga o token antigo e gera novo par:
   - `accessToken`
   - `refreshToken`

### Logout

1. `POST /api/auth/logout`
2. envia `refreshToken`
3. backend revoga o refresh token informado

### Forgot Password

1. `POST /api/auth/forgot-password`
2. sempre retorna `200`
3. se o e-mail existir, gera token de reset

### Reset Password

1. `POST /api/auth/reset-password`
2. envia `token` + `password`
3. backend valida existência, expiração, uso e política mínima

### Change Password

1. `POST /api/auth/change-password`
2. exige JWT
3. envia `currentPassword`, `newPassword`, `confirmNewPassword`
4. backend valida senha atual e política mínima

## Política atual de senha

- mínimo 8 caracteres
- pelo menos 1 letra
- pelo menos 1 número
- não aceita somente espaços

## Pendências curtas

- envio real de e-mail para reset
- listagem e revogação de sessões por usuário
- verificação de e-mail
- lockout por conta além do rate limit por IP
- MFA

## Observações de segurança

- erro de login continua genérico
- forgot password não revela se o e-mail existe
- reset token expira e é de uso único
- refresh token é rotacionado e revogado após uso
- reuse de refresh token revoga tokens ativos do usuário
- login tem limite de 5 tentativas por minuto por IP
- integração backend -> Python depende de `FAROL_INTERNAL_API_KEY`
- a chave interna não deve ser logada nem versionada

## DEV

- URL base local da API: `http://localhost:5258`
- serviço Python local esperado: `http://127.0.0.1:8000`
- variável obrigatória para integração interna:
  - `FAROL_INTERNAL_API_KEY`

## Validação DEV feita hoje

- build backend: ok
- testes de auth/backend: ok
- testes do serviço Python: ok
- PostgreSQL local em Docker: ok
- migrations aplicadas no banco local: ok
- serviço Python local: ok
- API ASP.NET Core em `Development`: ok
- smoke test real validado:
  - `GET /health`
  - `POST /api/auth/register`
  - `POST /api/auth/refresh`
  - `POST /api/auth/change-password`
  - `POST /api/auth/login`
  - `POST /api/auth/forgot-password`
  - `POST /api/auth/logout`
  - `GET /api/insights/month-health`
  - `POST /analyze/v1` no Python:
    - sem header -> `401`
    - com `X-Farol-Internal-Key` válido -> `200`

## Ajustes operacionais feitos para DEV

- logging da API passou a usar `Console` e `Debug`, removendo o provider padrão de `EventLog` que estava quebrando requisições locais neste ambiente Windows
- migrations `AddPasswordResetTokens` e `AddRefreshTokens` receberam metadata do EF para serem reconhecidas e aplicadas no banco local

## Retomada rápida

1. subir PostgreSQL local ou Docker Desktop
2. configurar `FAROL_INTERNAL_API_KEY` no backend e no serviço Python
3. subir o serviço Python em `127.0.0.1:8000`
4. subir a API ASP.NET Core em `http://localhost:5258`
5. validar `GET /health`
6. usar a coleção Postman de auth para validar login, refresh e fluxos de senha
