from __future__ import annotations

from fastapi import FastAPI

from app.analysis import analyze_financial_snapshot
from app.models import FinancialAnalysisRequest, FinancialAnalysisResponse

app = FastAPI(
    title="Farol Intelligence Service",
    version="0.1.0",
    description="Deterministic financial intelligence engine for Farol.",
)


@app.post("/analyze/v1", response_model=FinancialAnalysisResponse)
def analyze_v1(payload: FinancialAnalysisRequest) -> FinancialAnalysisResponse:
    result = analyze_financial_snapshot(payload.model_dump())
    return FinancialAnalysisResponse.model_validate(result)
