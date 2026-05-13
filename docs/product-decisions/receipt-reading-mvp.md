# Leitura de comprovantes financeiros no MVP

## Status

Decisao tomada: a extracao inicial de comprovantes deve ficar no servico Python, sem
persistencia de arquivo bruto. O backend .NET continua responsavel por autenticacao,
autorizacao, validacao de usuario, conta, categoria e gravacao.

O MVP nao deve enviar comprovantes para servicos externos.

## Contrato de resposta

```json
{
  "documentType": "pix_receipt",
  "amount": 0,
  "paidAt": null,
  "receiverName": null,
  "payerName": null,
  "transactionId": null,
  "confidence": 0,
  "rawText": ""
}
```

O Python retorna apenas candidatos. O backend decide se o dado e aceitavel e onde
persistir.

## Alternativas avaliadas

### 1. PaddleOCR

Decisao: usar como principal candidato para imagens e fotos de comprovantes.

Pros:

- roda localmente, sem envio para API externa
- bom encaixe para fotos, prints e PDFs digitalizados
- suporta varios idiomas e documentos em imagem/PDF
- licenca Apache 2.0

Contras:

- dependencia pesada para o container
- pode baixar modelos se a imagem Docker nao for preparada corretamente
- exige benchmark com comprovantes brasileiros reais ou sinteticos anonimizados
- pode precisar de ajuste fino de pre-processamento para foto ruim

Uso recomendado:

- OCR principal para imagem/foto
- modelos fixados e empacotados no build, sem download em runtime de producao

### 2. pdfplumber

Decisao: usar como primeira escolha para PDFs textuais e layout simples no MVP.

Pros:

- bom para PDF machine-generated
- extrai texto, tabelas e elementos de layout
- licenca MIT
- dependencia mais simples que OCR completo

Contras:

- nao resolve bem PDF escaneado sozinho
- tabelas e layout variam bastante entre bancos
- precisa fallback para imagem/OCR quando nao houver texto selecionavel

Uso recomendado:

- primeira tentativa para `application/pdf`
- se texto extraido for baixo ou vazio, encaminhar para OCR local

### 3. PyMuPDF

Decisao: nao usar como default de producao antes de validar licenciamento.

Pros:

- rapido e eficiente para extracao/conversao de PDF
- forte para texto, imagens e renderizacao de paginas
- util para transformar pagina PDF em imagem antes do OCR

Contras:

- licenca open source AGPL; para SaaS/produto fechado pode exigir abertura de codigo
  ou licenca comercial
- adiciona decisao juridica/operacional antes de entrar em producao

Uso recomendado:

- prototipo tecnico ou uso futuro somente se a licenca AGPL/comercial for aceita
- nao bloquear o MVP em cima dele

### 4. OpenCV

Decisao: usar como ferramenta auxiliar de pre-processamento.

Pros:

- melhora imagem antes do OCR: grayscale, threshold, deskew, crop
- licenca Apache 2.0 em versoes atuais
- roda localmente

Contras:

- nao extrai texto sozinho
- aumenta complexidade da pipeline
- ajustes podem ficar dependentes de formato de comprovante

Uso recomendado:

- aplicar apenas quando OCR bruto tiver baixa confianca
- manter transformacoes simples e testaveis

### 5. Tesseract

Decisao: manter como fallback ou baseline comparativo.

Pros:

- OCR maduro, local e open source
- licenca Apache 2.0
- bom baseline para texto impresso simples

Contras:

- requer binario nativo e dados de idioma instalados
- geralmente exige mais pre-processamento que PaddleOCR
- pode ter desempenho inferior em fotos ruins e layouts modernos

Uso recomendado:

- fallback local
- comparacao de qualidade/custo contra PaddleOCR

## Decisao tecnica recomendada

Pipeline inicial:

1. Validar tamanho, extensao e MIME type.
2. Se PDF textual: tentar `pdfplumber`.
3. Se PDF sem texto ou imagem: aplicar OCR local com PaddleOCR.
4. Se OCR falhar ou ficar abaixo do limiar: comparar com Tesseract.
5. Usar OpenCV apenas como pre-processamento controlado.
6. Extrair candidatos por heuristicas deterministicas para PIX.
7. Retornar candidatos com `confidence`.

Nao persistir arquivo bruto no Python. O arquivo deve existir apenas em memoria durante a
requisicao.

## Contrato entre .NET e Python

Responsabilidade do backend .NET:

- autenticar usuario
- autorizar acesso
- validar ownership de conta/categoria
- limitar upload no endpoint publico
- gravar ou rejeitar dados finais

Responsabilidade do Python:

- validar novamente tamanho e tipo por defesa em profundidade
- extrair texto localmente
- inferir candidatos
- retornar `confidence`
- nao persistir arquivo bruto

## Regras de seguranca

- nao persistir arquivo bruto no Python
- nao logar conteudo completo do comprovante
- nao expor endpoint Python publicamente
- exigir `X-Internal-API-Key`
- limitar upload, sugestao inicial: 5 MB
- aceitar apenas PDF, PNG, JPG/JPEG, WEBP e TIFF
- validar extensao e MIME type
- retornar `confidence`
- limitar `rawText` retornado, sugestao inicial: 4000 caracteres
- nao tomar decisao financeira final no Python

## Prototipo local controlado

O prototipo inicial deve validar:

- parsing de texto sintetico de PIX
- validacao de tamanho, MIME e extensao
- retorno no contrato esperado
- ausencia de escrita em disco

Ele nao deve criar endpoint publico ainda.

## Plano de testes

### Sinteticos

- PIX completo com valor, data, recebedor, pagador e E2E ID
- PIX sem pagador
- PIX sem E2E ID
- comprovante sem valor
- texto nao PIX
- arquivo acima do limite
- MIME/extensao divergentes

### Anonimizados

- comprovantes reais com nomes trocados
- CPFs/CNPJs removidos ou mascarados
- IDs transacionais trocados por valores ficticios
- prints com baixa resolucao
- PDF textual e PDF escaneado

### Metricas minimas

- taxa de extracao de valor
- taxa de extracao de data
- taxa de extracao de recebedor
- taxa de falso positivo em documento nao PIX
- distribuicao de `confidence`

## Variaveis de ambiente futuras

- `ReceiptReading__MaxUploadBytes`
- `ReceiptReading__AllowedMimeTypes__0`
- `ReceiptReading__AllowedExtensions__0`
- `ReceiptReading__RawTextMaxChars`
- `ReceiptReading__OcrEngine`
- `ReceiptReading__OcrLanguage`
- `ReceiptReading__MinConfidence`

## Fontes consultadas

- [PaddleOCR GitHub](https://github.com/PaddlePaddle/PaddleOCR): OCR local para PDF/imagem, Apache 2.0.
- [pdfplumber GitHub](https://github.com/jsvine/pdfplumber): extracao de texto, layout e tabelas em PDF.
- [pdfplumber License](https://github.com/jsvine/pdfplumber/blob/stable/LICENSE.txt): licenca MIT.
- [PyMuPDF](https://pymupdf.io/pymupdf): extracao/conversao de PDFs, com licenca AGPL/comercial.
- [OpenCV License](https://opencv.org/license/): Apache 2.0 nas versoes 4.5+.
- [Tesseract GitHub](https://github.com/tesseract-ocr/tesseract): OCR engine local, Apache 2.0.
