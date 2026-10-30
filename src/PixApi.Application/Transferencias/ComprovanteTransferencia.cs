namespace PixApi.Application.Transferencias;

// Resultado da transferência. DateTimeOffset em UTC = o Instant do Java.
public sealed record ComprovanteTransferencia(
    string ChaveOrigem,
    string ChaveDestino,
    decimal Valor,
    decimal SaldoOrigem,
    DateTimeOffset RealizadaEm);
