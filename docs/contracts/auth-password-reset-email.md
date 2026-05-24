# Contrato frontend: recuperacao de senha por email

## Escopo

Este contrato descreve os endpoints que o frontend usa para solicitar email de
recuperacao de senha e concluir a troca de senha.

Base local: `http://localhost:5258`

Prefixo: `/api/auth`

Formato de sucesso:

```json
{
  "data": {}
}
```

Formato de erro:

```json
{
  "error": {
    "code": "string",
    "message": "string"
  }
}
```

## POST /api/auth/forgot-password

Solicita envio de email transacional com link de recuperacao.

Auth: publica.

Rate limit: politica `auth-sensitive`.

Request body:

| Propriedade | Tipo | Obrigatoria | Observacao |
| --- | --- | --- | --- |
| `email` | `string` | sim | Email informado pelo usuario. |

Exemplo:

```json
{
  "email": "maria@email.com"
}
```

Sucesso `200`:

```json
{
  "data": {
    "message": "If the email exists, password reset instructions have been sent."
  }
}
```

Tipos da resposta de sucesso:

| Propriedade | Tipo |
| --- | --- |
| `data.message` | `string` |

Erro de validacao `400`:

```json
{
  "error": {
    "code": "validation_error",
    "message": "Email is required."
  }
}
```

Rate limit `429`:

```json
{
  "error": {
    "code": "rate_limited",
    "message": "Muitas tentativas. Tente novamente em alguns instantes."
  }
}
```

Observacoes de seguranca:

- A resposta de sucesso e generica quando o email existe ou nao existe.
- O token de reset nunca e retornado na resposta HTTP.
- Falha de entrega de email nao deve revelar existencia da conta ao cliente.
- O frontend nao deve persistir email ou token de reset em `localStorage`.
- A URL base usada no email vem da configuracao backend `Email__PublicBaseUrl`.

## POST /api/auth/reset-password

Conclui a redefinicao usando o token recebido no link do email.

Auth: publica.

Rate limit: politica `auth-sensitive`.

Request body:

| Propriedade | Tipo | Obrigatoria | Observacao |
| --- | --- | --- | --- |
| `token` | `string` | sim | Token opaco recebido no link de email. |
| `password` | `string` | sim | Nova senha conforme politica atual. |

Exemplo:

```json
{
  "token": "reset-token",
  "password": "Password456"
}
```

Sucesso `200`:

```json
{
  "data": {
    "message": "Password has been reset successfully."
  }
}
```

Tipos da resposta de sucesso:

| Propriedade | Tipo |
| --- | --- |
| `data.message` | `string` |

Erro de token invalido ou expirado `400`:

```json
{
  "error": {
    "code": "invalid_reset_token",
    "message": "Password reset token is invalid or expired."
  }
}
```

Erro de senha fraca `400`:

```json
{
  "error": {
    "code": "validation_error",
    "message": "Password must be at least 8 characters long, contain at least 1 letter and 1 number, and cannot be only spaces."
  }
}
```

Rate limit `429`:

```json
{
  "error": {
    "code": "rate_limited",
    "message": "Muitas tentativas. Tente novamente em alguns instantes."
  }
}
```

Observacoes de seguranca:

- O token deve ser lido da URL de recuperacao e enviado uma unica vez para a API.
- O frontend deve limpar o token da memoria apos sucesso ou erro definitivo.
- Nao registrar token em analytics, logs, breadcrumbs, query tracking ou mensagens de erro.
- O backend invalida o token usado e tokens ativos anteriores do mesmo usuario.

## Outros emails de autenticacao

Nao ha contrato de frontend para boas-vindas ou confirmacao de conta nesta fase.
O backend ainda nao possui endpoint nem estado de confirmacao de conta. Quando esse
fluxo existir, ele deve ser documentado como contrato separado.
