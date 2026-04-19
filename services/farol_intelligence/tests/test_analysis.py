from __future__ import annotations

import sys
from pathlib import Path
import unittest

SERVICE_ROOT = Path(__file__).resolve().parents[1]

if str(SERVICE_ROOT) not in sys.path:
    sys.path.insert(0, str(SERVICE_ROOT))

from app.analysis import analyze_financial_snapshot


def make_payload() -> dict:
    return {
        "contractVersion": "v1",
        "reference": {
            "userId": "user-1",
            "month": 3,
            "year": 2026,
            "currency": "BRL",
        },
        "totals": {
            "income": 6000.0,
            "expense": 4000.0,
            "balance": 2000.0,
            "plannedBudget": 1000.0,
            "budgetSpent": 400.0,
            "budgetRemaining": 600.0,
            "freeToSpend": 1400.0,
        },
        "bills": {
            "pendingAmount": 300.0,
            "overdueAmount": 0.0,
            "pendingCount": 1,
            "overdueCount": 0,
            "upcoming7DaysAmount": 100.0,
            "upcoming7DaysCount": 1,
        },
        "categories": [
            {
                "categoryId": "salary",
                "name": "Salario",
                "type": "income",
                "amount": 6000.0,
            },
            {
                "categoryId": "market",
                "name": "Alimentacao",
                "type": "expense",
                "amount": 1200.0,
            },
        ],
    }


class FinancialAnalysisTests(unittest.TestCase):
    def test_analyze_should_return_healthy_when_no_risk_is_active(self) -> None:
        result = analyze_financial_snapshot(make_payload())

        self.assertEqual("healthy", result["status"])
        self.assertEqual(100, result["score"])
        self.assertEqual("healthy_month", result["insights"][0]["type"])
        self.assertEqual("keep_tracking", result["recommendedActions"][0]["id"])

    def test_analyze_should_return_attention_when_bill_is_overdue_but_impact_is_moderate(self) -> None:
        payload = make_payload()
        payload["bills"]["overdueCount"] = 1
        payload["bills"]["overdueAmount"] = 220.0

        result = analyze_financial_snapshot(payload)

        self.assertEqual("attention", result["status"])
        self.assertEqual("overdue_bills", result["insights"][0]["type"])
        self.assertEqual("medium", result["insights"][0]["severity"])
        self.assertEqual(82, result["score"])
        self.assertEqual("review_overdue_bills", result["recommendedActions"][0]["id"])

    def test_analyze_should_return_attention_when_high_balance_makes_small_debt_low_impact(self) -> None:
        payload = make_payload()
        payload["totals"]["income"] = 18000.0
        payload["totals"]["expense"] = 3000.0
        payload["totals"]["balance"] = 15000.0
        payload["totals"]["freeToSpend"] = 8800.0
        payload["bills"]["overdueCount"] = 1
        payload["bills"]["overdueAmount"] = 200.0
        payload["bills"]["maxOverdueDays"] = 2

        result = analyze_financial_snapshot(payload)

        self.assertEqual("attention", result["status"])
        self.assertEqual("overdue_bills", result["insights"][0]["type"])
        self.assertEqual("low", result["insights"][0]["severity"])
        self.assertEqual(92, result["score"])
        self.assertIn("impacto pequeno", result["insights"][0]["cause"])

    def test_analyze_should_return_critical_when_free_money_is_negative(self) -> None:
        payload = make_payload()
        payload["totals"]["freeToSpend"] = -250.0
        payload["bills"]["upcoming7DaysCount"] = 0
        payload["bills"]["upcoming7DaysAmount"] = 0.0

        result = analyze_financial_snapshot(payload)

        self.assertEqual("critical", result["status"])
        self.assertEqual(70, result["score"])
        self.assertEqual("negative_free_money", result["insights"][0]["type"])
        self.assertEqual("Seu dinheiro livre ficou negativo neste mes.", result["message"])
        self.assertIn("dinheiro livre ficou negativo", result["summary"]["cause"])
        self.assertIn("Pause novos gastos ajustaveis", result["summary"]["action"])
        self.assertNotIn("baixo", result["message"].lower())
        self.assertEqual("review_expenses", result["recommendedActions"][0]["id"])

    def test_analyze_should_return_critical_when_balance_negative_and_variable_expense_high(self) -> None:
        payload = make_payload()
        payload["totals"]["income"] = 5000.0
        payload["totals"]["expense"] = 4200.0
        payload["totals"]["balance"] = 800.0
        payload["totals"]["freeToSpend"] = -100.0
        payload["bills"]["upcoming7DaysCount"] = 0
        payload["bills"]["upcoming7DaysAmount"] = 0.0
        payload["categories"] = [
            {"categoryId": "market", "name": "Mercado", "type": "expense", "amount": 2500.0},
            {"categoryId": "delivery", "name": "Delivery", "type": "expense", "amount": 1500.0},
        ]

        result = analyze_financial_snapshot(payload)

        self.assertEqual("critical", result["status"])
        self.assertEqual("negative_balance_high_variable_expense", result["insights"][0]["type"])
        self.assertEqual("Seu dinheiro livre ficou negativo neste mes.", result["message"])
        self.assertIn("despesas variaveis", result["summary"]["cause"])
        self.assertIn("Corte ou adie despesas variaveis", result["summary"]["action"])
        self.assertTrue(
            any(
                "Saldo do mes" in reason or "despesas variaveis" in reason
                for reason in result["reasons"]
            )
        )
        self.assertEqual("review_expenses", result["recommendedActions"][0]["id"])

    def test_analyze_should_return_attention_when_small_debt_has_long_delay_but_low_impact(self) -> None:
        payload = make_payload()
        payload["totals"]["income"] = 18000.0
        payload["totals"]["expense"] = 3000.0
        payload["totals"]["balance"] = 15000.0
        payload["totals"]["freeToSpend"] = 8800.0
        payload["bills"]["overdueCount"] = 1
        payload["bills"]["overdueAmount"] = 200.0
        payload["bills"]["maxOverdueDays"] = 8

        result = analyze_financial_snapshot(payload)

        self.assertEqual("attention", result["status"])
        self.assertEqual("overdue_bills_long", result["insights"][0]["type"])
        self.assertEqual("medium", result["insights"][0]["severity"])
        self.assertEqual(82, result["score"])
        self.assertTrue(any("8 dias" in reason for reason in result["reasons"]))

    def test_analyze_should_prioritize_expense_review_when_negative_cash_and_overdue_bills_overlap(self) -> None:
        payload = make_payload()
        payload["totals"]["income"] = 5000.0
        payload["totals"]["expense"] = 4200.0
        payload["totals"]["balance"] = 800.0
        payload["totals"]["freeToSpend"] = -180.0
        payload["bills"]["overdueCount"] = 1
        payload["bills"]["overdueAmount"] = 220.0
        payload["bills"]["maxOverdueDays"] = 8
        payload["bills"]["upcoming7DaysCount"] = 0
        payload["bills"]["upcoming7DaysAmount"] = 0.0
        payload["categories"] = [
            {"categoryId": "market", "name": "Mercado", "type": "expense", "amount": 2500.0},
            {"categoryId": "delivery", "name": "Delivery", "type": "expense", "amount": 1500.0},
        ]

        result = analyze_financial_snapshot(payload)

        self.assertEqual("critical", result["status"])
        self.assertEqual(
            [
                "overdue_bills_long",
                "negative_balance_high_variable_expense",
                "overdue_bills",
            ],
            [item["type"] for item in result["insights"]],
        )
        self.assertEqual("review_expenses", result["recommendedActions"][0]["id"])
        self.assertIn("review_overdue_bills", [item["id"] for item in result["recommendedActions"]])

    def test_analyze_should_return_critical_when_short_term_bills_exceed_free_money(self) -> None:
        payload = make_payload()
        payload["totals"]["freeToSpend"] = 400.0
        payload["bills"]["upcoming7DaysCount"] = 2
        payload["bills"]["upcoming7DaysAmount"] = 900.0

        result = analyze_financial_snapshot(payload)

        self.assertEqual("critical", result["status"])
        self.assertEqual("short_term_bills_pressure", result["insights"][0]["type"])
        self.assertEqual(80, result["score"])

    def test_analyze_should_return_attention_when_budget_is_overspent(self) -> None:
        payload = make_payload()
        payload["totals"]["budgetSpent"] = 1100.0
        payload["totals"]["budgetRemaining"] = -100.0
        payload["totals"]["freeToSpend"] = 2000.0

        result = analyze_financial_snapshot(payload)

        self.assertEqual("attention", result["status"])
        self.assertEqual("budget_overspent", result["insights"][0]["type"])
        self.assertEqual(85, result["score"])

    def test_analyze_should_detect_high_non_essential_spending(self) -> None:
        payload = make_payload()
        payload["categories"] = [
            {
                "categoryId": "delivery",
                "name": "Delivery",
                "type": "expense",
                "amount": 1200.0,
            },
            {
                "categoryId": "shopping",
                "name": "Compras",
                "type": "expense",
                "amount": 400.0,
            },
        ]
        payload["totals"]["expense"] = 4000.0

        result = analyze_financial_snapshot(payload)

        self.assertEqual("attention", result["status"])
        self.assertTrue(
            any(item["type"] == "high_non_essential_spending" for item in result["insights"])
        )
        self.assertEqual(90, result["score"])

    def test_analyze_should_return_attention_when_period_pending_bills_exceed_free_money(self) -> None:
        payload = make_payload()
        payload["totals"]["freeToSpend"] = 500.0
        payload["bills"]["pendingCount"] = 3
        payload["bills"]["pendingAmount"] = 900.0
        payload["bills"]["upcoming7DaysCount"] = 0
        payload["bills"]["upcoming7DaysAmount"] = 0.0

        result = analyze_financial_snapshot(payload)

        self.assertEqual("attention", result["status"])
        self.assertEqual("period_financial_pressure", result["insights"][0]["type"])
        self.assertEqual(85, result["score"])

    def test_analyze_should_flag_predictable_commitments_when_they_consume_month_folga(self) -> None:
        payload = make_payload()
        payload["totals"]["freeToSpend"] = 600.0
        payload["bills"]["predictableCount"] = 3
        payload["bills"]["predictableAmount"] = 2200.0
        payload["bills"]["recurringCount"] = 2
        payload["bills"]["recurringAmount"] = 1200.0
        payload["bills"]["installmentCount"] = 1
        payload["bills"]["installmentAmount"] = 1000.0

        result = analyze_financial_snapshot(payload)

        self.assertEqual("attention", result["status"])
        self.assertEqual("predictable_commitments_pressure", result["insights"][0]["type"])
        self.assertEqual("review_bills", result["recommendedActions"][0]["id"])

    def test_analyze_should_explain_predictable_commitments_inside_negative_free_money_summary(self) -> None:
        payload = make_payload()
        payload["totals"]["freeToSpend"] = -300.0
        payload["bills"]["upcoming7DaysCount"] = 0
        payload["bills"]["upcoming7DaysAmount"] = 0.0
        payload["bills"]["predictableCount"] = 2
        payload["bills"]["predictableAmount"] = 900.0

        result = analyze_financial_snapshot(payload)

        self.assertEqual("critical", result["status"])
        self.assertEqual("negative_free_money", result["insights"][0]["type"])
        self.assertIn("contas recorrentes", result["summary"]["cause"])
        self.assertIn("compromissos previsiveis", result["summary"]["action"])

    def test_analyze_should_return_critical_when_balance_is_low_and_debt_is_relevant(self) -> None:
        payload = make_payload()
        payload["totals"]["income"] = 4000.0
        payload["totals"]["expense"] = 3100.0
        payload["totals"]["balance"] = 900.0
        payload["totals"]["freeToSpend"] = 300.0
        payload["bills"]["overdueCount"] = 1
        payload["bills"]["overdueAmount"] = 450.0
        payload["bills"]["maxOverdueDays"] = 2

        result = analyze_financial_snapshot(payload)

        self.assertEqual("critical", result["status"])
        self.assertEqual("overdue_bills", result["insights"][0]["type"])
        self.assertEqual("high", result["insights"][0]["severity"])
        self.assertEqual(65, result["score"])

    def test_analyze_should_return_critical_when_multiple_relevant_debts_keep_pressure_high(self) -> None:
        payload = make_payload()
        payload["totals"]["income"] = 7000.0
        payload["totals"]["expense"] = 5200.0
        payload["totals"]["balance"] = 1800.0
        payload["totals"]["freeToSpend"] = 900.0
        payload["bills"]["overdueCount"] = 3
        payload["bills"]["overdueAmount"] = 900.0
        payload["bills"]["pendingCount"] = 5
        payload["bills"]["pendingAmount"] = 2400.0
        payload["bills"]["maxOverdueDays"] = 6

        result = analyze_financial_snapshot(payload)

        self.assertEqual("critical", result["status"])
        self.assertEqual("overdue_bills", result["insights"][0]["type"])
        self.assertEqual("high", result["insights"][0]["severity"])
        self.assertEqual(50, result["score"])

    def test_analyze_should_limit_insights_to_top_three(self) -> None:
        payload = make_payload()
        payload["bills"]["overdueCount"] = 1
        payload["bills"]["overdueAmount"] = 220.0
        payload["bills"]["pendingCount"] = 3
        payload["bills"]["pendingAmount"] = 2500.0
        payload["bills"]["upcoming7DaysCount"] = 2
        payload["bills"]["upcoming7DaysAmount"] = 1800.0
        payload["totals"]["freeToSpend"] = -100.0
        payload["totals"]["budgetSpent"] = 1500.0
        payload["totals"]["budgetRemaining"] = -500.0
        payload["categories"] = [
            {
                "categoryId": "delivery",
                "name": "Delivery",
                "type": "expense",
                "amount": 1500.0,
            }
        ]

        result = analyze_financial_snapshot(payload)

        self.assertEqual(3, len(result["insights"]))
        self.assertEqual("negative_free_money", result["insights"][0]["type"])
        self.assertEqual("short_term_bills_pressure", result["insights"][1]["type"])
        self.assertEqual("overdue_bills", result["insights"][2]["type"])
        self.assertEqual("Seu dinheiro livre ficou negativo neste mes.", result["message"])
        self.assertIn("dinheiro livre ficou negativo", result["summary"]["cause"])

    def test_analyze_should_deduplicate_recommended_actions_when_multiple_insights_share_target(self) -> None:
        payload = make_payload()
        payload["totals"]["freeToSpend"] = -100.0
        payload["totals"]["budgetSpent"] = 1300.0
        payload["totals"]["budgetRemaining"] = -300.0
        payload["bills"]["upcoming7DaysCount"] = 0
        payload["bills"]["upcoming7DaysAmount"] = 0.0

        result = analyze_financial_snapshot(payload)

        self.assertEqual("critical", result["status"])
        self.assertEqual(
            len({item["id"] for item in result["recommendedActions"]}),
            len(result["recommendedActions"]),
        )
        self.assertEqual(
            ["review_expenses", "review_budget"],
            [item["id"] for item in result["recommendedActions"]],
        )


if __name__ == "__main__":
    unittest.main()
