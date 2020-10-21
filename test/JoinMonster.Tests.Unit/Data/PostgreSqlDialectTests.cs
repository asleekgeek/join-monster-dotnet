using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using GraphQL;
using JoinMonster.Data;
using JoinMonster.Language.AST;
using Xunit;

namespace JoinMonster.Tests.Unit.Data
{
    public class PostgreSqlDialectTests
    {
        [Fact]
        public void Quote_WithString_QuotesString()
        {
            var dialect = new PostgresSqlDialect();

            var quoted = dialect.Quote("product");

            quoted.Should().Be("\"product\"");
        }

        [Fact]
        public void Quote_WithJsonField_QuotesString()
        {
            var dialect = new PostgresSqlDialect();

            var quoted = dialect.Quote("product->>'id'");

            quoted.Should().Be("\"product\"->>'id'");
        }

        [Fact]
        public void Quote_WithJsonFieldAsString_QuotesString()
        {
            var dialect = new PostgresSqlDialect();

            var quoted = dialect.Quote("product->'id'");

            quoted.Should().Be("\"product\"->'id'");
        }

        [Fact]
        public void CompositeKey_WithParentTableAndKeys_ReturnsCombinedString()
        {
            var dialect = new PostgresSqlDialect();

            var sql = dialect.CompositeKey("people", new[] {"id", "firstName", "lastName"});

            sql.Should().Be("NULLIF(CONCAT(\"people\".\"id\", \"people\".\"firstName\", \"people\".\"lastName\"), '')");
        }

        [Fact]
        public void HandleJoinedOneToManyPaginated_WhenCalledWithOffsetPagination_ReturnsPagedJoinString()
        {
            var dialect = new PostgresSqlDialect();
            var compilerContext = new SqlCompilerContext(new SqlCompiler(dialect));
            var parent = new SqlTable(null, null, "products", "products", "products", new Dictionary<string, object>(), true);
            parent.AddColumn("id", "id", "id", true);
            var node = new SqlTable(null, null, "variants", "variants", "variants", new Dictionary<string, object>(), true)
            {
                Join = (join, _, __, ___) => join.On("id", "productId"),
                OrderBy = new OrderBy("variants", "id", SortDirection.Ascending),
                Where = (where, _, __, ___) => where.Column("id", 1, "<>")
            };
            node.AddColumn("id", "id", "id", true);

            var arguments = new Dictionary<string, object>();
            var context = new ResolveFieldContext();
            var tables = new List<string>();

            dialect.HandleJoinedOneToManyPaginated(parent, node, arguments, context, tables, compilerContext,
                "\"products\".\"id\" = \"variants\".\"productId\"");

            tables.Should()
                .Contain("LEFT JOIN LATERAL (\n  SELECT \"variants\".*, COUNT(*) OVER () AS \"$total\"\n  FROM \"variants\" \"variants\"\n  WHERE \"products\".\"id\" = \"variants\".\"productId\" AND \"variants\".\"id\" <> @p0\n  ORDER BY \"variants\".\"id\" ASC\n  LIMIT ALL OFFSET 0\n) \"variants\" ON \"products\".\"id\" = \"variants\".\"productId\"");
        }

        [Fact]
        public void HandleJoinedOneToManyPaginated_WhenCalledWithKeysetPagination_ReturnsPagedJoinString()
        {
            var dialect = new PostgresSqlDialect();
            var compilerContext = new SqlCompilerContext(new SqlCompiler(dialect));
            var parent = new SqlTable(null, null, "products", "products", "products", new Dictionary<string, object>(), true);
            parent.AddColumn("id", "id", "id", true);
            var node = new SqlTable(null, null, "variants", "variants", "variants", new Dictionary<string, object>(), true)
            {
                Join = (join, _, __, ___) => join.On("id", "productId"),
                OrderBy = new OrderBy("products", "id", SortDirection.Ascending),
                SortKey = new SortKey("products", "id", "id", SortDirection.Ascending),
                Where = (where, _, __, ___) => where.Column("id", 1, "<>")

            };
            node.AddColumn("id", "id", "id", true);

            var arguments = new Dictionary<string, object>();
            var context = new ResolveFieldContext();
            var tables = new List<string>();

            dialect.HandleJoinedOneToManyPaginated(parent, node, arguments, context, tables, compilerContext,
                "\"products\".\"id\" = \"variants\".\"productId\"");

            tables.Should()
                .Contain(@"LEFT JOIN LATERAL (
  SELECT ""variants"".*
  FROM ""variants"" ""variants""
  WHERE ""products"".""id"" = ""variants"".""productId"" AND ""variants"".""id"" <> @p0
  ORDER BY ""variants"".""id"" ASC
  LIMIT ALL
) ""variants"" ON ""products"".""id"" = ""variants"".""productId""");
        }

        [Fact]
        public void HandleJoinedOneToManyPaginated_WhenCalledWithKeysetPaginationAndFirstAndAfter_ReturnsPagedJoinString()
        {
            var dialect = new PostgresSqlDialect();
            var compilerContext = new SqlCompilerContext(new SqlCompiler(dialect));
            var parent = new SqlTable(null, null, "products", "products", "products", new Dictionary<string, object>(),
                true);
            parent.AddColumn("id", "id", "id", true);
            var node = new SqlTable(null, null, "variants", "variants", "variants", new Dictionary<string, object>(),
                true)
            {
                Join = (join, _, __, ___) => join.On("id", "productId"),
                SortKey = new SortKey("products", "id", "id", SortDirection.Descending),
                Where = (where, _, __, ___) => where.Column("id", 1, "<>")

            };
            node.AddColumn("id", "id", "id", true);

            var arguments = new Dictionary<string, object>
            {
                {"first", 2},
                {"after", Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {id = 1})))}
            };
            var context = new ResolveFieldContext();
            var tables = new List<string>();

            dialect.HandleJoinedOneToManyPaginated(parent, node, arguments, context, tables, compilerContext,
                "\"products\".\"id\" = \"variants\".\"productId\"");

            using (new AssertionScope())
            {
                tables.Should()
                    .Contain(@"LEFT JOIN LATERAL (
  SELECT ""variants"".*
  FROM ""variants"" ""variants""
  WHERE ""products"".""id"" = ""variants"".""productId"" AND ""variants"".""id"" <> @p0 AND (""variants"".""id"") > (@p1)
  ORDER BY ""variants"".""id"" DESC
  LIMIT 3
) ""variants"" ON ""products"".""id"" = ""variants"".""productId""");

                compilerContext.Parameters.Should()
                    .BeEquivalentTo(new Dictionary<string, object>
                    {
                        {"@p0", 1},
                        {"@p1", 1}
                    });
            }
        }

        [Fact]
        public void HandleJoinedOneToManyPaginated_WhenCalledWithKeysetPaginationAndLastAndBefore_ReturnsPagedJoinString()
        {
            var dialect = new PostgresSqlDialect();
            var compilerContext = new SqlCompilerContext(new SqlCompiler(dialect));
            var parent = new SqlTable(null, null, "products", "products", "products", new Dictionary<string, object>(), true);
            parent.AddColumn("id", "id", "id", true);
            var node = new SqlTable(null, null, "variants", "variants", "variants", new Dictionary<string, object>(), true)
            {
                Join = (join, _, __, ____) => join.On("id", "productId"),
                OrderBy = new OrderBy("products", "id", SortDirection.Ascending),
                SortKey = new SortKey("products", "id", "id", SortDirection.Ascending),
                Where = (where, _, __, ___) => where.Column("id", 1, "<>")
            };
            node.AddColumn("id", "id", "id", true);

            var arguments = new Dictionary<string, object>
            {
                {"last", 5},
                {"before", Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id = 2 })))}
            };
            var context = new ResolveFieldContext();
            var tables = new List<string>();

            dialect.HandleJoinedOneToManyPaginated(parent, node, arguments, context, tables, compilerContext,
                "\"products\".\"id\" = \"variants\".\"productId\"");

            using (new AssertionScope())
            {
                tables.Should()
                    .Contain(@"LEFT JOIN LATERAL (
  SELECT ""variants"".*
  FROM ""variants"" ""variants""
  WHERE ""products"".""id"" = ""variants"".""productId"" AND ""variants"".""id"" <> @p0 AND (""variants"".""id"") < (@p1)
  ORDER BY ""variants"".""id"" DESC
  LIMIT 6
) ""variants"" ON ""products"".""id"" = ""variants"".""productId""");

                compilerContext.Parameters.Should()
                    .BeEquivalentTo(new Dictionary<string, object>
                    {
                        {"@p0", 1},
                        {"@p1", 2}
                    });
            }
        }

        [Fact]
        public void HandlePaginationAtRoot_WhenCalledWithOffsetPagination_ReturnsPagedJoinString()
        {
            var dialect = new PostgresSqlDialect();
            var compilerContext = new SqlCompilerContext(new SqlCompiler(dialect));
            var node = new SqlTable(null, null, "variants", "variants", "variants", new Dictionary<string, object>(), true)
            {
                OrderBy = new OrderBy("variants", "id", SortDirection.Ascending),
                Where = (where, _, __, ___) => where.Column("id", 1, "<>")
            };
            node.AddColumn("id", "id", "id", true);

            var arguments = new Dictionary<string, object>();
            var context = new ResolveFieldContext();
            var tables = new List<string>();

            dialect.HandlePaginationAtRoot(null, node, arguments, context, tables, compilerContext);

            using (new AssertionScope())
            {
                tables.Should()
                    .Contain(@"FROM (
  SELECT ""variants"".*, COUNT(*) OVER () AS ""$total""
  FROM ""variants"" ""variants""
  WHERE ""variants"".""id"" <> @p0
  ORDER BY ""variants"".""id"" ASC
  LIMIT ALL OFFSET 0
) ""variants""");

                compilerContext.Parameters.Should()
                    .BeEquivalentTo(new Dictionary<string, object>
                    {
                        {"@p0", 1},
                    });
            }
        }

        [Fact]
        public void HandlePaginationAtRoot_WhenCalledWithKeysetPagination_ReturnsPagedJoinString()
        {
            var dialect = new PostgresSqlDialect();
            var compilerContext = new SqlCompilerContext(new SqlCompiler(dialect));
            var node = new SqlTable(null, null, "variants", "variants", "variants", new Dictionary<string, object>(),
                true)
            {
                OrderBy = new OrderBy("products", "id", SortDirection.Ascending),
                SortKey = new SortKey("products", "id", "id", SortDirection.Ascending),
                Where = (where, _, __, ___) => where.Column("id", 1, "<>")
            };
            node.AddColumn("id", "id", "id", true);

            var arguments = new Dictionary<string, object>();
            var context = new ResolveFieldContext();
            var tables = new List<string>();

            dialect.HandlePaginationAtRoot(null, node, arguments, context, tables, compilerContext);

            using (new AssertionScope())
            {
                tables.Should()
                    .Contain(@"FROM (
  SELECT ""variants"".*
  FROM variants ""variants""
  WHERE ""variants"".""id"" <> @p0
  ORDER BY ""variants"".""id"" ASC
  LIMIT ALL
) ""variants""");

                compilerContext.Parameters.Should()
                    .BeEquivalentTo(new Dictionary<string, object>
                    {
                        {"@p0", 1}
                    });
            }
        }

        [Fact]
        public void HandlePaginationAtRoot_WhenCalledWithKeysetPaginationAndFirstAndAfter_ReturnsPagedJoinString()
        {
            var dialect = new PostgresSqlDialect();
            var compilerContext = new SqlCompilerContext(new SqlCompiler(dialect));
            var node = new SqlTable(null, null, "variants", "variants", "variants", new Dictionary<string, object>(), true)
            {
                SortKey = new SortKey("products", "id", "id", SortDirection.Descending),
                Where = (where, _, __, ___) => where.Column("id", 1, "<>")
            };
            node.AddColumn("id", "id", "id", true);

            var arguments = new Dictionary<string, object>
            {
                {"first", 2},
                {"after", Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id = 2 })))}
            };
            var context = new ResolveFieldContext();
            var tables = new List<string>();

            dialect.HandlePaginationAtRoot(null, node, arguments, context, tables, compilerContext);

            using (new AssertionScope())
            {
                tables.Should()
                    .Contain(@"FROM (
  SELECT ""variants"".*
  FROM variants ""variants""
  WHERE (""variants"".""id"") > (@p0) AND ""variants"".""id"" <> @p1
  ORDER BY ""variants"".""id"" DESC
  LIMIT 3
) ""variants""");

                compilerContext.Parameters.Should()
                    .BeEquivalentTo(new Dictionary<string, object>
                    {
                        {"@p0", 2},
                        {"@p1", 1}
                    });
            }
        }

        [Fact]
        public void HandlePaginationAtRoot_WhenCalledWithKeysetPaginationAndMultipleSortKeysAndFirstAndAfter_ReturnsPagedJoinString()
        {
            var dialect = new PostgresSqlDialect();
            var compilerContext = new SqlCompilerContext(new SqlCompiler(dialect));
            var node = new SqlTable(null, null, "variants", "variants", "variants", new Dictionary<string, object>(), true)
            {
                SortKey = new SortKey("products", "price", "price", SortDirection.Descending)
                {
                    ThenBy = new SortKey("products", "name", "name", SortDirection.Ascending)
                },
                Where = (where, _, __, ___) => where.Column("id", 1, "<>")
            };
            node.AddColumn("id", "id", "id", true);

            var arguments = new Dictionary<string, object>
            {
                {"first", 2},
                {"after", Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { price = 199, name = "Jacket" })))}
            };
            var context = new ResolveFieldContext();
            var tables = new List<string>();

            dialect.HandlePaginationAtRoot(null, node, arguments, context, tables, compilerContext);

            using (new AssertionScope())
            {
                tables.Should()
                    .Contain(@"FROM (
  SELECT ""variants"".*
  FROM variants ""variants""
  WHERE (""variants"".""price"", ""variants"".""name"") > (@p0, @p1) AND ""variants"".""id"" <> @p2
  ORDER BY ""variants"".""price"" DESC, ""variants"".""name"" ASC
  LIMIT 3
) ""variants""");

                compilerContext.Parameters.Should()
                    .BeEquivalentTo(new Dictionary<string, object>
                    {
                        {"@p0", 199},
                        {"@p1", "Jacket"},
                        {"@p2", 1}
                    });
            }
        }

        [Fact]
        public void HandlePaginationAtRoot_WhenCalledWithKeysetPaginationAndLastAndBefore_ReturnsPagedJoinString()
        {
            var dialect = new PostgresSqlDialect();
            var compilerContext = new SqlCompilerContext(new SqlCompiler(dialect));
            var node = new SqlTable(null, null, "variants", "variants", "variants", new Dictionary<string, object>(), true)
            {
                OrderBy = new OrderBy("products", "id", SortDirection.Ascending),
                SortKey = new SortKey("products", "id", "id", SortDirection.Ascending),
                Where = (where, _, __, ___) => where.Column("id", 1, "<>")
            };
            node.AddColumn("id", "id", "id", true);

            var arguments = new Dictionary<string, object>
            {
                {"last", 5},
                {"before", Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id = 2 })))}
            };
            var context = new ResolveFieldContext();
            var tables = new List<string>();

            dialect.HandlePaginationAtRoot(null, node, arguments, context, tables, compilerContext);

            using (new AssertionScope())
            {
                tables.Should()
                    .Contain(@"FROM (
  SELECT ""variants"".*
  FROM variants ""variants""
  WHERE (""variants"".""id"") < (@p0) AND ""variants"".""id"" <> @p1
  ORDER BY ""variants"".""id"" DESC
  LIMIT 6
) ""variants""");

                compilerContext.Parameters.Should()
                    .BeEquivalentTo(new Dictionary<string, object>
                    {
                        {"@p0", 2},
                        {"@p1", 1}
                    });
            }
        }
    }
}
