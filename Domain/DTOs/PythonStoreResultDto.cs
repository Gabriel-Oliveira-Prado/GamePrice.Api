using System.Text.Json.Serialization;

namespace GamePrice.Api.Domain.DTOs
{
    public class PythonStoreResultDto
    {
        [JsonPropertyName("nome")]
        public string? Nome { get; set; }

        [JsonPropertyName("preco_atual")]
        public string? PrecoAtual { get; set; }

        [JsonPropertyName("preco_original")]
        public string? PrecoOriginal { get; set; }

        [JsonPropertyName("imagem")]
        public string? Imagem { get; set; }

        [JsonPropertyName("link")]
        public string? Link { get; set; }

        [JsonPropertyName("erro")]
        public string? Erro { get; set; }
    }
}
