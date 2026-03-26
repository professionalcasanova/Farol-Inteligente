from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, Field


class AnalysisReferenceRequest(BaseModel):
    userId: str = Field(min_length=1)
    month: int = Field(ge=1, le=12)
    year: int = Field(ge=2000, le=2100)
    currency: str = Field(min_length=3, max_length=3)


class AnalysisTotalsRequest(BaseModel):
    income: float
    expense: float
    balance: float
    plannedBudget: float
    budgetSpent: float
    budgetRemaining: float
    freeToSpend: float


class AnalysisBillsRequest(BaseModel):
    pendingAmount: float
    overdueAmount: float
    pendingCount: int = Field(ge=0)
    overdueCount: int = Field(ge=0)
    upcoming7DaysAmount: float
    upcoming7DaysCount: int = Field(ge=0)


class AnalysisCategoryRequest(BaseModel):
    categoryId: str | None = None
    name: str = Field(min_length=1)
    type: Literal["income", "expense"]
    amount: float


class FinancialAnalysisRequest(BaseModel):
    contractVersion: Literal["v1"]
    reference: AnalysisReferenceRequest
    totals: AnalysisTotalsRequest
    bills: AnalysisBillsRequest
    categories: list[AnalysisCategoryRequest] = Field(default_factory=list)


class AnalysisSummaryResponse(BaseModel):
    message: str
    cause: str
    action: str


class AnalysisInsightResponse(BaseModel):
    type: str
    severity: Literal["low", "medium", "high"]
    priority: int
    message: str
    cause: str
    action: str


class RecommendedActionResponse(BaseModel):
    id: str
    label: str
    target: str


class FinancialAnalysisResponse(BaseModel):
    contractVersion: Literal["v1"]
    status: Literal["healthy", "attention", "critical"]
    score: int = Field(ge=0, le=100)
    summary: AnalysisSummaryResponse
    insights: list[AnalysisInsightResponse]
    recommendedActions: list[RecommendedActionResponse]
