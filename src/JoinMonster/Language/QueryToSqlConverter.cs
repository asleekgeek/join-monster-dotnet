using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL;
using GraphQL.Language.AST;
using GraphQL.Types;
using JoinMonster.Configs;
using JoinMonster.Language.AST;
using Argument = JoinMonster.Language.AST.Argument;
using Arguments = JoinMonster.Language.AST.Arguments;

namespace JoinMonster.Language
{
    /// <summary>
    /// The <see cref="QueryToSqlConverter"/> is responsible for converting GraphQL Query Ast to SQL Ast.
    /// </summary>
    public class QueryToSqlConverter
    {
        /// <summary>
        /// Convert the GraphQL Query Ast to SQL Ast.
        /// </summary>
        /// <param name="context">The <see cref="IResolveFieldContext"/>.</param>
        /// <returns>A <see cref="Node"/> representing the SQL Ast.</returns>
        /// <exception cref="ArgumentNullException">If <c>context</c> is null.</exception>
        public virtual Node Convert(IResolveFieldContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var fieldAst = context.FieldAst;
            var field = context.FieldDefinition;
            var parentType = context.ParentType.GetNamedType();

            return Convert(fieldAst, field, parentType, 0, context.UserContext);
        }

        private Node Convert(Field fieldAst, FieldType field, IGraphType parentType, int depth,
            IDictionary<string, object> userContext)
        {
            var sqlColumnConfig = field.GetSqlColumnConfig();
            if (sqlColumnConfig?.Ignored == true)
                return new SqlNoop();

            var gqlType = field.ResolvedType.GetNamedType();

            var sqlTableConfig = gqlType.GetSqlTableConfig();

            if (gqlType is IComplexGraphType complexGraphType)
            {
                if(sqlTableConfig == null)
                    return new SqlNoop();

                if (depth >= 1)
                {
                    //TODO: Validate that either join, batch or junction is set on the field
                }

                return HandleTable(fieldAst, field, complexGraphType, sqlTableConfig, depth, userContext);
            }

            if (sqlColumnConfig != null || field.Resolver == null)
                return HandleColumn(fieldAst, field, gqlType, sqlColumnConfig, depth, userContext);

            return new SqlNoop();
        }

        private Node HandleTable(Field fieldAst, FieldType field, IComplexGraphType graphType,
            SqlTableConfig config, int depth, IDictionary<string, object> userContext)
        {
            var tableName = config.Table;
            var tableAs = fieldAst.Name;

            var columns = new SqlColumns();

            if (config.UniqueKey.Length == 1)
            {
                columns.Add(new SqlColumn(config.UniqueKey[0], config.UniqueKey[0], config.UniqueKey[0]));
            }
            else
            {
                var clumsyName = string.Join("#", config.UniqueKey);
                columns.Add(new SqlComposite(config.UniqueKey, clumsyName, clumsyName));
            }

            if (config.AlwaysFetch != null)
            {
                foreach (var column in config.AlwaysFetch)
                    columns.Add(new SqlColumn(column, column, column));
            }

            var tables = new SqlTables();

            HandleSelections(columns, tables, graphType, fieldAst.SelectionSet.Selections, depth, userContext);

            var arguments = HandleArguments(fieldAst);
            var grabMany = field.ResolvedType.IsListType();
            var where = field.GetSqlWhere();
            var join = field.GetSqlJoin();

            return new SqlTable(tableName, tableAs, columns, tables, arguments, grabMany, where, join)
                .WithLocation(fieldAst.SourceLocation);
        }

        private Node HandleColumn(Field fieldAst, FieldType field, IGraphType graphType,
            SqlColumnConfig? config, int depth, IDictionary<string, object> userContext)
        {
            var fieldName = fieldAst.Name;
            var columnName = config?.Column ?? fieldName;
            var columnAs = fieldName;

            return new SqlColumn(columnName, fieldName, columnAs).WithLocation(fieldAst.SourceLocation);
        }

        private Arguments HandleArguments(Field fieldAst)
        {
            var arguments = new Arguments();
            if (fieldAst.Arguments != null)
            {
                foreach (var arg in fieldAst.Arguments)
                {
                    var value = new ValueNode(arg.Value.Value).WithLocation(arg.Value.SourceLocation);
                    var argument = new Argument(arg.Name, value).WithLocation(arg.SourceLocation);
                    arguments.Add(argument);
                }
            }
            return arguments;
        }

        private void HandleSelections(SqlColumns sqlColumns, SqlTables tables,
            IComplexGraphType graphType, IEnumerable<ISelection> selections, int depth, IDictionary<string, object> userContext)
        {
            foreach (var selection in selections)
            {
                switch (selection)
                {
                    case Field fieldAst:
                        var field = graphType.GetField(fieldAst.Name);
                        var node = Convert(fieldAst, field, graphType, ++depth, userContext);

                        switch (node)
                        {
                            case SqlColumn sqlColumn:
                                var existing = sqlColumns.FirstOrDefault(x => x.FieldName == fieldAst.Name);
                                if (existing != null)
                                    continue;

                                sqlColumns.Add(sqlColumn);
                                break;
                            case SqlTable sqlTable:
                                tables.Add(sqlTable);
                                break;
                            case SqlNoop _:
                                continue;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(node), $"Unknown node type ${node.GetType()}");
                        }

                        break;
                    case InlineFragment _:
                        break;
                    case FragmentSpread _:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(selection), $"Unknown selection kind: {selection.GetType()}");
                }
            }
        }
    }
}
