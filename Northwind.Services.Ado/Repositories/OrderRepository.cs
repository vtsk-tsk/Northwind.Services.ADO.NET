using System.Data.Common;
using System.Globalization;
using Northwind.Services.Repositories;

namespace Northwind.Services.Ado.Repositories
{
    public sealed class OrderRepository : IOrderRepository
    {
        private readonly DbProviderFactory dbFactory;
        private readonly string connectionString;

        public OrderRepository(DbProviderFactory dbFactory, string connectionString)
        {
            this.dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<long> AddOrderAsync(Order order)
        {
            long orderId = -1;
            using (DbConnection connection = this.dbFactory.CreateConnection())
            {
                connection.ConnectionString = this.connectionString;
                await connection.OpenAsync();
                DbTransaction transaction = connection.BeginTransaction();
                try
                {
                    using (DbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = $"INSERT INTO Orders (CustomerID, EmployeeID, OrderDate, RequiredDate, ShippedDate, ShipVia, Freight, ShipName, ShipAddress, ShipCity, ShipRegion, ShipPostalCode, ShipCountry) " +
                            $"VALUES (@CustomerId, @EmployeeId, @OrderDate, @RequiredDate, @ShippedDate, @ShipVia, @Freight, @ShipName, @ShipAddress, @ShipCity, @ShipRegion, @ShipPostalCode, @ShipCountry); " +
                            $"SELECT last_insert_rowid();";

                        DbParameter pOrderId = command.CreateParameter();
                        pOrderId.ParameterName = "@OrderId";
                        pOrderId.Value = order.Id;
                        command.Parameters.Add(pOrderId);

                        DbParameter pCustomerId = command.CreateParameter();
                        pCustomerId.ParameterName = "@CustomerId";
                        pCustomerId.Value = order.Customer.Code.Code;
                        command.Parameters.Add(pCustomerId);

                        DbParameter pEmployeeId = command.CreateParameter();
                        pEmployeeId.ParameterName = "@EmployeeId";
                        pEmployeeId.Value = order.Employee.Id;
                        command.Parameters.Add(pEmployeeId);

                        DbParameter pOrderDate = command.CreateParameter();
                        pOrderDate.ParameterName = "@OrderDate";
                        pOrderDate.Value = order.OrderDate;
                        command.Parameters.Add(pOrderDate);

                        DbParameter pRequiredDate = command.CreateParameter();
                        pRequiredDate.ParameterName = "@RequiredDate";
                        pRequiredDate.Value = order.RequiredDate;
                        command.Parameters.Add(pRequiredDate);

                        DbParameter pShippedDate = command.CreateParameter();
                        pShippedDate.ParameterName = "@ShippedDate";
                        pShippedDate.Value = order.ShippedDate;
                        command.Parameters.Add(pShippedDate);

                        DbParameter pShipVia = command.CreateParameter();
                        pShipVia.ParameterName = "@ShipVia";
                        pShipVia.Value = order.Shipper.Id;
                        command.Parameters.Add(pShipVia);

                        DbParameter pFreight = command.CreateParameter();
                        pFreight.ParameterName = "@Freight";
                        pFreight.Value = order.Freight;
                        command.Parameters.Add(pFreight);

                        DbParameter pShipName = command.CreateParameter();
                        pShipName.ParameterName = "@ShipName";
                        pShipName.Value = order.ShipName;
                        command.Parameters.Add(pShipName);

                        DbParameter pShipAddress = command.CreateParameter();
                        pShipAddress.ParameterName = "@ShipAddress";
                        pShipAddress.Value = order.ShippingAddress.Address;
                        command.Parameters.Add(pShipAddress);

                        DbParameter pShipCity = command.CreateParameter();
                        pShipCity.ParameterName = "@ShipCity";
                        pShipCity.Value = order.ShippingAddress.City;
                        command.Parameters.Add(pShipCity);

                        DbParameter pShipRegion = command.CreateParameter();
                        pShipRegion.ParameterName = "@ShipRegion";
                        pShipRegion.Value = order.ShippingAddress.Region ?? string.Empty;
                        command.Parameters.Add(pShipRegion);

                        DbParameter pPostalCode = command.CreateParameter();
                        pPostalCode.ParameterName = "@ShipPostalCode";
                        pPostalCode.Value = order.ShippingAddress.PostalCode;
                        command.Parameters.Add(pPostalCode);

                        DbParameter pShipCountry = command.CreateParameter();
                        pShipCountry.ParameterName = "@ShipCountry";
                        pShipCountry.Value = order.ShippingAddress.Country;
                        command.Parameters.Add(pShipCountry);

                        orderId = (long)await command.ExecuteScalarAsync();

                        command.CommandText = $"DELETE FROM OrderDetails WHERE OrderID = @OrderId";
                        await command.ExecuteNonQueryAsync();

                        foreach (var od in order.OrderDetails)
                        {
                            command.CommandText = $"INSERT INTO OrderDetails (OrderID, ProductID, UnitPrice, Quantity, Discount ) " +
                                $"VALUES (@OrderId, @ProductId, @UnitPrice, @Quantity, @Discount )";
                            command.Parameters.Clear();

                            pOrderId.ParameterName = "@OrderId";
                            pOrderId.Value = orderId;
                            command.Parameters.Add(pOrderId);

                            DbParameter pProductId = command.CreateParameter();
                            pProductId.ParameterName = "@ProductId";
                            pProductId.Value = od.Product.Id;
                            command.Parameters.Add(pProductId);

                            DbParameter pUnitPrice = command.CreateParameter();
                            pUnitPrice.ParameterName = "@UnitPrice";
                            pUnitPrice.Value = od.UnitPrice;
                            command.Parameters.Add(pUnitPrice);

                            DbParameter pQuantity = command.CreateParameter();
                            pQuantity.ParameterName = "@Quantity";
                            pQuantity.Value = od.Quantity;
                            command.Parameters.Add(pQuantity);

                            DbParameter pDiscount = command.CreateParameter();
                            pDiscount.ParameterName = "@Discount";
                            pDiscount.Value = od.Discount;
                            command.Parameters.Add(pDiscount);

                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new RepositoryException(ex.Message);
                }
            }

            return orderId;
        }

        public async Task<Order> GetOrderAsync(long orderId)
        {
            if (orderId <= 0)
            {
                throw new RepositoryException(nameof(orderId));
            }

            Order order = new Order(orderId);
            using (DbConnection connection = this.dbFactory.CreateConnection())
            {
                connection.ConnectionString = this.connectionString;

                DbCommand command = connection.CreateCommand();
                command.CommandText = $"SELECT *, " +
                    $"c.CompanyName AS CustomerCompany, " +
                    $"s.CompanyName AS ShipCompany, " +
                    $"o.CustomerID AS Customer " +
                    $"FROM Orders o " +
                    $"INNER JOIN Customers c ON c.CustomerID = o.CustomerID " +
                    $"INNER JOIN Employees e ON e.EmployeeID = o.EmployeeID " +
                    $"INNER JOIN Shippers s ON s.ShipperID = o.ShipVia " +
                    $"WHERE o.OrderID = {orderId}";

                await connection.OpenAsync();
                using (DbDataReader reader = await command.ExecuteReaderAsync())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            order.Customer = new Customer(new CustomerCode(reader["Customer"].ToString()))
                            {
                                CompanyName = reader["CustomerCompany"].ToString(),
                            };
                            order.Employee = new Employee((long)reader["EmployeeID"])
                            {
                                FirstName = reader["FirstName"].ToString(),
                                LastName = reader["LastName"].ToString(),
                                Country = reader["Country"].ToString(),
                            };
                            order.OrderDate = Convert.ToDateTime(reader["OrderDate"].ToString(), new CultureInfo("en-us"));
                            order.RequiredDate = Convert.ToDateTime(reader["RequiredDate"].ToString(), new CultureInfo("en-us"));
                            order.ShippedDate = Convert.ToDateTime(reader["ShippedDate"].ToString(), new CultureInfo("en-us"));
                            order.Shipper = new Shipper((long)reader["ShipVia"])
                            {
                                CompanyName = reader["ShipCompany"].ToString(),
                            };
                            order.Freight = (double)reader["Freight"];
                            order.ShipName = reader["ShipName"].ToString();
                            order.ShippingAddress = new ShippingAddress(
                                address: reader["ShipAddress"].ToString(),
                                city: reader["ShipCity"].ToString(),
                                region: reader["ShipRegion"].ToString() == string.Empty ? null : reader["ShipRegion"].ToString(),
                                postalCode: reader["ShipPostalCode"].ToString(),
                                country: reader["ShipCountry"].ToString());
                        }
                    }
                }

                command.CommandText = $"SELECT *, " +
                    $"od.UnitPrice AS price " +
                    $"FROM OrderDetails od " +
                    $"INNER JOIN Products p ON p.ProductID = od.ProductID " +
                    $"INNER JOIN Suppliers s ON s.SupplierID = p.SupplierID " +
                    $"INNER JOIN Categories c ON c.CategoryID = p.CategoryID " +
                    $"WHERE OrderID = {orderId}";

                using (DbDataReader reader = await command.ExecuteReaderAsync())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            var orderDetail = new OrderDetail(order)
                            {
                                Product = new Product((long)reader["ProductID"])
                                {
                                    ProductName = reader["ProductName"].ToString(),
                                    SupplierId = (long)reader["SupplierID"],
                                    Supplier = reader["CompanyName"].ToString(),
                                    CategoryId = (long)reader["CategoryID"],
                                    Category = reader["CategoryName"].ToString(),
                                },
                                UnitPrice = (double)reader["price"],
                                Quantity = (long)reader["Quantity"],
                                Discount = (double)reader["Discount"],
                            };
                            order.OrderDetails.Add(orderDetail);
                        }
                    }
                }
            }

            return order;
        }

        public async Task<IList<Order>> GetOrdersAsync(int skip, int count)
        {
            if (skip < 0 || count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var orders = new List<Order>();

            using (DbConnection connection = this.dbFactory.CreateConnection())
            {
                connection.ConnectionString = this.connectionString;

                DbCommand command = connection.CreateCommand();
                command.CommandText = $"SELECT * FROM Orders LIMIT {count} OFFSET {skip}";

                await connection.OpenAsync();
                using DbDataReader reader = await command.ExecuteReaderAsync();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        var order = new Order((long)reader["OrderID"])
                        {
                            Customer = new Customer(
                                new CustomerCode(reader["CustomerID"].ToString())),
                            Employee = new Employee((long)reader["EmployeeID"]),
                        };
                        orders.Add(order);
                    }
                }
            }

            return orders;
        }

        public async Task RemoveOrderAsync(long orderId)
        {
            using DbConnection connection = this.dbFactory.CreateConnection();
            connection.ConnectionString = this.connectionString;
            await connection.OpenAsync();
            DbTransaction transaction = connection.BeginTransaction();
            try
            {
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = $"DELETE FROM OrderDetails WHERE OrderID = @OrderId";
                    DbParameter pOrderId = command.CreateParameter();
                    pOrderId.ParameterName = "@OrderId";
                    pOrderId.Value = orderId;
                    command.Parameters.Add(pOrderId);

                    await command.ExecuteNonQueryAsync();

                    command.CommandText = $"DELETE FROM Orders WHERE OrderID = @OrderId";
                    await command.ExecuteNonQueryAsync();
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new RepositoryException(ex.Message);
            }
        }

        public async Task UpdateOrderAsync(Order order)
        {
            using DbConnection connection = this.dbFactory.CreateConnection();
            connection.ConnectionString = this.connectionString;
            await connection.OpenAsync();
            DbTransaction transaction = connection.BeginTransaction();
            try
            {
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = $"UPDATE Orders " +
                        $"SET CustomerID = @CustomerId, EmployeeID = @EmployeeId, OrderDate = @OrderDate, RequiredDate = @RequiredDate, ShippedDate = @ShippedDate, " +
                            $"ShipVia = @ShipVia, Freight = @Freight, ShipName = @ShipName, ShipAddress = @ShipAddress, ShipCity = @ShipCity, ShipRegion = @ShipRegion, " +
                            $"ShipPostalCode = @ShipPostalCode, ShipCountry = @ShipCountry " +
                        $"WHERE OrderID = @OrderId";

                    DbParameter pOrderId = command.CreateParameter();
                    pOrderId.ParameterName = "@OrderId";
                    pOrderId.Value = order.Id;
                    command.Parameters.Add(pOrderId);

                    DbParameter pCustomerId = command.CreateParameter();
                    pCustomerId.ParameterName = "@CustomerId";
                    pCustomerId.Value = order.Customer.Code.Code;
                    command.Parameters.Add(pCustomerId);

                    DbParameter pEmployeeId = command.CreateParameter();
                    pEmployeeId.ParameterName = "@EmployeeId";
                    pEmployeeId.Value = order.Employee.Id;
                    command.Parameters.Add(pEmployeeId);

                    DbParameter pOrderDate = command.CreateParameter();
                    pOrderDate.ParameterName = "@OrderDate";
                    pOrderDate.Value = order.OrderDate;
                    command.Parameters.Add(pOrderDate);

                    DbParameter pRequiredDate = command.CreateParameter();
                    pRequiredDate.ParameterName = "@RequiredDate";
                    pRequiredDate.Value = order.RequiredDate;
                    command.Parameters.Add(pRequiredDate);

                    DbParameter pShippedDate = command.CreateParameter();
                    pShippedDate.ParameterName = "@ShippedDate";
                    pShippedDate.Value = order.ShippedDate;
                    command.Parameters.Add(pShippedDate);

                    DbParameter pShipVia = command.CreateParameter();
                    pShipVia.ParameterName = "@ShipVia";
                    pShipVia.Value = order.Shipper.Id;
                    command.Parameters.Add(pShipVia);

                    DbParameter pFreight = command.CreateParameter();
                    pFreight.ParameterName = "@Freight";
                    pFreight.Value = order.Freight;
                    command.Parameters.Add(pFreight);

                    DbParameter pShipName = command.CreateParameter();
                    pShipName.ParameterName = "@ShipName";
                    pShipName.Value = order.ShipName;
                    command.Parameters.Add(pShipName);

                    DbParameter pShipAddress = command.CreateParameter();
                    pShipAddress.ParameterName = "@ShipAddress";
                    pShipAddress.Value = order.ShippingAddress.Address;
                    command.Parameters.Add(pShipAddress);

                    DbParameter pShipCity = command.CreateParameter();
                    pShipCity.ParameterName = "@ShipCity";
                    pShipCity.Value = order.ShippingAddress.City;
                    command.Parameters.Add(pShipCity);

                    DbParameter pShipRegion = command.CreateParameter();
                    pShipRegion.ParameterName = "@ShipRegion";
                    pShipRegion.Value = order.ShippingAddress.Region;
                    command.Parameters.Add(pShipRegion);

                    DbParameter pPostalCode = command.CreateParameter();
                    pPostalCode.ParameterName = "@ShipPostalCode";
                    pPostalCode.Value = order.ShippingAddress.PostalCode;
                    command.Parameters.Add(pPostalCode);

                    DbParameter pShipCountry = command.CreateParameter();
                    pShipCountry.ParameterName = "@ShipCountry";
                    pShipCountry.Value = order.ShippingAddress.Country;
                    command.Parameters.Add(pShipCountry);

                    await command.ExecuteNonQueryAsync();

                    command.CommandText = $"DELETE FROM OrderDetails WHERE OrderID = @OrderId";
                    await command.ExecuteNonQueryAsync();

                    foreach (var od in order.OrderDetails)
                    {
                        command.CommandText = $"INSERT INTO OrderDetails (OrderID, ProductID, UnitPrice, Quantity, Discount ) " +
                            $"VALUES (@OrderId, @ProductId, @UnitPrice, @Quantity, @Discount )";
                        command.Parameters.Clear();

                        pOrderId.ParameterName = "@OrderId";
                        pOrderId.Value = order.Id;
                        command.Parameters.Add(pOrderId);

                        DbParameter pProductId = command.CreateParameter();
                        pProductId.ParameterName = "@ProductId";
                        pProductId.Value = od.Product.Id;
                        command.Parameters.Add(pProductId);

                        DbParameter pUnitPrice = command.CreateParameter();
                        pUnitPrice.ParameterName = "@UnitPrice";
                        pUnitPrice.Value = od.UnitPrice;
                        command.Parameters.Add(pUnitPrice);

                        DbParameter pQuantity = command.CreateParameter();
                        pQuantity.ParameterName = "@Quantity";
                        pQuantity.Value = od.Quantity;
                        command.Parameters.Add(pQuantity);

                        DbParameter pDiscount = command.CreateParameter();
                        pDiscount.ParameterName = "@Discount";
                        pDiscount.Value = od.Discount;
                        command.Parameters.Add(pDiscount);

                        await command.ExecuteNonQueryAsync();
                    }
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new RepositoryException(ex.Message);
            }
        }
    }
}
