# Homologation Category Normalization

This runbook prepares the homologation database for the category cleanup introduced in `v1.1`.

Goal:

- remove duplicated system categories caused by legacy spelling or mojibake
- preserve references from transactions and budgets
- leave the database ready for the next `master` deploy, which will still seed any missing canonical categories on API startup

Important:

- this is a data migration, not a schema migration
- run it with a database backup already taken
- run it before merging `dev` into `master`
- the SQL script is idempotent for the known aliases covered here

Script:

- `scripts/sql/normalize-system-categories.sql`

## 1. Backup

Recommended:

```powershell
pg_dump --format=custom --file ".\farol-homolog-before-category-normalization.dump" "<HOMOLOG_DATABASE_URL>"
```

If you prefer plain SQL:

```powershell
pg_dump --format=plain --file ".\farol-homolog-before-category-normalization.sql" "<HOMOLOG_DATABASE_URL>"
```

## 2. Pre-check

This query gives a fast picture of duplicated system category names already stored:

```sql
SELECT type, name, COUNT(*) AS total
FROM categories
WHERE is_system = TRUE
GROUP BY type, name
HAVING COUNT(*) > 1
ORDER BY type, name;
```

## 3. Run the normalization

```powershell
psql "<HOMOLOG_DATABASE_URL>" -v ON_ERROR_STOP=1 -f ".\scripts\sql\normalize-system-categories.sql"
```

Expected result:

- the script finishes with `COMMIT`
- the final `SELECT` returns zero rows

If the final query returns rows, stop and inspect before deploying.

## 4. Post-check

Confirm that the known problematic names no longer coexist:

```sql
SELECT type, name
FROM categories
WHERE is_system = TRUE
  AND name IN (
    'Alimentacao',
    'Alimentação',
    'AlimentaÃ§Ã£o',
    'Contas e servicos',
    'Contas e serviços',
    'Contas e serviÃ§os',
    'Beneficios',
    'Benefícios',
    'BenefÃ­cios',
    'Transferencia recebida',
    'Transferência recebida',
    'TransferÃªncia recebida'
  )
ORDER BY type, name;
```

Check that references still exist:

```sql
SELECT COUNT(*) AS categorized_transactions
FROM transactions
WHERE category_id IS NOT NULL;

SELECT COUNT(*) AS budget_template_rows
FROM budget_template_categories;

SELECT COUNT(*) AS monthly_budget_rows
FROM monthly_budget_categories;
```

You are not validating a specific total here. The point is to confirm the migration did not wipe references.

## 5. Deploy sequence

Recommended order:

1. take backup
2. run the normalization SQL in homologation
3. merge `dev` into `master`
4. let the API redeploy
5. smoke test categories, transactions, budget, and dashboard

Why this order:

- the SQL handles consolidation of existing duplicated data
- the deploy then brings the latest seed logic and UI fixes
- the API startup can still create any missing canonical categories that were not yet present

## 6. Smoke test after deploy

Validate at least:

1. category list without duplicated spellings
2. old transactions still showing categories
3. planning screen still loading and saving
4. dashboard quick entry still listing categories correctly
5. transaction edit screen showing `Excluir transação` with correct text

## Tradeoff

This script intentionally does not create brand-new missing system categories.

Reason:

- creating new IDs inside a standalone SQL script would add unnecessary operational complexity
- the application seed already handles insertion of missing canonical categories on startup
- the high-risk part is preserving and consolidating existing references, which this script handles directly
