-- ============================================================
--  LIBRERÍA LA INMACULADA
--  Script completo de base de datos (v7)
--  Sistema de inventario y facturación - librería de escuela primaria
-- ------------------------------------------------------------
--  Qué hace, en orden:
--    1. Crea la base de datos (si no existe)
--    2. Borra los objetos anteriores (v6 y v7) para poder re-ejecutarlo
--    3. Tablas de seguridad
--    4. Tablas de catálogos
--    5. Compras (con IVA)
--    6. Ventas (sin IVA)
--    7. Ajustes de inventario (pérdidas)
--    8. Índices
--    9. Vistas
--   10. Triggers de Fecha_Actualizacion
--   (La base queda VACÍA: sin usuarios, catálogos ni movimientos)
--
--  Cambios respecto a v6 (marcados con  -- [v7]  en el código):
--    * Factura               -> Compra          (PK idCompra)
--    * Factura_Detalle       -> Compra_Detalle  (PK idDetalleCompra)
--    * Factura_Venta         -> Venta           (PK idVenta)
--    * Factura_Venta_Detalle -> Venta_Detalle   (PK idDetalleVenta)
--    * Columnas sin "Factura": Fecha_Compra, SubTotal, IVA, Total
--      (se conserva Num_Factura_Proveedor = número impreso del proveedor)
--    * CAT_Articulos.Precio_Unitario -> Precio_Compra
--    * Venta_Detalle.Costo_Unitario (costo al momento de vender)
--    * Vistas renombradas: V_Compra, V_CompraDetalle, V_Venta, V_VentaDetalle
--    * V_VentaDetalle calcula la ganancia de cada línea
--
--  Las operaciones con varias tablas (registrar/anular venta o compra,
--  registrar pérdida) se programan en C# con SqlTransaction.
--
--  ¡ATENCIÓN! La sección 2 BORRA las tablas y sus datos.
--  Requiere SQL Server 2016 SP1 o superior. Ejecutar completo (F5).
-- ============================================================

-- ============================================================
-- 1. BASE DE DATOS
-- ============================================================
USE master;
GO

IF DB_ID('LibreriaLaInmaculada') IS NULL
    CREATE DATABASE LibreriaLaInmaculada COLLATE Modern_Spanish_CI_AS;
GO

USE LibreriaLaInmaculada;
GO

SET NOCOUNT ON;
GO

-- ============================================================
-- 2. LIMPIEZA (orden inverso a las dependencias)
--    Incluye los nombres de v6 por si la base ya tenía esa versión.
-- ============================================================
DROP VIEW  IF EXISTS dbo.V_PerdidasDevoluciones, dbo.V_VentaDetalle, dbo.V_Venta,
                     dbo.V_CompraDetalle, dbo.V_Compra, dbo.V_Articulos;
DROP VIEW  IF EXISTS dbo.V_FacturaVentaDetalle, dbo.V_FacturaVenta,
                     dbo.V_FacturaDetalle, dbo.V_Factura;                -- v6
DROP TABLE IF EXISTS dbo.Perdidas_Devoluciones;
DROP TABLE IF EXISTS dbo.Venta_Detalle;
DROP TABLE IF EXISTS dbo.Factura_Venta_Detalle;                          -- v6
DROP TABLE IF EXISTS dbo.Venta;
DROP TABLE IF EXISTS dbo.Factura_Venta;                                  -- v6
DROP TABLE IF EXISTS dbo.Compra_Detalle;
DROP TABLE IF EXISTS dbo.Factura_Detalle;                                -- v6
DROP TABLE IF EXISTS dbo.Compra;
DROP TABLE IF EXISTS dbo.Factura;                                        -- v6
DROP TABLE IF EXISTS dbo.CAT_Motivo_Perdida;
DROP TABLE IF EXISTS dbo.CAT_Articulos;
DROP TABLE IF EXISTS dbo.CAT_Categoria;
DROP TABLE IF EXISTS dbo.CAT_Proveedor;
DROP TABLE IF EXISTS dbo.Usuarios;
GO

-- ============================================================
-- 3. TABLAS DE SEGURIDAD
-- ============================================================

-- Tabla: Usuarios
--   "USER" es palabra reservada en SQL Server; la columna se llama Usuario.
--   Password: hash PBKDF2-SHA256 generado en C#, formato
--             PBKDF2-SHA256$iteraciones$salt_base64$hash_base64
--   Rol: Administrador (propietaria) o Vendedor (caja)
CREATE TABLE dbo.Usuarios (
    idUsuario       INT IDENTITY(1,1) NOT NULL,
    Usuario         VARCHAR(50)  NOT NULL,
    Nombre_Completo VARCHAR(100) NULL,
    Password        VARCHAR(256) NOT NULL,
    Rol             VARCHAR(20)  NOT NULL CONSTRAINT DF_Usuarios_Rol    DEFAULT 'Vendedor',
    Activo          BIT          NOT NULL CONSTRAINT DF_Usuarios_Activo DEFAULT 1,
    Fecha_Registro  DATE NOT NULL CONSTRAINT DF_Usuarios_FechaReg DEFAULT GETDATE(),
    CONSTRAINT PK_Usuarios PRIMARY KEY (idUsuario),
    CONSTRAINT UQ_Usuarios_Usuario UNIQUE (Usuario),
    CONSTRAINT CK_Usuarios_Rol CHECK (Rol IN ('Administrador', 'Vendedor'))
);
GO

-- ============================================================
-- 4. TABLAS DE CATÁLOGOS
-- ============================================================

-- Tabla: CAT_Proveedor
CREATE TABLE dbo.CAT_Proveedor (
    idProveedor          INT IDENTITY(1,1) NOT NULL,
    Nombre               VARCHAR(100) NOT NULL,
    Contacto_Nombre      VARCHAR(100) NULL,
    Telefono             VARCHAR(20)  NULL,
    Email                VARCHAR(100) NULL,
    Direccion            VARCHAR(255) NULL,
    Activo               BIT  NOT NULL CONSTRAINT DF_Proveedor_Activo DEFAULT 1,
    Fecha_Registro       DATE NOT NULL CONSTRAINT DF_Proveedor_FechaReg DEFAULT GETDATE(),
    Fecha_Actualizacion  DATE NOT NULL CONSTRAINT DF_Proveedor_FechaAct DEFAULT GETDATE(),
    CONSTRAINT PK_CAT_Proveedor PRIMARY KEY (idProveedor),
    CONSTRAINT UQ_Proveedor_Nombre UNIQUE (Nombre)
);
GO

-- Tabla: CAT_Categoria
CREATE TABLE dbo.CAT_Categoria (
    idCategoria          INT IDENTITY(1,1) NOT NULL,
    Nombre               VARCHAR(80) NOT NULL,
    Activo               BIT  NOT NULL CONSTRAINT DF_Categoria_Activo DEFAULT 1,
    Fecha_Registro       DATE NOT NULL CONSTRAINT DF_Categoria_FechaReg DEFAULT GETDATE(),
    Fecha_Actualizacion  DATE NOT NULL CONSTRAINT DF_Categoria_FechaAct DEFAULT GETDATE(),
    CONSTRAINT PK_CAT_Categoria PRIMARY KEY (idCategoria),
    CONSTRAINT UQ_Categoria_Nombre UNIQUE (Nombre)
);
GO

-- Tabla: CAT_Articulos
--   Cantidad      = stock disponible (nunca negativo)
--   Stock_Minimo  = cuando Cantidad <= Stock_Minimo hay que reabastecer
--   Precio_Compra = costo de la última compra | Precio_Venta = precio al público
CREATE TABLE dbo.CAT_Articulos (
    idArticulo           INT IDENTITY(1,1) NOT NULL,
    idCategoria          INT NOT NULL,
    Descripcion          VARCHAR(150) NOT NULL,
    Cantidad             INT NOT NULL CONSTRAINT DF_Articulos_Cantidad DEFAULT 0,
    Stock_Minimo         INT NOT NULL CONSTRAINT DF_Articulos_StockMin DEFAULT 5,
    Precio_Compra        DECIMAL(10,2) NOT NULL CONSTRAINT DF_Articulos_PrecioC DEFAULT 0,   -- [v7]
    Precio_Venta         DECIMAL(10,2) NOT NULL CONSTRAINT DF_Articulos_PrecioV DEFAULT 0,
    Activo               BIT  NOT NULL CONSTRAINT DF_Articulos_Activo DEFAULT 1,
    Fecha_Registro       DATE NOT NULL CONSTRAINT DF_Articulos_FechaReg DEFAULT GETDATE(),
    Fecha_Actualizacion  DATE NOT NULL CONSTRAINT DF_Articulos_FechaAct DEFAULT GETDATE(),
    CONSTRAINT PK_CAT_Articulos PRIMARY KEY (idArticulo),
    CONSTRAINT UQ_Articulos_Descripcion UNIQUE (Descripcion),
    CONSTRAINT FK_Articulos_Categoria FOREIGN KEY (idCategoria)
        REFERENCES dbo.CAT_Categoria(idCategoria),
    CONSTRAINT CK_Articulos_Cantidad CHECK (Cantidad >= 0),
    CONSTRAINT CK_Articulos_StockMin CHECK (Stock_Minimo >= 0),
    CONSTRAINT CK_Articulos_PrecioC  CHECK (Precio_Compra >= 0),
    CONSTRAINT CK_Articulos_PrecioV  CHECK (Precio_Venta >= 0)
);
GO

-- Tabla: CAT_Motivo_Perdida
CREATE TABLE dbo.CAT_Motivo_Perdida (
    idMotivoPerdida  INT IDENTITY(1,1) NOT NULL,
    Descripcion      VARCHAR(150) NOT NULL,
    Fecha_Registro   DATE NOT NULL CONSTRAINT DF_Motivo_FechaReg DEFAULT GETDATE(),
    CONSTRAINT PK_CAT_Motivo_Perdida PRIMARY KEY (idMotivoPerdida),
    CONSTRAINT UQ_Motivo_Descripcion UNIQUE (Descripcion)
);
GO

-- ============================================================
-- 5. TABLAS TRANSACCIONALES - COMPRAS (CON IVA)
-- ============================================================

-- Tabla: Compra (encabezado)  [v7: antes Factura]
--   idCompra              = número interno (IDENTITY)
--   Num_Factura_Proveedor = número impreso en la factura física del proveedor
--   SubTotal e IVA se guardan al registrar la compra, sumando el detalle
--   en la misma transacción. Total es calculado.
CREATE TABLE dbo.Compra (
    idCompra               INT IDENTITY(1,1) NOT NULL,
    Num_Factura_Proveedor  VARCHAR(30) NOT NULL,
    idProveedor            INT NOT NULL,
    idUsuario              INT NOT NULL,
    Fecha_Compra           DATE NOT NULL CONSTRAINT DF_Compra_Fecha    DEFAULT GETDATE(),
    Fecha_Registro         DATE NOT NULL CONSTRAINT DF_Compra_FechaReg DEFAULT GETDATE(),
    Fecha_Actualizacion    DATE NOT NULL CONSTRAINT DF_Compra_FechaAct DEFAULT GETDATE(),
    SubTotal               DECIMAL(12,2) NOT NULL CONSTRAINT DF_Compra_SubTotal DEFAULT 0,
    IVA                    DECIMAL(12,2) NOT NULL CONSTRAINT DF_Compra_IVA      DEFAULT 0,
    Total AS (SubTotal + IVA),
    Estado                 VARCHAR(10) NOT NULL CONSTRAINT DF_Compra_Estado DEFAULT 'Activa',
    CONSTRAINT PK_Compra PRIMARY KEY (idCompra),
    CONSTRAINT UQ_Compra_Proveedor_Num UNIQUE (idProveedor, Num_Factura_Proveedor),
    CONSTRAINT FK_Compra_Proveedor FOREIGN KEY (idProveedor)
        REFERENCES dbo.CAT_Proveedor(idProveedor),
    CONSTRAINT FK_Compra_Usuario FOREIGN KEY (idUsuario)
        REFERENCES dbo.Usuarios(idUsuario),
    CONSTRAINT CK_Compra_Estado CHECK (Estado IN ('Activa', 'Anulada')),
    CONSTRAINT CK_Compra_Montos CHECK (SubTotal >= 0 AND IVA >= 0)
);
GO

-- Tabla: Compra_Detalle  [v7: antes Factura_Detalle]
--   Porcentaje_IVA se guarda por línea: 15 normal, 0 si la línea viene exenta.
--   Si la tasa cambia en el futuro, las compras viejas NO se alteran.
CREATE TABLE dbo.Compra_Detalle (
    idDetalleCompra         INT IDENTITY(1,1) NOT NULL,
    idCompra                INT NOT NULL,
    idArticulo              INT NOT NULL,
    Cantidad                INT NOT NULL,
    Precio_Compra_Unitario  DECIMAL(10,2) NOT NULL,
    Porcentaje_IVA          DECIMAL(5,2)  NOT NULL CONSTRAINT DF_CompraDet_PorcIVA DEFAULT 15.00,
    -- Campos calculados (IVA redondeado a 2 decimales para que SubTotal + IVA = Total)
    SubTotal AS CAST(Cantidad * Precio_Compra_Unitario AS DECIMAL(12,2)),
    IVA      AS CAST(ROUND(Cantidad * Precio_Compra_Unitario * Porcentaje_IVA / 100, 2) AS DECIMAL(12,2)),
    Total    AS CAST(Cantidad * Precio_Compra_Unitario
                     + ROUND(Cantidad * Precio_Compra_Unitario * Porcentaje_IVA / 100, 2) AS DECIMAL(12,2)),
    Fecha_Registro          DATE NOT NULL CONSTRAINT DF_CompraDet_FechaReg DEFAULT GETDATE(),
    Fecha_Actualizacion     DATE NOT NULL CONSTRAINT DF_CompraDet_FechaAct DEFAULT GETDATE(),
    CONSTRAINT PK_Compra_Detalle PRIMARY KEY (idDetalleCompra),
    CONSTRAINT UQ_CompraDet_Compra_Articulo UNIQUE (idCompra, idArticulo),
    CONSTRAINT FK_CompraDet_Compra FOREIGN KEY (idCompra)
        REFERENCES dbo.Compra(idCompra),
    CONSTRAINT FK_CompraDet_Articulo FOREIGN KEY (idArticulo)
        REFERENCES dbo.CAT_Articulos(idArticulo),
    CONSTRAINT CK_CompraDet_Cantidad CHECK (Cantidad > 0),
    CONSTRAINT CK_CompraDet_Precio   CHECK (Precio_Compra_Unitario >= 0),
    CONSTRAINT CK_CompraDet_PorcIVA  CHECK (Porcentaje_IVA BETWEEN 0 AND 100)
);
GO

-- ============================================================
-- 6. TABLAS TRANSACCIONALES - VENTAS (SIN IVA)
-- ============================================================

-- Tabla: Venta (encabezado)  [v7: antes Factura_Venta]
--   Fecha_Venta guarda fecha y hora (cortes de caja, ventas por horario).
--   Total se guarda al registrar la venta, sumando el detalle en la
--   misma transacción.
CREATE TABLE dbo.Venta (
    idVenta         INT IDENTITY(1,1) NOT NULL,
    idUsuario       INT NOT NULL,
    Fecha_Venta     DATETIME2(0) NOT NULL CONSTRAINT DF_Venta_Fecha    DEFAULT SYSDATETIME(),
    Fecha_Registro  DATE NOT NULL         CONSTRAINT DF_Venta_FechaReg DEFAULT GETDATE(),
    Total           DECIMAL(12,2) NOT NULL CONSTRAINT DF_Venta_Total DEFAULT 0,
    Estado          VARCHAR(10) NOT NULL   CONSTRAINT DF_Venta_Estado DEFAULT 'Activa',
    CONSTRAINT PK_Venta PRIMARY KEY (idVenta),
    CONSTRAINT FK_Venta_Usuario FOREIGN KEY (idUsuario)
        REFERENCES dbo.Usuarios(idUsuario),
    CONSTRAINT CK_Venta_Estado CHECK (Estado IN ('Activa', 'Anulada')),
    CONSTRAINT CK_Venta_Total  CHECK (Total >= 0)
);
GO

-- Tabla: Venta_Detalle  [v7: antes Factura_Venta_Detalle]
--   Precio_Venta_Unitario = precio cobrado en el momento de la venta
--   Costo_Unitario        = Precio_Compra del artículo en el momento de la
--                           venta (permite calcular la ganancia exacta)  [v7]
CREATE TABLE dbo.Venta_Detalle (
    idDetalleVenta         INT IDENTITY(1,1) NOT NULL,
    idVenta                INT NOT NULL,
    idArticulo             INT NOT NULL,
    Cantidad               INT NOT NULL,
    Precio_Venta_Unitario  DECIMAL(10,2) NOT NULL,
    Costo_Unitario         DECIMAL(10,2) NOT NULL,                                       -- [v7]
    Total AS CAST(Cantidad * Precio_Venta_Unitario AS DECIMAL(12,2)),
    CONSTRAINT PK_Venta_Detalle PRIMARY KEY (idDetalleVenta),
    CONSTRAINT UQ_VentaDet_Venta_Articulo UNIQUE (idVenta, idArticulo),
    CONSTRAINT FK_VentaDet_Venta FOREIGN KEY (idVenta)
        REFERENCES dbo.Venta(idVenta),
    CONSTRAINT FK_VentaDet_Articulo FOREIGN KEY (idArticulo)
        REFERENCES dbo.CAT_Articulos(idArticulo),
    CONSTRAINT CK_VentaDet_Cantidad CHECK (Cantidad > 0),
    CONSTRAINT CK_VentaDet_Precio   CHECK (Precio_Venta_Unitario >= 0),
    CONSTRAINT CK_VentaDet_Costo    CHECK (Costo_Unitario >= 0)                         -- [v7]
);
GO

-- ============================================================
-- 7. TABLAS TRANSACCIONALES - AJUSTES DE INVENTARIO
-- ============================================================

-- Tabla: Perdidas_Devoluciones
--   Solo SALIDAS de inventario que no son venta: daño, extravío,
--   producto seco, uso interno y devolución AL PROVEEDOR.
--   La devolución de un CLIENTE se maneja anulando la venta; si el
--   producto regresa dañado, además se registra aquí como pérdida.
CREATE TABLE dbo.Perdidas_Devoluciones (
    idPerdida        INT IDENTITY(1,1) NOT NULL,
    idArticulo       INT NOT NULL,
    idMotivoPerdida  INT NOT NULL,
    idUsuario        INT NOT NULL,
    Cantidad         INT NOT NULL,
    Observacion      VARCHAR(255) NULL,
    Fecha_Registro   DATE NOT NULL CONSTRAINT DF_Perdidas_FechaReg DEFAULT GETDATE(),
    CONSTRAINT PK_Perdidas_Devoluciones PRIMARY KEY (idPerdida),
    CONSTRAINT FK_Perdidas_Articulo FOREIGN KEY (idArticulo)
        REFERENCES dbo.CAT_Articulos(idArticulo),
    CONSTRAINT FK_Perdidas_Motivo FOREIGN KEY (idMotivoPerdida)
        REFERENCES dbo.CAT_Motivo_Perdida(idMotivoPerdida),
    CONSTRAINT FK_Perdidas_Usuario FOREIGN KEY (idUsuario)
        REFERENCES dbo.Usuarios(idUsuario),
    CONSTRAINT CK_Perdidas_Cantidad CHECK (Cantidad > 0)
);
GO

-- ============================================================
-- 8. ÍNDICES
--    SQL Server crea índice para PK y UNIQUE, pero NO para las FK.
-- ============================================================
CREATE INDEX IX_Articulos_Categoria ON dbo.CAT_Articulos (idCategoria);
CREATE INDEX IX_Compra_Fecha        ON dbo.Compra (Fecha_Compra);
CREATE INDEX IX_Compra_Usuario      ON dbo.Compra (idUsuario);
CREATE INDEX IX_CompraDet_Articulo  ON dbo.Compra_Detalle (idArticulo);
CREATE INDEX IX_Venta_Fecha         ON dbo.Venta (Fecha_Venta) INCLUDE (Total, Estado);
CREATE INDEX IX_Venta_Usuario       ON dbo.Venta (idUsuario);
CREATE INDEX IX_VentaDet_Articulo   ON dbo.Venta_Detalle (idArticulo);
CREATE INDEX IX_Perdidas_Articulo   ON dbo.Perdidas_Devoluciones (idArticulo);
CREATE INDEX IX_Perdidas_Fecha      ON dbo.Perdidas_Devoluciones (Fecha_Registro);
-- (idCompra / idVenta en los detalles ya quedan indexados por sus UNIQUE;
--  idProveedor en Compra por UQ_Compra_Proveedor_Num)
GO

-- ============================================================
-- 9. VISTAS
-- ============================================================

-- Inventario (con alerta de reabastecimiento)
CREATE OR ALTER VIEW dbo.V_Articulos
AS
SELECT
    A.idArticulo,
    A.Descripcion,
    A.idCategoria,
    C.Nombre            AS Categoria,
    A.Cantidad          AS Stock_Disponible,
    A.Stock_Minimo,
    CAST(CASE WHEN A.Cantidad <= A.Stock_Minimo THEN 1 ELSE 0 END AS BIT) AS Bajo_Stock,
    A.Precio_Compra,
    A.Precio_Venta,
    A.Precio_Venta - A.Precio_Compra AS Margen_Unitario,
    A.Activo,
    A.Fecha_Registro,
    A.Fecha_Actualizacion
FROM dbo.CAT_Articulos A
INNER JOIN dbo.CAT_Categoria C ON A.idCategoria = C.idCategoria;
GO

-- Compras: encabezado  [v7: antes V_Factura]
CREATE OR ALTER VIEW dbo.V_Compra
AS
SELECT
    CO.idCompra,
    CO.Num_Factura_Proveedor,
    CO.idProveedor,
    P.Nombre        AS Proveedor,
    CO.Fecha_Compra,
    CO.SubTotal,
    CO.IVA,
    CO.Total,
    CO.Estado,
    U.Usuario       AS Registrado_Por,
    CO.Fecha_Registro
FROM dbo.Compra CO
INNER JOIN dbo.CAT_Proveedor P ON CO.idProveedor = P.idProveedor
INNER JOIN dbo.Usuarios U      ON CO.idUsuario   = U.idUsuario;
GO

-- Compras: detalle  [v7: antes V_FacturaDetalle]
CREATE OR ALTER VIEW dbo.V_CompraDetalle
AS
SELECT
    CD.idDetalleCompra,
    CD.idCompra,
    CO.Num_Factura_Proveedor,
    CO.Fecha_Compra,
    P.Nombre                    AS Proveedor,
    C.Nombre                    AS Categoria,
    CD.idArticulo,
    A.Descripcion               AS Nombre_Articulo,
    CD.Cantidad,
    CD.Precio_Compra_Unitario,
    CD.Porcentaje_IVA,
    CD.SubTotal,
    CD.IVA,
    CD.Total,
    CO.Estado,
    CD.Fecha_Registro
FROM dbo.Compra_Detalle CD
INNER JOIN dbo.Compra CO        ON CD.idCompra    = CO.idCompra
INNER JOIN dbo.CAT_Proveedor P  ON CO.idProveedor = P.idProveedor
INNER JOIN dbo.CAT_Articulos A  ON CD.idArticulo  = A.idArticulo
INNER JOIN dbo.CAT_Categoria C  ON A.idCategoria  = C.idCategoria;
GO

-- Ventas: encabezado  [v7: antes V_FacturaVenta]
CREATE OR ALTER VIEW dbo.V_Venta
AS
SELECT
    V.idVenta,
    V.Fecha_Venta,
    V.Total,
    V.Estado,
    U.Usuario       AS Vendedor,
    V.Fecha_Registro
FROM dbo.Venta V
INNER JOIN dbo.Usuarios U ON V.idUsuario = U.idUsuario;
GO

-- Ventas: detalle con ganancia por línea  [v7: antes V_FacturaVentaDetalle]
CREATE OR ALTER VIEW dbo.V_VentaDetalle
AS
SELECT
    VD.idDetalleVenta,
    VD.idVenta,
    V.Fecha_Venta,
    VD.idArticulo,
    A.Descripcion               AS Nombre_Articulo,
    C.Nombre                    AS Categoria,
    VD.Cantidad,
    VD.Precio_Venta_Unitario,
    VD.Costo_Unitario,
    VD.Total,
    CAST(VD.Cantidad * (VD.Precio_Venta_Unitario - VD.Costo_Unitario) AS DECIMAL(12,2)) AS Ganancia,
    V.Estado
FROM dbo.Venta_Detalle VD
INNER JOIN dbo.Venta V          ON VD.idVenta     = V.idVenta
INNER JOIN dbo.CAT_Articulos A  ON VD.idArticulo  = A.idArticulo
INNER JOIN dbo.CAT_Categoria C  ON A.idCategoria  = C.idCategoria;
GO

-- Pérdidas y devoluciones (con su costo)
CREATE OR ALTER VIEW dbo.V_PerdidasDevoluciones
AS
SELECT
    PD.idPerdida,
    PD.idArticulo,
    A.Descripcion   AS Nombre_Articulo,
    M.Descripcion   AS Motivo,
    PD.Cantidad,
    CAST(PD.Cantidad * A.Precio_Compra AS DECIMAL(12,2)) AS Costo_Aproximado,
    PD.Observacion,
    U.Usuario       AS Registrado_Por,
    PD.Fecha_Registro
FROM dbo.Perdidas_Devoluciones PD
INNER JOIN dbo.CAT_Articulos A      ON PD.idArticulo      = A.idArticulo
INNER JOIN dbo.CAT_Motivo_Perdida M ON PD.idMotivoPerdida = M.idMotivoPerdida
INNER JOIN dbo.Usuarios U           ON PD.idUsuario       = U.idUsuario;
GO

-- ============================================================
-- 10. TRIGGERS DE FECHA_ACTUALIZACION
--     El DEFAULT GETDATE() solo actúa al INSERTAR. Estos triggers
--     ponen la fecha del día en cada UPDATE. Si el propio UPDATE
--     ya asigna Fecha_Actualizacion, se respeta ese valor.
-- ============================================================
CREATE OR ALTER TRIGGER dbo.TR_Proveedor_FechaAct ON dbo.CAT_Proveedor AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(Fecha_Actualizacion) RETURN;
    UPDATE T SET Fecha_Actualizacion = CAST(GETDATE() AS DATE)
    FROM dbo.CAT_Proveedor T JOIN inserted i ON i.idProveedor = T.idProveedor;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_Categoria_FechaAct ON dbo.CAT_Categoria AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(Fecha_Actualizacion) RETURN;
    UPDATE T SET Fecha_Actualizacion = CAST(GETDATE() AS DATE)
    FROM dbo.CAT_Categoria T JOIN inserted i ON i.idCategoria = T.idCategoria;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_Articulos_FechaAct ON dbo.CAT_Articulos AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(Fecha_Actualizacion) RETURN;
    UPDATE T SET Fecha_Actualizacion = CAST(GETDATE() AS DATE)
    FROM dbo.CAT_Articulos T JOIN inserted i ON i.idArticulo = T.idArticulo;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_Compra_FechaAct ON dbo.Compra AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(Fecha_Actualizacion) RETURN;
    UPDATE T SET Fecha_Actualizacion = CAST(GETDATE() AS DATE)
    FROM dbo.Compra T JOIN inserted i ON i.idCompra = T.idCompra;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_CompraDet_FechaAct ON dbo.Compra_Detalle AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(Fecha_Actualizacion) RETURN;
    UPDATE T SET Fecha_Actualizacion = CAST(GETDATE() AS DATE)
    FROM dbo.Compra_Detalle T JOIN inserted i ON i.idDetalleCompra = T.idDetalleCompra;
END;
GO

PRINT 'Base de datos LibreriaLaInmaculada v7 creada (vacía).';
GO
