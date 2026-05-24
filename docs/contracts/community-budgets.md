# Contrato frontend: orcamentos da comunidade

## Base

Base local:

```text
http://localhost:5258
```

Prefixo:

```text
/api/community-budgets
```

Auth:

```http
Authorization: Bearer {accessToken}
```

Todas as respostas de sucesso usam envelope:

```json
{
  "data": {}
}
```

Todas as respostas de erro usam envelope:

```json
{
  "error": {
    "code": "string",
    "message": "string"
  }
}
```

## Enums

`type`: `income`, `expense`, `reserve`, `debt`, `investment`

`allocationType`: `fixed_amount`, `percentage`

`status`:

- `draft`
- `published`
- `hidden`
- `reported`

`reportReason`:

- `sensitive_data`
- `offensive_content`
- `spam`
- `misleading`
- `other`

## Objeto CommunityBudgetItemRequest

| Propriedade | Tipo | Obrigatoria | Observacao |
| --- | --- | --- | --- |
| `name` | `string` | sim | Nome do item. |
| `categoryName` | `string` | sim | Categoria textual do modelo comunitario. |
| `type` | `string` | sim | Um dos valores de `type`. |
| `allocationType` | `string` | sim | `fixed_amount` ou `percentage`. |
| `amount` | `number|null` | condicional | Obrigatorio se `allocationType=fixed_amount`; deve ser maior que zero. |
| `percentage` | `number|null` | condicional | Obrigatorio se `allocationType=percentage`; deve ser maior que zero e menor ou igual a 100. |
| `notes` | `string|null` | nao | Observacao curta, sem dados sensiveis. |
| `sortOrder` | `number` | sim | Inteiro maior ou igual a zero. |

## Objeto CommunityBudgetRequest

| Propriedade | Tipo | Obrigatoria | Observacao |
| --- | --- | --- | --- |
| `title` | `string` | sim | Maximo 120 caracteres. |
| `description` | `string` | sim | Maximo 500 caracteres. |
| `targetProfile` | `string` | sim | Maximo 120 caracteres. |
| `monthlyIncomeReference` | `number|null` | nao | Valor positivo quando informado. |
| `isPublic` | `boolean` | sim | Compatibilidade; `true` equivale a `status=published` quando `status` nao e enviado. |
| `status` | `string|null` | nao | Aceita apenas `draft` ou `published` em criacao/edicao pelo usuario. Se enviado, prevalece sobre `isPublic`. |
| `items` | `CommunityBudgetItemRequest[]` | sim | Obrigatorio ter ao menos 1 item se `status=published`. |

Exemplo:

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

## Objeto CommunityBudgetResponse

| Propriedade | Tipo |
| --- | --- |
| `id` | `string uuid` |
| `ownerUserId` | `string uuid` |
| `title` | `string` |
| `description` | `string` |
| `targetProfile` | `string` |
| `monthlyIncomeReference` | `number|null` |
| `isPublic` | `boolean` |
| `status` | `string` |
| `reportCount` | `number` |
| `createdAt` | `string date-time` |
| `updatedAt` | `string date-time` |
| `items` | `CommunityBudgetItemResponse[]` |

`CommunityBudgetItemResponse` tem as mesmas propriedades do request de item, mais `id`.

## Objeto CommunityBudgetReportRequest

| Propriedade | Tipo | Obrigatoria | Observacao |
| --- | --- | --- | --- |
| `reason` | `string` | sim | Um dos valores de `reportReason`. |
| `description` | `string|null` | nao | Maximo 500 caracteres, sem dados sensiveis. |

Exemplo:

```json
{
  "reason": "sensitive_data",
  "description": "O modelo parece conter contato pessoal."
}
```

## Objeto CommunityBudgetReportResponse

| Propriedade | Tipo |
| --- | --- |
| `id` | `string uuid` |
| `communityBudgetId` | `string uuid` |
| `reason` | `string` |
| `description` | `string|null` |
| `createdAt` | `string date-time` |

## POST /api/community-budgets

Cria um orcamento comunitario.

Parametros de rota/query: nenhum.

Request body: `CommunityBudgetRequest`.

Sucesso `200`:

```json
{
  "data": {
    "id": "00000000-0000-0000-0000-000000000000",
    "ownerUserId": "00000000-0000-0000-0000-000000000000",
    "title": "Orcamento familia enxuto",
    "description": "Modelo simples para organizar gastos essenciais.",
    "targetProfile": "Casal com um filho",
    "monthlyIncomeReference": 4200,
    "isPublic": true,
    "status": "published",
    "reportCount": 0,
    "createdAt": "2026-05-17T18:12:50+00:00",
    "updatedAt": "2026-05-17T18:12:50+00:00",
    "items": []
  }
}
```

Erro de validacao `400`:

```json
{
  "error": {
    "code": "validation_error",
    "message": "Public community budget must have at least one item."
  }
}
```

## GET /api/community-budgets

Lista somente orcamentos `published`.

Parametros de rota/query: nenhum.

Sucesso `200`:

```json
{
  "data": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "ownerUserId": "00000000-0000-0000-0000-000000000000",
      "title": "Orcamento familia enxuto",
      "description": "Modelo simples para organizar gastos essenciais.",
      "targetProfile": "Casal com um filho",
      "monthlyIncomeReference": 4200,
      "isPublic": true,
      "status": "published",
      "reportCount": 0,
      "createdAt": "2026-05-17T18:12:50+00:00",
      "updatedAt": "2026-05-17T18:12:50+00:00",
      "itemCount": 2
    }
  ]
}
```

## GET /api/community-budgets/{id}

Busca um orcamento `published` ou um orcamento do proprio usuario. Orcamentos
`reported` e `hidden` so podem ser buscados pelo dono; para outros usuarios retornam `404`.

Parametros:

| Nome | Origem | Tipo |
| --- | --- | --- |
| `id` | rota | `string uuid` |

Sucesso `200`: `CommunityBudgetResponse`.

Nao encontrado ou sem permissao `404`:

```json
{
  "error": {
    "code": "community_budget_not_found",
    "message": "Community budget was not found."
  }
}
```

## PUT /api/community-budgets/{id}

Atualiza um orcamento criado pelo usuario autenticado quando o status atual e `draft` ou `published`.

Parametros:

| Nome | Origem | Tipo |
| --- | --- | --- |
| `id` | rota | `string uuid` |

Request body: `CommunityBudgetRequest`.

Sucesso `200`: `CommunityBudgetResponse`.

Sem permissao ou inexistente: `404`.

Status atual bloqueado para edicao `400`:

```json
{
  "error": {
    "code": "community_budget_not_editable",
    "message": "Community budget cannot be edited in the current status."
  }
}
```

## DELETE /api/community-budgets/{id}

Remove um orcamento criado pelo usuario autenticado.

Parametros:

| Nome | Origem | Tipo |
| --- | --- | --- |
| `id` | rota | `string uuid` |

Sucesso `204`: sem body.

Sem permissao ou inexistente: `404`.

## POST /api/community-budgets/{id}/import

Importa um orcamento `published` como copia privada e independente.

Parametros:

| Nome | Origem | Tipo |
| --- | --- | --- |
| `id` | rota | `string uuid` |

Request body: vazio.

Sucesso `200`: `CommunityBudgetResponse` com `status=draft`, `isPublic=false` e novo `id`.

Sem permissao ou orcamento nao `published`: `404`.

## POST /api/community-budgets/{id}/reports

Denuncia um orcamento comunitario publico.

Parametros:

| Nome | Origem | Tipo |
| --- | --- | --- |
| `id` | rota | `string uuid` |

Request body: `CommunityBudgetReportRequest`.

Sucesso `200`:

```json
{
  "data": {
    "id": "00000000-0000-0000-0000-000000000000",
    "communityBudgetId": "00000000-0000-0000-0000-000000000000",
    "reason": "sensitive_data",
    "description": "O modelo parece conter contato pessoal.",
    "createdAt": "2026-05-17T18:24:09+00:00"
  }
}
```

Efeito da regra MVP: a primeira denuncia valida marca o orcamento como `reported`,
define `isPublic=false` e remove o item da listagem publica. A denuncia nao deleta o
orcamento automaticamente.

Motivo invalido `400`:

```json
{
  "error": {
    "code": "validation_error",
    "message": "Community budget report reason is invalid."
  }
}
```

Denuncia do proprio orcamento `400`:

```json
{
  "error": {
    "code": "cannot_report_own_budget",
    "message": "Users cannot report their own community budget."
  }
}
```

Denuncia duplicada pelo mesmo usuario `409`:

```json
{
  "error": {
    "code": "duplicate_report",
    "message": "Community budget was already reported by this user."
  }
}
```

Orcamento inexistente, privado, `reported` ou `hidden`: `404`.

## Regras de permissao

- Criar: usuario autenticado.
- Listar: usuario autenticado; retorna apenas `published`.
- Buscar: `published` para qualquer usuario autenticado; demais status apenas para o dono.
- Atualizar: apenas o dono, somente quando status atual e `draft` ou `published`.
- Remover: apenas o dono.
- Importar: qualquer usuario autenticado pode importar orcamento `published`.
- Denunciar: qualquer usuario autenticado pode denunciar orcamento `published` de outro usuario uma unica vez.

## Seguranca

- Campos textuais nao devem conter email, CPF, CNPJ, tokens, senhas, cartao ou sequencias numericas longas.
- O backend nao calcula recomendacao financeira oficial.
- A importacao nao cria vinculo editavel com o original.
- Orcamento nao publico de outro usuario sempre retorna `404`.
- A listagem publica nao expoe nome, email ou outros dados pessoais do autor; o contrato inclui apenas `ownerUserId`.
- O endpoint de denuncia nao retorna `reporterUserId`.
