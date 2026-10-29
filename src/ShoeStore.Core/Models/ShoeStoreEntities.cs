using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;
using System.Data.Entity.SqlServer;

namespace ShoeStore.Core.Models;

public sealed class StoreConfiguration : DbConfiguration
{
    public StoreConfiguration()
    {
        SetProviderServices("System.Data.SqlClient", SqlProviderServices.Instance);
    }
}

[DbConfigurationType(typeof(StoreConfiguration))]
public partial class ShoeStoreEntities : DbContext
{
    static ShoeStoreEntities() => Database.SetInitializer<ShoeStoreEntities>(null);

    public ShoeStoreEntities(string connectionString) : base(CreateConnection(connectionString), true)
    {
        Configuration.LazyLoadingEnabled = false;
        Configuration.ProxyCreationEnabled = false;
    }

    private static DbConnection CreateConnection(string connectionString) => new EntityConnection(
        new EntityConnectionStringBuilder
        {
            Metadata = "res://ShoeStore.Core/ShoeStore.Core.Models.ShoeStoreModel.csdl|res://ShoeStore.Core/ShoeStore.Core.Models.ShoeStoreModel.ssdl|res://ShoeStore.Core/ShoeStore.Core.Models.ShoeStoreModel.msl",
            Provider = "System.Data.SqlClient",
            ProviderConnectionString = connectionString
        }.ConnectionString);

    public virtual DbSet<Roles> Roles { get; set; } = null!;
    public virtual DbSet<Users> Users { get; set; } = null!;
    public virtual DbSet<Categories> Categories { get; set; } = null!;
    public virtual DbSet<Subcategories> Subcategories { get; set; } = null!;
    public virtual DbSet<Manufacturers> Manufacturers { get; set; } = null!;
    public virtual DbSet<Products> Products { get; set; } = null!;
    public virtual DbSet<Sizes> Sizes { get; set; } = null!;
    public virtual DbSet<StockItems> StockItems { get; set; } = null!;
    public virtual DbSet<Orders> Orders { get; set; } = null!;
    public virtual DbSet<OrderLines> OrderLines { get; set; } = null!;
}

