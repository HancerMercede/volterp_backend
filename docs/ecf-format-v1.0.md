# Formato Comprobante Fiscal Electrónico (e-CF) V 1.0 — referencia de campos

Extraído de **"Formato Comprobante Fiscal Electrónico (e-CF) V 1.0"** (DGII, modificado 30/10/2025, 89 páginas),
documento oficial que define el XML del comprobante. URL de origen:

```
https://dgii.gov.do/cicloContribuyente/facturacion/comprobantesFiscalesElectronicosE-CF/
  Documentacin%20sobre%20eCF/Formatos%20XML/Formato%20Comprobante%20Fiscal%20Electr%C3%B3nico%20(e-CF)%20v1.0.pdf
```

> La carpeta se llama `Documentacin sobre eCF` **sin acento**; codificar el acento da 404.
> El PDF **no** define el elemento raíz de forma literal: el documento detalla las *áreas* (secciones).
> El campo de versión del formato vive dentro de `<IdDoc>`.

## Composición del e-CF

| Sección | 31 | 32 | 33 | 34 | 41 | 43 | 44 | 45 | 46 | 47 |
|---|---|---|---|---|---|---|---|---|---|---|
| A. Encabezado | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |
| B. Detalle de Bienes o Servicios | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |
| C. Subtotales Informativos | 3 | 3 | 3 | 3 | 3 | 3 | 3 | 3 | 3 | 3 |
| D. Descuentos o Recargos | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 |
| E. Paginación | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 |
| F. Información de Referencia | 2 | 2 | 1 | 1 | 2 | 2 | 2 | 2 | 2 | 2 |
| G. Fecha y Hora de la firma digital | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |
| H. Firma Digital | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |

El número es la cantidad de campos que la sección exige en ese tipo de comprobante.

## A. Encabezado - identificacion del e-CF: emisor, comprador y datos tributarios

- `IdDoc`
- `TipoeCF`
- `eNCF`
- `IndicadorNotaCredito`
- `IndicadorEnvioDiferido`
- `IndicadorMontoGravado`
- `TipoIngresos`
- `TipoPago`
- `FechaLimitePago`
- `TerminoPago`
### `FormaPago`

- `MontoPago`
- `TipoCuentaPago`
- `NumeroCuentaPago`
- `BancoPago`
- `FechaDesde`
- `FechaHasta`
- `TotalPaginas`
### `Emisor`

- `RNCEmisor`
- `RazonSocialEmisor`
- `NombreComercial`
- `Sucursal`
- `DireccionEmisor`
- `Municipio`
- `Provincia`
### `TablaTelefonoEmisor`

- `TelefonoEmisor`
- `CorreoEmisor`
- `WebSite`
- `ActividadEconomica`
- `CodigoVendedor`
- `NumeroFacturaInterna`
- `NumeroPedidoInterno`
- `ZonaVenta`
- `RutaVenta`
- `FechaEmision`
### `Comprador`

- `RNCComprador`
- `RazonSocialComprador`
- `ContactoComprador`
- `CorreoComprador`
- `DireccionComprador`
- `MunicipioComprador`
- `ProvinciaComprador`
- `PaisComprador`
- `FechaEntrega`
- `ContactoEntrega`
- `DireccionEntrega`
- `TelefonoAdicional`
- `FechaOrdenCompra`
- `NumeroOrdenCompra`
- `CodigoInternoComprador`
- `ResponsablePago`
- `InformacionesAdicionales`
- `FechaEmbarque`
- `NumeroEmbarque`
- `NumeroReferencia`
- `NombrePuertoEmbarque`
- `CondicionesEntrega`
- `TotalFob`
- `Seguro`
- `Flete`
- `OtrosGastos`
- `TotalCif`
- `RegimenAduanero`
- `NombrePuertoSalida`
- `NombrePuertoDesembarque`
- `PesoBruto`
- `PesoNeto`
- `UnidadPesoBruto`
- `UnidadPesoNeto`
- `CantidadBulto`
- `UnidadBulto`
- `VolumenBulto`
- `UnidadVolumen`
### `Transporte`

- `ViaTransporte`
- `PaisOrigen`
- `DireccionDestino`
- `PaisDestino`
- `NumeroViaje`
- `Conductor`
- `DocumentoTransporte`
- `Ficha`
- `Placa`
- `RutaTransporte`
- `ZonaTransporte`
- `NumeroAlbaran`
### `Totales`

- `MontoGravadoTotal`
- `MontoGravadoI1`
- `MontoGravadoI2`
- `MontoGravadoI3`
- `MontoExento`
- `ITBIS1`
- `ITBIS2`
- `ITBIS3`
- `TotalITBIS`
- `TotalITBIS1`
- `TotalITBIS2`
- `TotalITBIS3`
- `MontoImpuestoAdicional`
- `TipoImpuesto`
- `TasaImpuestoAdicional`
- `OtrosImpuestosAdicionales`
- `MontoTotal`
- `MontoNoFacturable`
- `MontoPeriodo`
- `SaldoAnterior`
- `MontoAvancePago`
- `ValorPagar`
- `TotalITBISRetenido`
- `TotalISRRetencion`
- `TotalITBISPercepcion`
- `TotalISRPercepcion`
### `OtraMoneda`

- `TipoMoneda`
- `TipoCambio`
- `MontoGravadoTotalOtraMoneda`
- `MontoGravado1OtraMoneda`
- `MontoGravado2OtraMoneda`
- `MontoGravado3OtraMoneda`
- `MontoExentoOtraMoneda`
- `TotalITBISOtraMoneda`
- `TotalITBIS1OtraMoneda`
- `TotalITBIS2OtraMoneda`
- `TotalITBIS3OtraMoneda`
- `ImpuestosAdicionalesOtraMoneda`
- `TipoImpuestoOtraMoneda`
- `MontoTotalOtraMoneda`
## B. Detalle de Bienes o Servicios - una linea por cada item

### `Item`

- `NumeroLinea`
### `TablaCodigosItem`

- `TipoCodigo`
- `CodigoItem`
- `IndicadorFacturacion`
### `Retencion`

- `MontoITBISRetenido`
- `MontoISRRetenido`
- `NombreItem`
- `IndicadorBienoServicio`
- `DescripcionItem`
- `CantidadItem`
- `UnidadMedida`
- `CantidadReferencia`
- `UnidadReferencia`
### `Subcantidad`

- `CodigoSubcantidad`
- `FechaElaboracion`
- `FechaVencimientoItem`
### `Mineria`

- `PesoNetoKilogramo`
- `PesoNetoMineria`
- `TipoAfiliacion`
- `Liquidacion`
- `PrecioUnitarioItem`
### `DescuentoMonto`

- `TipoSubDescuento`
- `MontoSubDescuento`
### `RecargoMonto`

- `TipoSubRecargo`
- `MontoSubRecargo`
- `TipoImpuesto`
### `OtraMonedaDetalle`

- `PrecioOtraMoneda`
- `DescuentoOtraMoneda`
- `RecargoOtraMoneda`
- `MontoItemOtraMoneda`
- `MontoItem`
## C. Subtotales Informativos - no alteran la base del impuesto

### `Subtotal`

- `NumeroSubTotal`
- `Orden`
- `SubTotalMontoGravadoTotal`
- `SubTotalMontoGravadoI1`
- `SubTotalMontoGravadoI2`
- `SubTotalMontoGravadoI3`
- `SubTotaITBIS`
- `SubTotaITBIS1`
- `SubTotaITBIS2`
- `SubTotaITBIS3`
- `SubTotalImpuestoAdicional`
- `SubTotalExento`
- `MontoSubTotal`
- `Lineas`
## D. Descuentos o Recargos - ajustes globales del e-CF

### `DescuentoORecargo`

- `NumeroLinea`
- `TipoAjuste`
- `IndicadorNorma1007`
- `TipoValor`
- `ValorDescuentooRecargo`
- `MontoDescuentooRecargo`
## E. Paginacion - paginas de la Representacion Impresa

### `Pagina`

- `PaginaNo`
- `NoLineaDesde`
- `NoLineaHasta`
- `SubtotalExentoPagina`
- `SubtotalItbisPagina`
- `SubtotalItbis1Pagina`
- `SubtotalItbis2Pagina`
- `SubtotalItbis3Pagina`
- `SubtotalImpuestoAdicionalPagina`
- `SubtotalImpuestoAdicional`
- `SubtotalOtrosImpuesto`
- `MontoSubtotalPagina`
## F. Informacion de Referencia - notas de credito/debito y reemplazo por contingencia

- `NCFModificado`
- `RNCOtroContribuyente`
- `FechaNCFModificado`
- `CodigoModificacion`
- `RazonModificacion`
## H. Firma Digital - XMLDSig sobre toda la informacion anterior

