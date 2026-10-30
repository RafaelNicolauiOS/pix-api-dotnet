using System.Globalization;

namespace PixApi.Domain;

// Fica no domínio porque é lançada pela própria Conta ao debitar.
// InvariantCulture: "50.01" sempre, mesmo num servidor em pt-BR (que escreveria "50,01").
public class SaldoInsuficienteException(string chavePix, decimal valor)
    : Exception($"Saldo insuficiente na chave {chavePix} para transferir R$ {valor.ToString(CultureInfo.InvariantCulture)}");
