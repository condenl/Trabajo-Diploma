# Verificación de preparación de compras y recepciones

Revisión del 14 de noviembre de 2026. Todos los ensayos SQL se realizaron en bases nuevas `Perfumeria_*_Test_*`; se conservaron la configuración de desarrollo y los datos existentes.

| Verificación | Resultado |
|---|---|
| Pruebas unitarias Release | 79 correctas; incluye sesión ausente, inactiva y sin permiso antes de acceder a datos |
| Componentes WinForms y SQL Server 16 | 160 verificaciones correctas |
| Componentes WinForms y LocalDB 17 | Las mismas 160 verificaciones correctas |
| Evaluación de proveedores N03 | 53 verificaciones correctas |
| PDF e importación/exportación XML | 39 verificaciones correctas |
| Precarga y procesos por actor | 62 verificaciones correctas |
| Compilación Release e Inno Setup 6 | Correctas; instalador actualizado |

Las pruebas nuevas comprueban importes ambiguos sin escrituras, precisión y rangos sin recortes, recuperación después de corregir, borrador oculto todavía pendiente, Nuevo/Cancelar con descarte aceptado y rechazado, dos perfumes con total correcto, actualización sin duplicar, eliminación de una línea e inclusión del último producto escrito al crear. El circuito continúa por revisión contable y emisión usando los actores correspondientes.

En recepción se verificaron cambios de orden y perfume con aceptación/rechazo del descarte, cantidad o vencimiento como únicos datos preparados, conservación al actualizar, refresco inmediato de stock y trazabilidad al guardar, y una recepción externa que reduce el saldo: conserva el texto original, identifica el exceso y bloquea un nuevo ingreso. Las pruebas operativas también ejercitan acceso y menús de Compras, Contabilidad, Depósito y Ventas. Se revisó visualmente el formulario nativo y su tabla de borrador.

En Enterprise Architect se actualizaron `N01-06a` (varios renglones y guardado de la lista) y `N01-06b` (consulta automática de stock y trazabilidad dentro de la confirmación). Se mantienen actores, nombres de clases, métodos reales, retornos tipados discontinuos y activaciones continuas. No cambian entidades, tablas ni relaciones de clases del dominio: las vistas de clases, actividades y DER mantienen su alcance.

Google Docs conserva 13 tablas, 48 imágenes y 27 diagramas EA únicos, ubicados en sus secciones y sin recortes. Se reemplazaron dos capturas y siete párrafos de las especificaciones de compra/recepción, con lenguaje abstracto. La comparación contra la exportación previa está en `EnterpriseArchitect/verification-demo-document.json`; el resto del texto e imágenes se conserva. Google reduce las capturas a un máximo de 2048 píxeles; se verificaron proporciones y contenido. Se revisaron las páginas afectadas del PDF exportado por Google Docs, porque el renderizador local requiere LibreOffice y no está instalado.

El EXE del instalador se recompiló con los nuevos binarios; esta revisión no añade migraciones ni cambia el asistente de instalación. Su compilación no sustituye una instalación gráfica en la computadora de la demo.

## Edición de renglones y pantallas pequeñas

La segunda pasada del 14 de noviembre agrega desplazamiento a Compras y N03: en áreas de contenido de 810 × 430 y 1320 × 650 se comprueba que las órdenes mantienen al menos 180 píxeles de altura, los resultados de N03 al menos 250, y las acciones pueden alcanzarse mediante desplazamiento. Se revisó la aplicación nativa con una ventana de menú de 1000 × 570, incluidos los gráficos y botones de decisión de N03.

Seleccionar una línea del borrador recupera sus valores y muestra **Actualizar renglón**. Elegir un perfume ya agregado identifica esa misma línea. Después de guardar una línea se prepara **Agregar producto** con campos limpios; no se permite quitar una fila sin selección. Actualizar los catálogos conserva la edición en curso.

Ventas restablece la cantidad al cambiar de perfume y después de registrar. Actualizar conserva la cantidad si se mantiene el perfume. **Nueva venta** protege carrito, cliente y edición numérica cuando se rechaza el descarte, y limpia esos datos al aceptarlo, sin registrar ventas ni movimientos.

La suite operativa comprende ahora **180 verificaciones** en cada motor SQL 16/17. Se conserva el resultado de **79 pruebas unitarias** y se verifica nuevamente PDF/XML por la continuidad de creación de compras. El ciclo de ensayo se comprobó en ambos motores: una copia nueva admite modificaciones y dos restauraciones al punto inicial; una sesión con destino ajeno se rechaza antes de restaurar. La prueba reproducible se encuentra en `Scripts/test-ensayo-demo.ps1`.

EA conserva 16 representaciones de clases y 132 mensajes verificados en las tres secuencias afectadas por sus clasificadores internos; las capturas cambian en `N01-06a` y `N02-06b`. Las especificaciones abstractas de compra y venta se ajustan en dos párrafos. Google Docs conserva sus 13 tablas, 48 imágenes y 27 diagramas únicos, sin recortes ni duplicados (`EnterpriseArchitect/verification-demo-edicion-document.json`). Se revisaron las páginas modificadas de su PDF nativo y se ajustó proporcionalmente la captura de compra para evitar una página casi vacía. Los casos de uso, entidades, relaciones de dominio y DER mantienen su alcance.
