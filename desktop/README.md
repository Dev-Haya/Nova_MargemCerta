# EstoqueApp

Solução em C# (.NET 8, WinForms) implementando o diagrama do projeto:

## Estrutura
- **Models/ItemEstoque.cs** — modelo do item com custo, venda atual e venda sugerida
- **Data/DatabaseService.cs** — SQLite local: migração, carga de custo e atualização pós-aprovação
- **Utils/PlanilhaImporter.cs** — leitura de .xlsx/.csv com mapeamento dinâmico de colunas
- **Models/PythonContracts.cs** — contrato JSON compatível com a API FastAPI
- **Services/PythonIntegrationService.cs** — HttpClient autenticado para processar preços com o Python e controle opcional do processo .exe
- **Services/LicenseService.cs** — trava comercial validando mensalidade no Firebase
- **Forms/MainForm.cs** — tela principal: drag-and-drop, tabela comparativa com alertas vermelhos
- **Forms/ColumnMapperForm.cs** — escolha dinâmica dos cabeçalhos da planilha
- **Forms/MaintenanceForm.cs** — manutenção administrativa de cadastros com busca, validação e backup
- **Forms/FilterForm.cs** — busca, filtro por situação e seleção de colunas visíveis
- **Forms/ApprovalReviewForm.cs** — revisão tabular e aprovação em lote das alterações
- **Forms/DashboardForm.cs** — indicadores operacionais de preços, alterações e quarentena
- **Forms/BackupForm.cs** — histórico de backups e restauração guiada

## Como rodar
1. Abra a pasta no Visual Studio (ou `dotnet build` pela linha de comando, exige SDK .NET 8 com workload de Windows Desktop).
2. Inicie o motor Python na porta `8000`:

	```powershell
	uvicorn engine.main:app --host 127.0.0.1 --port 8000
	```

3. Configure `APP_SECRET_TOKEN` no mesmo valor usado pelo motor Python. O padrão de desenvolvimento é `teste`.
4. Em desenvolvimento, o Firebase é opcional. Para validar licença, configure `FIREBASE_PROJECT_ID` e `FIREBASE_CLIENT_ID`.
5. Rode o projeto (F5).

O desktop tenta reutilizar um motor Python que já esteja ativo. Caso não exista, inicia automaticamente `python -m uvicorn engine.main:app --host 127.0.0.1 --port 8000` e encerra apenas o processo que ele próprio iniciou ao fechar.

## Pontos que você vai precisar ajustar
- **Firebase**: use `FIREBASE_PROJECT_ID` com o Project ID do Firebase e `FIREBASE_CLIENT_ID` com o ID do documento na coleção `licencas`. Em `APP_ENVIRONMENT=Production`, ambos são obrigatórios.
- **Modo de desenvolvimento**: sem essas variáveis, o desktop abre sem validação de licença.
- **Python**: o desktop usa `GET /ping` e `POST /process-prices`, protegidos por `X-App-Token`.
- **URL alternativa**: defina `MOTOR_PYTHON_URL` se o motor não estiver em `http://127.0.0.1:8000`.
- **Inicialização do Python**: defina `MOTOR_PYTHON_EXE`, `MOTOR_PYTHON_ARGS` e `MOTOR_PYTHON_WORKDIR` quando o Python ou o executável empacotado estiverem fora do diretório padrão.
- **Caminho do .exe do Python**: `ProcessoPythonController.Iniciar("caminho\\para\\programa.exe")` pode ser usado quando o motor for distribuído junto com o desktop.
- **Manutenção de cadastro**: use o botão `Manutenção de cadastro` na tela principal. A gravação cria backup em `backups/` e registra alterações na tabela `AuditoriaItens`.
- **Exclusão de cadastro**: selecione um ou mais registros na manutenção e use `Excluir selecionado(s)`. A operação exige autorização, confirmação, backup e registra a remoção na auditoria.
- **Autorização administrativa**: defina `APP_ADMIN_PASSWORD`. Sem essa variável, a manutenção só permite salvar em `APP_ENVIRONMENT=Development`.
- **Usuário da auditoria**: defina `APP_USER`; caso contrário, o sistema usa o nome do usuário do Windows.
- **Filtros da operação**: use `Filtros e colunas` para buscar produtos, mostrar apenas alterações/quarentena/aprovados e escolher quais colunas ficam visíveis.
- **Ordenação e filtros rápidos**: clique no cabeçalho para ordenar; clique com o botão direito para filtrar uma coluna específica.
- **Atalhos**: `Ctrl+I` importa planilhas, `Ctrl+P` processa preços, `Ctrl+A` abre aprovação, `Ctrl+F` abre filtros e `Ctrl+D` alterna o tema.
- **Indicadores visuais**: amarelo representa aumento, verde redução e vermelho quarentena.
- **Barra de progresso**: aparece durante a leitura de planilhas, especialmente em importações em lote.
- **Tema**: use `Alternar tema` ou `Ctrl+D`; a preferência da sessão pode ser iniciada com `APP_THEME=dark`.
- **Aprovação em lote**: `Aprovar` abre uma tabela com todos os preços alterados. Use `Aprovar todos e salvar` para confirmar o lote ou marque somente os itens desejados e use `Salvar selecionados`.
- **Dashboard**: mostra produtos cadastrados, alterações, aumentos, reduções, quarentena e margem média sugerida.
- **Regras avançadas**: configure margem por fornecedor, faixas de custo, limites mínimo/máximo por produto, markup ou margem, arredondamento comercial e promoções com período de validade.
- **Simulação financeira**: o `Dashboard` exibe faturamento atual estimado, faturamento sugerido e impacto do lote com base no estoque.
- **Exportação**: `Exportar CSV` salva os itens atualmente visíveis, respeitando busca e filtros.
- **Importação em lote**: `Importar Planilha` aceita vários arquivos; cada arquivo é validado antes de ser gravado.
- **Monitoramento de pasta**: `Monitorar pasta` observa arquivos `.csv` e `.xlsx` novos, espera a cópia terminar e abre o mapeamento para importar automaticamente.
- **Histórico**: `Histórico / backup` permite criar e restaurar backups. Antes de restaurar, o estado atual também é preservado.
- **Concorrência**: apenas uma instância do desktop é permitida. O banco usa mutex nomeado por arquivo e transações `Serializable`; gravações, backups e restaurações aguardam até 30 segundos pelo bloqueio e retornam uma mensagem específica se ele estiver ocupado.
- **Layouts de planilha**: o primeiro arquivo com um conjunto novo de cabeçalhos solicita o mapeamento. O resultado é salvo em `layouts-colunas.json` e reutilizado automaticamente em arquivos futuros, inclusive no monitoramento de pasta.

## Integrações externas pendentes

Integrações com ERP, marketplace, e-mail, login Firebase, agendamento do Windows e envio automático de relatórios precisam de credenciais, fornecedor e formato de API definidos. O sistema mantém o motor Python local desacoplado para receber esses adaptadores sem alterar a operação principal.

## Testes

Execute os testes do motor Python:

```powershell
.venv\Scripts\python.exe -m pytest engine\tests -q
```

Execute os testes do desktop, incluindo SQLite, auditoria, restauração, filtros e aprovação:

```powershell
dotnet test desktop.tests\EstoqueApp.Tests.csproj
```

Os backups recebem milissegundos e identificador único para evitar colisões quando duas operações ocorrem no mesmo segundo.
