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

    def test_analyze_should_return_critical_when_bill_is_overdue(self) -> None:
        payload = make_payload()
        payload["bills"]["overdueCount"] = 1
        payload["bills"]["overdueAmount"] = 220.0

        result = analyze_financial_snapshot(payload)

        self.assertEqual("critical", result["status"])
        self.assertEqual("overdue_bills", result["insights"][0]["type"])
        self.assertLess(result["score"], 100)
        self.assertEqual("review_overdue_bills", result["recommendedActions"][0]["id"])

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
                "Saldo do m" in reason or "despesas vari" in reason
                for reason in result["reasons"]
            )
        )

    def test_analyze_should_return_critical_when_max_overdue_days_is_7_or_more(self) -> None:
        payload = make_payload()
        payload["bills"]["maxOverdueDays"] = 8
        payload["bills"]["overdueCount"] = 1

        result = analyze_financial_snapshot(payload)

        self.assertEqual("critical", result["status"])
        self.assertEqual("overdue_bills_long", result["insights"][0]["type"])
        self.assertTrue(any("8 dias" in reason for reason in result["reasons"]))

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
        self.assertEqual("overdue_bills", result["insights"][0]["type"])
        self.assertEqual("negative_free_money", result["insights"][1]["type"])
        self.assertEqual("short_term_bills_pressure", result["insights"][2]["type"])
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
