namespace PixApi.Application.Contas;

// Exceções "de negócio" da Application. A Api as converte em ProblemDetails (ApiExceptionHandler).
// Em C# todas as exceções são "unchecked" (não existe "throws" na assinatura).
public class ContaNaoEncontradaException(long id) : Exception($"Conta não encontrada: id={id}");

public class ChavePixDuplicadaException(string chavePix) : Exception($"Chave Pix já cadastrada: {chavePix}");

public class ChavePixNaoEncontradaException(string chavePix) : Exception($"Chave Pix não encontrada: {chavePix}");
