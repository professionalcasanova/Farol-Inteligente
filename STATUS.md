# Backend Status

## Implementado

- Password Reset
  - `POST /api/auth/forgot-password`
  - `POST /api/auth/reset-password`
  - token persistido, expiracao e uso unico
- Change Password
  - `POST /api/auth/change-password`
  - exige JWT
  - valida senha atual, confirmacao e politica minima
- Refresh Token + Logout
  - `POST /api/auth/refresh`
  - `POST /api/auth/logout`
  - rotacao de refresh token
- Sessoes do usuario
  - `GET /api/auth/sessions`
  - `DELETE /api/auth/sessions/{sessionId}`
  - lista apenas sessoes ativas do usuario autenticado
  - nao expoe refresh token puro
- Rate Limiting no login
  - `POST /api/auth/login`
  - 5 tentativas por minuto por IP
  - retorna `429` ao exceder
- Autenticacao backend -> Python
  - backend envia `X-Farol-Internal-Key`
  - servico Python valida `FAROL_INTERNAL_API_KEY`
- Fallback C# para inteligencia financeira
  - `GET /api/insights/month-health` usa fallback local seguro se o Python falhar, expirar, retornar status nao-2xx ou payload invalido
- Padronizacao de respostas da API
  - sucesso: `{ "data": ... }`
  - erro: `{ "error": { "code", "message", "details?" } }`
- Correcoes de encoding
  - categorias usam nomes canonicos com acentuacao correta
  - importacao CSV tolera cabecalhos com UTF-8 legado repetido em extratos bancarios
- TDD
  - `AGENTS.md` atualizado
  - testes de auth, sessoes, integracao interna e contrato API/Python adicionados ou ajustados

## Endpoints atuais

| Metodo | Rota | Auth |
|---|---|---|
| `POST` | `/api/auth/register` | publica |
| `POST` | `/api/auth/login` | publica + rate limit |
| `POST` | `/api/auth/forgot-password` | publica |
| `POST` | `/api/auth/reset-password` | publica |
| `POST` | `/api/auth/refresh` | publica |
| `POST` | `/api/auth/logout` | publica |
| `POST` | `/api/auth/change-password` | `Bearer JWT` |
| `GET` | `/api/auth/sessions` | `Bearer JWT` |
| `DELETE` | `/api/auth/sessions/{sessionId}` | `Bearer JWT` |
| `GET` | `/api/insights/month-health` | `Bearer JWT` |
| `GET` | `/health` | publica |

## Fluxos principais

### Login

1. `POST /api/auth/login`
2. retorna `data.accessToken` + `data.refreshToken`
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

### Sessions

1. `GET /api/auth/sessions`
2. exige JWT
3. retorna sessoes ativas do usuario autenticado com `id`, `createdAt`, `expiresAt`, `revoked`
4. `DELETE /api/auth/sessions/{sessionId}` revoga apenas sessao do proprio usuario

### Forgot Password

1. `POST /api/auth/forgot-password`
2. sempre retorna `200`
3. se o e-mail existir, gera token de reset

### Reset Password

1. `POST /api/auth/reset-password`
2. envia `token` + `password`
3. backend valida existencia, expiracao, uso e politica minima

### Change Password

1. `POST /api/auth/change-password`
2. exige JWT
3. envia `currentPassword`, `newPassword`, `confirmNewPassword`
4. backend valida senha atual e politica minima

### Month Health com Python

1. `GET /api/insights/month-health`
2. backend monta snapshot financeiro local
3. backend chama `POST /analyze/v1` no servico Python
4. header obrigatorio: `X-Farol-Internal-Key`
5. se o Python falhar, expirar, rejeitar ou retornar payload invalido, a API responde com fallback local seguro

## Integracao API C# -> Python

- chamada atual: `GET /api/insights/month-health` no backend usa `POST /analyze/v1` no servico Python
- endpoint Python de saude: `GET /health`, publico
- endpoint Python interno: `POST /analyze/v1`, exige `X-Farol-Internal-Key`
- chave compartilhada: `FAROL_INTERNAL_API_KEY`
- request enviado: `contractVersion`, `reference`, `totals`, `bills`, `categories`
- response esperado: `contractVersion`, `status`, `score`, `summary`, `insights`, `recommendedActions`
- status codes Python esperados: `200` sucesso, `401` chave ausente/invalida, `422` payload invalido
- timeout do backend: `FinancialIntelligence:TimeoutSeconds`
- comportamento resiliente: falha, timeout, status nao-2xx ou payload invalido do Python geram fallback local seguro no backend
- a chave interna nao deve ser logada nem versionada

## Politica atual de senha

- minimo 8 caracteres
- pelo menos 1 letra
- pelo menos 1 numero
- nao aceita somente espacos

## Pendencias curtas

- envio real de e-mail para reset
- verificacao de e-mail
- lockout por conta alem do rate limit por IP
- MFA

## Observacoes de seguranca

- erro de login continua generico
- forgot password nao revela se o e-mail existe
- reset token expira e e de uso unico
- refresh token e rotacionado e revogado apos uso
- reuse de refresh token revoga tokens ativos do usuario
- sessoes de usuario nao expoem refresh token puro
- login tem limite de 5 tentativas por minuto por IP
- integracao backend -> Python depende de `FAROL_INTERNAL_API_KEY`
- a chave interna nao deve ser logada nem versionada
- falha ou payload invalido do Python nao derruba `month-health`; a API usa fallback local

## DEV

- URL base local da API: `http://localhost:5258`
- servico Python local esperado: `http://127.0.0.1:8000`
- variavel obrigatoria para integracao interna:
  - `FAROL_INTERNAL_API_KEY`

## Validacao final

- C# tests: `209/209`
- Python tests: `23/23`
- build backend: OK
- testes de auth/backend: OK
- testes de integracao API C# -> Python: OK
- testes do servico Python: OK
- PostgreSQL local em Docker: OK
- migrations aplicadas no banco local: OK
- API ASP.NET Core em `Development`: OK

## Retomada rapida

1. subir PostgreSQL local ou Docker Desktop
2. configurar `FAROL_INTERNAL_API_KEY` no backend e no servico Python
3. subir o servico Python em `127.0.0.1:8000`
4. subir a API ASP.NET Core em `http://localhost:5258`
5. validar `GET /health`
6. usar a colecao Postman de auth para validar login, refresh, sessoes e fluxos de senha
