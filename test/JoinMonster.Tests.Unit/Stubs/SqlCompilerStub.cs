using System.Threading.Tasks;
using GraphQL.Types;
using JoinMonster.Data;
using JoinMonster.Language.AST;

namespace JoinMonster.Tests.Unit.Stubs
{
    public class SqlCompilerStub : SqlCompiler
    {
        private readonly string _sql;

        public SqlCompilerStub(string sql = null) : base(new SqlDialectStub())
        {
            _sql = sql;
        }

        public override Task<string> Compile(Node node, IResolveFieldContext context)
        {
            return Task.FromResult(_sql);
        }
    }
}
