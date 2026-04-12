function createFarolBetaFeedbackForm() {
  var form = FormApp.create('Farol Beta Fechado - Feedback de Teste');

  form.setDescription(
    'Obrigado por testar o Farol. Este formulario leva cerca de 4 a 6 minutos e nos ajuda a entender clareza, valor percebido e pontos de atrito do beta.'
  );
  form.setIsQuiz(false);
  form.setAllowResponseEdits(true);
  form.setLimitOneResponsePerUser(false);
  form.setShowLinkToRespondAgain(false);
  form.setConfirmationMessage(
    'Recebemos seu feedback. Obrigado por ajudar a evoluir o beta do Farol.'
  );

  form.addTextItem()
    .setTitle('Nome')
    .setRequired(false);

  form.addTextItem()
    .setTitle('Email ou WhatsApp para contato')
    .setHelpText('Opcional. Use apenas se topar um contato rapido para esclarecer algum ponto.')
    .setRequired(false);

  form.addMultipleChoiceItem()
    .setTitle('Qual melhor descreve seu perfil hoje?')
    .setChoiceValues([
      'CLT',
      'Autonomo ou freelancer',
      'Empreendedor',
      'Estudante',
      'Organizando financas da familia',
      'Prefiro nao informar'
    ])
    .setRequired(true);

  form.addCheckboxItem()
    .setTitle('Quais partes do Farol voce testou?')
    .setChoiceValues([
      'Cadastro ou login',
      'Dashboard',
      'Contas e saldo',
      'Transacoes',
      'Importacao CSV',
      'Contas a pagar',
      'Planejamento do mes',
      'Alertas e saude financeira'
    ])
    .showOtherOption(true)
    .setRequired(true);

  var feedbackTypeSection = form.addPageBreakItem()
    .setTitle('Tipo de retorno');
  var typeItem = form.addMultipleChoiceItem();
  typeItem.setTitle('Que tipo de feedback voce quer enviar agora?')
    .setRequired(true);

  var generalSection = form.addPageBreakItem()
    .setTitle('Feedback geral de experiencia');

  form.addScaleItem()
    .setTitle('Quao facil foi entender o valor do Farol na primeira sessao?')
    .setBounds(1, 5)
    .setLabels('Nada claro', 'Muito claro')
    .setRequired(true);

  form.addMultipleChoiceItem()
    .setTitle('Voce conseguiu concluir o que tentou fazer?')
    .setChoiceValues([
      'Sim, sem ajuda',
      'Sim, mas com alguma duvida',
      'Parcialmente',
      'Nao consegui concluir'
    ])
    .setRequired(true);

  form.addMultipleChoiceItem()
    .setTitle('Onde voce percebeu mais valor?')
    .setChoiceValues([
      'Dashboard e resumo do mes',
      'Contas a pagar',
      'Planejamento do mes',
      'Alertas',
      'Importacao CSV',
      'Ainda nao percebi valor claro'
    ])
    .showOtherOption(true)
    .setRequired(true);

  form.addParagraphTextItem()
    .setTitle('O que ficou mais claro ou mais util para voce?')
    .setRequired(true);

  form.addParagraphTextItem()
    .setTitle('O que ficou confuso ou exigiu esforco demais para entender?')
    .setRequired(true);

  form.addParagraphTextItem()
    .setTitle('Em que momento voce sentiu mais atrito ou quase desistiu?')
    .setRequired(true);

  form.addParagraphTextItem()
    .setTitle('Se pudesse mudar uma coisa no produto agora, o que mudaria primeiro?')
    .setRequired(true);

  form.addScaleItem()
    .setTitle('Depois deste teste, qual a chance de voce voltar a usar o Farol?')
    .setBounds(0, 10)
    .setLabels('Nao voltaria', 'Voltaria com certeza')
    .setRequired(true);

  var bugSection = form.addPageBreakItem()
    .setTitle('Relato de bug ou bloqueio');

  form.addTextItem()
    .setTitle('Em qual tela ou fluxo o problema aconteceu?')
    .setHelpText('Exemplo: login, dashboard, transacoes, contas a pagar, planejamento do mes.')
    .setRequired(true);

  form.addParagraphTextItem()
    .setTitle('O que voce tentou fazer?')
    .setRequired(true);

  form.addParagraphTextItem()
    .setTitle('O que voce esperava que acontecesse?')
    .setRequired(true);

  form.addParagraphTextItem()
    .setTitle('O que aconteceu de fato?')
    .setRequired(true);

  form.addParagraphTextItem()
    .setTitle('Se apareceu mensagem de erro, cole aqui')
    .setRequired(false);

  form.addMultipleChoiceItem()
    .setTitle('Qual foi o impacto desse problema?')
    .setChoiceValues([
      'Bloqueou totalmente o uso',
      'Atrapalhou, mas consegui seguir',
      'E um detalhe ou desconforto'
    ])
    .setRequired(true);

  form.addTextItem()
    .setTitle('Se tiver screenshot ou video, cole o link aqui')
    .setHelpText('Pode ser Google Drive, Loom, Imgur ou outro link acessivel.')
    .setRequired(false);

  form.addParagraphTextItem()
    .setTitle('Existe algum contexto adicional que ajudaria a reproduzir o problema?')
    .setRequired(false);

  var closingSection = form.addPageBreakItem()
    .setTitle('Fechamento');

  form.addMultipleChoiceItem()
    .setTitle('Podemos entrar em contato se surgir alguma duvida?')
    .setChoiceValues([
      'Sim',
      'Nao'
    ])
    .setRequired(true);

  form.addParagraphTextItem()
    .setTitle('Alguma observacao final?')
    .setRequired(false);

  generalSection.setGoToPage(closingSection);
  bugSection.setGoToPage(closingSection);

  typeItem.setChoices([
    typeItem.createChoice('Feedback geral sobre a experiencia', generalSection),
    typeItem.createChoice('Bug, erro ou bloqueio', bugSection)
  ]);

  Logger.log('Formulario criado com sucesso.');
  Logger.log('Editar formulario: ' + form.getEditUrl());
  Logger.log('Responder formulario: ' + form.getPublishedUrl());
}
