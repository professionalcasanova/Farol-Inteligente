from __future__ import annotations

import importlib
import os
import sys
import unittest
from pathlib import Path

from fastapi.testclient import TestClient

SERVICE_ROOT = Path(__file__).resolve().parents[1]

if str(SERVICE_ROOT) not in sys.path:
    sys.path.insert(0, str(SERVICE_ROOT))


def load_app(*, environment: str = "testing", internal_api_key: str | None = "test-internal-key"):
    os.environ["FAROL_ENVIRONMENT"] = environment

    if internal_api_key is None:
        os.environ.pop("FAROL_INTERNAL_API_KEY", None)
    else:
        os.environ["FAROL_INTERNAL_API_KEY"] = internal_api_key

    app_main = importlib.import_module("app.main")
    app_main = importlib.reload(app_main)
    return app_main.app


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
            "income": 5000,
            "expense": 3000,
            "balance": 2000,
            "plannedBudget": 2500,
            "budgetSpent": 2000,
            "budgetRemaining": 500,
            "freeToSpend": 2000,
        },
        "bills": {
            "pendingAmount": 0,
            "overdueAmount": 0,
            "pendingCount": 0,
            "overdueCount": 0,
            "upcoming7DaysAmount": 0,
            "upcoming7DaysCount": 0,
            "maxOverdueDays": 0,
            "predictableAmount": 0,
            "predictableCount": 0,
            "recurringAmount": 0,
            "recurringCount": 0,
            "installmentAmount": 0,
            "installmentCount": 0,
        },
        "categories": [],
    }


class HealthEndpointTests(unittest.TestCase):
    def test_health_returns_ok(self) -> None:
        client = TestClient(load_app())

        response = client.get("/health")

        self.assertEqual(200, response.status_code)
        self.assertEqual(
            {
                "status": "ok",
                "service": "farol-intelligence",
            },
            response.json(),
        )


class InternalAuthenticationTests(unittest.TestCase):
    def test_analyze_without_header_should_return_unauthorized(self) -> None:
        client = TestClient(load_app())

        response = client.post("/analyze/v1", json=make_payload())

        self.assertEqual(401, response.status_code)

    def test_analyze_with_invalid_header_should_return_unauthorized(self) -> None:
        client = TestClient(load_app())

        response = client.post(
            "/analyze/v1",
            json=make_payload(),
            headers={"X-Farol-Internal-Key": "wrong-key"},
        )

        self.assertEqual(401, response.status_code)

    def test_analyze_with_valid_header_should_return_success(self) -> None:
        client = TestClient(load_app())

        response = client.post(
            "/analyze/v1",
            json=make_payload(),
            headers={"X-Farol-Internal-Key": "test-internal-key"},
        )

        self.assertEqual(200, response.status_code)
        self.assertEqual("healthy", response.json()["status"])

    def test_create_app_without_key_in_production_should_fail(self) -> None:
        with self.assertRaises(RuntimeError):
            load_app(environment="production", internal_api_key=None)
