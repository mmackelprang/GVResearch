using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Iaet.Catalog;

public class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite("DataSource=catalog.db")
            .Options;
        return new CatalogDbContext(options);
    }
}
