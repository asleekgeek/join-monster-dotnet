using System;
using JoinMonster.Configs;

namespace JoinMonster.Builders
{
    public class SqlColumnConfigBuilder
    {
        private SqlColumnConfigBuilder(SqlColumnConfig sqlColumnConfig)
        {
            SqlColumnConfig = sqlColumnConfig;
        }

        /// <summary>
        /// Create a new instance of the <see cref="SqlColumnConfigBuilder"/>
        /// </summary>
        /// <param name="columnName">A column name.</param>
        /// <returns>The <see cref="SqlColumnConfigBuilder"/>.</returns>
        public static SqlColumnConfigBuilder Create(string columnName)
        {
            if (columnName == null) throw new ArgumentNullException(nameof(columnName));

            var config = new SqlColumnConfig(columnName);

            return new SqlColumnConfigBuilder(config);
        }

        /// <summary>
        /// The SQL column configuration.
        /// </summary>
        public SqlColumnConfig SqlColumnConfig { get; }

        /// <summary>
        /// Set whether the column should be ignored from the generated SQL query.
        /// </summary>
        /// <param name="ignored"><c>true</c> if the column should be ignored, otherwise <c>false</c>.</param>
        /// <returns>The <see cref="SqlColumnConfigBuilder"/>.</returns>
        public SqlColumnConfigBuilder Ignore(bool ignored = true)
        {
            SqlColumnConfig.Ignored = ignored;
            return this;
        }

        /// <summary>
        /// Set the dependant columns, a custom resolver must be specified on the field.
        /// </summary>
        /// <param name="columnNames">The column names to select.</param>
        /// <returns>The <see cref="SqlColumnConfigBuilder"/>.</returns>
        public SqlColumnConfigBuilder Dependencies(params string[] columnNames)
        {
            SqlColumnConfig.Dependencies = columnNames;
            return this;
        }

        /// <summary>
        /// Set a method that resolves to a RAW SQL expression.
        /// </summary>
        /// <param name="expressionResolver">The expression resolver.</param>
        /// <returns>The <see cref="SqlColumnConfigBuilder"/>.</returns>
        public SqlColumnConfigBuilder Expression(ExpressionDelegate expressionResolver)
        {
            SqlColumnConfig.Expression = expressionResolver;
            return this;
        }
    }
}
