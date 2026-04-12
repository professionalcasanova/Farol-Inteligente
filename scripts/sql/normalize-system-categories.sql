BEGIN;

SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '5min';

CREATE TEMP TABLE category_alias_map (
    category_type integer NOT NULL,
    alias_name text NOT NULL,
    canonical_name text NOT NULL
) ON COMMIT DROP;

INSERT INTO category_alias_map (category_type, alias_name, canonical_name)
VALUES
    (2, 'Moradia', 'Moradia'),
    (2, 'Alimentação', 'Alimentação'),
    (2, 'Alimentacao', 'Alimentação'),
    (2, 'AlimentaÃ§Ã£o', 'Alimentação'),
    (2, 'AlimentaÃƒÂ§ÃƒÂ£o', 'Alimentação'),
    (2, 'Contas e serviços', 'Contas e serviços'),
    (2, 'Contas e servicos', 'Contas e serviços'),
    (2, 'Contas e serviÃ§os', 'Contas e serviços'),
    (2, 'Mercado', 'Mercado'),
    (2, 'Restaurantes', 'Restaurantes'),
    (2, 'Transporte', 'Transporte'),
    (2, 'Mobilidade', 'Mobilidade'),
    (2, 'Saúde', 'Saúde'),
    (2, 'Saude', 'Saúde'),
    (2, 'SaÃºde', 'Saúde'),
    (2, 'Educação', 'Educação'),
    (2, 'Educacao', 'Educação'),
    (2, 'EducaÃ§Ã£o', 'Educação'),
    (2, 'Lazer', 'Lazer'),
    (2, 'Compras', 'Compras'),
    (2, 'Cuidados pessoais', 'Cuidados pessoais'),
    (2, 'Família e filhos', 'Família e filhos'),
    (2, 'Familia e filhos', 'Família e filhos'),
    (2, 'FamÃ­lia e filhos', 'Família e filhos'),
    (2, 'Pets', 'Pets'),
    (2, 'Impostos e taxas', 'Impostos e taxas'),
    (2, 'Viagem', 'Viagem'),
    (2, 'Presentes e doações', 'Presentes e doações'),
    (2, 'Presentes e doacoes', 'Presentes e doações'),
    (2, 'Presentes e doaÃ§Ãµes', 'Presentes e doações'),
    (2, 'Assinaturas', 'Assinaturas'),
    (2, 'Outros', 'Outros'),
    (2, 'Sem categoria', 'Sem categoria'),
    (1, 'Salário', 'Salário'),
    (1, 'Salario', 'Salário'),
    (1, 'SalÃ¡rio', 'Salário'),
    (1, 'Adiantamento', 'Adiantamento'),
    (1, 'Benefícios', 'Benefícios'),
    (1, 'Beneficios', 'Benefícios'),
    (1, 'BenefÃ­cios', 'Benefícios'),
    (1, 'Freelance', 'Freelance'),
    (1, 'Comissão', 'Comissão'),
    (1, 'Comissao', 'Comissão'),
    (1, 'ComissÃ£o', 'Comissão'),
    (1, 'Reembolso', 'Reembolso'),
    (1, 'Venda', 'Venda'),
    (1, 'Aluguel recebido', 'Aluguel recebido'),
    (1, 'Restituição', 'Restituição'),
    (1, 'Restituicao', 'Restituição'),
    (1, 'RestituiÃ§Ã£o', 'Restituição'),
    (1, 'Rendimento', 'Rendimento'),
    (1, 'Transferência recebida', 'Transferência recebida'),
    (1, 'Transferencia recebida', 'Transferência recebida'),
    (1, 'TransferÃªncia recebida', 'Transferência recebida'),
    (1, 'TransferÃƒÂªncia recebida', 'Transferência recebida'),
    (1, 'Presente recebido', 'Presente recebido'),
    (1, 'Outros', 'Outros'),
    (1, 'Sem categoria', 'Sem categoria');

CREATE TEMP TABLE recognized_system_categories AS
SELECT DISTINCT
    categories.id,
    categories.type,
    categories.name,
    category_alias_map.canonical_name
FROM categories
JOIN category_alias_map
    ON category_alias_map.category_type = categories.type
   AND category_alias_map.alias_name = categories.name
WHERE categories.is_system;

CREATE TEMP TABLE canonical_keepers AS
SELECT DISTINCT ON (recognized_system_categories.type, recognized_system_categories.canonical_name)
    recognized_system_categories.type,
    recognized_system_categories.canonical_name,
    recognized_system_categories.id AS keeper_id
FROM recognized_system_categories
ORDER BY
    recognized_system_categories.type,
    recognized_system_categories.canonical_name,
    CASE
        WHEN recognized_system_categories.name = recognized_system_categories.canonical_name THEN 0
        ELSE 1
    END,
    recognized_system_categories.id;

CREATE TEMP TABLE category_merge_map AS
SELECT
    recognized_system_categories.id AS source_category_id,
    canonical_keepers.keeper_id AS target_category_id,
    recognized_system_categories.type,
    recognized_system_categories.canonical_name
FROM recognized_system_categories
JOIN canonical_keepers
    ON canonical_keepers.type = recognized_system_categories.type
   AND canonical_keepers.canonical_name = recognized_system_categories.canonical_name
WHERE recognized_system_categories.id <> canonical_keepers.keeper_id;

UPDATE categories
SET name = canonical_keepers.canonical_name
FROM canonical_keepers
WHERE categories.id = canonical_keepers.keeper_id
  AND categories.name <> canonical_keepers.canonical_name;

UPDATE transactions
SET category_id = category_merge_map.target_category_id
FROM category_merge_map
WHERE transactions.category_id = category_merge_map.source_category_id;

UPDATE budget_template_categories AS target
SET planned_amount = ROUND(target.planned_amount + source.planned_amount, 2)
FROM category_merge_map
JOIN budget_template_categories AS source
    ON source.category_id = category_merge_map.source_category_id
WHERE target.budget_template_id = source.budget_template_id
  AND target.category_id = category_merge_map.target_category_id;

DELETE FROM budget_template_categories AS source
USING category_merge_map, budget_template_categories AS target
WHERE source.category_id = category_merge_map.source_category_id
  AND target.budget_template_id = source.budget_template_id
  AND target.category_id = category_merge_map.target_category_id;

UPDATE budget_template_categories
SET category_id = category_merge_map.target_category_id
FROM category_merge_map
WHERE budget_template_categories.category_id = category_merge_map.source_category_id;

UPDATE monthly_budget_categories AS target
SET planned_amount = ROUND(target.planned_amount + source.planned_amount, 2)
FROM category_merge_map
JOIN monthly_budget_categories AS source
    ON source.category_id = category_merge_map.source_category_id
WHERE target.monthly_budget_id = source.monthly_budget_id
  AND target.category_id = category_merge_map.target_category_id;

DELETE FROM monthly_budget_categories AS source
USING category_merge_map, monthly_budget_categories AS target
WHERE source.category_id = category_merge_map.source_category_id
  AND target.monthly_budget_id = source.monthly_budget_id
  AND target.category_id = category_merge_map.target_category_id;

UPDATE monthly_budget_categories
SET category_id = category_merge_map.target_category_id
FROM category_merge_map
WHERE monthly_budget_categories.category_id = category_merge_map.source_category_id;

DELETE FROM categories
USING category_merge_map
WHERE categories.id = category_merge_map.source_category_id;

SELECT
    canonical_name,
    type,
    COUNT(*) AS surviving_rows
FROM (
    SELECT categories.type, categories.name AS canonical_name
    FROM categories
    JOIN category_alias_map
        ON category_alias_map.category_type = categories.type
       AND category_alias_map.canonical_name = categories.name
    WHERE categories.is_system
) AS canonical_categories
GROUP BY canonical_name, type
HAVING COUNT(*) > 1;

COMMIT;
