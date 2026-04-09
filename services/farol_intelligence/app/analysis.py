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
    "message": "Seu mês está sob controle até aqui.",
    "cause": "Você não tem sinais fortes de pressão financeira imediata neste período.",
    "action": "Continue registrando o mês para manter essa clareza.",
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
    free_to_spend = _to_decimal(totals["freeToSpend"])
    planned_budget = _to_decimal(totals["plannedBudget"])
    budget_spent = _to_decimal(totals["budgetSpent"])
    total_expense = _to_decimal(totals["expense"])

    budget_overrun = max(budget_spent - planned_budget, 0.0)
    non_essential_expense_amount = _calculate_non_essential_expense(categories)
    non_essential_expense_ratio = (
        non_essential_expense_amount / total_expense if total_expense > 0 else 0.0
    )

    variable_expense_ratio = (
        total_expense / income if income > 0 else 0.0
    )
    max_overdue_days = int(bills.get("maxOverdueDays", 0))

    facts = {
        "income": income,
        "free_to_spend": free_to_spend,
        "budget_overrun": budget_overrun,
        "overdue_count": int(bills["overdueCount"]),
        "max_overdue_days": max_overdue_days,
        "pending_count": int(bills["pendingCount"]),
        "pending_amount": _to_decimal(bills["pendingAmount"]),
        "upcoming_7_days_count": int(bills["upcoming7DaysCount"]),
        "upcoming_7_days_amount": _to_decimal(bills["upcoming7DaysAmount"]),
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
    if facts.get("max_overdue_days", 0) >= 7:
        return STATUS_CRITICAL

    if (
        facts["free_to_spend"] < 0
        and facts.get("variable_expense_ratio", 0.0) > 0.8
    ):
        return STATUS_CRITICAL

    if facts["overdue_count"] > 0:
        return STATUS_CRITICAL

    if facts["free_to_spend"] < 0:
        return STATUS_CRITICAL

    if (
        facts["upcoming_7_days_count"] > 0
        and facts["upcoming_7_days_amount"] > facts["free_to_spend"]
    ):
        return STATUS_CRITICAL

    if facts["budget_overrun"] >= 50:
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

    if facts.get("max_overdue_days", 0) >= 7:
        score -= 40

    if facts["overdue_count"] > 0:
        score -= 35

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

    if facts.get("max_overdue_days", 0) >= 7:
        insights.append(
            InsightDefinition(
                type="overdue_bills_long",
                severity=SEVERITY_HIGH,
                priority=110,
                message="Você tem uma conta com mais de 7 dias de atraso.",
                cause=f"Conta está atrasada há {facts['max_overdue_days']} dias, aumentando risco de juros.",
                action="Priorize o pagamento das contas fixas vencidas.",
            )
        )

    if facts["free_to_spend"] < 0 and facts.get("variable_expense_ratio", 0.0) > 0.8:
        insights.append(
            InsightDefinition(
                type="negative_balance_high_variable_expense",
                severity=SEVERITY_HIGH,
                priority=105,
                message="Saldo negativo e despesas variáveis muito altas em relação à renda.",
                cause=(
                    f"Saldo do mês está em {facts['free_to_spend']:.2f} e "
                    f"despesas variáveis representam {facts['variable_expense_ratio'] * 100:.1f}% da renda."
                ),
                action="Suspenda despesas não essenciais até estabilizar o saldo.",
            )
        )

    if facts["overdue_count"] > 0:
        insights.append(
            InsightDefinition(
                type="overdue_bills",
                severity=SEVERITY_HIGH,
                priority=100,
                message="Você tem contas vencidas que precisam de atenção imediata.",
                cause="Existem vencimentos atrasados pressionando seu mês.",
                action="Priorize quitar ou renegociar as contas vencidas primeiro.",
            )
        )

    if facts["free_to_spend"] < 0:
        insights.append(
            InsightDefinition(
                type="negative_free_money",
                severity=SEVERITY_HIGH,
                priority=90,
                message="Seu dinheiro livre ficou negativo neste mês.",
                cause="Depois dos gastos e do que ainda está reservado, faltou folga no mês.",
                action="Evite novos gastos agora e revise as maiores saídas do período.",
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
                message="Os próximos vencimentos já pressionam sua folga imediata.",
                cause="O que vence nos próximos dias está acima do dinheiro livre disponível.",
                action="Organize os próximos pagamentos antes de assumir novos gastos.",
            )
        )

    if facts["budget_overrun"] >= 50:
        insights.append(
            InsightDefinition(
                type="budget_overspent",
                severity=SEVERITY_MEDIUM,
                priority=60,
                message="Seu planejamento do mês já foi ultrapassado.",
                cause="Você gastou acima do que tinha planejado nas categorias acompanhadas.",
                action="Revise o planejamento e reduza gastos ajustáveis no restante do mês.",
            )
        )

    if facts["pending_count"] >= 2 and facts["pending_amount"] > facts["free_to_spend"]:
        insights.append(
            InsightDefinition(
                type="period_financial_pressure",
                severity=SEVERITY_MEDIUM,
                priority=50,
                message="As contas do período já pressionam seu mês.",
                cause="O total ainda pendente está acima da sua folga financeira atual.",
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
                message="Uma parte alta das suas saídas está em gastos não essenciais.",
                cause="Lazer, compras e outros gastos ajustáveis estão pesando acima do ideal.",
                action="Comece cortando gastos menos urgentes para recuperar folga no mês.",
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
                "label": "Ver resumo do mês",
                "target": "/dashboard",
            },
        ],
        "negative_free_money": [
            {
                "id": "review_expenses",
                "label": "Ver saídas do mês",
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
                "label": "Ver próximos vencimentos",
                "target": "/bills",
            },
            {
                "id": "review_cash_flow",
                "label": "Ver resumo do mês",
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
                "label": "Ver saídas do mês",
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
                "label": "Ver saídas do mês",
                "target": "/transactions",
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
                "label": "Ver saídas do mês",
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
        "high_non_essential_spending": 4,
        "overdue_bills_long": 5,
        "overdue_bills": 6,
        "period_financial_pressure": 7,
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


def _to_decimal(value: Any) -> float:
    return round(float(value), 2)
