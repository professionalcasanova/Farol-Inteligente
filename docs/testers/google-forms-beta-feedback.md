# Formulario de feedback do beta no Google Forms

## Objetivo

Gerar rapidamente um formulario de feedback para a rodada de beta fechado do Farol, sem adicionar escopo de backend nem criar operacao paralela no produto.

O script em [google-forms-beta-feedback-form.gs](/C:/Users/masuc/Desktop/PensarNoNome/docs/testers/google-forms-beta-feedback-form.gs) cria um Google Form com:

- identificacao opcional
- contexto do perfil do tester
- fluxos testados
- trilha de feedback geral
- trilha de bug ou bloqueio
- perguntas finais de follow-up

## Como usar

1. Abra `https://script.google.com`.
2. Crie um novo projeto.
3. Cole o conteudo de [google-forms-beta-feedback-form.gs](/C:/Users/masuc/Desktop/PensarNoNome/docs/testers/google-forms-beta-feedback-form.gs).
4. Execute a funcao `createFarolBetaFeedbackForm`.
5. Autorize o acesso solicitado pelo Google.
6. Abra `Execution log` para copiar:
   - URL de edicao do formulario
   - URL publica para responder

## Estrutura do formulario

### Bloco inicial

- Nome
- Email ou WhatsApp para contato
- Perfil do tester
- Partes do Farol testadas

### Escolha do tipo de retorno

- Feedback geral sobre a experiencia
- Bug, erro ou bloqueio

### Trilha de feedback geral

- Clareza de valor na primeira sessao
- Se conseguiu concluir o que tentou fazer
- Onde percebeu mais valor
- O que foi mais util
- O que ficou confuso
- Onde sentiu mais atrito
- O que mudaria primeiro
- Chance de voltar a usar

### Trilha de bug

- Tela ou fluxo afetado
- O que tentou fazer
- Resultado esperado
- Resultado real
- Mensagem de erro
- Impacto
- Link de screenshot ou video
- Contexto adicional

### Fechamento

- Permissao para contato
- Observacao final

## Triagem recomendada

Depois de receber respostas, classifique cada item em uma destas etiquetas:

- `clareza de valor`
- `dashboard e alertas`
- `contas a pagar`
- `planejamento do mes`
- `transacoes`
- `importacao csv`
- `login ou sessao`
- `bug bloqueante`
- `ajuste de ux`

Isso ajuda a conectar a nova rodada com o backlog ja consolidado em [v2-feedback-backlog.md](/C:/Users/masuc/Desktop/PensarNoNome/docs/testers/v2-feedback-backlog.md).

## Tradeoffs

- Google Forms reduz atrito e acelera a coleta.
- O contexto tecnico fica mais manual do que em uma coleta embutida no produto.
- Para beta fechado, essa troca vale a pena porque o objetivo agora e aprender rapido, nao instrumentar tudo.
