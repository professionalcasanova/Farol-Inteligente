# Handoff frontend: backend Farol atualizado

Data: 2026-05-24

Este documento resume o que esta implementado no backend e o que o frontend pode
consumir agora. Contratos detalhados continuam nos documentos especificos em
`docs/contracts/`.

## Base comum

Base local:

```text
http://localhost:5258
```

Autenticacao:

```http
Authorization: Bearer {accessToken}
```

Formato predominante dos endpoints novos:

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

## Autenticacao e recuperacao de senha

Contrato detalhado:

- [auth-password-reset-email.md](C:/Users/masuc/Desktop/PensarNoNome/docs/contracts/auth-password-reset-email.md)

### POST /api/auth/forgot-password

Publico. Solicita email de recuperacao de senha.

Request:

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

Observacoes para frontend:

- A resposta e sempre generica para nao revelar se o email existe.
- O token de reset nao volta na resposta HTTP.
- O link enviado por email usa a base configurada no backend.
- O frontend deve ler o token da URL de reset e nao persistir em `localStorage`.

### POST /api/auth/reset-password

Publico. Conclui a troca de senha.

Request:

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

Erros relevantes:

- `400 invalid_reset_token`
- `400 validation_error`
- `429 rate_limited`

## Contas financeiras

### PUT /api/accounts/{id}

Atualiza nome, tipo e opcionalmente status ativo.

Mudanca relevante:

- `isActive` agora e opcional.
- Se `isActive` nao for enviado, o backend mantem o status atual da conta.

Request:

```json
{
  "name": "Dinheiro",
  "type": "Cash"
}
```

Request com status:

```json
{
  "name": "Conta Corrente",
  "type": "BankAccount",
  "isActive": false
}
```

### DELETE /api/accounts/{id}

Remove uma conta financeira do usuario autenticado.

Regras:

- remove apenas conta do proprio usuario;
- retorna `404` se a conta nao existir ou pertencer a outro usuario;
- retorna `409` se a conta tiver transacoes;
- retorna `204` quando remove com sucesso.

Sucesso:

```http
204 No Content
```

Erro com transacoes:

```json
{
  "error": {
    "message": "Financial account cannot be deleted because it has transactions."
  }
}
```

## Orcamentos da comunidade

Contrato detalhado:

- [community-budgets.md](C:/Users/masuc/Desktop/PensarNoNome/docs/contracts/community-budgets.md)

Prefixo:

```text
/api/community-budgets
```

Todos os endpoints exigem usuario autenticado.

### Endpoints disponiveis

```http
POST /api/community-budgets
GET /api/community-budgets
GET /api/community-budgets/{id}
PUT /api/community-budgets/{id}
DELETE /api/community-budgets/{id}
POST /api/community-budgets/{id}/import
POST /api/community-budgets/{id}/reports
```

### Enums

`status`:

- `draft`
- `published`
- `hidden`
- `reported`

`type`:

- `income`
- `expense`
- `reserve`
- `debt`
- `investment`

`allocationType`:

- `fixed_amount`
- `percentage`

`reportReason`:

- `sensitive_data`
- `offensive_content`
- `spam`
- `misleading`
- `other`

### Criar ou editar orcamento

Request:

```json
{
  "title": "Orcamento familia enxuto",
  "description": "Modelo simples para organizar gastos essenciais.",
  "targetProfile": "Casal com um filho",
  "monthlyIncomeReference": 4200,
  "isPublic": true,
  "status": "published",
  "items": [
    {
      "name": "Aluguel",
      "categoryName": "Moradia",
      "type": "expense",
      "allocationType": "fixed_amount",
      "amount": 1200,
      "percentage": null,
      "notes": null,
      "sortOrder": 1
    }
  ]
}
```

Notas:

- `status` prevalece sobre `isPublic` quando enviado.
- Usuarios comuns so podem enviar `draft` ou `published`.
- `published` exige ao menos 1 item.
- `hidden` e `reported` sao estados reservados para moderacao/sistema.

### Listagem publica

`GET /api/community-budgets` retorna apenas orcamentos `published`.

Orcamentos `reported` e `hidden` nao aparecem.

### Importacao

`POST /api/community-budgets/{id}/import`

Regras:

- importa apenas orcamento `published`;
- cria uma copia independente;
- copia importada nasce como `draft` e `isPublic=false`.

### Denuncia

`POST /api/community-budgets/{id}/reports`

Request:

```json
{
  "reason": "sensitive_data",
  "description": "O modelo parece conter contato pessoal."
}
```

Sucesso `200`:

```json
{
  "data": {
    "id": "00000000-0000-0000-0000-000000000000",
    "communityBudgetId": "00000000-0000-0000-0000-000000000000",
    "reason": "sensitive_data",
    "description": "O modelo parece conter contato pessoal.",
    "createdAt": "2026-05-24T00:00:00+00:00"
  }
}
```

Regras:

- usuario nao pode denunciar o proprio orcamento;
- usuario nao pode denunciar o mesmo orcamento mais de uma vez;
- primeira denuncia valida marca o orcamento como `reported`;
- denuncia nao deleta o orcamento automaticamente.

Erros relevantes:

- `400 cannot_report_own_budget`
- `400 validation_error`
- `404 community_budget_not_found`
- `409 duplicate_report`

## Comunidade/feed futuro

Documento de preparacao:

- [community-feed-preparation.md](C:/Users/masuc/Desktop/PensarNoNome/docs/product-decisions/community-feed-preparation.md)

Importante para frontend:

- nao existe endpoint de feed ainda;
- nao existe forum;
- nao existem comentarios;
- nao existe painel administrativo;
- orcamentos compartilhaveis sao a primeira etapa comunitaria.

## Postman

A colecao `Farol.Auth.postman_collection.json` foi atualizada com:

- recuperacao de senha por email;
- orcamentos da comunidade;
- importacao de orcamento;
- denuncia de orcamento.

## Checklist para integracao frontend

- Criar telas/servicos usando os contratos em `docs/contracts`.
- Tratar `404` como inexistente ou sem permissao nos recursos comunitarios.
- Nao exibir `hidden` ou `reported` em areas publicas.
- Nao armazenar token de reset em storage persistente.
- Nao assumir que denuncia remove o item do banco; ela apenas oculta publicamente.
- Usar `status` como campo principal de publicacao de orcamento.
- Manter `isPublic` apenas como compatibilidade visual ou legado.
