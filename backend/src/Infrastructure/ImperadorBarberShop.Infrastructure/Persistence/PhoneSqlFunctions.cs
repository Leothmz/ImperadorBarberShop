using System.Data.Common;
using System.Runtime.CompilerServices;
using ImperadorBarberShop.Domain.ValueObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ImperadorBarberShop.Infrastructure.Persistence;

/// <summary>
/// Expõe <see cref="BrazilianPhone"/> ao SQL, para que a migração que criou Clients agrupe os
/// telefones legados com as mesmas regras do agendamento, sem uma segunda cópia delas em SQL.
/// Devolvem NULL para telefone ilegível.
/// </summary>
/// <remarks>
/// Registradas por comando, não na abertura da conexão: os testes (e quem mais passar ao EF
/// uma conexão já aberta) nunca disparam ConnectionOpened. Por isso um script gerado com
/// <c>dotnet ef migrations script</c> não roda no sqlite3 puro — as migrações sobem pelo app.
/// </remarks>
public sealed class PhoneSqlFunctions : DbCommandInterceptor
{
    public const string CanonicalFunction = "imperador_phone_canonical";
    public const string MatchKeyFunction = "imperador_phone_match_key";

    public static readonly PhoneSqlFunctions Instance = new();

    private readonly ConditionalWeakTable<SqliteConnection, object> _registered = new();

    private PhoneSqlFunctions() { }

    public override DbCommand CommandCreated(CommandEndEventData eventData, DbCommand result)
    {
        if (result.Connection is SqliteConnection connection)
            _registered.GetValue(connection, Register);
        return result;
    }

    // Numa conexão fechada o Microsoft.Data.Sqlite guarda a função e a aplica ao abrir
    private static object Register(SqliteConnection connection)
    {
        connection.CreateFunction(CanonicalFunction,
            (string? raw) => BrazilianPhone.TryParse(raw, out var phone) ? phone.Canonical : null,
            isDeterministic: true);
        connection.CreateFunction(MatchKeyFunction,
            (string? raw) => BrazilianPhone.TryParse(raw, out var phone) ? phone.MatchKey : null,
            isDeterministic: true);
        return new object();
    }
}
