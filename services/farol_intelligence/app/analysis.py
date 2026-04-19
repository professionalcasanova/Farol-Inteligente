from __future__ import annotations

from dataclasses import dataclass
from typing import Any
import unicodedata

CONTRACT_VERSION = "v1"

STATUS_HEALTHY = "healthy"
STATUS_ATTENTION = "attention"
STATUS_CRITICAL = "critical"

SEVERITY_LOW = "low"
SEVERITY_MEDIUM = "medium"
SEVERITY_HIGH = "high"

OVERDUE_IMPACT_LOW = "low"
OVERDUE_IMPACT_MODERATE = "moderate"
OVERDUE_IMPACT_HIGH = "high"

LOW_PRIORITY_CATEGORY_NAMES = {
    "bares",
    "compras",
    "delivery",
    "entretenimento",
    "hobby",
    "hobbies",
    "ifood",
    "lazer",
    "presente",
    "presentes",
    "restaurante",
    "restaurantes",
    "shopping",
    "streaming",
    "superfluos",
    "supérfluos",
    "viagem",
    "viagens",
}

HEALTHY_SUMMARY = {
    "message": "Seu mes esta sob controle ate aqui.",
    "cause": "Voce nao tem sinais fortes de pressao financeira imediata neste periodo.",
    "action": "Continue registrando o mes para manter essa clareza.",
}


@dataclass(frozen=True)
class InsightDefinition:
    type: str
    severity: str
    priority: int
    message: str
    cause: str
    action: str


def analyze_financial_snapshot(snapshot: dict[str, Any]) -> dict[str, Any]:
    totals = snapshot["totals"]
    bills = snapshot["bills"]
    categories = snapshot.get("categories", [])

    income = _to_decimal(totals.get("income", 0.0))
    expense = _to_decimal(totals["expense"])
    balance = _to_decimal(totals.get("balance", 0.0))
    free_to_spend = _to_decimal(totals["freeToSpend"])
    planned_budget = _to_decimal(totals["plannedBudget"])
    budget_spent = _to_decimal(totals["budgetSpent"])
    overdue_amount = _to_decimal(bills.get("overdueAmount", 0.0))
    overdue_count = int(bills["overdueCount"])
    max_overdue_days = int(bills.get("maxOverdueDays", 0))

    budget_overrun = max(budget_spent - planned_budget, 0.0)
    non_essential_expense_amount = _calculate_non_essential_expense(categories)
    non_essential_expense_ratio = (
        non_essential_expense_amount / expense if expense > 0 else 0.0
    )
    variable_expense_ratio = expense / income if income > 0 else 0.0

    overdue_amount_vs_balance = _calculate_relative_impact(overdue_amount, balance)
    overdue_amount_vs_income = _calculate_relative_impact(overdue_amount, income)
    overdue_impact = _resolve_overdue_impact(
        overdue_count=overdue_count,
        overdue_amount=overdue_amount,
        balance=balance,
        income=income,
        max_overdue_days=max_overdue_days,
    )

    facts = {
        "income": income,
        "expense": expense,
        "balance": balance,
        "free_to_spend": free_to_spend,
        "budget_overrun": budget_overrun,
        "overdue_count": overdue_count,
        "overdue_amount": overdue_amount,
        "max_overdue_days": max_overdue_days,
        "overdue_impact": overdue_impact,
        "overdue_amount_vs_balance": overdue_amount_vs_balance,
        "overdue_amount_vs_income": overdue_amount_vs_income,
        "pending_count": int(bills["pendingCount"]),
        "pending_amount": _to_decimal(bills["pendingAmount"]),
        "upcoming_7_days_count": int(bills["upcoming7DaysCount"]),
        "upcoming_7_days_amount": _to_decimal(bills["upcoming7DaysAmount"]),
        "predictable_pending_count": int(bills.get("predictableCount", 0)),
        "predictable_pending_amount": _to_decimal(bills.get("predictableAmount", 0.0)),
        "recurring_pending_count": int(bills.get("recurringCount", 0)),
        "recurring_pending_amount": _to_decimal(bills.get("recurringAmount", 0.0)),
        "installment_pending_count": int(bills.get("installmentCount", 0)),
        "installment_pending_amount": _to_decimal(bills.get("installmentAmount", 0.0)),
        "non_essential_expense_amount": non_essential_expense_amount,
        "non_essential_expense_ratio": non_essential_expense_ratio,
        "variable_expense_ratio": variable_expense_ratio,
    }

    status = _resolve_status(facts)
    score = _calculate_score(facts)
    insights = _resolve_insights(facts)
    if not insights:
        insights = [_healthy_month_insight()]

    top_insights = insights[:3]
    summary = _build_summary(facts, top_insights)
    recommended_actions = _resolve_recommended_actions(top_insights)

    return {
        "contractVersion": CONTRACT_VERSION,
        "status": status,
        "score": score,
        "summary": summary,
        "message": summary["message"],
        "reasons": [item.cause for item in top_insights],
        "actions": [item.action for item in top_insights],
        "insights": [_serialize_insight(item) for item in top_insights],
        "recommendedActions": recommended_actions,
    }


def _resolve_status(facts: dict[str, Any]) -> str:
    if (
        facts["free_to_spend"] < 0
        and facts.get("variable_expense_ratio", 0.0) > 0.8
    ):
        return STATUS_CRITICAL

    if facts["free_to_spend"] < 0:
        return STATUS_CRITICAL

    if facts.get("overdue_impact") == OVERDUE_IMPACT_HIGH:
        return STATUS_CRITICAL

    if (
        facts["upcoming_7_days_count"] > 0
        and facts["upcoming_7_days_amount"] > facts["free_to_spend"]
    ):
        return STATUS_CRITICAL

    if facts["budget_overrun"] >= 50:
        return STATUS_ATTENTION

    if (
        facts["predictable_pending_count"] >= 2
        and facts["predictable_pending_amount"] >= facts["income"] * 0.3
    ):
        return STATUS_ATTENTION

    if facts["overdue_count"] > 0:
        return STATUS_ATTENTION

    if facts["pending_count"] >= 2 and facts["pending_amount"] > facts["free_to_spend"]:
        return STATUS_ATTENTION

    if (
        facts["non_essential_expense_amount"] >= 200
        and facts["non_essential_expense_ratio"] >= 0.25
    ):
        return STATUS_ATTENTION

    return STATUS_HEALTHY


def _calculate_score(facts: dict[str, Any]) -> int:
    score = 100
    score -= _resolve_overdue_score_penalty(facts)

    if facts["free_to_spend"] < 0 and facts.get("variable_expense_ratio", 0.0) > 0.8:
        score -= 35

    if facts["free_to_spend"] < 0:
        score -= 30

    if (
        facts["upcoming_7_days_count"] > 0
        and facts["upcoming_7_days_amount"] > facts["free_to_spend"]
    ):
        score -= 20

    if facts["budget_overrun"] >= 50:
        score -= 15

    if (
        facts["predictable_pending_count"] >= 2
        and facts["predictable_pending_amount"] >= facts["income"] * 0.3
    ):
        score -= 10

    if facts["pending_count"] >= 2 and facts["pending_amount"] > facts["free_to_spend"]:
        score -= 15

    if (
        facts["non_essential_expense_amount"] >= 200
        and facts["non_essential_expense_ratio"] >= 0.25
    ):
        score -= 10

    return max(0, min(100, score))


def _resolve_insights(facts: dict[str, Any]) -> list[InsightDefinition]:
    insights: list[InsightDefinition] = []
    overdue_severity = _resolve_overdue_severity(facts)
    overdue_priority = _resolve_overdue_priority(facts)

    if facts.get("max_overdue_days", 0) >= 7:
        insights.append(
            InsightDefinition(
                type="overdue_bills_long",
                severity=overdue_severity,
                priority=overdue_priority + 5,
                message="Voce tem uma conta com atraso prolongado.",
                cause=(
                    f"Existe uma conta atrasada ha {facts['max_overdue_days']} dias e "
                    "isso aumenta o risco de juros mesmo quando o valor ainda e pequeno."
                ),
                action="Revise as contas vencidas e resolva primeiro o que evita juros ou bloqueios.",
            )
        )

    if facts["free_to_spend"] < 0 and facts.get("variable_expense_ratio", 0.0) > 0.8:
        insights.append(
            InsightDefinition(
                type="negative_balance_high_variable_expense",
                severity=SEVERITY_HIGH,
                priority=105,
                message="Saldo negativo e despesas variaveis muito altas em relacao a renda.",
                cause=(
                    f"Saldo do mes esta em {facts['free_to_spend']:.2f} e "
                    f"despesas variaveis representam {facts['variable_expense_ratio'] * 100:.1f}% da renda."
                ),
                action="Suspenda despesas nao essenciais ate estabilizar o saldo.",
            )
        )

    if facts["overdue_count"] > 0:
        insights.append(
            InsightDefinition(
                type="overdue_bills",
                severity=overdue_severity,
                priority=overdue_priority,
                message=_build_overdue_message(facts),
                cause=_build_overdue_cause(facts),
                action="Priorize quitar ou renegociar as contas vencidas primeiro.",
            )
        )

    if facts["free_to_spend"] < 0:
        insights.append(
            InsightDefinition(
                type="negative_free_money",
                severity=SEVERITY_HIGH,
                priority=90,
                message="Seu dinheiro livre ficou negativo neste mes.",
                cause="Depois dos gastos e do que ainda esta reservado, faltou folga no mes.",
                action="Evite novos gastos agora e revise as maiores saidas do periodo.",
            )
        )

    if (
        facts["upcoming_7_days_count"] > 0
        and facts["upcoming_7_days_amount"] > facts["free_to_spend"]
    ):
        insights.append(
            InsightDefinition(
                type="short_term_bills_pressure",
                severity=SEVERITY_HIGH,
                priority=80,
                message="Os proximos vencimentos ja pressionam sua folga imediata.",
                cause="O que vence nos proximos dias esta acima do dinheiro livre disponivel.",
                action="Organize os proximos pagamentos antes de assumir novos gastos.",
            )
        )

    if facts["budget_overrun"] >= 50:
        insights.append(
            InsightDefinition(
                type="budget_overspent",
                severity=SEVERITY_MEDIUM,
                priority=60,
                message="Seu planejamento do mes ja foi ultrapassado.",
                cause="Voce gastou acima do que tinha planejado nas categorias acompanhadas.",
                action="Revise o planejamento e reduza gastos ajustaveis no restante do mes.",
            )
        )

    if (
        facts["predictable_pending_count"] >= 2
        and facts["predictable_pending_amount"] >= facts["income"] * 0.3
    ):
        insights.append(
            InsightDefinition(
                type="predictable_commitments_pressure",
                severity=SEVERITY_MEDIUM,
                priority=55,
                message="Compromissos previsiveis ja estao pesando no seu mes.",
                cause=(
                    "Contas recorrentes e parcelas abertas ja consomem uma parte relevante "
                    "da sua folga antes de novos gastos entrarem."
                ),
                action="Revise as contas recorrentes e parcelas do mes antes de assumir novas saidas.",
            )
        )

    if facts["pending_count"] >= 2 and facts["pending_amount"] > facts["free_to_spend"]:
        insights.append(
            InsightDefinition(
                type="period_financial_pressure",
                severity=SEVERITY_MEDIUM,
                priority=50,
                message="As contas do periodo ja pressionam seu mes.",
                cause="O total ainda pendente esta acima da sua folga financeira atual.",
                action="Veja as contas a pagar e organize a ordem de prioridade.",
            )
        )

    if (
        facts["non_essential_expense_amount"] >= 200
        and facts["non_essential_expense_ratio"] >= 0.25
    ):
        insights.append(
            InsightDefinition(
                type="high_non_essential_spending",
                severity=SEVERITY_MEDIUM,
                priority=40,
                message="Uma parte alta das suas saidas esta em gastos nao essenciais.",
                cause="Lazer, compras e outros gastos ajustaveis estao pesando acima do ideal.",
                action="Comece cortando gastos menos urgentes para recuperar folga no mes.",
            )
        )

    return sorted(insights, key=lambda item: item.priority, reverse=True)


def _healthy_month_insight() -> InsightDefinition:
    return InsightDefinition(
        type="healthy_month",
        severity=SEVERITY_LOW,
        priority=10,
        message=HEALTHY_SUMMARY["message"],
        cause=HEALTHY_SUMMARY["cause"],
        action=HEALTHY_SUMMARY["action"],
    )


def _build_summary(
    facts: dict[str, Any],
    insights: list[InsightDefinition],
) -> dict[str, str]:
    if facts["free_to_spend"] < 0:
        return _build_negative_free_money_summary(facts)

    insight = insights[0]
    return {
        "message": insight.message,
        "cause": insight.cause,
        "action": insight.action,
    }


def _build_negative_free_money_summary(facts: dict[str, Any]) -> dict[str, str]:
    if facts.get("overdue_count", 0) > 0:
        cause = (
            "Depois dos gastos e compromissos do mes, seu dinheiro livre ficou negativo e "
            "as contas vencidas aumentam o risco de faltar dinheiro para o restante do periodo."
        )
        action = (
            "Priorize primeiro as contas essenciais ja vencidas e suspenda gastos ajustaveis "
            "ate recuperar folga no caixa."
        )
    elif (
        facts.get("predictable_pending_count", 0) > 0
        and facts.get("predictable_pending_amount", 0.0) >= max(abs(facts["free_to_spend"]), 1.0)
    ):
        cause = (
            "Depois dos gastos e compromissos do mes, seu dinheiro livre ficou negativo e "
            "contas recorrentes ou parcelas ja tomam boa parte do que precisa caber no restante do periodo."
        )
        action = (
            "Confirme primeiro os compromissos previsiveis que mantem sua rotina e depois "
            "corte ou adie gastos ajustaveis para recuperar folga no caixa."
        )
    elif (
        facts.get("upcoming_7_days_count", 0) > 0
        and facts.get("upcoming_7_days_amount", 0.0) > facts["free_to_spend"]
    ):
        cause = (
            "Depois dos gastos e reservas do mes, seu dinheiro livre ficou negativo e os "
            "proximos vencimentos nao cabem sem reorganizar prioridades agora."
        )
        action = (
            "Pare novos gastos ajustaveis e organize a ordem dos proximos pagamentos "
            "antes que o atraso se espalhe pelo restante do mes."
        )
    elif facts.get("variable_expense_ratio", 0.0) > 0.8:
        cause = (
            "Depois dos gastos do mes, seu dinheiro livre ficou negativo e as despesas "
            "variaveis estao altas demais em relacao a sua renda."
        )
        action = (
            "Corte ou adie despesas variaveis agora e revise as maiores saidas do mes "
            "para recuperar folga financeira."
        )
    else:
        cause = (
            "Depois dos gastos e do que ja esta comprometido no mes, seu dinheiro livre ficou "
            "negativo e voce corre risco de nao conseguir bancar o restante do periodo sem ajuste."
        )
        action = (
            "Pause novos gastos ajustaveis e revise as maiores saidas do mes para decidir "
            "o que pode ser reduzido ou adiado agora."
        )

    return {
        "message": "Seu dinheiro livre ficou negativo neste mes.",
        "cause": cause,
        "action": action,
    }


def _resolve_recommended_actions(
    insights: list[InsightDefinition],
) -> list[dict[str, str]]:
    action_map = {
        "overdue_bills_long": [
            {
                "id": "review_overdue_bills",
                "label": "Ver contas vencidas",
                "target": "/bills?status=overdue",
            },
            {
                "id": "review_cash_flow",
                "label": "Ver resumo do mes",
                "target": "/dashboard",
            },
        ],
        "negative_balance_high_variable_expense": [
            {
                "id": "review_expenses",
                "label": "Ver saidas do mes",
                "target": "/transactions",
            },
            {
                "id": "review_budget",
                "label": "Revisar planejamento",
                "target": "/budget",
            },
        ],
        "overdue_bills": [
            {
                "id": "review_overdue_bills",
                "label": "Ver contas vencidas",
                "target": "/bills?status=overdue",
            },
            {
                "id": "review_cash_flow",
                "label": "Ver resumo do mes",
                "target": "/dashboard",
            },
        ],
        "negative_free_money": [
            {
                "id": "review_expenses",
                "label": "Ver saidas do mes",
                "target": "/transactions",
            },
            {
                "id": "review_budget",
                "label": "Revisar planejamento",
                "target": "/budget",
            },
        ],
        "short_term_bills_pressure": [
            {
                "id": "review_upcoming_bills",
                "label": "Ver proximos vencimentos",
                "target": "/bills",
            },
            {
                "id": "review_cash_flow",
                "label": "Ver resumo do mes",
                "target": "/dashboard",
            },
        ],
        "budget_overspent": [
            {
                "id": "review_budget",
                "label": "Revisar planejamento",
                "target": "/budget",
            },
            {
                "id": "review_expenses",
                "label": "Ver saidas do mes",
                "target": "/transactions",
            },
        ],
        "period_financial_pressure": [
            {
                "id": "review_bills",
                "label": "Ver contas a pagar",
                "target": "/bills",
            },
            {
                "id": "review_expenses",
                "label": "Ver saidas do mes",
                "target": "/transactions",
            },
        ],
        "predictable_commitments_pressure": [
            {
                "id": "review_bills",
                "label": "Ver compromissos do mes",
                "target": "/bills",
            },
            {
                "id": "review_budget",
                "label": "Revisar planejamento",
                "target": "/budget",
            },
        ],
        "high_non_essential_spending": [
            {
                "id": "review_categories",
                "label": "Ver onde gastou mais",
                "target": "/dashboard",
            },
            {
                "id": "review_expenses",
                "label": "Ver saidas do mes",
                "target": "/transactions",
            },
        ],
        "healthy_month": [
            {
                "id": "keep_tracking",
                "label": "Continuar acompanhando",
                "target": "/dashboard",
            }
        ],
    }

    action_priority = {
        "negative_balance_high_variable_expense": 0,
        "negative_free_money": 1,
        "short_term_bills_pressure": 2,
        "budget_overspent": 3,
        "predictable_commitments_pressure": 4,
        "high_non_essential_spending": 5,
        "overdue_bills_long": 6,
        "overdue_bills": 7,
        "period_financial_pressure": 8,
        "healthy_month": 99,
    }

    actions: list[dict[str, str]] = []
    seen_ids: set[str] = set()
    ordered_insights = sorted(
        insights,
        key=lambda item: (action_priority.get(item.type, 50), -item.priority),
    )

    for insight in ordered_insights:
        for action in action_map.get(insight.type, []):
            if action["id"] in seen_ids:
                continue

            seen_ids.add(action["id"])
            actions.append(action)

    return actions


def _resolve_overdue_impact(
    *,
    overdue_count: int,
    overdue_amount: float,
    balance: float,
    income: float,
    max_overdue_days: int,
) -> str:
    if overdue_count <= 0:
        return OVERDUE_IMPACT_LOW

    if overdue_amount <= 0:
        base_impact = OVERDUE_IMPACT_MODERATE if max_overdue_days >= 7 else OVERDUE_IMPACT_LOW
    elif balance <= 0 or income <= 0:
        base_impact = OVERDUE_IMPACT_HIGH
    else:
        impact_on_balance = overdue_amount / balance
        impact_on_income = overdue_amount / income

        if impact_on_balance < 0.10 and impact_on_income < 0.05:
            base_impact = OVERDUE_IMPACT_LOW
        elif impact_on_balance < 0.30 and impact_on_income < 0.15:
            base_impact = OVERDUE_IMPACT_MODERATE
        else:
            base_impact = OVERDUE_IMPACT_HIGH

    if max_overdue_days >= 7:
        if base_impact == OVERDUE_IMPACT_LOW:
            return OVERDUE_IMPACT_MODERATE
        if base_impact == OVERDUE_IMPACT_MODERATE:
            return OVERDUE_IMPACT_HIGH

    return base_impact


def _resolve_overdue_score_penalty(facts: dict[str, Any]) -> int:
    if facts["overdue_count"] <= 0:
        return 0

    impact = facts.get("overdue_impact")

    if impact == OVERDUE_IMPACT_HIGH:
        return 35
    if impact == OVERDUE_IMPACT_MODERATE:
        return 18
    return 8


def _resolve_overdue_severity(facts: dict[str, Any]) -> str:
    impact = facts.get("overdue_impact")

    if impact == OVERDUE_IMPACT_HIGH:
        return SEVERITY_HIGH
    if impact == OVERDUE_IMPACT_MODERATE:
        return SEVERITY_MEDIUM
    return SEVERITY_LOW


def _resolve_overdue_priority(facts: dict[str, Any]) -> int:
    impact = facts.get("overdue_impact")

    if impact == OVERDUE_IMPACT_HIGH:
        return 100
    if impact == OVERDUE_IMPACT_MODERATE:
        return 70
    return 45


def _build_overdue_message(facts: dict[str, Any]) -> str:
    impact = facts.get("overdue_impact")

    if impact == OVERDUE_IMPACT_HIGH:
        return "Voce tem contas vencidas com impacto alto no mes."
    if impact == OVERDUE_IMPACT_MODERATE:
        return "Voce tem contas vencidas que pedem atencao."
    return "Voce tem contas vencidas de baixo impacto por enquanto."


def _build_overdue_cause(facts: dict[str, Any]) -> str:
    overdue_amount = facts.get("overdue_amount", 0.0)
    balance_ratio = facts.get("overdue_amount_vs_balance", 0.0) * 100
    income_ratio = facts.get("overdue_amount_vs_income", 0.0) * 100
    impact = facts.get("overdue_impact")

    if impact == OVERDUE_IMPACT_HIGH:
        return (
            f"As contas vencidas somam {overdue_amount:.2f}, consumindo uma parte relevante "
            f"do saldo ({balance_ratio:.1f}%) ou da renda ({income_ratio:.1f}%)."
        )
    if impact == OVERDUE_IMPACT_MODERATE:
        return (
            f"As contas vencidas somam {overdue_amount:.2f} e ja merecem atencao para nao "
            f"pressionar mais o saldo ({balance_ratio:.1f}%) no restante do mes."
        )
    return (
        f"As contas vencidas somam {overdue_amount:.2f}, com impacto pequeno sobre o saldo "
        f"({balance_ratio:.1f}%) e a renda ({income_ratio:.1f}%)."
    )


def _calculate_non_essential_expense(categories: list[dict[str, Any]]) -> float:
    total = 0.0

    for category in categories:
        if category.get("type") != "expense":
            continue

        normalized_name = _normalize_category_name(str(category.get("name", "")))

        if normalized_name in LOW_PRIORITY_CATEGORY_NAMES:
            total += _to_decimal(category.get("amount", 0.0))

    return total


def _normalize_category_name(value: str) -> str:
    normalized = unicodedata.normalize("NFKD", value.strip().lower())
    return "".join(character for character in normalized if not unicodedata.combining(character))


def _serialize_insight(insight: InsightDefinition) -> dict[str, Any]:
    return {
        "type": insight.type,
        "severity": insight.severity,
        "priority": insight.priority,
        "message": insight.message,
        "cause": insight.cause,
        "action": insight.action,
    }


def _calculate_relative_impact(amount: float, reference: float) -> float:
    if amount <= 0 or reference <= 0:
        return 0.0

    return amount / reference


def _to_decimal(value: Any) -> float:
    return round(float(value), 2)
