using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using WebApp.Api.Configuration;

namespace WebApp.Api.Data;

public sealed class DesignTimeEvaluationTaskDbContextFactory : IDesignTimeDbContextFactory<EvaluationTaskDbContext>
{
	public EvaluationTaskDbContext CreateDbContext(string[] args)
	{
		DbContextOptionsBuilder<EvaluationTaskDbContext> builder = new DbContextOptionsBuilder<EvaluationTaskDbContext>();
		builder.UseNpgsql(
			"Host=localhost;Database=fs0002_design;Username=postgres;Password=postgres",
			npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "fs0002"));

		return new EvaluationTaskDbContext(
			builder.Options,
			Options.Create(new EvaluationTaskSyncOptions()));
	}
}