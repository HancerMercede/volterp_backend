using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoSeedImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Demo imagery for the seeded dataset. Every photo was verified to answer 200
            // before being referenced here, and each product keeps its own picture.
            migrationBuilder.Sql("""
                UPDATE "Products" p SET "ImageUrl" = m."url"
                FROM (VALUES
                    ('Aceite Vegetal Ybarra 1L',              'https://images.unsplash.com/photo-1550989460-0adf9ea622e2?w=100'),
                    ('Agua Mineral Sans Souci 1.5L',          'https://images.unsplash.com/photo-1526947425960-945c6e72858f?w=100'),
                    ('Arroz Ciro 5lb',                         'https://images.unsplash.com/photo-1586201375761-83865001e31c?w=100'),
                    ('Azúcar Refinada Blanca 5lb',             'https://images.unsplash.com/photo-1615485290382-441e4d049cb5?w=100'),
                    ('Cerveza Presidente 355ml',               'https://images.unsplash.com/photo-1436076863939-06870fe779c2?w=100'),
                    ('Detergente Líquido Concentrado 1Gal',    'https://images.unsplash.com/photo-1583947215259-38e31be8751f?w=100'),
                    ('Harina de Trigo Fugo 50lb',              'https://images.unsplash.com/photo-1590080875515-8a3a8dc5735e?w=100'),
                    ('Leche Entera 1L',                        'https://images.unsplash.com/photo-1541167760496-1628856ab772?w=100'),
                    ('Queso Blanco Dominicano 500g',           'https://images.unsplash.com/photo-1486297678162-eb2a19b0a32d?w=100'),
                    ('Refresco Cola 1.5L',                     'https://images.unsplash.com/photo-1551024709-8f23befc6f87?w=100'),
                    ('Amoxicilina 500mg Cápsulas',             'https://images.unsplash.com/photo-1584308666744-24d5c474f2ae?w=100'),
                    ('Azitromicina 500mg Tabletas',             'https://images.unsplash.com/photo-1607619056574-7b8d3ee536b2?w=100'),
                    ('Gel Higiénico Íntimo 200ml',             'https://images.unsplash.com/photo-1615397349754-cfa2066a298e?w=100'),
                    ('Ibuprofeno 400mg Tabletas',              'https://images.unsplash.com/photo-1585421514738-01798e348b17?w=100'),
                    ('Loratadina 10mg Tabletas',                'https://images.unsplash.com/photo-1620916566398-39f1143ab7be?w=100'),
                    ('Multivitamínico 60 Tabletas',             'https://images.unsplash.com/photo-1522335789203-aabd1fc54bc9?w=100'),
                    ('Paracetamol 500mg Tabletas',              'https://images.unsplash.com/photo-1576671081837-49000212a370?w=100'),
                    ('Shampoo Antidandruffo 400ml',             'https://images.unsplash.com/photo-1608248543803-ba4f8c70ae0b?w=100'),
                    ('Tensiómetro Digital de Brazo',            'https://images.unsplash.com/photo-1584982751601-97dcc096659c?w=100'),
                    ('Termómetro Digital Infrared',            'https://images.unsplash.com/photo-1596462502278-27bfdc403348?w=100'),
                    ('Barniz Marino 1Gal',                     'https://images.unsplash.com/photo-1562259949-e8e7689d7828?w=100'),
                    ('Cemento Gris 50kg',                      'https://images.unsplash.com/photo-1504307651254-35680f356dfd?w=100'),
                    ('Codo PVC 4 pulg 90 Grados',              'https://images.unsplash.com/photo-1621905252507-b35492cc74b4?w=100'),
                    ('Llave de Impacto 1/2 pulg',              'https://images.unsplash.com/photo-1530124566582-a618bc2615dc?w=100'),
                    ('Mortero Seco 40kg',                      'https://images.unsplash.com/photo-1503387762-592deb58ef4e?w=100'),
                    ('Nivel de Burbuja 60cm',                   'https://images.unsplash.com/photo-1558618666-fcd25c85cd64?w=100'),
                    ('Pintura Acrílica Blanco Galón',          'https://images.unsplash.com/photo-1615874959474-d609969a20ed?w=100'),
                    ('Tubería PVC 4 pulg x 6m',                'https://images.unsplash.com/photo-1541123437800-1bb1317badc2?w=100'),
                    ('Varilla Corrugada 1/2 pulg x 30ft',       'https://images.unsplash.com/photo-1541888946425-d81bb19240f5?w=100'),
                    ('Varilla Corrugada 3/8 pulg x 30ft',       'https://images.unsplash.com/photo-1590069261209-f8e9b8642343?w=100'),
                    ('Café Dominicano',                        'https://images.unsplash.com/photo-1447933601403-0c6688de566e?w=100'),
                    ('Chocolate en Taza',                       'https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=100'),
                    ('Croissant de Mantequilla',                'https://images.unsplash.com/photo-1517433670267-08bbd4be890f?w=100'),
                    ('Empanada de Carne',                       'https://images.unsplash.com/photo-1550617931-e17a7b70dce2?w=100'),
                    ('Pan Blanco Francés',                      'https://images.unsplash.com/photo-1509440159596-0249088772ff?w=100'),
                    ('Pan Integral de Avena',                   'https://images.unsplash.com/photo-1517686469429-8bdb88b9f907?w=100'),
                    ('Sándwich de Pavo',                        'https://images.unsplash.com/photo-1528735602780-2552fd46c7af?w=100'),
                    ('Empacado de Pollo con Arroz',             'https://images.unsplash.com/photo-1571877227200-a0d98ea607e9?w=100'),
                    ('Tarta de Frutas',                         'https://images.unsplash.com/photo-1565958011703-44f9829ba187?w=100'),
                    ('Torta de Chocolate 8 Pulgadas',           'https://images.unsplash.com/photo-1578985545062-69928b1d9587?w=100'),
                    ('Memoria DDR4 16GB 3200MHz',               'https://images.unsplash.com/photo-1555617981-dac3880eac6e?w=100'),
                    ('Monitor 24 pulgadas Full HD',             'https://images.unsplash.com/photo-1527443224154-c4a3942d3acf?w=100'),
                    ('Monitor 27 pulgadas 4K',                  'https://images.unsplash.com/photo-1625842268584-8f3296236761?w=100'),
                    ('Mouse Inalámbrico Ergonómico',            'https://images.unsplash.com/photo-1527864550417-7fd91fc51a46?w=100'),
                    ('Notebook Pro 14 pulgadas 16GB',           'https://images.unsplash.com/photo-1496181133206-80ce9b88a853?w=100'),
                    ('Notebook Ultrabook 13 pulgadas',          'https://images.unsplash.com/photo-1581094794329-c8112a89af12?w=100'),
                    ('Router WiFi 6 AX3000',                     'https://images.unsplash.com/photo-1558494949-ef010cbdcc31?w=100'),
                    ('SSD Externo 1TB USB-C',                   'https://images.unsplash.com/photo-1581092918056-0c4c3acd3789?w=100'),
                    ('Switch Gigabit 24 Puertos',               'https://images.unsplash.com/photo-1600880292203-757bb62b4baf?w=100'),
                    ('Teclado Mecánico RGB',                    'https://images.unsplash.com/photo-1511467687858-23d96c32e4ae?w=100')
                ) AS m("name", "url")
                WHERE p."Name" = m."name";
                """);

            // A sale line shows the picture of the product it sold.
            migrationBuilder.Sql("""
                UPDATE "SaleItems" si SET "ProductImageUrl" = p."ImageUrl"
                FROM "Products" p
                WHERE si."ProductId" = p."Id";
                """);

            // Faces: pravatar only accepts a numeric seed, so it is derived from the id.
            migrationBuilder.Sql("""
                UPDATE "Employees" SET "ImageUrl" = 'https://i.pravatar.cc/150?img=' || (1 + (abs(hashtext("Id"::text)) % 70));
                UPDATE "Clients" SET "ImageUrl" = 'https://i.pravatar.cc/150?img=' || (1 + (abs(hashtext("Id"::text)) % 70));
                UPDATE "Suppliers" SET "ImageUrl" = 'https://i.pravatar.cc/150?img=' || (1 + (abs(hashtext("Id"::text)) % 70));
                """);

            migrationBuilder.Sql("""
                UPDATE "Companies" SET "LogoUrl" = 'https://ui-avatars.com/api/?name=' || replace("Name", ' ', '%20') || '&background=random&size=128'
                WHERE "Id" <> '00000001-0000-0000-0000-000000000001';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "SaleItems" SET "ProductImageUrl" = NULL
                WHERE "SaleId" IN (SELECT "Id" FROM "Sales" WHERE "CompanyId" IN (
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e01',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e66',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ecb',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f30',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f95'));

                UPDATE "Products" SET "ImageUrl" = NULL WHERE "CompanyId" IN (
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e01',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e66',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ecb',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f30',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f95');

                UPDATE "Employees" SET "ImageUrl" = NULL WHERE "CompanyId" IN (
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e01',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e66',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ecb',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f30',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f95');

                UPDATE "Clients" SET "ImageUrl" = NULL WHERE "CompanyId" IN (
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e01',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e66',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ecb',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f30',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f95');

                UPDATE "Suppliers" SET "ImageUrl" = NULL WHERE "CompanyId" IN (
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e01',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e66',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ecb',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f30',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f95');

                UPDATE "Companies" SET "LogoUrl" = NULL WHERE "Id" IN (
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e01',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e66',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ecb',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f30',
                    '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f95');
                """);
        }
    }
}
