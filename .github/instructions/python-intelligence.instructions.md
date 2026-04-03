---
description: "Use when: working with Python intelligence service code, analysis logic, or FastAPI endpoints. Provides guidance on deterministic rules, insight prioritization, and service integration."
applyTo: ["services/**/*.py"]
---

## Python Intelligence Instructions

### Service Architecture
- Single endpoint: POST /analyze/v1
- Deterministic rule-based insights (no ML)
- Prioritizes insights by type and severity

### Analysis Logic
- Categorizes month as healthy/attention/critical
- Rules in [analysis.py](services/farol_intelligence/app/analysis.py)
- No external API calls

### Integration
- Called from .NET Insights endpoints
- No visible retry or fallback logic

### Testing
- Unit tests in tests/test_analysis.py

### Common Patterns
- Use FastAPI with uvicorn
- Install with pip install -e .