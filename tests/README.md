# Backend Tests

Este diretorio concentra os testes automatizados do backend ASP.NET Core.

## Estrutura atual

- `Farol.Tests/Domain/`: unit tests de entidades e regras de dominio
- `Farol.Tests/Auth/`: unit tests de servicos de autenticacao e seguranca
- `Farol.Tests/Seeding/`: testes de seeding e dados base
- `Farol.Tests/Api/`: integration tests de endpoints usando `FarolApiFactory`
- `Farol.Tests/Smoke/`: smoke tests de migracao e infraestrutura

## Padrao de nomenclatura

Os testes devem seguir o formato:

```text
Metodo_Condicao_ResultadoEsperado
```

Exemplos:

- `ChangePassword_InvalidCurrentPassword_ShouldFail`
- `ResetPassword_WeakPassword_ShouldReturnBadRequest`
- `ValidatePassword_WithoutNumber_ShouldReturnInvalid`

## Regra de uso

- novos comportamentos de dominio devem entrar primeiro em testes unitarios
- novos endpoints devem ter pelo menos um teste de sucesso e um de falha em `Api/`
- testes devem validar comportamento observado, com setup minimo e asserts claros
