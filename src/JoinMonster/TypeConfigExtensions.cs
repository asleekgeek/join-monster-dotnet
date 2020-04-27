using System;
using GraphQL;
using GraphQL.Utilities;
using JoinMonster.Builders;
using JoinMonster.Configs;

namespace JoinMonster
{
    public static class TypeConfigExtensions
    {
        public static SqlTableConfigBuilder SqlTable(this TypeConfig typeConfig, string tableName, string uniqueKey) =>
            SqlTable(typeConfig, tableName, new[] {uniqueKey});

        public static SqlTableConfigBuilder SqlTable(this TypeConfig typeConfig, string tableName, string[] uniqueKey)
        {
            if (typeConfig == null) throw new ArgumentNullException(nameof(typeConfig));

            var builder = SqlTableConfigBuilder.Create(tableName, uniqueKey);
            typeConfig.WithMetadata(nameof(SqlTableConfig), builder.SqlTableConfig);
            return builder;
        }
    }
}
