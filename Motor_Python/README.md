# Motor de Precificação Local

Este projeto implementa uma API em Python com FastAPI para processar planilhas de fornecedores, calcular preços sugeridos e registrar logs estruturados de forma robusta.

## O que o projeto faz

A aplicação permite:

- receber planilhas em CSV, XLSX ou XLS;
- normalizar e limpar os dados da planilha;
- comparar os itens do fornecedor com os dados atuais;
- calcular preços sugeridos com base em regras de margem e limites de variação;
- retornar um resumo com itens em quarentena e alterações de preço;
- registrar logs detalhados em arquivos JSON rotacionados.

## Estrutura do projeto

- `main.py`: definição da API FastAPI, middlewares, rotas e autenticação simples.
- `services.py`: leitura de planilhas e lógica de processamento de dados.
- `schemas.py`: modelos Pydantic para entrada e saída da API.
- `logging_config.py`: configuração de logging com rotação de arquivos e logs por nível.
- `tests/`: testes automatizados para validação do comportamento da API e do sistema de logs.

## Requisitos

- Python 3.10+
- Dependências listadas em `requirements.txt`

## Instalação

### 1. Criar um ambiente virtual

No PowerShell:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
```

### 2. Instalar as dependências

```powershell
pip install -r requirements.txt
```

## Executando a aplicação

```powershell
uvicorn main:app --reload --host 0.0.0.0 --port 8000
```

A API ficará disponível em:

- Swagger UI: http://localhost:8000/docs
- ReDoc: http://localhost:8000/redoc

## Autenticação

As rotas protegidas exigem o header:

```http
X-App-Token: teste
```

O valor atual está definido no código como `teste` para fins de desenvolvimento.

## Endpoints

### Health check

```http
GET /ping
```

Resposta esperada:

```json
{
  "status": "online",
  "servidor": "Motor Python pronto!"
}
```

### Processar preços

```http
POST /process-prices
```

Exemplo de corpo:

```json
{
  "planilha_fornecedor": [
    {
      "codigo": "001",
      "descricao": "Produto A",
      "preco_custo": 10.5,
      "marca": "Marca X",
      "categoria": "Geral",
      "fornecedor": "Fornecedor 1"
    }
  ],
  "dados_atuais": [
    {
      "codigo": "001",
      "preco_venda_atual": 15.0,
      "preco_custo_atual": 10.0,
      "estoque": 20
    }
  ],
  "regras": {
    "margem_padrao": 0.3,
    "aumento_maximo_percentual": 0.15,
    "reducao_maxima_percentual": 0.05
  }
}
```

### Upload de planilha

```http
POST /upload-planilha
```

O endpoint aceita arquivos `.csv`, `.xlsx` e `.xls` enviados via multipart/form-data.

Exemplo com PowerShell:

```powershell
curl -X POST "http://localhost:8000/upload-planilha" `
  -H "X-App-Token: teste" `
  -F "file=@C:\caminho\para\planilha.xlsx"
```

## Logs

O projeto usa um sistema de logging configurado em `logging_config.py`.

### Variáveis de ambiente

Você pode ajustar o comportamento dos logs sem alterar o código:

- `APP_LOG_MAX_BYTES`: tamanho máximo do arquivo principal em bytes antes da rotação. Padrão: `5242880` (5 MB)
- `APP_LOG_BACKUP_COUNT`: quantidade de arquivos de backup a manter. Padrão: `3`
- `APP_LOG_ROTATION_WHEN`: frequência da rotação. Padrão: `midnight`
- `APP_LOG_ROTATION_INTERVAL`: intervalo da rotação, em minutos, quando o modo não for `midnight`. Padrão: `1`
- `APP_LOG_KEEP_DAYS`: quantidade de dias para manter arquivos antigos de log. Padrão: `7`
- `APP_LOG_LEVEL`: nível mínimo de log. Padrão: `INFO`
- `APP_LOG_CONSOLE`: habilita logs no console quando definido como `true`

### Exemplo no PowerShell

```powershell
$env:APP_LOG_MAX_BYTES="1048576"
$env:APP_LOG_BACKUP_COUNT="5"
$env:APP_LOG_KEEP_DAYS="14"
$env:APP_LOG_LEVEL="DEBUG"
$env:APP_LOG_CONSOLE="true"
```

### Arquivos gerados

- `app.log`: arquivo atual
- `app.info.log`, `app.warning.log`, `app.error.log`: logs por nível
- `app.log.1`, `app.log.2`, etc.: arquivos de backup gerados pela rotação

## Testes

Para executar os testes:

```powershell
pytest -q
```

## Observações

Este projeto foi pensado como uma API local para processamento interno de precificação. Para uso em produção, recomenda-se substituir o token fixo por autenticação mais robusta, como OAuth2, JWT ou integração com um serviço de autenticação.

