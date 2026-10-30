namespace PixApi.Application.Eventos;

// Evento de domínio: um fato que JÁ aconteceu (verbo no passado).
// "record" = tipo imutável com igualdade por valor (como uma struct com "let" em Swift, ou o record do Java).
// O Id único permite que os consumidores ignorem duplicatas (idempotência).
public sealed record TransferenciaRealizadaEvent(
    Guid Id,
    string ChaveOrigem,
    string ChaveDestino,
    decimal Valor,
    DateTimeOffset RealizadaEm);
