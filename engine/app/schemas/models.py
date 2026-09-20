from pydantic import BaseModel, Field


class ItemPlanilha(BaseModel):
    codigo: str = ""
    descricao: str | None = "Sem descrição"
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
    margem_por_marca: dict[str, float] = Field(default_factory=dict)
    margem_por_categoria: dict[str, float] = Field(default_factory=dict)
    margem_por_fornecedor: dict[str, float] = Field(default_factory=dict)
    faixas_custo: list[dict[str, float]] = Field(default_factory=list)
    preco_minimo_por_produto: dict[str, float] = Field(default_factory=dict)
    preco_maximo_por_produto: dict[str, float] = Field(default_factory=dict)
    tipo_calculo: str = Field(default="margem")
    arredondamento: str = Field(default="nenhum")
    promocoes: list[dict[str, object]] = Field(default_factory=list)
    aumento_maximo_percentual: float = Field(default=0.15)
    reducao_maxima_percentual: float = Field(default=0.05)


class RequestProcessamento(BaseModel):
    planilha_fornecedor: list[ItemPlanilha]
    dados_atuais: list[ItemDadoAtual] = []
    regras: RegrasNegocio = RegrasNegocio()


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
    faturamento_atual: float = 0.0
    faturamento_sugerido: float = 0.0
    margem_atual: float = 0.0
    margem_sugerida: float = 0.0


class ResponseProcessamento(BaseModel):
    sucesso: bool
    mensagem: str
    resumo: ResumoProcessamento
    itens: list[ItemProcessado]
