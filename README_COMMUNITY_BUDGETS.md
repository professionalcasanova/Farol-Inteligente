# Community Budgets API

## Objetivo

O MVP de orcamentos da comunidade permite que usuarios autenticados criem modelos de
orcamento, publiquem esses modelos, visualizem modelos publicos de outros usuarios e
importem uma copia independente para a propria conta.

Nao ha forum, comentarios, curtidas, ranking social ou recomendacao financeira oficial.

## Rotas

Base local:

```text
http://localhost:5258/api/community-budgets
```

Todas as rotas exigem:

```http
Authorization: Bearer {accessToken}
```

Endpoints:

- `POST /api/community-budgets`: cria um orcamento comunitario.
- `GET /api/community-budgets`: lista somente orcamentos publicos.
- `GET /api/community-budgets/{id}`: busca orcamento publico ou orcamento do proprio usuario.
- `PUT /api/community-budgets/{id}`: atualiza orcamento do proprio usuario.
- `DELETE /api/community-budgets/{id}`: remove orcamento do proprio usuario.
- `POST /api/community-budgets/{id}/import`: importa orcamento publico como copia privada.
- `POST /api/community-budgets/{id}/reports`: denuncia um orcamento publico.

## Modelo

`CommunityBudget`:

- `Id`
- `OwnerUserId`
- `Title`
- `Description`
- `TargetProfile`
- `MonthlyIncomeReference`
- `IsPublic`
- `Status`
- `CreatedAtUtc`
- `UpdatedAtUtc`

`CommunityBudgetItem`:

- `Id`
- `CommunityBudgetId`
- `Name`
- `CategoryName`
- `Type`
- `AllocationType`
- `Amount`
- `Percentage`
- `Notes`
- `SortOrder`

`CommunityBudgetReport`:

- `Id`
- `CommunityBudgetId`
- `ReporterUserId`
- `Reason`
- `Description`
- `CreatedAtUtc`

Enums de contrato HTTP:

- `type`: `income`, `expense`, `reserve`, `debt`, `investment`
- `allocationType`: `fixed_amount`, `percentage`
- `status`: `draft`, `published`, `hidden`, `reported`
- `reason`: `sensitive_data`, `offensive_content`, `spam`, `misleading`, `other`

## Regras

- apenas o dono pode editar orcamentos em `draft` ou `published`
- apenas o dono pode remover um orcamento
- usuarios podem visualizar orcamentos `published` de outros usuarios
- orcamentos privados de outro usuario retornam `404`
- orcamentos `reported` ou `hidden` nao aparecem na listagem publica
- orcamento `published` nao pode ficar vazio
- importacao cria uma copia independente em `draft` e privada (`isPublic=false`)
- a copia importada nao guarda vinculo editavel com o original
- `fixed_amount` exige `amount > 0` e `percentage = null`
- `percentage` exige `0 < percentage <= 100` e `amount = null`
- campos textuais rejeitam email, CPF, CNPJ, sequencias numericas longas e termos de segredo
- um usuario nao pode denunciar o mesmo orcamento mais de uma vez
- um usuario nao pode denunciar o proprio orcamento
- a primeira denuncia valida marca o orcamento como `reported` e remove o item da listagem publica
- denuncias nao deletam orcamentos automaticamente

## Persistencia

A migration `20260517181250_AddCommunityBudgets` cria:

- `community_budgets`
- `community_budget_items`

A migration `20260517182409_AddCommunityBudgetModeration` cria:

- coluna `Status` em `community_budgets`
- tabela `community_budget_reports`
- indice unico por `CommunityBudgetId` e `ReporterUserId`

As tabelas usam `OwnerUserId` com FK para `users`, cascade delete de itens e denuncias
quando o orcamento e removido, e FK restrita para o usuario denunciante.
