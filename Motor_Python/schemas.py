from pydantic import BaseModel, Field

# --- ESTRUTURA DOS DADOS QUE ENTRAM ---

class ItemPlanilha(BaseModel):
    codigo: str = ""
    descricao: str | None = "Sem descrição"
    # Mudamos de gt=0 (maior que zero) para ge=0 (maior ou igual a zero) e permitimos default 0.0
    preco_custo: float = Field(default=0.0, ge=0, description="Preço de custo")
    marca: str | None = "Sem Marca"
    categoria: str | None = "Geral"
    fornecedor: str | None = ""

class ItemDadoAtual(BaseModel):
    codigo: str
    preco_venda_atual: float = 0.0
    preco_custo_atual: float | None = 0.0
    estoque: int | None = 0

class RegrasNegocio(BaseModel):
    margem_padrao: float = Field(default=0.30, ge=0, le=1.0)
    aumento_maximo_percentual: float = Field(default=0.15)
    reducao_maxima_percentual: float = Field(default=0.05)

class RequestProcessamento(BaseModel):
    planilha_fornecedor: list[ItemPlanilha]
    dados_atuais: list[ItemDadoAtual] = []
    regras: RegrasNegocio = RegrasNegocio()


# --- ESTRUTURA DOS DADOS QUE SAEM ---

class ItemProcessado(BaseModel):
    codigo: str
    descricao: str
    marca: str
    categoria: str
    preco_custo_novo: float
    preco_venda_atual: float
    preco_venda_sugerido: float
    margem_aplicada: float
    diferenca_valor: float
    quarentena: bool
    alerta: str | None = None

class ResumoProcessamento(BaseModel):
    total_itens: int
    itens_em_quarentena: int
    alteracoes_de_preco: int

class ResponseProcessamento(BaseModel):
    sucesso: bool
    mensagem: str
    resumo: ResumoProcessamento
    itens: list[ItemProcessado]
