from __future__ import annotations

import os

from fastapi import Depends, FastAPI, Header, HTTPException

from app.analysis import analyze_financial_snapshot
from app.models import FinancialAnalysisRequest, FinancialAnalysisResponse

INTERNAL_API_KEY_ENV = "FAROL_INTERNAL_API_KEY"
INTERNAL_API_KEY_HEADER = "X-Farol-Internal-Key"
ENVIRONMENT_ENV = "FAROL_ENVIRONMENT"
DEVELOPMENT_ENVIRONMENTS = {"development", "dev", "local"}


def _current_environment() -> str:
    return os.getenv(ENVIRONMENT_ENV, "development").strip().lower()


def _configured_internal_api_key() -> str | None:
    value = os.getenv(INTERNAL_API_KEY_ENV)
    if value is None:
        return None

    normalized = value.strip()
    return normalized or None


def _validate_startup_configuration() -> None:
    if _configured_internal_api_key() is None and _current_environment() not in DEVELOPMENT_ENVIRONMENTS:
        raise RuntimeError(f"{INTERNAL_API_KEY_ENV} must be configured outside development.")


def require_internal_api_key(
    internal_api_key: str | None = Header(default=None, alias=INTERNAL_API_KEY_HEADER),
) -> None:
    expected_key = _configured_internal_api_key()

    if expected_key is None or internal_api_key != expected_key:
        raise HTTPException(status_code=401, detail="Unauthorized")


def create_app() -> FastAPI:
    _validate_startup_configuration()

    app = FastAPI(
        title="Farol Intelligence Service",
        version="0.1.0",
        description="Deterministic financial intelligence engine for Farol.",
    )

    @app.get("/health")
    def health() -> dict[str, str]:
        return {
            "status": "ok",
            "service": "farol-intelligence",
        }

    @app.post(
        "/analyze/v1",
        response_model=FinancialAnalysisResponse,
        dependencies=[Depends(require_internal_api_key)],
    )
    def analyze_v1(payload: FinancialAnalysisRequest) -> FinancialAnalysisResponse:
        result = analyze_financial_snapshot(payload.model_dump())
        return FinancialAnalysisResponse.model_validate(result)

    return app


app = create_app()
