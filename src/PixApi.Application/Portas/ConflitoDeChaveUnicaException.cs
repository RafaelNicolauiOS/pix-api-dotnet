namespace PixApi.Application.Portas;

// A Infrastructure traduz a violação de UNIQUE do banco (PostgreSQL 23505) para esta exceção,
// e a Application a traduz para a regra de negócio. Assim a Application não conhece o Npgsql.
public class ConflitoDeChaveUnicaException(Exception causa)
    : Exception("Violação de chave única no banco", causa);
