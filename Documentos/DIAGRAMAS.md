# Documentos — diagramas de Colorsin

Diagramas en formato Mermaid (`.mmd`). Todos se derivaron del sistema en
funcionamiento, no de un diseño previo, así que describen lo que hay, no lo
que se planeó.

| Archivo | Qué contiene | Fuente de la que se derivó |
| --- | --- | --- |
| `er-base-datos.mmd` | Entidad-relación: 18 tablas, 134 columnas, 36 claves foráneas | `information_schema` de `colorsin_inventario`, leído el 2026-09-23 |
| `casos-uso-general.mmd` | Visión general: 3 actores y los 7 grupos de casos de uso | `PoliticasAutorizacion.cs` + los `RequireAuthorization` de cada endpoint |
| `casos-uso-inventario.mmd` | Inventario y lotes | `InventarioEndpoints.cs` |
| `casos-uso-compras.mmd` | Compras, proveedores y recepciones | `ComprasEndpoints.cs` |
| `casos-uso-ventas.mmd` | Ventas, clientes y precios | `VentasEndpoints.cs` |
| `casos-uso-traslados.mmd` | Traslados entre sedes y novedades | `TransferenciasEndpoints.cs` |
| `casos-uso-administracion.mmd` | Usuarios y tablero | `ComunEndpoints.cs`, `DashboardEndpoints.cs` |

## Cómo leerlos

**Cardinalidades del E-R.** Salen de la nulabilidad real de cada clave foránea:
`||` cuando la columna es `NOT NULL`, `|o` cuando admite nulo. No se excluye
nada por criterio propio salvo `__EFMigrationsHistory`, que es contabilidad de
EF Core y no del negocio.

**Roles en los casos de uso.** Los tres roles forman una jerarquía estricta,
tomada de `PoliticasAutorizacion.cs`:

| Política | Roles que la cumplen |
| --- | --- |
| Ninguna, solo autenticado | Operador, Gerente de Sucursal, Administrador General |
| `supervision` | Gerente de Sucursal, Administrador General |
| `solo-admin-general` | Administrador General |
| `recepcion-mercancia` | Los tres |

Como cada nivel contiene al anterior, **cada caso de uso se conecta solo al rol
mínimo** que lo permite. El color marca la restricción: amarillo exige
supervisión, rojo exige Administrador General.

**Notación.** Mermaid no tiene diagrama de casos de uso nativo. Se emula con
`flowchart`: `subgraph` como frontera del sistema, forma de estadio `([...])`
para los casos de uso y rectángulos para los actores. Si hace falta notación
UML estricta —elipses reales, estereotipos `<<include>>` y `<<extend>>`— habría
que rehacerlos en PlantUML.

## Cómo renderizarlos

- **VS Code**: extensión *Markdown Preview Mermaid Support*, o *Mermaid Editor*
  para abrir `.mmd` directamente.
- **En línea**: pegar el contenido en <https://mermaid.live>.
- **A imagen**: `npx -y @mermaid-js/mermaid-cli -i er-base-datos.mmd -o er-base-datos.png`
- **GitHub**: renderiza Mermaid dentro de bloques ` ```mermaid ` en archivos
  `.md`, pero no los `.mmd` sueltos.

## Vigencia

El esquema de la base y los endpoints cambian con frecuencia. Estos diagramas
reflejan el estado del **2026-09-23**. Si el diagrama y el código se
contradicen, manda el código: regenéralos leyendo `information_schema` y los
endpoints otra vez.
