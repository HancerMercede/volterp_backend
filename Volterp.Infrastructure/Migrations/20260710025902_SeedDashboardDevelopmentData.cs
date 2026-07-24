using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedDashboardDevelopmentData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ========================
            // Categories
            // ========================
            migrationBuilder.Sql(@"
                INSERT INTO ""Categories"" (""Id"", ""Name"", ""Description"", ""CompanyId"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
                VALUES
                (1, 'Computación',  'Equipos y accesorios de computación', 1, true, '2026-01-01 00:00:00Z', NULL),
                (2, 'Accesorios',   'Periféricos y accesorios',           1, true, '2026-01-01 00:00:00Z', NULL),
                (3, 'Muebles',      'Muebles de oficina',                 1, true, '2026-01-01 00:00:00Z', NULL),
                (4, 'Otros',        'Otras categorías',                   1, true, '2026-01-01 00:00:00Z', NULL) ON CONFLICT (""Id"") DO NOTHING;
            ");

            // ========================
            // Products
            // ========================
            migrationBuilder.Sql(@"
                INSERT INTO ""Products"" (""Id"", ""Name"", ""Category"", ""Stock"", ""Price"", ""ImageUrl"", ""Description"", ""CompanyId"", ""IsActive"", ""CategoryId"", ""CreatedAt"", ""UpdatedAt"")
                VALUES
                (1, 'Laptop HP Pavilion 15',         'Computación', 12, 45000, 'https://images.unsplash.com/photo-1496181133206-80ce9b88a853?w=100', 'Laptop HP Pavilion 15 con procesador Intel Core i5 y 16GB RAM', 1, true, 1, '2026-01-01 00:00:00Z', NULL),
                (2, 'Mouse Logitech MX Master 3',    'Accesorios',  30, 4500,  'https://images.unsplash.com/photo-1527864550417-7fd91fc51a46?w=100', 'Mouse inalámbrico Logitech MX Master 3 de alta precisión', 1, true, 2, '2026-01-01 00:00:00Z', NULL),
                (3, 'Monitor Samsung 27"" 4K',       'Computación', 8,  29000, 'https://images.unsplash.com/photo-1527443224154-c4a3942d3acf?w=100', 'Monitor Samsung 27 pulgadas 4K UHD para uso profesional', 1, true, 1, '2026-01-01 00:00:00Z', NULL),
                (4, 'Teclado Corsair K70',           'Accesorios',  15, 8900,  'https://images.unsplash.com/photo-1511467687858-23d96c32e4ae?w=100', 'Teclado mecánico Corsair K70 con retroiluminación RGB', 1, true, 2, '2026-01-01 00:00:00Z', NULL),
                (5, 'Auriculares Sony WH-1000XM5',   'Accesorios',  5,  12500, NULL, 'Auriculares inalámbricos Sony WH-1000XM5 con cancelación de ruido', 1, true, 2, '2026-01-01 00:00:00Z', NULL),
                (6, 'Silla Ergonómica',              'Muebles',     3,  45000, NULL, 'Silla ergonómica de oficina con soporte lumbar ajustable', 1, true, 3, '2026-01-01 00:00:00Z', NULL),
                (7, 'Escritorio Eléctrico Flexispot','Muebles',     4,  22000, NULL, 'Escritorio eléctrico ajustable en altura Flexispot', 1, true, 3, '2026-01-01 00:00:00Z', NULL),
                (8, 'Webcam Logitech StreamCam',     'Accesorios',  10, 6500,  NULL, 'Cámara web Logitech StreamCam 1080p para streaming', 1, true, 2, '2026-01-01 00:00:00Z', NULL) ON CONFLICT (""Id"") DO NOTHING;
            ");

            // ========================
            // Clients
            // ========================
            migrationBuilder.Sql(@"
                INSERT INTO ""Clients"" (""Id"", ""Name"", ""Email"", ""Phone"", ""Address"", ""CompanyId"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"", ""ImageUrl"")
                VALUES
                (1, 'Carlos Mendoza',       'carlos@example.com',        '809-555-0101', 'Av. Winston Churchill 123, Santo Domingo', 1, true, '2026-01-01 00:00:00Z', NULL, NULL),
                (2, 'Laura Díaz',           'laura@example.com',         '809-555-0102', 'Calle Las Palmas 45, Santiago',            1, true, '2026-01-01 00:00:00Z', NULL, NULL),
                (3, 'TechSolutions SRL',    'info@techsolutions.do',     '809-555-0103', 'Av. 27 de Febrero 300, Santo Domingo',     1, true, '2026-01-01 00:00:00Z', NULL, NULL),
                (4, 'María Fernández',      'maria@example.com',         '809-555-0104', 'Calle El Conde 78, Santo Domingo',         1, true, '2026-01-01 00:00:00Z', NULL, NULL),
                (5, 'Distribuidora del Sur', 'ventas@delsur.com',        '809-555-0105', 'Av. Independencia 500, San Cristóbal',     1, true, '2026-01-01 00:00:00Z', NULL, NULL) ON CONFLICT (""Id"") DO NOTHING;
            ");

            // ========================
            // Suppliers
            // ========================
            migrationBuilder.Sql(@"
                INSERT INTO ""Suppliers"" (""Id"", ""Name"", ""Email"", ""Phone"", ""Address"", ""Category"", ""ContactPerson"", ""CompanyId"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"", ""ImageUrl"")
                VALUES
                (1, 'TechDistributor', 'contacto@techdist.com',  '809-555-0201', 'Av. Sarasota 20, Santo Domingo',    'Tecnología', 'Juan Pérez',     1, true, '2026-01-01 00:00:00Z', NULL, NULL),
                (2, 'AudioWorld',      'ventas@audioworld.com',  '809-555-0202', 'Calle Benito Juárez 15, Santiago', 'Accesorios', 'Ana Gómez',      1, true, '2026-01-01 00:00:00Z', NULL, NULL),
                (3, 'MueblesOffice',   'info@mueblesoffice.com', '809-555-0203', 'Av. Los Próceres 80, Santo Domingo','Muebles',    'Pedro Martínez', 1, true, '2026-01-01 00:00:00Z', NULL, NULL) ON CONFLICT (""Id"") DO NOTHING;
            ");

            // ========================
            // Sales (Status: 0=Pending, 1=Completed)
            // ========================
            migrationBuilder.Sql(@"
                INSERT INTO ""Sales"" (""Id"", ""CompanyId"", ""ClienteId"", ""ClienteName"", ""Status"", ""Total"", ""Notes"", ""CreatedAt"", ""UpdatedAt"")
                VALUES
                (1, 1, 1, 'Carlos Mendoza',       1, 12000, 'Laptop HP Pavilion — venta directa',        '2026-07-03 10:00:00Z', '2026-07-03 12:00:00Z'),
                (2, 1, 2, 'Laura Díaz',           1, 15000, 'Monitor Samsung + teclado',                 '2026-07-04 11:30:00Z', '2026-07-04 14:00:00Z'),
                (3, 1, 3, 'TechSolutions SRL',    1, 11000, 'Kit de mouses y teclados corporativos',     '2026-07-05 09:15:00Z', '2026-07-05 11:00:00Z'),
                (4, 1, 4, 'María Fernández',      1, 18000, 'Silla ergonómica + webcam',                 '2026-07-06 14:00:00Z', '2026-07-06 16:30:00Z'),
                (5, 1, 5, 'Distribuidora del Sur',1, 22000, '2 laptops HP Pavilion para inventario',     '2026-07-07 16:45:00Z', '2026-07-07 18:00:00Z'),
                (6, 1, 1, 'Carlos Mendoza',       1, 25000, 'Escritorio eléctrico + auriculares',        '2026-07-08 12:00:00Z', '2026-07-08 14:00:00Z'),
                (7, 1, 2, 'Laura Díaz',           0, 8000,  'Webcam Logitech + auriculares (pendiente)', '2026-07-09 15:30:00Z', NULL) ON CONFLICT (""Id"") DO NOTHING;
            ");

            // ========================
            // SaleItems
            // ========================
            migrationBuilder.Sql(@"
                INSERT INTO ""SaleItems"" (""Id"", ""SaleId"", ""ProductId"", ""ProductName"", ""ProductCategory"", ""ProductCode"", ""ProductImageUrl"", ""Quantity"", ""UnitPrice"", ""Subtotal"")
                VALUES
                (1,  1, 3, 'Monitor Samsung 27"" 4K',       'Computación', NULL, 'https://images.unsplash.com/photo-1527443224154-c4a3942d3acf?w=100', 1, 29000, 29000),
                (2,  1, 2, 'Mouse Logitech MX Master 3',   'Accesorios',  NULL, 'https://images.unsplash.com/photo-1527864550417-7fd91fc51a46?w=100', 2, 4500,   9000),
                (3,  2, 3, 'Monitor Samsung 27"" 4K',       'Computación', NULL, 'https://images.unsplash.com/photo-1527443224154-c4a3942d3acf?w=100', 1, 29000, 29000),
                (4,  2, 4, 'Teclado Corsair K70',           'Accesorios',  NULL, 'https://images.unsplash.com/photo-1511467687858-23d96c32e4ae?w=100', 1, 8900,   8900),
                (5,  3, 2, 'Mouse Logitech MX Master 3',   'Accesorios',  NULL, 'https://images.unsplash.com/photo-1527864550417-7fd91fc51a46?w=100', 2, 4500,   9000),
                (6,  3, 4, 'Teclado Corsair K70',           'Accesorios',  NULL, 'https://images.unsplash.com/photo-1511467687858-23d96c32e4ae?w=100', 1, 8900,   8900),
                (7,  4, 6, 'Silla Ergonómica',              'Muebles',     NULL, NULL, 1, 45000, 45000),
                (8,  4, 8, 'Webcam Logitech StreamCam',    'Accesorios',  NULL, NULL, 1, 6500,   6500),
                (9,  5, 1, 'Laptop HP Pavilion 15',         'Computación', NULL, 'https://images.unsplash.com/photo-1496181133206-80ce9b88a853?w=100', 2, 45000, 90000),
                (10, 5, 2, 'Mouse Logitech MX Master 3',   'Accesorios',  NULL, 'https://images.unsplash.com/photo-1527864550417-7fd91fc51a46?w=100', 2, 4500,   9000),
                (11, 6, 7, 'Escritorio Eléctrico Flexispot','Muebles',     NULL, NULL, 1, 22000, 22000),
                (12, 6, 5, 'Auriculares Sony WH-1000XM5',  'Accesorios',  NULL, NULL, 1, 12500, 12500),
                (13, 7, 8, 'Webcam Logitech StreamCam',    'Accesorios',  NULL, NULL, 1, 6500,   6500),
                (14, 7, 5, 'Auriculares Sony WH-1000XM5',  'Accesorios',  NULL, NULL, 1, 12500, 12500) ON CONFLICT (""Id"") DO NOTHING;
            ");

            // ========================
            // Purchases (Status: string — Pending, Completed)
            // ========================
            migrationBuilder.Sql(@"
                INSERT INTO ""Purchases"" (""Id"", ""CompanyId"", ""SupplierId"", ""SupplierName"", ""Status"", ""Total"", ""Notes"", ""CreatedAt"", ""UpdatedAt"", ""CreatedBy"", ""UpdatedBy"")
                VALUES
                (1, 1, 1, 'TechDistributor', 'Completed', 85000, 'Monitores y laptops',         '2026-07-01 08:00:00Z', '2026-07-02 10:00:00Z', NULL, NULL),
                (2, 1, 2, 'AudioWorld',      'Completed', 12500, 'Auriculares Sony',            '2026-07-03 09:30:00Z', '2026-07-04 11:00:00Z', NULL, NULL),
                (3, 1, 3, 'MueblesOffice',   'Pending',   67000, 'Sillas ergonómicas (tránsito)','2026-07-08 14:00:00Z', NULL, NULL, NULL),
                (4, 1, 1, 'TechDistributor', 'Pending',   22000, 'Teclados y mouses corporativos','2026-07-09 16:00:00Z', NULL, NULL, NULL) ON CONFLICT (""Id"") DO NOTHING;
            ");

            // ========================
            // PurchaseItems
            // ========================
            migrationBuilder.Sql(@"
                INSERT INTO ""PurchaseItems"" (""Id"", ""PurchaseId"", ""ProductId"", ""ProductName"", ""ProductCode"", ""Quantity"", ""UnitPrice"", ""Subtotal"")
                VALUES
                (1, 1, 3, 'Monitor Samsung 27"" 4K',       'MON-001', 2, 29000, 58000),
                (2, 1, 1, 'Laptop HP Pavilion 15',         'LAP-001', 1, 45000, 45000),
                (3, 2, 5, 'Auriculares Sony WH-1000XM5',   'AUR-001', 1, 12500, 12500),
                (4, 2, 8, 'Webcam Logitech StreamCam',     'WEB-001', 1, 6500,   6500),
                (5, 3, 6, 'Silla Ergonómica',              'SIL-001', 2, 45000, 90000),
                (6, 3, 7, 'Escritorio Eléctrico Flexispot','ESC-001', 1, 22000, 22000),
                (7, 4, 2, 'Mouse Logitech MX Master 3',    'MOU-001', 3, 4500,  13500),
                (8, 4, 4, 'Teclado Corsair K70',           'TEC-001', 2, 8900,  17800) ON CONFLICT (""Id"") DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove in reverse dependency order
            migrationBuilder.Sql("DELETE FROM \"PurchaseItems\";");
            migrationBuilder.Sql("DELETE FROM \"Purchases\";");
            migrationBuilder.Sql("DELETE FROM \"SaleItems\";");
            migrationBuilder.Sql("DELETE FROM \"Sales\";");
            migrationBuilder.Sql("DELETE FROM \"Suppliers\";");
            migrationBuilder.Sql("DELETE FROM \"Clients\";");
            migrationBuilder.Sql("DELETE FROM \"Products\";");
            migrationBuilder.Sql("DELETE FROM \"Categories\";");
        }
    }
}
