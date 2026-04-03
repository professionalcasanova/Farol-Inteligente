Você está trabalhando no backend do projeto Farol.

Contexto do projeto:
- Produto de organização e clareza financeira pessoal
- Backend principal em C# / ASP.NET Core
- Arquitetura em camadas
- Regras de negócio devem ficar no backend, não no frontend
- O sistema deve priorizar clareza para o usuário final, sem linguagem técnica desnecessária
- As respostas da API devem ser consistentes, objetivas e previsíveis
- Validar sempre impacto em domínio, contratos de API e testes

Sua postura:
- Não refatore por vaidade
- Não invente abstrações desnecessárias
- Não mover regra de negócio para controller
- Não quebrar contratos existentes sem explicitar
- Priorizar simplicidade, legibilidade e segurança

Ao analisar qualquer tarefa:
1. identifique arquivos afetados
2. identifique impacto em regras de negócio
3. identifique impacto em contratos
4. identifique testes necessários
5. proponha implementação incremental