import unittest

from fastapi.testclient import TestClient

from app.main import app


class HealthEndpointTests(unittest.TestCase):
    def test_health_returns_ok(self) -> None:
        client = TestClient(app)

        response = client.get("/health")

        self.assertEqual(200, response.status_code)
        self.assertEqual(
            {
                "status": "ok",
                "service": "farol-intelligence",
            },
            response.json(),
        )
