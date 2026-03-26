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

    def test_analyze_should_return_critical_when_bill_is_overdue(self) -> None:
        payload = make_payload()
        payload["bills"]["overdueCount"] = 1
        payload["bills"]["overdueAmount"] = 220.0

        result = analyze_financial_snapshot(payload)

        self.assertEqual("critical", result["status"])
        self.assertEqual("overdue_bills", result["insights"][0]["type"])
        self.assertLess(result["score"], 100)

    def test_analyze_should_return_attention_when_budget_is_overspent(self) -> None:
        payload = make_payload()
        payload["totals"]["budgetSpent"] = 1100.0
        payload["totals"]["budgetRemaining"] = -100.0
        payload["totals"]["freeToSpend"] = 2000.0

        result = analyze_financial_snapshot(payload)

        self.assertEqual("attention", result["status"])
        self.assertEqual("budget_overspent", result["insights"][0]["type"])

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


if __name__ == "__main__":
    unittest.main()
