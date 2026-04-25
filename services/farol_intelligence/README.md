# Farol Intelligence Service

Serviço Python de inteligência financeira determinística do Farol.

## O que faz

- recebe um snapshot financeiro mensal já consolidado pela API C#
- classifica o mês em `healthy`, `attention` ou `critical`
- calcula um score de `0` a `100`
- gera insights priorizados e ações recomendadas

## Estrutura

```txt
services/farol_intelligence/
  app/
    analysis.py
    main.py
    models.py
  tests/
    test_analysis.py
    test_main.py
  pyproject.toml
  README.md
```

## Requisitos

- Python 3.11+

## Rodando localmente

No diretório `services/farol_intelligence`:

```powershell
python -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -e .
python -m uvicorn app.main:app --reload --port 8000
```

Endpoints úteis:

- `GET http://127.0.0.1:8000/health`
- `POST http://127.0.0.1:8000/analyze/v1`

## Rodando testes

```powershell
python -m unittest discover tests
```

## Limites deste serviço

- este diretório não contém frontend
- este diretório não substitui a API .NET
- a integração oficial acontece por HTTP, consumida pelo backend do Farol
